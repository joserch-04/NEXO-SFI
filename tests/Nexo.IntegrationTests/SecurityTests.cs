using Nexo.Api;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Nexo.Application;
using Nexo.Domain;
using Xunit;
namespace Nexo.IntegrationTests;

public class SecurityTests
{
    private const string Key = "test-only-security-key-at-least-32-bytes-long";
    private static readonly UserInput Admin = new("admin", "Administrador", "Password123456");
    [Fact]
    public async Task Login_cookie_me_refresh_replay_and_logout()
    {
        using var f = new Factory(); await f.Seed(); using var c = f.Client();
        var login = await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = Admin.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
        var payload = await login.Content.ReadFromJsonAsync<TokenResponse>();
        c.DefaultRequestHeaders.Authorization = new("Bearer", payload!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/auth/me")).StatusCode);
        c.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        var refresh = await c.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var second = await refresh.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotEqual(cookie, refresh.Headers.GetValues("Set-Cookie").Single());
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsync("/api/v1/auth/refresh", null)).StatusCode);
        c.DefaultRequestHeaders.Authorization = new("Bearer", second!.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/auth/me")).StatusCode);
        using var fresh = f.Client(); var token = await Login(fresh);
        fresh.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);
        fresh.DefaultRequestHeaders.Add("Cookie", token.Cookie);
        Assert.Equal(HttpStatusCode.NoContent, (await fresh.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.GetAsync("/api/v1/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Permissions_are_enforced_and_refreshed_from_store()
    {
        using var f = new Factory(); await f.Seed(); using var c = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/usuarios")).StatusCode);
        var token = await Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/usuarios")).StatusCode);
        f.Store.Db.RolePermissions[1] = [];
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/v1/roles", new { name = "Escalada" })).StatusCode);
    }
    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public async Task Invalid_jwt_is_rejected(string defect)
    {
        using var f = new Factory(); await f.Seed(); using var c = f.Client();
        var valid = await Login(c);
        c.DefaultRequestHeaders.Authorization = new("Bearer", valid.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/auth/me")).StatusCode);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(valid.AccessToken);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(defect == "issuer" ? "wrong" : "Nexo.Local", defect == "audience" ? "wrong" : "Nexo.Web",
            [new Claim("sub", "1"), new Claim("ver", original.Claims.Single(x => x.Type == "ver").Value), new Claim("sid", original.Claims.Single(x => x.Type == "sid").Value)], now.AddHours(-1), defect == "expired" ? now.AddMinutes(-1) : now.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(defect == "signature" ? new string('z', 48) : Key)), SecurityAlgorithms.HmacSha256));
        c.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Csrf_origin_https_bad_credentials_and_rate_limit()
    {
        using var f = new Factory(); await f.Seed(); using var c = f.Client();
        c.DefaultRequestHeaders.Remove("X-Nexo-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = Admin.Password })).StatusCode);
        c.DefaultRequestHeaders.Add("X-Nexo-CSRF", "1"); c.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = Admin.Password })).StatusCode);
        c.DefaultRequestHeaders.Remove("Origin");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("http://localhost/api/v1/auth/login", new { username = "admin", password = Admin.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "missing", password = "bad" })).StatusCode);
        HttpResponseMessage? last = null;
        for (var i = 0; i < 12; i++) last = await c.PostAsJsonAsync("/api/v1/auth/login", new { username = "missing", password = "bad" });
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
    [Fact]
    public async Task User_management_password_revocation_last_admin_and_role_management()
    {
        using var f = new Factory(); await f.Seed(); using var c = f.Client();
        var t = await Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", t.AccessToken);
        var created = await c.PostAsJsonAsync("/api/v1/usuarios", new UserInput("cajero", "Cajero", "Password123456"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<UserView>())!;
        Assert.DoesNotContain("password", await created.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("Password123456", f.Store.Db.UserData[user.Id].PasswordHash);
        var roleResponse = await c.PostAsJsonAsync("/api/v1/roles", new RoleInput("Lectura"));
        Assert.Equal(HttpStatusCode.Created, roleResponse.StatusCode);
        var role = (await roleResponse.Content.ReadFromJsonAsync<SecurityRole>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"/api/v1/roles/{role.Id}/permisos", new IdsInput([1]))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"/api/v1/usuarios/{user.Id}/roles", new IdsInput([role.Id]))).StatusCode);
        using var worker = f.Client(); var wt = await Login(worker, "cajero"); worker.DefaultRequestHeaders.Authorization = new("Bearer", wt.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await worker.GetAsync("/api/v1/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.DeleteAsync("/api/v1/usuarios/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync("/api/v1/usuarios/1")).StatusCode);
        Assert.True(f.Store.Db.UserData[1].Active);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync("/api/v1/usuarios/1/roles", new IdsInput([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PostAsJsonAsync($"/api/v1/usuarios/{user.Id}/cambio-contrasena", new PasswordInput("Password123456", "Different123456"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await worker.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync($"/api/v1/usuarios/{user.Id}/cambio-contrasena", new PasswordInput(null, "Different123456"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await worker.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/v1/usuarios/{user.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await worker.PostAsJsonAsync("/api/v1/auth/login", new { username = "cajero", password = "Different123456" })).StatusCode);
    }
    [Fact]
    public async Task Concurrent_refresh_only_one_succeeds_and_replay_revokes_family()
    {
        using var f = new Factory(); await f.Seed();
        using var scope = f.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<SecurityService>();
        var session = (await service.Login("admin", Admin.Password))!;
        var outcomes = await Task.WhenAll(service.Refresh(session.RefreshToken), service.Refresh(session.RefreshToken));
        var winner = Assert.Single(outcomes, x => x is not null)!;
        Assert.Null(await service.Refresh(winner.RefreshToken));
        Assert.Null(await service.Authenticate(winner.User.Id, winner.User.Version, winner.Family));
    }
    [Fact]
    public async Task Bootstrap_is_local_and_one_time_and_password_policy_is_enforced()
    {
        using var f = new Factory(); await f.Seed(); using var scope = f.Services.CreateScope(); var s = scope.ServiceProvider.GetRequiredService<SecurityService>();
        Assert.Equal("administrator_exists", (await Assert.ThrowsAsync<SecurityError>(() => s.Bootstrap("other", "Other", "Password123456"))).Code);
        Assert.Equal("password_policy", (await Assert.ThrowsAsync<SecurityError>(() => s.SaveUser(1, 0, new("weak", "Weak", "short")))).Code);
        using var c = f.Client(); Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/api/v1/auth/register", null)).StatusCode);
    }
    [Theory]
    [InlineData("GET", "/api/v1/usuarios")]
    [InlineData("POST", "/api/v1/usuarios")]
    [InlineData("GET", "/api/v1/usuarios/1")]
    [InlineData("PUT", "/api/v1/usuarios/1")]
    [InlineData("DELETE", "/api/v1/usuarios/1")]
    [InlineData("GET", "/api/v1/usuarios/1/roles")]
    [InlineData("PUT", "/api/v1/usuarios/1/roles")]
    [InlineData("POST", "/api/v1/usuarios/1/cambio-contrasena")]
    [InlineData("GET", "/api/v1/roles")]
    [InlineData("POST", "/api/v1/roles")]
    [InlineData("GET", "/api/v1/roles/1")]
    [InlineData("PUT", "/api/v1/roles/1")]
    [InlineData("DELETE", "/api/v1/roles/1")]
    [InlineData("GET", "/api/v1/roles/1/permisos")]
    [InlineData("PUT", "/api/v1/roles/1/permisos")]
    [InlineData("GET", "/api/v1/permisos")]
    public async Task Every_administration_route_requires_authentication(string method, string path)
    {
        using var f = new Factory();
        using var c = f.Client();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(request)).StatusCode);
    }
    [Fact]
    public async Task Expired_refresh_and_access_family_are_rejected()
    {
        using var f = new Factory(); await f.Seed();
        using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SecurityService>();
        var session = (await service.Login("admin", Admin.Password))!;
        var stored = f.Store.Db.Sessions.Single();
        f.Store.Db.Sessions[stored.Key] = stored.Value with { Expires = DateTime.UtcNow.AddSeconds(-1) };
        Assert.Null(await service.Refresh(session.RefreshToken));
        Assert.Null(await service.Authenticate(session.User.Id, session.User.Version, session.Family));
    }
    [Theory]
    [InlineData("Vendedor/Cajero")]
    [InlineData("Consulta")]
    public async Task Only_administrator_can_change_password_even_if_worker_has_management_permission(string roleName)
    {
        using var f = new Factory(); await f.Seed();
        using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SecurityService>();
        var worker = await service.SaveUser(1, 0, new("worker", "Worker", "Password123456"));
        var role = await service.SaveRole(1, 0, new(roleName));
        await service.AssignPermissions(1, role.Id, [2]);
        await service.AssignRoles(1, worker.Id, [role.Id]);
        using var c = f.Client(); var login = await Login(c, "worker"); c.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        var response = await c.PostAsJsonAsync($"/api/v1/usuarios/{worker.Id}/cambio-contrasena", new PasswordInput("Password123456", "Different123456"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("administrator_required", await response.Content.ReadAsStringAsync());
        Assert.NotNull(await service.Login("worker", "Password123456"));
        Assert.Null(await service.Login("worker", "Different123456"));
        var admin = await service.Login("admin", "Password123456");
        Assert.NotNull(admin);
        await service.ChangePassword(1, 1, new("Password123456", "AdminChanged1234"));
        Assert.NotNull(await service.Login("admin", "AdminChanged1234"));
    }

    private sealed record TokenResponse(string AccessToken);
    private sealed record LoginResult(string AccessToken, string Cookie);
    private static async Task<LoginResult> Login(HttpClient c, string username = "admin")
    {
        var r = await c.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Password123456" }); r.EnsureSuccessStatusCode();
        return new((await r.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken, r.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        public MemoryStore Store { get; } = new();
        public HttpClient Client() { var c = CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false }); c.DefaultRequestHeaders.Add("X-Nexo-CSRF", "1"); return c; }
        public async Task Seed() { using var scope = Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<SecurityService>().Bootstrap(Admin.Username, Admin.Name, Admin.Password!); }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MySql"] = "Server=localhost;Database=unused;User ID=unused", ["Jwt:SigningKey"] = Key }));
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<ISecurityStore>(Store)));
        }
    }
}
