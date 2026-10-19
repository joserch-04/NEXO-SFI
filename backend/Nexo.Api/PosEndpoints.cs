using System.Globalization;
using System.Security.Claims;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
namespace Nexo.Api;

public static class PosEndpoints
{
 private static long Actor(HttpContext c)=>long.Parse(c.User.FindFirstValue("sub")!,CultureInfo.InvariantCulture);
 private static bool Has(HttpContext c,string p)=>c.User.HasClaim("permission",p);
 // Los turnos incluyen importes del arqueo: el historial ajeno exige administrador.
 private static bool AllShifts(HttpContext c)=>c.User.IsInRole("Administrador")&&Has(c,"cajas.historial");
 public static void AddPos(this WebApplicationBuilder builder)
 {
  builder.Services.AddScoped<IPosStore,MySqlPosStore>();builder.Services.AddScoped<PosService>();
  builder.Services.AddScoped<ProductImages>();
  var configured=builder.Configuration["Storage:ProductImages"]??"App_Data/product-images";
  var root=Path.IsPathRooted(configured)?configured:Path.Combine(builder.Environment.ContentRootPath,configured);
  builder.Services.AddSingleton<IProductImageFiles>(new ProductImageFiles(root));
  builder.Services.AddAuthorization(o=>{foreach(var p in PosService.Permissions)o.AddPolicy(p,b=>b.RequireAuthenticatedUser().RequireClaim("permission",p));});
 }
 public static void MapPos(this WebApplication app)
 {
  var g=app.MapGroup("/api/v1").WithTags("Punto de venta y caja").RequireAuthorization();
  g.AddEndpointFilter(async (ctx,next)=>
  {
   ctx.HttpContext.Response.Headers.CacheControl="no-store";
   if(!ctx.HttpContext.Request.IsHttps)return Results.Problem(statusCode:400,title:"HTTPS requerido");
   return await next(ctx);
  });
  g.MapPut("/productos/{id:long}/imagen",(long id,ProductImageInput input,ProductImages s,HttpContext c)=>s.Save(Actor(c),id,input)).RequireAuthorization("catalogo.administrar");
  g.MapGet("/productos/{id:long}/imagen",async(long id,ProductImages s,HttpContext c)=>
  {c.Response.Headers.XContentTypeOptions="nosniff";var bytes=await s.Read(id);return bytes==null?Results.NotFound():Results.File(bytes,"image/png");}).RequireAuthorization("catalogo.leer");
  g.MapGet("/cajas",(PosService s)=>s.Registers()).RequireAuthorization("cajas.leer");
  g.MapPost("/cajas",async(Register input,PosService s,HttpContext c)=>new{id=await s.SaveRegister(Actor(c),input with{Id=0})}).RequireAuthorization("cajas.administrar");
  g.MapPut("/cajas/{id:long}",async(long id,Register input,PosService s,HttpContext c)=>new{id=await s.SaveRegister(Actor(c),input with{Id=id})}).RequireAuthorization("cajas.administrar");
  g.MapGet("/pos/configuracion",(PosService s)=>s.Settings()).RequireAuthorization("ventas.registrar");
  g.MapGet("/turnos",(PosService s,HttpContext c,int page=1)=>s.Shifts(Actor(c),AllShifts(c),page)).RequireAuthorization("cajas.leer");
  g.MapPost("/turnos",async(OpenInput input,PosService s,HttpContext c)=>new{id=await s.Open(Actor(c),input)}).RequireAuthorization("cajas.abrir");
  g.MapGet("/turnos/{id:long}/arqueo",(long id,PosService s,HttpContext c)=>s.Reconcile(Actor(c),AllShifts(c),id)).RequireAuthorization("cajas.arquear");
  g.MapPost("/turnos/{id:long}/cierre",async(long id,CloseInput input,PosService s,HttpContext c)=>new{id=await s.Close(Actor(c),id,input)}).RequireAuthorization("cajas.cerrar","cajas.arquear");
  g.MapPost("/turnos/{id:long}/movimientos",async(long id,CashInput input,PosService s,HttpContext c)=>new{id=await s.Cash(Actor(c),id,input)}).RequireAuthorization("cajas.movimientos");
  g.MapGet("/ventas",(PosService s,HttpContext c,long? shiftId,int page=1)=>s.Sales(Actor(c),Has(c,"cajas.historial"),shiftId,page)).RequireAuthorization("ventas.leer");
  g.MapPost("/ventas",async(SaleInput input,PosService s,HttpContext c)=>new{id=await s.Confirm(Actor(c),Has(c,"ventas.descuentos"),input)}).RequireAuthorization("ventas.registrar");
  g.MapGet("/ventas/{id:long}/comprobante",(long id,PosService s,HttpContext c)=>s.Receipt(Actor(c),Has(c,"cajas.historial"),id)).RequireAuthorization("ventas.leer");
  g.MapPost("/ventas/{id:long}/anulacion",async(long id,VoidInput input,PosService s,HttpContext c)=>new{id=await s.Void(Actor(c),id,input)}).RequireAuthorization("ventas.anular");
 }
}
