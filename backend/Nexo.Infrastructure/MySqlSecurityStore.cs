using Dapper;
using MySqlConnector;
using Nexo.Domain;
namespace Nexo.Infrastructure;

public sealed class MySqlSecurityStore(MySqlConnectionFactory factory) : ISecurityStore
{
    public async Task<T> Execute<T>(Func<ISecuritySession, Task<T>> action, CancellationToken ct = default)
    {
        await using var connection = await factory.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT id FROM seguridad_bloqueo WHERE id=1 FOR UPDATE", transaction: transaction, cancellationToken: ct));
        var result = await action(new Session(connection, transaction, ct));
        await transaction.CommitAsync(ct);
        return result;
    }
    private sealed class Session(MySqlConnection db, MySqlTransaction tx, CancellationToken ct) : ISecuritySession
    {
        private CommandDefinition Q(string sql, object? args = null) => new(sql, args, tx, commandTimeout: 10, cancellationToken: ct);
        private const string UserSql = "SELECT id, nombre_usuario Username, nombre Name, password_hash PasswordHash, activo Active, version_sesion Version FROM usuarios ";
        public Task<SecurityUser?> User(long id) => db.QuerySingleOrDefaultAsync<SecurityUser>(Q(UserSql + "WHERE id=@id", new { id }));
        public Task<SecurityUser?> Find(string username) => db.QuerySingleOrDefaultAsync<SecurityUser>(Q(UserSql + "WHERE nombre_usuario=@username", new { username }));
        public async Task<IReadOnlyList<SecurityUser>> Users(int offset, int limit) => (await db.QueryAsync<SecurityUser>(Q(UserSql + "ORDER BY id LIMIT @limit OFFSET @offset", new { limit, offset }))).ToList();
        public async Task<IReadOnlyList<SecurityRole>> Roles() => (await db.QueryAsync<SecurityRole>(Q("SELECT id,nombre Name,activo Active FROM roles ORDER BY id"))).ToList();
        public async Task<IReadOnlyList<Permission>> Permissions() => (await db.QueryAsync<Permission>(Q("SELECT id,codigo Code,descripcion Description FROM permisos ORDER BY id"))).ToList();
        public async Task<string[]> UserPermissions(long id) => (await db.QueryAsync<string>(Q("SELECT DISTINCT p.codigo FROM permisos p JOIN rol_permisos rp ON rp.permiso_id=p.id JOIN roles r ON r.id=rp.rol_id AND r.activo=1 JOIN usuario_roles ur ON ur.rol_id=r.id WHERE ur.usuario_id=@id", new { id }))).ToArray();
        public async Task<string[]> UserRoles(long id) => (await db.QueryAsync<string>(Q("SELECT r.nombre FROM roles r JOIN usuario_roles ur ON ur.rol_id=r.id WHERE ur.usuario_id=@id AND r.activo=1", new { id }))).ToArray();
        public async Task<long[]> RolePermissionIds(long role) => (await db.QueryAsync<long>(Q("SELECT permiso_id FROM rol_permisos WHERE rol_id=@role ORDER BY permiso_id", new { role }))).ToArray();
        public async Task<long> SaveUser(SecurityUser u)
        {
            if (u.Id == 0) return await db.ExecuteScalarAsync<long>(Q("INSERT INTO usuarios(nombre_usuario,nombre,password_hash,activo) VALUES(@Username,@Name,@PasswordHash,@Active); SELECT LAST_INSERT_ID();", u));
            await db.ExecuteAsync(Q("UPDATE usuarios SET nombre_usuario=@Username,nombre=@Name,password_hash=@PasswordHash,activo=@Active,version_sesion=@Version WHERE id=@Id", u));
            return u.Id;
        }
        public async Task<long> SaveRole(SecurityRole r)
        {
            if (r.Id == 0) return await db.ExecuteScalarAsync<long>(Q("INSERT INTO roles(nombre,activo) VALUES(@Name,@Active); SELECT LAST_INSERT_ID();", r));
            await db.ExecuteAsync(Q("UPDATE roles SET nombre=@Name,activo=@Active WHERE id=@Id", r));
            return r.Id;
        }
        // Solo las tablas de unión reciben DELETE; el historial se conserva.
        public async Task SetRoles(long user, long[] roles)
        {
            await db.ExecuteAsync(Q("DELETE FROM usuario_roles WHERE usuario_id=@user", new { user }));
            foreach (var role in roles) await db.ExecuteAsync(Q("INSERT INTO usuario_roles VALUES(@user,@role)", new { user, role }));
        }
        public async Task SetPermissions(long role, long[] permissions)
        {
            await db.ExecuteAsync(Q("DELETE FROM rol_permisos WHERE rol_id=@role", new { role }));
            foreach (var permission in permissions) await db.ExecuteAsync(Q("INSERT INTO rol_permisos VALUES(@role,@permission)", new { role, permission }));
        }
        public async Task<bool> FamilyActive(long user, string family)
        {
            var active = await db.ExecuteScalarAsync<bool>(Q("SELECT EXISTS(SELECT 1 FROM sesiones_refresh WHERE usuario_id=@user AND familia=@family AND revocado_en IS NULL AND expira_en>UTC_TIMESTAMP(6) AND ultima_actividad>UTC_TIMESTAMP(6)-INTERVAL 5 MINUTE)", new { user, family }));
            if (active) await db.ExecuteAsync(Q("UPDATE sesiones_refresh SET ultima_actividad=UTC_TIMESTAMP(6) WHERE usuario_id=@user AND familia=@family AND revocado_en IS NULL", new { user, family }));
            else await RevokeFamily(family);
            return active;
        }
        public Task<int> AdminCount() => db.ExecuteScalarAsync<int>(Q("SELECT COUNT(DISTINCT u.id) FROM usuarios u JOIN usuario_roles ur ON ur.usuario_id=u.id JOIN roles r ON r.id=ur.rol_id WHERE u.activo=1 AND r.activo=1 AND r.nombre='Administrador'"));
        public Task<RefreshSession?> Refresh(byte[] hash) => db.QuerySingleOrDefaultAsync<RefreshSession>(Q("SELECT id,usuario_id UserId,CAST(familia AS CHAR(100)) Family,hash,expira_en Expires,revocado_en Revoked,ultima_actividad LastActivity FROM sesiones_refresh WHERE hash=@hash", new { hash }));
        public Task<long> AddRefresh(long user, string family, byte[] hash, DateTime expires) => db.ExecuteScalarAsync<long>(Q("INSERT INTO sesiones_refresh(usuario_id,familia,hash,creado_en,expira_en,ultima_actividad) VALUES(@user,@family,@hash,UTC_TIMESTAMP(6),@expires,UTC_TIMESTAMP(6)); SELECT LAST_INSERT_ID();", new { user, family, hash, expires }));
        public Task RevokeFamily(string family) => db.ExecuteAsync(Q("UPDATE sesiones_refresh SET revocado_en=COALESCE(revocado_en,UTC_TIMESTAMP(6)) WHERE familia=@family", new { family }));
        public Task RevokeUser(long user) => db.ExecuteAsync(Q("UPDATE sesiones_refresh SET revocado_en=COALESCE(revocado_en,UTC_TIMESTAMP(6)) WHERE usuario_id=@user", new { user }));
        public Task Rotate(long oldId, long newId) => db.ExecuteAsync(Q("UPDATE sesiones_refresh SET revocado_en=UTC_TIMESTAMP(6),reemplazo_id=@newId WHERE id=@oldId", new { oldId, newId }));
        public Task Audit(long? actor, string action, long? resource) => db.ExecuteAsync(Q("INSERT INTO auditoria(actor_id,accion,recurso_tipo,recurso_id,fecha,resultado,correlacion,resumen) VALUES(@actor,@action,'seguridad',@resource,UTC_TIMESTAMP(6),'EXITO',@correlation,'Cambio de seguridad')", new { actor, action, resource, correlation = Guid.NewGuid().ToString() }));
    }
}
