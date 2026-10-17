using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace Nexo.Api;

public sealed class GlobalExceptionHandler(IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // No registrar mensajes de excepciones que podrían contener SQL o credenciales.
        var expected = exception as Nexo.Application.SecurityError;
        var catalog = exception as Nexo.Domain.CatalogError;
        if (expected is null && catalog is null) logger.LogError("Error no controlado {ExceptionType}. TraceId: {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = expected?.Status ?? catalog?.Status ?? 500;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = expected is null && catalog is null ? "Error interno" : "Solicitud rechazada",
                Detail = expected is null && catalog is null ? "Ocurrió un error al procesar la solicitud." : "Revise los datos y permisos de la operación.",
                Extensions = { ["code"] = expected?.Code ?? catalog?.Code ?? "internal_error" }
            }
        });
        return true;
    }
}
