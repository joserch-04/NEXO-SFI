using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace Nexo.Api;

public sealed class GlobalExceptionHandler(IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // No registrar mensajes de excepciones que podrían contener SQL o credenciales.
        logger.LogError("Error no controlado {ExceptionType}. TraceId: {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = 500;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = 500, Title = "Error interno", Detail = "Ocurrió un error al procesar la solicitud.",
                Extensions = { ["code"] = "internal_error" }
            }
        });
        return true;
    }
}
