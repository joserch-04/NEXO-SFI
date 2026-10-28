using System.Globalization;
using System.Security.Claims;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
namespace Nexo.Api;

public static class CatalogEndpoints
{
    private static long Actor(HttpContext c) => long.Parse(c.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);
    public static void AddCatalog(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICatalogStore, MySqlCatalogStore>();
        builder.Services.AddScoped<CatalogService>();
        builder.Services.AddAuthorization(options => { foreach (var p in CatalogService.Permissions) options.AddPolicy(p, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", p)); });
    }
    public static void MapCatalog(this WebApplication app)
    {
        var catalog = app.MapGroup("/api/v1").WithTags("Catálogo e inventario").RequireAuthorization();
        catalog.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (!context.HttpContext.Request.IsHttps) return Results.Problem(statusCode: 400, title: "HTTPS requerido");
            return await next(context);
        });
        foreach (var kind in new[] { "categorias", "subcategorias", "proveedores", "ubicaciones" })
        {
            var read = kind == "proveedores" ? "proveedores.leer" : "catalogo.leer";
            var write = kind == "proveedores" ? "proveedores.administrar" : "catalogo.administrar";
            catalog.MapGet("/" + kind, (CatalogService s) => s.Masters(kind)).RequireAuthorization(read);
            catalog.MapPost("/" + kind, async (CatalogMaster input, CatalogService s, HttpContext c) => Results.Created($"/api/v1/{kind}", new { id = await s.SaveMaster(Actor(c), kind, input with { Id = 0 }) })).RequireAuthorization(write);
            catalog.MapPut("/" + kind + "/{id:long}", async (long id, CatalogMaster input, CatalogService s, HttpContext c) => new { id = await s.SaveMaster(Actor(c), kind, input with { Id = id }) }).RequireAuthorization(write);
            catalog.MapDelete("/" + kind + "/{id:long}", async (long id, uint version, CatalogService s, HttpContext c) =>
            {
                var value = (await s.Masters(kind)).SingleOrDefault(x => x.Id == id) ?? throw new SecurityError(404, "catalog_not_found");
                await s.SaveMaster(Actor(c), kind, value with { Active = false, Version = version }); return Results.NoContent();
            }).RequireAuthorization(write);
        }
        catalog.MapGet("/productos", (CatalogService s, string? search, long? categoryId, long? subcategoryId, bool? active, long? locationId, int page = 1, int pageSize = 25) => s.Products(search, categoryId, subcategoryId, active, locationId, page, pageSize)).RequireAuthorization("catalogo.leer");
        catalog.MapGet("/productos/{id:long}", async (long id, CatalogService s) => await s.Product(id) is { } p ? Results.Ok(p) : Results.NotFound()).RequireAuthorization("catalogo.leer");
        catalog.MapPost("/productos", async (CatalogProduct input, CatalogService s, HttpContext c) => { var id = await s.SaveProduct(Actor(c), input with { Id = 0 }); return Results.Created($"/api/v1/productos/{id}", new { id }); }).RequireAuthorization("catalogo.administrar");
        catalog.MapPut("/productos/{id:long}", async (long id, CatalogProduct input, CatalogService s, HttpContext c) => new { id = await s.SaveProduct(Actor(c), input with { Id = id }) }).RequireAuthorization("catalogo.administrar");
        catalog.MapDelete("/productos/{id:long}", async (long id, uint version, CatalogService s, HttpContext c) => { var p = await s.Product(id) ?? throw new SecurityError(404, "catalog_not_found"); await s.SaveProduct(Actor(c), p with { Active = false, Version = version }); return Results.NoContent(); }).RequireAuthorization("catalogo.administrar");
        catalog.MapGet("/inventario/existencias", (CatalogService s, long? productId, long? locationId) => s.Stocks(productId, locationId)).RequireAuthorization("inventario.leer");
        catalog.MapGet("/inventario/stock-bajo", (CatalogService s) => s.LowStock()).RequireAuthorization("inventario.leer");
        catalog.MapGet("/inventario/movimientos", (CatalogService s, DateTime from, DateTime to, long? productId, string? type, long? locationId, int page = 1, int pageSize = 25) => s.Movements(from, to, productId, type, page, pageSize, locationId)).RequireAuthorization("inventario.leer");
        foreach (var (path, type, permission) in new[] { ("entradas", "ENTRADA", "inventario.entradas"), ("salidas", "SALIDA", "inventario.ajustar"), ("ajustes", "AJUSTE", "inventario.ajustar"), ("traslados", "TRASLADO", "inventario.ajustar") })
            catalog.MapPost("/inventario/" + path, async (InventoryInput input, CatalogService s, HttpContext c) => { CatalogService.Require(input.Type == type); return new { id = await s.Move(Actor(c), input) }; }).RequireAuthorization(permission);
    }
}
