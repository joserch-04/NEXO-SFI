using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
using Xunit;
namespace Nexo.IntegrationTests;

public class PosApiTests
{
 private sealed record Result(long Id);
 [MySqlFact]
 public async Task Http_register_open_sale_receipt_image_reconcile_void_and_close()
 {
  var factory=new MySqlConnectionFactory(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!);
  // Actor exclusivo para que las pruebas concurrentes de servicio conserven sus turnos propios.
  var actor=await new MySqlSecurityStore(factory).Execute(db=>db.SaveUser(new(0,"http.f7."+Guid.NewGuid().ToString("N"),"Prueba HTTP F7","",false,1)));
  using var f=new Factory(actor);using var c=f.CreateClient(new(){BaseAddress=new("https://localhost")});
  async Task<long> Post(string path,object body){var r=await c.PostAsJsonAsync("/api/v1"+path,body);Assert.True(r.IsSuccessStatusCode,await r.Content.ReadAsStringAsync());return(await r.Content.ReadFromJsonAsync<Result>())!.Id;}
  var register=await Post("/cajas",new Register(0,"HTTP-"+Guid.NewGuid().ToString("N")[..20],"Caja HTTP",true,false,0));
  var open=new OpenInput(Guid.NewGuid().ToString(),register,100);var shift=await Post("/turnos",open);Assert.Equal(shift,await Post("/turnos",open));
  var catalog=new CatalogService(new MySqlCatalogStore(factory));var category=(await catalog.Masters("categorias")).First(x=>x.Active).Id;
  var product=await catalog.SaveProduct(actor,new(0,"HTTP-"+Guid.NewGuid().ToString("N"),null,"Preparado HTTP",category,null,25,10,"PORCION",0,false,true,0,[]));
  var p=(await catalog.Product(product))!;
  var image=await c.PutAsJsonAsync($"/api/v1/productos/{product}/imagen",new ProductImageInput(p.Version,BusinessProfileApiTests.IconFixture));image.EnsureSuccessStatusCode();
  var uploaded=(await image.Content.ReadFromJsonAsync<CatalogProduct>())!;
  var bytes=await c.GetAsync($"/api/v1/productos/{product}/imagen");Assert.Equal("image/png",bytes.Content.Headers.ContentType?.MediaType);Assert.Equal("nosniff",bytes.Headers.GetValues("X-Content-Type-Options").Single());
  var saleInput=new SaleInput(Guid.NewGuid().ToString(),shift,"Cliente HTTP",[new(product,1,0)],[new("EFECTIVO",50)]);
  var sale=await Post("/ventas",saleInput);Assert.Equal(sale,await Post("/ventas",saleInput));
  var receipt=(await c.GetFromJsonAsync<Receipt>($"/api/v1/ventas/{sale}/comprobante"))!;Assert.Equal(25,receipt.Payments.Single().Change);
  Assert.Single((await c.GetFromJsonAsync<Sale[]>($"/api/v1/ventas?shiftId={shift}"))!);
  foreach(var method in new[]{"TARJETA","TRANSFERENCIA"})
  {
   var invalid=saleInput with{Key=Guid.NewGuid().ToString(),Payments=[new(method,25)]};
   var rejected=await c.PostAsJsonAsync("/api/v1/ventas",invalid);Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);
   Assert.Contains("payment_reference_required",await rejected.Content.ReadAsStringAsync());
  }
  var mixed=saleInput with{Key=Guid.NewGuid().ToString(),Payments=[new("EFECTIVO",5),new("TARJETA",10,"NFC-000010"),new("TRANSFERENCIA",10,"TRF-000010")]};
  var mixedId=await Post("/ventas",mixed);Assert.Equal(mixedId,await Post("/ventas",mixed));
  var mixedReceipt=(await c.GetFromJsonAsync<Receipt>($"/api/v1/ventas/{mixedId}/comprobante"))!;
  Assert.Equal("NFC-000010",mixedReceipt.Payments.Single(x=>x.Method=="TARJETA").Reference);
  Assert.Equal("TRF-000010",mixedReceipt.Payments.Single(x=>x.Method=="TRANSFERENCIA").Reference);
  await Post($"/turnos/{shift}/movimientos",new CashInput(Guid.NewGuid().ToString(),"INGRESO",10,"Ingreso HTTP"));
  Assert.Equal(140,(await c.GetFromJsonAsync<Reconciliation>($"/api/v1/turnos/{shift}/arqueo"))!.Expected);
  await Post($"/ventas/{sale}/anulacion",new VoidInput(Guid.NewGuid().ToString(),shift,"Prueba HTTP"));
  await Post($"/ventas/{mixedId}/anulacion",new VoidInput(Guid.NewGuid().ToString(),shift,"Prueba referencias HTTP"));
  await Post($"/turnos/{shift}/cierre",new CloseInput(Guid.NewGuid().ToString(),110,"Cierre HTTP"));
  Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/v1/ventas",saleInput with{Key=Guid.NewGuid().ToString()})).StatusCode);
  var removed=await c.PutAsJsonAsync($"/api/v1/productos/{product}/imagen",new ProductImageInput(uploaded.Version,null));removed.EnsureSuccessStatusCode();
  Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v1/productos/{product}/imagen")).StatusCode);
 }
 private sealed class Factory(long actor):WebApplicationFactory<Program>
 {
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
   builder.UseEnvironment("Development");
   builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:MySql"]=Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL"),["Jwt:SigningKey"]="test-only-pos-api-key-with-more-than-32-bytes",["Storage:ProductImages"]=Path.Combine(Path.GetTempPath(),"nexo-pos-http-images")}));
   builder.ConfigureServices(s=>s.AddAuthentication(o=>{o.DefaultAuthenticateScheme="pos-test";o.DefaultChallengeScheme="pos-test";}).AddScheme<AuthenticationSchemeOptions,Handler>("pos-test",o=>o.ClaimsIssuer=actor.ToString(System.Globalization.CultureInfo.InvariantCulture)));
  }
 }
 private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
 {
  protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(AuthenticateResult.Success(new(new(new ClaimsIdentity(PosService.Permissions.Concat(CatalogService.Permissions).Select(p=>new Claim("permission",p)).Append(new Claim("sub",Options.ClaimsIssuer!)),Scheme.Name)),Scheme.Name)));
 }
}
