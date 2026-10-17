using System.Globalization;
using System.Security.Claims;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
namespace Nexo.Api;

public static class BusinessProfileEndpoints
{
    public static void AddBusinessProfile(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IBusinessProfileStore, MySqlBusinessProfileStore>();
        builder.Services.AddScoped<BusinessProfileService>();
        builder.Services.AddAuthorization(options => options.AddPolicy("configuracion.administrar", policy =>
            policy.RequireAuthenticatedUser().RequireRole("Administrador").RequireClaim("permission", "configuracion.administrar")));
    }
    public static void MapBusinessProfile(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/configuracion/negocio").WithTags("Configuración del negocio").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (!context.HttpContext.Request.IsHttps) return Results.Problem(statusCode: 400, title: "HTTPS requerido", extensions: new Dictionary<string, object?> { ["code"] = "https_required" });
            return await next(context);
        });
        // La identidad visual se comparte con todos los operadores autenticados.
        group.MapGet("", (BusinessProfileService service, CancellationToken ct) => service.Read(ct));
        group.MapPut("", (BusinessProfile input, BusinessProfileService service, HttpContext c, CancellationToken ct) =>
            service.Save(long.Parse(c.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture), input, ct)).RequireAuthorization("configuracion.administrar");
    }
}
