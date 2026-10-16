using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
namespace Nexo.Api;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "Nexo.Local";
    public string Audience { get; set; } = "Nexo.Web";
    public string SigningKey { get; set; } = "";
    public SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(SigningKey));
}
public sealed record LoginInput(string Username, string Password);
public sealed record IdsInput(long[] Ids);
public static class SecurityEndpoints
{
    private const string Cookie = "__Secure-nexo-refresh";
    public static void AddSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ISecurityStore, MySqlSecurityStore>();
        builder.Services.AddScoped<IPasswordHasher<SecurityUser>, PasswordHasher<SecurityUser>>();
        builder.Services.AddScoped<SecurityService>();
        builder.Services.AddOptions<JwtSettings>().Bind(builder.Configuration.GetSection("Jwt"))
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32 && !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience), "Configure Jwt:SigningKey (mínimo 32 bytes), Issuer y Audience.").ValidateOnStart();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<JwtSettings>>((o, settings) =>
            {
                var jwt = settings.Value;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.Key,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(15),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                {
                    var p = context.Principal!;
                    if (!long.TryParse(p.FindFirstValue("sub"), out var id) || !uint.TryParse(p.FindFirstValue("ver"), out var version) || p.FindFirstValue("sid") is not { } family) { context.Fail("invalid_session"); return; }
                    var service = context.HttpContext.RequestServices.GetRequiredService<SecurityService>();
                    var identity = await service.Authenticate(id, version, family);
                    if (identity is null) { context.Fail("invalid_session"); return; }
                    // Los roles y permisos se obtienen de la BD, no se confía en privilegios antiguos.
                    var claims = (ClaimsIdentity)p.Identity!;
                    foreach (var claim in claims.FindAll("permission").Concat(claims.FindAll("role")).ToArray()) claims.RemoveClaim(claim);
                    claims.AddClaims(identity.Permissions.Select(x => new Claim("permission", x)));
                    claims.AddClaims(identity.Roles.Select(x => new Claim("role", x)));
                }
                };
            });
        builder.Services.AddAuthorization(options =>
        {
            foreach (var p in SecurityService.ManagementPermissions) options.AddPolicy(p, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", p));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
    }
    private static long Actor(HttpContext c) => long.Parse(c.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);
    private static CookieOptions CookieOptions(DateTime? expires = null) => new() { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/v1/auth", Expires = expires.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc)) : null, IsEssential = true };
    private static IResult Issue(HttpContext c, SessionResult session, JwtSettings jwt)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience,
            [new("sub", session.User.Id.ToString(CultureInfo.InvariantCulture)), new("name", session.User.Username), new("ver", session.User.Version.ToString(CultureInfo.InvariantCulture)), new("sid", session.Family), new("jti", Guid.NewGuid().ToString())],
            now, now.AddMinutes(10), new SigningCredentials(jwt.Key, SecurityAlgorithms.HmacSha256));
        c.Response.Cookies.Append(Cookie, session.RefreshToken, CookieOptions(session.RefreshExpires));
        c.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresIn = 600, user = session.User });
    }
    public static void MapSecurity(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Sesión");
        auth.AddEndpointFilter(async (context, next) =>
        {
            var c = context.HttpContext;
            c.Response.Headers.CacheControl = "no-store";
            if (!c.Request.IsHttps) return Results.Problem(statusCode: 400, title: "HTTPS requerido", extensions: new Dictionary<string, object?> { ["code"] = "https_required" });
            if (HttpMethods.IsPost(c.Request.Method))
            {
                if (c.Request.Headers["X-Nexo-CSRF"] != "1") return Results.StatusCode(403);
                var origin = c.Request.Headers.Origin.ToString();
                var origins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                var sameOrigin = $"{c.Request.Scheme}://{c.Request.Host}";
                if (origin.Length > 0 && origin != sameOrigin && !origins.Contains(origin, StringComparer.Ordinal)) return Results.StatusCode(403);
            }
            return await next(context);
        });
        auth.MapPost("/login", async (LoginInput input, SecurityService service, HttpContext c, Microsoft.Extensions.Options.IOptions<JwtSettings> jwt) =>
        {
            var session = await service.Login(input.Username, input.Password);
            return session is null ? Results.Unauthorized() : Issue(c, session, jwt.Value);
        }).RequireRateLimiting("auth");
        auth.MapPost("/refresh", async (SecurityService service, HttpContext c, Microsoft.Extensions.Options.IOptions<JwtSettings> jwt) =>
        {
            if (!c.Request.Cookies.TryGetValue(Cookie, out var token) || token.Length > 128) return Results.Unauthorized();
            var session = await service.Refresh(token);
            if (session is not null) return Issue(c, session, jwt.Value);
            c.Response.Cookies.Delete(Cookie, CookieOptions()); return Results.Unauthorized();
        }).RequireRateLimiting("auth");
        auth.MapPost("/logout", async (SecurityService service, HttpContext c) =>
        {
            if (c.Request.Cookies.TryGetValue(Cookie, out var token) && token.Length <= 128) await service.Logout(token);
            c.Response.Cookies.Delete(Cookie, CookieOptions()); return Results.NoContent();
        });
        auth.MapGet("/me", (HttpContext c) => Results.Ok(new { id = Actor(c), username = c.User.Identity!.Name, roles = c.User.FindAll("role").Select(x => x.Value), permissions = c.User.FindAll("permission").Select(x => x.Value) })).RequireAuthorization();
        var users = app.MapGroup("/api/v1/usuarios").WithTags("Usuarios").RequireAuthorization();
        users.MapGet("/", (SecurityService s, HttpContext c, int page = 1, int pageSize = 25) => s.Users(Actor(c), page, pageSize)).RequireAuthorization("usuarios.leer");
        users.MapGet("/{id:long}", (long id, SecurityService s, HttpContext c) => s.User(Actor(c), id)).RequireAuthorization("usuarios.leer");
        users.MapGet("/{id:long}/roles", (long id, SecurityService s, HttpContext c) => s.UserRoles(Actor(c), id)).RequireAuthorization("usuarios.leer");
        users.MapPost("/", async (UserInput input, SecurityService s, HttpContext c) => { var u = await s.SaveUser(Actor(c), 0, input); return Results.Created($"/api/v1/usuarios/{u.Id}", u); }).RequireAuthorization("usuarios.administrar");
        users.MapPut("/{id:long}", (long id, UserInput input, SecurityService s, HttpContext c) => s.SaveUser(Actor(c), id, input)).RequireAuthorization("usuarios.administrar");
        users.MapDelete("/{id:long}", async (long id, SecurityService s, HttpContext c) => { await s.Disable(Actor(c), id); return Results.NoContent(); }).RequireAuthorization("usuarios.administrar");
        users.MapPut("/{id:long}/roles", async (long id, IdsInput input, SecurityService s, HttpContext c) => { if (input.Ids is null) throw new SecurityError(400, "invalid_roles"); await s.AssignRoles(Actor(c), id, input.Ids); return Results.NoContent(); }).RequireAuthorization("roles.asignar");
        users.MapPost("/{id:long}/cambio-contrasena", async (long id, PasswordInput input, SecurityService s, HttpContext c) => { await s.ChangePassword(Actor(c), id, input); return Results.NoContent(); });
        var roles = app.MapGroup("/api/v1/roles").WithTags("Roles").RequireAuthorization();
        roles.MapGet("/", (SecurityService s, HttpContext c) => s.Roles(Actor(c))).RequireAuthorization("roles.leer");
        roles.MapGet("/{id:long}", async (long id, SecurityService s, HttpContext c) => { var role = (await s.Roles(Actor(c))).SingleOrDefault(r => r.Id == id); return role is null ? Results.NotFound() : Results.Ok(role); }).RequireAuthorization("roles.leer");
        roles.MapGet("/{id:long}/permisos", (long id, SecurityService s, HttpContext c) => s.RolePermissions(Actor(c), id)).RequireAuthorization("roles.leer");
        roles.MapPost("/", async (RoleInput input, SecurityService s, HttpContext c) => { var r = await s.SaveRole(Actor(c), 0, input); return Results.Created($"/api/v1/roles/{r.Id}", r); }).RequireAuthorization("roles.administrar");
        roles.MapPut("/{id:long}", (long id, RoleInput input, SecurityService s, HttpContext c) => s.SaveRole(Actor(c), id, input)).RequireAuthorization("roles.administrar");
        roles.MapDelete("/{id:long}", async (long id, SecurityService s, HttpContext c) => { var r = (await s.Roles(Actor(c))).SingleOrDefault(r => r.Id == id) ?? throw new SecurityError(404, "role_not_found"); await s.SaveRole(Actor(c), id, new(r.Name, false)); return Results.NoContent(); }).RequireAuthorization("roles.administrar");
        roles.MapPut("/{id:long}/permisos", async (long id, IdsInput input, SecurityService s, HttpContext c) => { if (input.Ids is null) throw new SecurityError(400, "invalid_permissions"); await s.AssignPermissions(Actor(c), id, input.Ids); return Results.NoContent(); }).RequireAuthorization("roles.administrar");
        app.MapGet("/api/v1/permisos", (SecurityService s, HttpContext c) => s.Permissions(Actor(c))).WithTags("Permisos").RequireAuthorization("roles.leer");
    }
}
