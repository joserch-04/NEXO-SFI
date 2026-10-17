using Dapper;
using MySqlConnector;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
using Xunit;
namespace Nexo.IntegrationTests;
public class MySqlBusinessProfileTests
{
    [MySqlFact]
    [Trait("Category", "MySql")]
    public async Task Persists_identity_with_audit_version_check_and_atomic_rollback()
    {
        var factory = new MySqlConnectionFactory(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!);
        var store = new MySqlBusinessProfileStore(factory);
        var actor = await new MySqlSecurityStore(factory).Execute(async db => (await db.Users(0, 1)).First().Id);
        var original = await store.Read();
        try
        {
            var service = new BusinessProfileService(store);
            var saved = await service.Save(actor, original with { Name = "Tienda de ensayo", Tagline = "Identidad compartida", IconDataUrl = BusinessProfileApiTests.IconFixture });
            Assert.Equal(original.Version + 1, saved.Version);
            Assert.Equal(saved, await new MySqlBusinessProfileStore(factory).Read());
            Assert.Equal(BusinessProfileApiTests.IconFixture, (await store.Read()).IconDataUrl);
            Assert.Null(await store.Save(actor, original));
            await using var connection = await factory.OpenAsync();
            var audits = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM auditoria WHERE accion='CONFIGURACION_NEGOCIO_GUARDAR' AND actor_id=@actor", new { actor });
            Assert.True(audits > 0);
            var error = await Assert.ThrowsAsync<MySqlException>(() => store.Save(long.MaxValue, saved with { Name = "No debe persistir" }));
            Assert.Equal(1452, error.Number);
            Assert.Equal(saved, await store.Read());
            Assert.Equal(audits, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM auditoria WHERE accion='CONFIGURACION_NEGOCIO_GUARDAR' AND actor_id=@actor", new { actor }));
            var removed = await service.Save(actor, saved with { IconDataUrl = null });
            Assert.Null(removed.IconDataUrl);
            Assert.Null((await store.Read()).IconDataUrl);
        }
        finally
        {
            var current = await store.Read();
            Assert.NotNull(await store.Save(actor, original with { Version = current.Version }));
        }
    }
}
