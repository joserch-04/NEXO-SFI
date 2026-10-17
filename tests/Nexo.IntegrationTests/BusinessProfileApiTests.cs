using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Domain;
using Xunit;
namespace Nexo.IntegrationTests;
public class BusinessProfileApiTests
{
    internal const string IconFixture = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQaQr4DwADegHm+SuGZwAAAABJRU5ErkJggg==";
    private const string Path = "/api/v1/configuracion/negocio";
    [Fact]
    public async Task Identity_is_private_but_readable_by_any_authenticated_operator()
    {
        using var f = new Factory(); using var c = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(Path)).StatusCode);
        c.DefaultRequestHeaders.Add("X-Test-User", "operator");
        var response = await c.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("Mi tienda", (await response.Content.ReadFromJsonAsync<BusinessProfile>())!.Name);
    }
    [Theory]
    [InlineData("Consulta", false)] [InlineData("Administrador", false)] [InlineData("Consulta", true)]
    public async Task Editing_requires_administrator_and_permission(string role, bool permission)
    {
        using var f = new Factory(); using var c = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        c.DefaultRequestHeaders.Add("X-Test-User", role); if (permission) c.DefaultRequestHeaders.Add("X-Test-Permission", "1");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync(Path, new BusinessProfile("No autorizado", "", 1))).StatusCode);
        Assert.Equal("Mi tienda", (await c.GetFromJsonAsync<BusinessProfile>(Path))!.Name);
    }
    [Fact]
    public async Task Admin_can_save_and_stale_or_invalid_request_is_rejected()
    {
        using var f = new Factory(); using var c = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        c.DefaultRequestHeaders.Add("X-Test-User", "Administrador"); c.DefaultRequestHeaders.Add("X-Test-Permission", "1");
        var response = await c.PutAsJsonAsync(Path, new BusinessProfile("  Tienda del cliente  ", "Siempre cerca", 1) { IconDataUrl = IconFixture });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Tienda del cliente", (await c.GetFromJsonAsync<BusinessProfile>(Path))!.Name);
        Assert.Equal(IconFixture, (await c.GetFromJsonAsync<BusinessProfile>(Path))!.IconDataUrl);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Path, new BusinessProfile("Versión anterior", "", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path, new BusinessProfile(" ", "", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path, new BusinessProfile("Tienda", "", 2) { IconDataUrl = "data:image/svg+xml;base64,PHN2Zy8+" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Path, new BusinessProfile("Tienda del cliente", "", 2) { IconDataUrl = null })).StatusCode);
        Assert.Null((await c.GetFromJsonAsync<BusinessProfile>(Path))!.IconDataUrl);

    }
    [Fact]
    public async Task Requires_https_for_reading_and_editing()
    {
        using var f = new Factory(); using var c = f.CreateClient(new() { BaseAddress = new("http://localhost") });
        c.DefaultRequestHeaders.Add("X-Test-User", "Administrador"); c.DefaultRequestHeaders.Add("X-Test-Permission", "1");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Path, new BusinessProfile("Tienda", "", 1))).StatusCode);
    }
    private sealed class Store : IBusinessProfileStore
    {
        private BusinessProfile value = new("Mi tienda", "", 1);
        public Task<BusinessProfile> Read(CancellationToken ct = default) => Task.FromResult(value);
        public Task<BusinessProfile?> Save(long actor, BusinessProfile profile, CancellationToken ct = default)
        {
            if (value.Version != profile.Version) return Task.FromResult<BusinessProfile?>(null);
            value = profile with { Version = value.Version + 1 }; return Task.FromResult<BusinessProfile?>(value);
        }
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MySql"] = "Server=localhost;Database=unused;User ID=unused", ["Jwt:SigningKey"] = "test-business-profile-key-with-at-least-32-bytes" }));
            builder.ConfigureServices(s => {
                s.Replace(ServiceDescriptor.Singleton<IBusinessProfileStore>(new Store()));
                s.AddAuthentication(o => { o.DefaultAuthenticateScheme = "business-test"; o.DefaultChallengeScheme = "business-test"; }).AddScheme<AuthenticationSchemeOptions, Handler>("business-test", _ => { });
            });
        }
    }
    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-User"].ToString();
            if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", "1"), new(ClaimTypes.Role, role) };
            if (Request.Headers.ContainsKey("X-Test-Permission")) claims.Add(new("permission", "configuracion.administrar"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
