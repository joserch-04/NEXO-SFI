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

// Rutas, autorización y PosService reales; solo se sustituye la persistencia y autenticación.
public class ShiftPrivacyApiTests
{
    [Theory]
    [InlineData("Vendedor/Cajero", false, true, 1, HttpStatusCode.OK)]
    [InlineData("Vendedor/Cajero", true, true, 1, HttpStatusCode.OK)]
    [InlineData("Vendedor/Cajero", false, true, 2, HttpStatusCode.Forbidden)]
    [InlineData("Vendedor/Cajero", true, true, 2, HttpStatusCode.Forbidden)]
    [InlineData("Vendedor/Cajero", true, true, 3, HttpStatusCode.OK)]
    [InlineData("Vendedor/Cajero", true, true, 4, HttpStatusCode.Forbidden)]
    [InlineData("Administrador", true, true, 4, HttpStatusCode.OK)]
    [InlineData("Consulta", true, true, 2, HttpStatusCode.Forbidden)]
    [InlineData("Administrador", true, true, 2, HttpStatusCode.OK)]
    [InlineData("Administrador", false, true, 2, HttpStatusCode.Forbidden)]
    [InlineData("Administrador", true, false, 2, HttpStatusCode.Forbidden)]
    [InlineData("Vendedor/Cajero", false, false, 1, HttpStatusCode.Forbidden)]
    public async Task Reconciliation_requires_ownership_or_administrator_with_history(
        string role, bool history, bool reconcile, int shift, HttpStatusCode expected)
    {
        using var app = new Factory(); using var client = Client(app, role, history, reconcile);
        var response = await client.GetAsync($"/api/v1/turnos/{shift}/arqueo");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var result = (await response.Content.ReadFromJsonAsync<Reconciliation>())!;
            Assert.Equal(shift, result.Shift.Id);
            Assert.Equal(100m, result.Expected);
        }
        else Assert.DoesNotContain("cashSales", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Vendedor/Cajero", false, 2)]
    [InlineData("Vendedor/Cajero", true, 2)]
    [InlineData("Consulta", true, 2)]
    [InlineData("Administrador", false, 2)]
    [InlineData("Administrador", true, 4)]
    public async Task Shift_list_does_not_leak_other_cashiers_closing_amounts(string role, bool history, int count)
    {
        using var app = new Factory(); using var client = Client(app, role, history, true);
        var shifts = (await client.GetFromJsonAsync<Shift[]>("/api/v1/turnos?page=1"))!;
        Assert.Equal(count, shifts.Length);
        if (count == 2) Assert.All(shifts, s => Assert.Equal(1L, s.UserId));
    }

    private static HttpClient Client(Factory app, string role, bool history, bool reconcile)
    {
        var client = app.CreateClient(new() { BaseAddress = new("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        if (history) client.DefaultRequestHeaders.Add("X-Test-History", "1");
        if (reconcile) client.DefaultRequestHeaders.Add("X-Test-Reconcile", "1");
        return client;
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MySql"] = "Server=localhost;Database=unused;User ID=unused",
                ["Jwt:SigningKey"] = "test-shift-privacy-key-with-at-least-32-bytes"
            }));
            builder.ConfigureServices(s =>
            {
                s.Replace(ServiceDescriptor.Singleton<IPosStore>(new Store()));
                s.AddAuthentication(o => { o.DefaultAuthenticateScheme = "shift-test"; o.DefaultChallengeScheme = "shift-test"; })
                    .AddScheme<AuthenticationSchemeOptions, Handler>("shift-test", _ => { });
            });
        }
    }

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim> { new("sub", "1"), new("role", Request.Headers["X-Test-Role"].ToString()), new("permission", "cajas.leer") };
            if (Request.Headers.ContainsKey("X-Test-History")) claims.Add(new("permission", "cajas.historial"));
            if (Request.Headers.ContainsKey("X-Test-Reconcile")) claims.Add(new("permission", "cajas.arquear"));
            var identity = new ClaimsIdentity(claims, Scheme.Name, "sub", "role");
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class Store : IPosStore, IPosSession
    {
        private readonly Shift[] shifts = [
            new(1, 1, 1, "Caja 1", "cajero1", DateTime.UtcNow, null, 100, "ABIERTO", null, null, null, null),
            new(2, 2, 2, "Caja 2", "cajero2", DateTime.UtcNow, DateTime.UtcNow, 100, "CERRADO", 100, 105, 5, "Cierre ajeno"),
            new(3, 1, 1, "Caja 1", "cajero1", DateTime.UtcNow, DateTime.UtcNow, 100, "CERRADO", 100, 100, 0, "Cierre propio"),
            new(4, 2, 2, "Caja 2", "cajero2", DateTime.UtcNow, null, 100, "ABIERTO", null, null, null, null)];
        public Task<T> Execute<T>(Func<IPosSession, Task<T>> action) => action(this);
        public Task<IReadOnlyList<Shift>> Shifts(long? actor, int offset) => Task.FromResult<IReadOnlyList<Shift>>(shifts.Where(s => actor == null || s.UserId == actor).Skip(offset).Take(50).ToArray());
        public Task<Shift?> Shift(long id) => Task.FromResult(shifts.SingleOrDefault(s => s.Id == id));
        public Task<Reconciliation> Reconcile(Shift shift) => Task.FromResult(new Reconciliation(shift, 0, 0, 0, 0, 100, 0, 0, 0, []));
        public Task<IReadOnlyList<Register>> Registers() => throw new NotSupportedException();
        public Task<long> SaveRegister(Register value) => throw new NotSupportedException();
        public Task<long> Open(long actor, OpenInput input) => throw new NotSupportedException();
        public Task Close(long id, CloseInput input, decimal expected) => throw new NotSupportedException();
        public Task<long> Cash(long actor, long shift, CashInput input) => throw new NotSupportedException();
        public Task<IReadOnlyList<Sale>> Sales(long? actor, long? shift, int offset) => throw new NotSupportedException();
        public Task<Receipt?> Receipt(long id) => throw new NotSupportedException();
        public Task<CatalogProduct?> Product(long id) => throw new NotSupportedException();
        public Task<SalesSettings> Settings() => throw new NotSupportedException();
        public Task<long> Confirm(long actor, SaleInput input, IReadOnlyList<SaleLine> lines, IReadOnlyList<SalePayment> payments) => throw new NotSupportedException();
        public Task<long> Void(long actor, Receipt receipt, VoidInput input) => throw new NotSupportedException();
        public Task<long?> Previous(long actor, string type, string key, byte[] hash) => throw new NotSupportedException();
        public Task Remember(long actor, string type, string key, byte[] hash, long id) => throw new NotSupportedException();
        public Task Audit(long actor, string action, long id) => throw new NotSupportedException();
    }
}
