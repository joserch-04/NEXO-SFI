namespace Nexo.Domain;

public sealed record SecurityUser(long Id, string Username, string Name, string PasswordHash, bool Active, uint Version) { public SecurityUser() : this(0, "", "", "", false, 0) { } }
public sealed record SecurityRole(long Id, string Name, bool Active) { public SecurityRole() : this(0, "", false) { } }
public sealed record Permission(long Id, string Code, string Description) { public Permission() : this(0, "", "") { } }
public sealed record RefreshSession(long Id, long UserId, string Family, byte[] Hash, DateTime Expires, DateTime? Revoked) { public RefreshSession() : this(0, 0, "", [], default, null) { } }
public interface ISecurityStore
{
    Task<T> Execute<T>(Func<ISecuritySession, Task<T>> action, CancellationToken ct = default);
}
// Una unidad de trabajo comparte conexión, transacción y bloqueo de seguridad.
public interface ISecuritySession
{
    Task<SecurityUser?> User(long id);
    Task<SecurityUser?> Find(string username);
    Task<IReadOnlyList<SecurityUser>> Users(int offset, int limit);
    Task<IReadOnlyList<SecurityRole>> Roles();
    Task<IReadOnlyList<Permission>> Permissions();
    Task<string[]> UserPermissions(long id);
    Task<string[]> UserRoles(long id);
    Task<long[]> RolePermissionIds(long role);
    Task<long> SaveUser(SecurityUser user);
    Task<long> SaveRole(SecurityRole role);
    Task SetRoles(long user, long[] roles);
    Task SetPermissions(long role, long[] permissions);
    Task<int> AdminCount();
    Task<RefreshSession?> Refresh(byte[] hash);
    Task<bool> FamilyActive(long user, string family);
    Task<long> AddRefresh(long user, string family, byte[] hash, DateTime expires);
    Task RevokeFamily(string family);
    Task RevokeUser(long user);
    Task Rotate(long oldId, long newId);
    Task Audit(long? actor, string action, long? resource);
}
