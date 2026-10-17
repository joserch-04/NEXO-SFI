using Microsoft.AspNetCore.Identity;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
using Xunit;
namespace Nexo.IntegrationTests;

public class MySqlSecurityTests
{
    private sealed class RollbackCheck : Exception;
    private sealed class ExistingSession(ISecuritySession session) : ISecurityStore
    {
        public Task<T> Execute<T>(Func<ISecuritySession, Task<T>> action, CancellationToken ct = default) => action(session);
    }
    [MySqlFact]
    [Trait("Category", "MySql")]
    public async Task Security_repository_and_services_work_with_real_mysql_and_rollback_test_data()
    {
        var store = new MySqlSecurityStore(new MySqlConnectionFactory(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!));
        var username = "test." + Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<RollbackCheck>(() => store.Execute<bool>(async db =>
        {
            var service = new SecurityService(new ExistingSession(db), new PasswordHasher<SecurityUser>());
            var adminRole = (await db.Roles()).Single(r => r.Name == "Administrador");
            var u = new SecurityUser(0, username, "Prueba transaccional", "", true, 1);
            var id = await db.SaveUser(u with { PasswordHash = new PasswordHasher<SecurityUser>().HashPassword(u, "Password123456") });
            await db.SetRoles(id, [adminRole.Id]);
            Assert.Contains("usuarios.administrar", await db.UserPermissions(id));
            Assert.Contains("Administrador", await db.UserRoles(id));
            var session = (await service.Login(username, "Password123456"))!;
            Assert.NotNull(session);
            Assert.NotNull(await service.Authenticate(id, session.User.Version, session.Family));
            var row = await db.Refresh(SecurityService.TokenHash(session.RefreshToken));
            Assert.NotNull(row); Assert.Null(row.Revoked);
            Assert.True(row.LastActivity > DateTime.UtcNow.AddMinutes(-1));
            Assert.InRange(row.Expires, DateTime.UtcNow.AddHours(7.99), DateTime.UtcNow.AddHours(8.01));
            var rotated = (await service.Refresh(session.RefreshToken))!;
            Assert.NotNull(rotated);
            Assert.Null(await service.Refresh(session.RefreshToken));
            Assert.Null(await service.Authenticate(id, session.User.Version, session.Family));
            var role = await service.SaveRole(id, 0, new("Prueba " + Guid.NewGuid().ToString("N")));
            var permission = (await db.Permissions()).Single(p => p.Code == "usuarios.leer");
            await service.AssignPermissions(id, role.Id, [permission.Id]);
            Assert.Equal(new long[] { permission.Id }, await service.RolePermissions(id, role.Id));
            var worker = await service.SaveUser(id, 0, new("worker." + Guid.NewGuid().ToString("N"), "Prueba", "Password123456"));
            await service.AssignRoles(id, worker.Id, [role.Id]);
            Assert.Equal(["usuarios.leer"], await db.UserPermissions(worker.Id));
            await service.ChangePassword(id, worker.Id, new(null, "Different123456"));
            await service.Disable(id, worker.Id);
            Assert.Null(await service.Login(worker.Username, "Different123456"));
            throw new RollbackCheck();
        }));
        Assert.Null(await store.Execute(db => db.Find(username)));
    }
}
