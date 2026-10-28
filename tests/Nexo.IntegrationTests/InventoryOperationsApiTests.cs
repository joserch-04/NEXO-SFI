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
using Xunit;
namespace Nexo.IntegrationTests;
public class InventoryOperationsApiTests
{
    [MySqlFact][Trait("Category","MySql")]
    public async Task Count_routes_capture_submit_cancel_and_reject_incomplete_or_invalid_receipt()
    {
        using var factory=new Factory();using var client=factory.CreateClient(new(){BaseAddress=new("https://localhost")});
        var locations=(await client.GetFromJsonAsync<CatalogMaster[]>("/api/v1/ubicaciones"))!;
        var create=new CountCreateInput(Guid.NewGuid().ToString(),locations.First(l=>l.Code=="B-01").Id,[1],"Conteo HTTP de ensayo");
        var response=await client.PostAsJsonAsync("/api/v1/inventario/conteos",create);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var count=(await response.Content.ReadFromJsonAsync<InventoryCountDetail>())!;
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/v1/inventario/conteos/{count.Count.Id}/enviar",new CountActionInput(count.Count.Version))).StatusCode);
        response=await client.PutAsJsonAsync($"/api/v1/inventario/conteos/{count.Count.Id}",new CountSaveInput(count.Count.Version,[new(1,count.Lines[0].SystemQuantity)]));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        count=(await response.Content.ReadFromJsonAsync<InventoryCountDetail>())!;
        response=await client.PostAsJsonAsync($"/api/v1/inventario/conteos/{count.Count.Id}/enviar",new CountActionInput(count.Count.Version));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        count=(await response.Content.ReadFromJsonAsync<InventoryCountDetail>())!;Assert.Equal("PENDIENTE",count.Count.State);
        response=await client.PostAsJsonAsync($"/api/v1/inventario/conteos/{count.Count.Id}/cancelar",new CountActionInput(count.Count.Version));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal("CANCELADO",(await response.Content.ReadFromJsonAsync<InventoryCountDetail>())!.Count.State);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/inventario/recepciones",new InventoryBatchInput(Guid.NewGuid().ToString(),"ENTRADA","RECEPCION","Sin proveedor",null,"DOC",[new(1,create.LocationId,null,1,2)]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/inventario/conteos")).StatusCode);
    }
    private sealed class Factory:WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:MySql"]=Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL"),["Jwt:SigningKey"]="test-inventory-operations-key-with-at-least-32-bytes"}));
            builder.ConfigureServices(s=>s.AddAuthentication(o=>{o.DefaultAuthenticateScheme="inventory-test";o.DefaultChallengeScheme="inventory-test";}).AddScheme<AuthenticationSchemeOptions,Handler>("inventory-test",_=>{}));
        }
    }
    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims=CatalogService.Permissions.Concat(["inventario.conteos","inventario.aprobar"]).Select(p=>new Claim("permission",p)).Append(new Claim("sub","1"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),Scheme.Name)));
        }
    }
}
