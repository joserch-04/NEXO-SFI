using System.Diagnostics;
using Nexo.Api;
using Microsoft.AspNetCore.Mvc;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;

var builder = WebApplication.CreateBuilder(args.Where(a => a != "--bootstrap-admin").ToArray());
#if DEBUG
// Visual Studio también puede iniciar el ejecutable sin aplicar launchSettings.json.
// En Debug permitimos recuperar el secreto local aunque no se establezca Development.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("MySql")))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("MySql")))
    {
        // Ruta del usuario de Windows, independiente de APPDATA heredada del proceso.
        var localSecretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "Roaming", "Microsoft", "UserSecrets",
            "nexo-api-local-development", "secrets.json");
        builder.Configuration.AddJsonFile(localSecretsPath, optional: true, reloadOnChange: false);
    }
}
// Configuración privada de esta instalación, copiada al directorio Debug.
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
    optional: true, reloadOnChange: false);
#endif
builder.Services.AddSingleton(services => new MySqlConnectionFactory(
    services.GetRequiredService<IConfiguration>().GetConnectionString("MySql")!));
builder.Services.AddScoped<IDatabaseProbe>(services => new MySqlDatabaseProbe(
    services.GetRequiredService<MySqlConnectionFactory>(),
    errorCode => services.GetRequiredService<ILogger<MySqlDatabaseProbe>>()
        .LogWarning("No se pudo conectar con MySQL. Código: {ErrorCode}", errorCode)));
builder.Services.AddScoped<ReadinessService>();
builder.AddSecurity();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions.TryAdd("code", $"http_{context.ProblemDetails.Status}");
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).WithMethods("GET", "POST", "PUT", "DELETE").WithHeaders("Content-Type", "Authorization", "X-Nexo-CSRF").AllowCredentials();
}));
var app = builder.Build();
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("MySql")))
    throw new InvalidOperationException(
        $"No se encontró ConnectionStrings:MySql. Entorno: {app.Environment.EnvironmentName}. " +
        "Configure los secretos de Nexo.Api o ConnectionStrings__MySql.");
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (!context.Request.IsHttps && (path.StartsWithSegments("/api/v1/auth") || path.StartsWithSegments("/api/v1/usuarios") || path.StartsWithSegments("/api/v1/roles") || path.StartsWithSegments("/api/v1/permisos")))
    {
        await Results.Problem(statusCode: 400, title: "HTTPS requerido", extensions: new Dictionary<string, object?> { ["code"] = "https_required" }).ExecuteAsync(context);
        return;
    }
    await next(context);
});
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapSecurity();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
var health = app.MapGroup("/api/v1/health").WithTags("Operación");
health.MapGet("/live", () => TypedResults.Ok(new ReadinessResult("alive")))
    .WithName("Liveness").WithSummary("Comprueba que la API está en ejecución.");
health.MapGet("/ready", async (ReadinessService service, CancellationToken cancellationToken) =>
{
    var result = await service.CheckAsync(cancellationToken);
    return result.Status == "ready" ? Results.Ok(result) : Results.Problem(
        statusCode: 503, title: "Servicio no disponible", detail: "No se pudo verificar la conexión de datos.",
        extensions: new Dictionary<string, object?> { ["code"] = "database_unavailable" });
}).WithName("Readiness").WithSummary("Comprueba la conexión MySQL.")
  .Produces<ReadinessResult>().ProducesProblem(503);
if (args.Contains("--bootstrap-admin"))
{
    using var scope = app.Services.CreateScope();
    Console.Write("Usuario: "); var username = Console.ReadLine() ?? "";
    Console.Write("Nombre: "); var name = Console.ReadLine() ?? "";
    Console.Write("Contraseña (no se muestra): ");
    var password = new System.Text.StringBuilder();
    if (Console.IsInputRedirected)
    {
        // Permite instalación automatizada por stdin, sin contraseñas en argumentos o archivos.
        password.Append(Console.ReadLine() ?? "");
    }
    else
    {
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && password.Length > 0) password.Length--;
            else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
        }
    }
    Console.WriteLine();
    await scope.ServiceProvider.GetRequiredService<SecurityService>().Bootstrap(username, name, password.ToString());
    password.Clear(); Console.WriteLine("Administrador inicial creado.");
    return;
}
app.Run();

public partial class Program;
