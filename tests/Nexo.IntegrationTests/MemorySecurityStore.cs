using Nexo.Domain;
namespace Nexo.IntegrationTests;

// Doble transaccional: restaura el estado si una regla falla.
internal sealed class MemoryStore : ISecurityStore
{
    private readonly SemaphoreSlim gate = new(1);
    public MemorySession Db { get; private set; } = new();
    public async Task<T> Execute<T>(Func<ISecuritySession, Task<T>> action, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct); var backup = Db.Clone();
        try { return await action(Db); } catch { Db = backup; throw; } finally { gate.Release(); }
    }
}
internal sealed class MemorySession : ISecuritySession
{
    public Dictionary<long, SecurityUser> UserData = new();
    public Dictionary<long, SecurityRole> RoleData = new() { [1] = new(1, "Administrador", true) };
    public Dictionary<long, long[]> UserRoleData = new();
    public Dictionary<long, long[]> RolePermissions = new() { [1] = [1, 2, 3, 4, 5] };
    public Dictionary<long, RefreshSession> Sessions = new();
    private static readonly Permission[] Catalog = Nexo.Application.SecurityService.ManagementPermissions.Select((p, i) => new Permission(i + 1, p, p)).ToArray();
    public MemorySession Clone() => new() { UserData = new(UserData), RoleData = new(RoleData), UserRoleData = new(UserRoleData), RolePermissions = new(RolePermissions), Sessions = new(Sessions) };
    public Task<SecurityUser?> User(long id) => Task.FromResult(UserData.GetValueOrDefault(id));
    public Task<SecurityUser?> Find(string name) => Task.FromResult(UserData.Values.SingleOrDefault(u => string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase)));
    public Task<IReadOnlyList<SecurityUser>> Users(int offset, int limit) => Task.FromResult<IReadOnlyList<SecurityUser>>(UserData.Values.OrderBy(u => u.Id).Skip(offset).Take(limit).ToArray());
    public Task<IReadOnlyList<SecurityRole>> Roles() => Task.FromResult<IReadOnlyList<SecurityRole>>(RoleData.Values.ToArray());
    public Task<IReadOnlyList<Permission>> Permissions() => Task.FromResult<IReadOnlyList<Permission>>(Catalog);
    public Task<string[]> UserRoles(long id) => Task.FromResult(UserRoleData.GetValueOrDefault(id, []).Where(i => RoleData[i].Active).Select(i => RoleData[i].Name).ToArray());
    public Task<string[]> UserPermissions(long id) => Task.FromResult(UserRoleData.GetValueOrDefault(id, []).Where(i => RoleData[i].Active).SelectMany(i => RolePermissions.GetValueOrDefault(i, [])).Distinct().Select(i => Catalog.Single(p => p.Id == i).Code).ToArray());
    public Task<long[]> RolePermissionIds(long role) => Task.FromResult(RolePermissions.GetValueOrDefault(role, []));
    public Task<long> SaveUser(SecurityUser u) { var id = u.Id == 0 ? UserData.Count + 1 : u.Id; UserData[id] = u with { Id = id }; return Task.FromResult(id); }
    public Task<long> SaveRole(SecurityRole r) { var id = r.Id == 0 ? RoleData.Count + 1 : r.Id; RoleData[id] = r with { Id = id }; return Task.FromResult(id); }
    public Task SetRoles(long user, long[] roles) { UserRoleData[user] = roles; return Task.CompletedTask; }
    public Task SetPermissions(long role, long[] permissions) { RolePermissions[role] = permissions; return Task.CompletedTask; }
    public Task<int> AdminCount() => Task.FromResult(UserData.Values.Count(u => u.Active && UserRoleData.GetValueOrDefault(u.Id, []).Any(i => RoleData[i].Active && RoleData[i].Name == "Administrador")));
    public Task<RefreshSession?> Refresh(byte[] hash) => Task.FromResult(Sessions.Values.SingleOrDefault(s => s.Hash.SequenceEqual(hash)));
    public async Task<bool> FamilyActive(long user, string family)
    {
        var current = Sessions.Values.SingleOrDefault(s => s.UserId == user && s.Family == family && s.Revoked is null);
        if (current is null || current.Expires <= DateTime.UtcNow || current.LastActivity <= DateTime.UtcNow.AddMinutes(-5)) { await RevokeFamily(family); return false; }
        Sessions[current.Id] = current with { LastActivity = DateTime.UtcNow }; return true;
    }
    public Task<long> AddRefresh(long user, string family, byte[] hash, DateTime expires) { long id = Sessions.Count + 1; Sessions[id] = new(id, user, family, hash, expires, null, DateTime.UtcNow); return Task.FromResult(id); }
    public Task RevokeFamily(string family) { foreach (var s in Sessions.Values.Where(s => s.Family == family).ToArray()) Sessions[s.Id] = s with { Revoked = s.Revoked ?? DateTime.UtcNow }; return Task.CompletedTask; }
    public Task RevokeUser(long user) { foreach (var s in Sessions.Values.Where(s => s.UserId == user).ToArray()) Sessions[s.Id] = s with { Revoked = s.Revoked ?? DateTime.UtcNow }; return Task.CompletedTask; }
    public Task Rotate(long oldId, long newId) { Sessions[oldId] = Sessions[oldId] with { Revoked = DateTime.UtcNow }; return Task.CompletedTask; }
    public Task Audit(long? actor, string action, long? resource) => Task.CompletedTask;
}
