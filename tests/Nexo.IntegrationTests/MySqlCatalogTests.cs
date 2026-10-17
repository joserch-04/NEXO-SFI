using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
using Xunit;
namespace Nexo.IntegrationTests;
public class MySqlCatalogTests
{
    private sealed class RollbackCheck : Exception;
    private sealed class Existing(ICatalogSession session) : ICatalogStore
    {
        public Task<T> Execute<T>(Func<ICatalogSession, Task<T>> action) => action(session);
    }
    private static CatalogProduct NewProduct(long category) => new(0, "TEST-" + Guid.NewGuid().ToString("N"), null, "Prueba catálogo F6", category, null, 25, 10, "UNIDAD", 5, true, true, 0, []);
    private static InventoryInput Input(long product, long location, string type, decimal quantity, long? destination = null) => new(Guid.NewGuid().ToString(), type, product, location, destination, quantity, "Verificación automática F6");
    [MySqlFact]
    [Trait("Category", "MySql")]
    public async Task Catalog_crud_stock_transfers_idempotency_and_validation_rollback()
    {
        var factory = new MySqlConnectionFactory(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!);
        var actor = await new MySqlSecurityStore(factory).Execute(async db => (await db.Users(0, 1)).First().Id);
        var store = new MySqlCatalogStore(factory);
        await Assert.ThrowsAsync<RollbackCheck>(() => store.Execute<bool>(async db =>
        {
            var service = new CatalogService(new Existing(db));
            var category = await service.SaveMaster(actor, "categorias", new(0, "Prueba " + Guid.NewGuid(), true, 0));
            var sub = await service.SaveMaster(actor, "subcategorias", new(0, "Sub", true, 0, ParentId: category));
            var supplier = await service.SaveMaster(actor, "proveedores", new(0, "Proveedor prueba", true, 0));
            var location = await service.SaveMaster(actor, "ubicaciones", new(0, "Origen prueba", true, 0, "T-" + Guid.NewGuid().ToString("N")[..20], "AREA"));
            var destination = await service.SaveMaster(actor, "ubicaciones", new(0, "Destino prueba", true, 0, "T-" + Guid.NewGuid().ToString("N")[..20], "ESTANTE", location));
            var definition = NewProduct(category) with { SubcategoryId = sub, SupplierIds = [supplier] };
            var id = await service.SaveProduct(actor, definition);
            var p = (await service.Product(id))!;
            Assert.Equal(new long[] { supplier }, p.SupplierIds);
            Assert.Single(await service.Products(p.Sku, category, sub, true, null, 1, 25));
            Assert.Contains(await service.LowStock(), x => x.ProductId == id && x.Quantity == 0);
            var entry = Input(id, location, "ENTRADA", 40);
            var op = await service.Move(actor, entry);
            Assert.Equal(op, await service.Move(actor, entry));
            Assert.Equal("idempotency_conflict", (await Assert.ThrowsAsync<CatalogError>(() => service.Move(actor, entry with { Quantity = 41 }))).Code);
            await service.Move(actor, Input(id, location, "TRASLADO", 10, destination));
            var stocks = await service.Stocks(id, null);
            Assert.Equal(30, stocks.Single(s => s.LocationId == location).Quantity);
            Assert.Equal(10, stocks.Single(s => s.LocationId == destination).Quantity);
            Assert.Equal("insufficient_stock", (await Assert.ThrowsAsync<CatalogError>(() => service.Move(actor, Input(id, destination, "SALIDA", 11)))).Code);
            Assert.Equal("stock_requires_tracking", (await Assert.ThrowsAsync<SecurityError>(() => service.SaveProduct(actor, p with { TrackInventory = false }))).Code);
            var loc = (await service.Masters("ubicaciones")).Single(x => x.Id == location);
            Assert.Equal("location_in_use", (await Assert.ThrowsAsync<SecurityError>(() => service.SaveMaster(actor, "ubicaciones", loc with { Active = false }))).Code);
            Assert.Equal("location_cycle", (await Assert.ThrowsAsync<SecurityError>(() => service.SaveMaster(actor, "ubicaciones", loc with { ParentId = destination }))).Code);
            // El fallo anterior se captura dentro de la transacción de prueba: no continuar con ese saldo;
            // una solicitud HTTP real revierte toda la transacción al propagar la excepción.
            await service.Move(actor, Input(id, location, "AJUSTE", -30));
            await service.Move(actor, Input(id, destination, "SALIDA", 10));
            await service.SaveProduct(actor, p with { SupplierIds = [], TrackInventory = false });
            p = (await service.Product(id))!;
            Assert.Empty(p.SupplierIds);
            Assert.DoesNotContain(await service.LowStock(), s => s.ProductId == id);
            Assert.Equal("inventory_disabled", (await Assert.ThrowsAsync<SecurityError>(() => service.Move(actor, Input(id, location, "ENTRADA", 1)))).Code);
            Assert.Equal("stale_version", (await Assert.ThrowsAsync<SecurityError>(() => service.SaveProduct(actor, p with { Version = 0 }))).Code);
            var history = await service.Movements(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), id, "TRASLADO", 1, 100);
            Assert.Equal(2, history.Count); Assert.Equal(0, history.Sum(x => x.Delta));
            await service.SaveProduct(actor, p with { Active = false });
            Assert.Empty(await service.Products(p.Sku, null, null, true, null, 1, 25));
            throw new RollbackCheck();
        }));
    }
    [MySqlFact]
    [Trait("Category", "MySql")]
    public async Task Concurrent_withdrawals_allow_only_one_and_failed_transfer_rolls_back()
    {
        var factory = new MySqlConnectionFactory(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!);
        var actor = await new MySqlSecurityStore(factory).Execute(async db => (await db.Users(0, 1)).First().Id);
        var service = new CatalogService(new MySqlCatalogStore(factory));
        var category = (await service.Masters("categorias")).First(x => x.Active).Id;
        var locations = (await service.Masters("ubicaciones")).Where(x => x.Active).Take(2).ToArray();
        // Recuperar fixtures propios si una ejecución anterior fue interrumpida durante finally.
        foreach (var abandoned in await service.Products("Prueba catálogo F6", null, null, true, null, 1, 100))
        {
            if (!abandoned.Sku.StartsWith("TEST-") || abandoned.Name != "Prueba catálogo F6") continue;
            foreach (var s in await service.Stocks(abandoned.Id, null)) if (s.Quantity > 0) await service.Move(actor, Input(abandoned.Id, s.LocationId, "SALIDA", s.Quantity));
            await service.SaveProduct(actor, abandoned with { Active = false });
        }
        var id = await service.SaveProduct(actor, NewProduct(category));
        try
        {
            await service.Move(actor, Input(id, locations[0].Id, "ENTRADA", 10));
            var failed = Input(id, locations[0].Id, "TRASLADO", 11, locations[1].Id);
            Assert.Equal("insufficient_stock", (await Assert.ThrowsAsync<CatalogError>(() => service.Move(actor, failed))).Code);
            Assert.Equal(10, (await service.Stocks(id, null)).Sum(x => x.Quantity));
            var key = Input(id, locations[0].Id, "SALIDA", 10);
            async Task<bool> Withdraw(InventoryInput input) { try { await service.Move(actor, input); return true; } catch (CatalogError e) when (e.Code == "insufficient_stock") { return false; } }
            var results = await Task.WhenAll(Withdraw(key), Withdraw(key with { Key = Guid.NewGuid().ToString() }));
            Assert.Single(results, x => x);
            Assert.Equal(0, (await service.Stocks(id, null)).Sum(x => x.Quantity));
            var history = await service.Movements(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), id, null, 1, 100);
            Assert.Equal(2, history.Count);
        }
        finally
        {
            foreach (var s in await service.Stocks(id, null)) if (s.Quantity > 0) await service.Move(actor, Input(id, s.LocationId, "SALIDA", s.Quantity));
            var p = (await service.Product(id))!; await service.SaveProduct(actor, p with { Active = false });
        }
    }
}
