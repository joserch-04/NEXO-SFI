using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Nexo.Domain;
namespace Nexo.Application;

public sealed class SecurityError(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record UserView(long Id, string Username, string Name, bool Active);
public sealed record IdentityView(long Id, string Username, string Name, uint Version, string[] Roles, string[] Permissions);
public sealed record SessionResult(IdentityView User, string RefreshToken, DateTime RefreshExpires, string Family);
public sealed record UserInput(string Username, string Name, string? Password, bool Active = true);
public sealed record RoleInput(string Name, bool Active = true);
public sealed record PasswordInput(string? CurrentPassword, string NewPassword);
public sealed class SecurityService(ISecurityStore store, IPasswordHasher<SecurityUser> hasher)
{
    private static readonly SecurityUser Dummy = new(0, "", "", "", false, 0);
    private static readonly string DummyHash = new PasswordHasher<SecurityUser>().HashPassword(Dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    public static readonly string[] ManagementPermissions = ["usuarios.leer", "usuarios.administrar", "roles.leer", "roles.administrar", "roles.asignar"];
    public static byte[] TokenHash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    public static void ValidatePassword(string? value)
    {
        if (value is null || value.Length is < 12 or > 128 || !value.Any(char.IsLetter) || !value.Any(char.IsDigit)) throw new SecurityError(400, "password_policy");
    }
    private static void ValidateUser(UserInput input)
    {
        if (input.Username is null || !Regex.IsMatch(input.Username, @"\A[a-zA-Z0-9._-]{3,80}\z") || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 160) throw new SecurityError(400, "invalid_user");
    }
    private static UserView View(SecurityUser u) => new(u.Id, u.Username, u.Name, u.Active);
    private static async Task<IdentityView> Identity(ISecuritySession db, SecurityUser u) => new(u.Id, u.Username, u.Name, u.Version, await db.UserRoles(u.Id), await db.UserPermissions(u.Id));
    private static async Task<SecurityUser> Required(ISecuritySession db, long id) => await db.User(id) ?? throw new SecurityError(404, "user_not_found");
    private static async Task Require(ISecuritySession db, long actor, string permission)
    {
        var user = await Required(db, actor);
        if (!user.Active || !(await db.UserPermissions(actor)).Contains(permission)) throw new SecurityError(403, "permission_denied");
    }
    private static async Task KeepAdmin(ISecuritySession db)
    {
        if (await db.AdminCount() == 0) throw new SecurityError(409, "last_administrator");
    }
    public Task<IdentityView?> Authenticate(long id, uint version, string family) => store.Execute<IdentityView?>(async db =>
    {
        var u = await db.User(id);
        // La familia está ligada al JWT; una sesión revocada invalida también su access token.
        if (u is null || !u.Active || u.Version != version || !await db.FamilyActive(id, family)) return null;
        return await Identity(db, u);
    });
    private static async Task<SessionResult> NewSession(ISecuritySession db, SecurityUser u, string? family = null, DateTime? expires = null, long? old = null)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var until = expires ?? DateTime.UtcNow.AddDays(7);
        family ??= Guid.NewGuid().ToString();
        var id = await db.AddRefresh(u.Id, family, TokenHash(token), until);
        if (old.HasValue) await db.Rotate(old.Value, id);
        return new(await Identity(db, u), token, until, family);
    }
    public Task<SessionResult?> Login(string? username, string? password) => store.Execute<SessionResult?>(async db =>
    {
        if (username is null || username.Length > 80 || password is null || password.Length > 128) return null;
        var u = await db.Find(username);
        var valid = hasher.VerifyHashedPassword(u ?? Dummy, u is { PasswordHash.Length: > 0 } ? u.PasswordHash : DummyHash, password);
        if (u is null || !u.Active || valid == PasswordVerificationResult.Failed) return null;
        if (valid == PasswordVerificationResult.SuccessRehashNeeded) { u = u with { PasswordHash = hasher.HashPassword(u, password) }; await db.SaveUser(u); }
        await db.Audit(u.Id, "LOGIN", u.Id);
        return await NewSession(db, u);
    });
    public Task<SessionResult?> Refresh(string token) => store.Execute<SessionResult?>(async db =>
    {
        var s = await db.Refresh(TokenHash(token));
        if (s is null) return null;
        if (s.Revoked.HasValue) { await db.RevokeFamily(s.Family); return null; }
        var u = await db.User(s.UserId);
        if (s.Expires <= DateTime.UtcNow || u is null || !u.Active) return null;
        return await NewSession(db, u, s.Family, s.Expires, s.Id);
    });
    public Task<bool> Logout(string token) => store.Execute(async db =>
    {
        var s = await db.Refresh(TokenHash(token));
        if (s is not null) { await db.RevokeFamily(s.Family); await db.Audit(s.UserId, "LOGOUT", s.UserId); }
        return true;
    });
    public Task<UserView[]> Users(long actor, int page, int pageSize) => store.Execute(async db =>
    {
        await Require(db, actor, "usuarios.leer");
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100) throw new SecurityError(400, "invalid_pagination");
        return (await db.Users((page - 1) * pageSize, pageSize)).Select(View).ToArray();
    });
    public Task<UserView> User(long actor, long id) => store.Execute(async db => { await Require(db, actor, "usuarios.leer"); return View(await Required(db, id)); });
    public Task<UserView> SaveUser(long actor, long id, UserInput input) => store.Execute(async db =>
    {
        await Require(db, actor, "usuarios.administrar"); ValidateUser(input);
        if (input.Password is not null && id != 0) throw new SecurityError(400, "use_password_endpoint");
        var existing = await db.Find(input.Username);
        if (existing is not null && existing.Id != id) throw new SecurityError(409, "username_exists");
        var u = id == 0 ? new SecurityUser(0, input.Username, input.Name, "", input.Active, 1) : await Required(db, id);
        if (id == 0) { ValidatePassword(input.Password); u = u with { PasswordHash = hasher.HashPassword(u, input.Password!) }; }
        u = u with { Username = input.Username, Name = input.Name.Trim(), Active = input.Active, Version = u.Version + 1 };
        var key = await db.SaveUser(u);
        await db.RevokeUser(key); await KeepAdmin(db); await db.Audit(actor, id == 0 ? "USUARIO_CREAR" : "USUARIO_EDITAR", key);
        return View(u with { Id = key });
    });
    public Task<bool> Disable(long actor, long id) => store.Execute(async db =>
    {
        await Require(db, actor, "usuarios.administrar"); var u = await Required(db, id);
        await db.SaveUser(u with { Active = false, Version = u.Version + 1 });
        await db.RevokeUser(id); await KeepAdmin(db); await db.Audit(actor, "USUARIO_DESACTIVAR", id); return true;
    });
    public Task<bool> ChangePassword(long actor, long id, PasswordInput input) => store.Execute(async db =>
    {
        // Regla de la tienda: ningún cajero/consulta cambia credenciales por sí mismo,
        // incluso si recibe accidentalmente un permiso administrativo.
        await Require(db, actor, "usuarios.administrar");
        if (!(await db.UserRoles(actor)).Contains("Administrador")) throw new SecurityError(403, "administrator_required");
        var u = await Required(db, id); ValidatePassword(input.NewPassword);
        if (actor == id)
        {
            if (input.CurrentPassword is null || input.CurrentPassword.Length > 128 || hasher.VerifyHashedPassword(u, u.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed) throw new SecurityError(400, "current_password_invalid");
        }
        else await Require(db, actor, "usuarios.administrar");
        await db.SaveUser(u with { PasswordHash = hasher.HashPassword(u, input.NewPassword), Version = u.Version + 1 });
        await db.RevokeUser(id); await db.Audit(actor, "PASSWORD_CAMBIAR", id); return true;
    });
    public Task<IReadOnlyList<SecurityRole>> Roles(long actor) => store.Execute(async db => { await Require(db, actor, "roles.leer"); return await db.Roles(); });
    public Task<IReadOnlyList<Permission>> Permissions(long actor) => store.Execute(async db => { await Require(db, actor, "roles.leer"); return await db.Permissions(); });
    public Task<string[]> UserRoles(long actor, long id) => store.Execute(async db =>
    {
        await Require(db, actor, "usuarios.leer"); await Required(db, id); return await db.UserRoles(id);
    });
    public Task<long[]> RolePermissions(long actor, long id) => store.Execute(async db =>
    {
        await Require(db, actor, "roles.leer");
        if (!(await db.Roles()).Any(r => r.Id == id)) throw new SecurityError(404, "role_not_found");
        return await db.RolePermissionIds(id);
    });
    public Task<SecurityRole> SaveRole(long actor, long id, RoleInput input) => store.Execute(async db =>
    {
        await Require(db, actor, "roles.administrar");
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 60) throw new SecurityError(400, "invalid_role");
        var roles = await db.Roles(); var old = roles.SingleOrDefault(r => r.Id == id);
        if (id != 0 && old is null) throw new SecurityError(404, "role_not_found");
        if (old?.Name == "Administrador") throw new SecurityError(409, "protected_role");
        if (roles.Any(r => r.Id != id && string.Equals(r.Name, input.Name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new SecurityError(409, "role_exists");
        var role = new SecurityRole(id, input.Name.Trim(), input.Active);
        var key = await db.SaveRole(role); await KeepAdmin(db); await db.Audit(actor, "ROL_GUARDAR", key); return role with { Id = key };
    });
    public Task<bool> AssignRoles(long actor, long id, long[] ids) => store.Execute(async db =>
    {
        await Require(db, actor, "roles.asignar"); await Required(db, id);
        if (actor == id) throw new SecurityError(409, "self_role_change");
        var roles = await db.Roles();
        if (ids.Length > 100 || ids.Distinct().Count() != ids.Length || ids.Any(i => !roles.Any(r => r.Id == i && r.Active))) throw new SecurityError(400, "invalid_roles");
        // Solo un administrador puede delegar privilegios que no posee.
        var owned = await db.UserRoles(actor);
        if (!owned.Contains("Administrador") && ids.Length > 0) throw new SecurityError(403, "administrator_required");
        await db.SetRoles(id, ids); await KeepAdmin(db);
        var u = await Required(db, id); await db.SaveUser(u with { Version = u.Version + 1 }); await db.RevokeUser(id);
        await db.Audit(actor, "ROLES_ASIGNAR", id); return true;
    });
    public Task<bool> AssignPermissions(long actor, long id, long[] ids) => store.Execute(async db =>
    {
        await Require(db, actor, "roles.administrar");
        if (!(await db.UserRoles(actor)).Contains("Administrador")) throw new SecurityError(403, "administrator_required");
        var role = (await db.Roles()).SingleOrDefault(r => r.Id == id) ?? throw new SecurityError(404, "role_not_found");
        if (role.Name == "Administrador") throw new SecurityError(409, "protected_role");
        var permissions = await db.Permissions();
        if (ids.Length > 100 || ids.Distinct().Count() != ids.Length || ids.Any(i => !permissions.Any(p => p.Id == i))) throw new SecurityError(400, "invalid_permissions");
        await db.SetPermissions(id, ids); await db.Audit(actor, "PERMISOS_ASIGNAR", id); return true;
    });
    // Solo invocado por el comando local; no hay endpoint público de registro inicial.
    public Task<bool> Bootstrap(string username, string name, string password) => store.Execute(async db =>
    {
        if (await db.AdminCount() > 0) throw new SecurityError(409, "administrator_exists");
        ValidateUser(new(username, name, password)); ValidatePassword(password);
        if (await db.Find(username) is not null) throw new SecurityError(409, "username_exists");
        var role = (await db.Roles()).Single(r => r.Name == "Administrador" && r.Active);
        var u = new SecurityUser(0, username, name, "", true, 1);
        var id = await db.SaveUser(u with { PasswordHash = hasher.HashPassword(u, password) });
        await db.SetRoles(id, [role.Id]); await db.Audit(id, "ADMIN_INICIAL", id); return true;
    });
}
