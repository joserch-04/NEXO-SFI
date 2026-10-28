using System.Globalization;
using System.Security.Claims;
using Nexo.Application;
using Nexo.Domain;
namespace Nexo.Api;
public static class InventoryOperationsEndpoints
{
    public static void AddInventoryOperations(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<InventoryOperationsService>();
        builder.Services.AddAuthorization(options=>{foreach(var permission in new[]{"inventario.conteos","inventario.aprobar"})options.AddPolicy(permission,p=>p.RequireAuthenticatedUser().RequireClaim("permission",permission));});
    }
    private static long Actor(HttpContext c)=>long.Parse(c.User.FindFirstValue("sub")!,CultureInfo.InvariantCulture);
    public static void MapInventoryOperations(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/inventario").WithTags("Inventario operativo").RequireAuthorization();
        group.AddEndpointFilter(async(context,next)=>{
            context.HttpContext.Response.Headers.CacheControl="no-store";
            if(!context.HttpContext.Request.IsHttps)return Results.Problem(statusCode:400,title:"HTTPS requerido");
            return await next(context);
        });
        foreach(var (path,type,permission) in new[]{("recepciones","ENTRADA","inventario.entradas"),("salidas-mercancia","SALIDA","inventario.ajustar"),("traslados-mercancia","TRASLADO","inventario.ajustar")})
            group.MapPost("/"+path,async(InventoryBatchInput input,InventoryOperationsService s,HttpContext c)=>{CatalogService.Require(input.Type==type,"invalid_inventory_operation");return new{id=await s.Register(Actor(c),input)};}).RequireAuthorization(permission);
        group.MapGet("/conteos",(InventoryOperationsService s,int page=1,int pageSize=25)=>s.Counts(page,pageSize)).RequireAuthorization("inventario.leer");
        group.MapGet("/conteos/{id:long}",(long id,InventoryOperationsService s)=>s.Count(id)).RequireAuthorization("inventario.leer");
        group.MapPost("/conteos",(CountCreateInput input,InventoryOperationsService s,HttpContext c)=>s.Create(Actor(c),input)).RequireAuthorization("inventario.conteos");
        group.MapPut("/conteos/{id:long}",(long id,CountSaveInput input,InventoryOperationsService s,HttpContext c)=>s.Save(Actor(c),id,input)).RequireAuthorization("inventario.conteos");
        group.MapPost("/conteos/{id:long}/enviar",(long id,CountActionInput input,InventoryOperationsService s,HttpContext c)=>s.Submit(Actor(c),id,input)).RequireAuthorization("inventario.conteos");
        group.MapPost("/conteos/{id:long}/cancelar",(long id,CountActionInput input,InventoryOperationsService s,HttpContext c)=>s.Cancel(Actor(c),id,input)).RequireAuthorization("inventario.conteos");
        group.MapPost("/conteos/{id:long}/aprobar",(long id,CountActionInput input,InventoryOperationsService s,HttpContext c)=>s.Approve(Actor(c),id,input)).RequireAuthorization("inventario.aprobar");
    }
}
