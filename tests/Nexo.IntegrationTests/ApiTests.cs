using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Domain;
using Xunit;
namespace Nexo.IntegrationTests;

public class ApiTests
{
    [Fact]
    public async Task Liveness_does_not_query_database()
    {
        using var factory = new ApiFactory("Development", new Probe(null));
        var response = await factory.CreateClient().GetAsync("/api/v1/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 503)]
    [InlineData(null, 500)]
    public async Task Readiness_handles_success_unavailability_and_unexpected_error(bool? available, int status)
    {
        using var factory = new ApiFactory("Development", new Probe(available));
        var response = await factory.CreateClient().GetAsync("/api/v1/health/ready");
        Assert.Equal(status, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        if (status == 200) return;
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.True(json.TryGetProperty("traceId", out _));
        Assert.Equal(status == 503 ? "database_unavailable" : "internal_error", json.GetProperty("code").GetString());
        Assert.DoesNotContain("sensitive", body);
    }
    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        using var factory = new ApiFactory("Development", new Probe(true));
        var response = await factory.CreateClient().GetAsync("/missing");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Openapi_describes_health_endpoints()
    {
        using var factory = new ApiFactory("Development", new Probe(true));
        var json = await factory.CreateClient().GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/v1/health/live", json);
        Assert.Contains("/api/v1/health/ready", json);
    }
    [Fact]
    public async Task Production_does_not_publish_openapi()
    {
        using var factory = new ApiFactory("Production", new Probe(true));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Cors_only_allows_configured_origin(string origin, bool allowed)
    {
        using var factory = new ApiFactory("Development", new Probe(true));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health/live");
        request.Headers.Add("Origin", origin);
        var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
    }
    private sealed class Probe(bool? available) : IDatabaseProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) =>
            available.HasValue ? Task.FromResult(available.Value) : throw new InvalidOperationException("sensitive SQL/password");
    }
    private sealed class ApiFactory(string environment, IDatabaseProbe probe) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-only-key-for-nexo-with-at-least-32-bytes",
                ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=nexo;User ID=nexo_app;Connection Timeout=2",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            }));
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Scoped<IDatabaseProbe>(_ => probe)));
        }
    }
}
