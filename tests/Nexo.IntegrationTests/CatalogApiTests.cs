using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Nexo.Application;
using Xunit;
namespace Nexo.IntegrationTests;
public class CatalogApiTests
{
    [MySqlFact]
    [Trait("Category", "MySql")]
    public async Task Authorized_catalog_routes_bind_and_read_real_mysql()
    {
        using var f = new Factory(); using var c = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        foreach (var path in new[] { "/categorias", "/subcategorias", "/proveedores", "/ubicaciones", "/productos?active=true&pageSize=100", "/productos/1", "/inventario/existencias", "/inventario/stock-bajo", "/inventario/movimientos?from=" + DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd") + "&to=" + DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd") })
        {
            var response = await c.GetAsync("/api/v1" + path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/productos?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/inventario/movimientos?from=2026-10-02&to=2026-10-01")).StatusCode);
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MySql"] = Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL"), ["Jwt:SigningKey"] = "test-only-catalog-key-with-at-least-32-bytes" }));
            builder.ConfigureServices(s => s.AddAuthentication(o => { o.DefaultAuthenticateScheme = "catalog-test"; o.DefaultChallengeScheme = "catalog-test"; }).AddScheme<AuthenticationSchemeOptions, Handler>("catalog-test", _ => { }));
        }
    }
    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(CatalogService.Permissions.Select(p => new Claim("permission", p)), Scheme.Name)), Scheme.Name)));
    }
}
