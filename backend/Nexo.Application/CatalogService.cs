using System.Security.Cryptography;
using System.Text.Json;
using Nexo.Domain;
namespace Nexo.Application;

public sealed class CatalogService(ICatalogStore store)
{
    public static readonly string[] Permissions = ["catalogo.leer", "catalogo.administrar", "proveedores.leer", "proveedores.administrar", "inventario.leer", "inventario.entradas", "inventario.ajustar"];
    public static void Require(bool condition, string code = "invalid_catalog", int status = 400) { if (!condition) throw new SecurityError(status, code); }
    private static void Page(int page, int size) => Require(page is >= 1 and <= 100000 && size is >= 1 and <= 100);
    public Task<IReadOnlyList<CatalogMaster>> Masters(string kind) => store.Execute(db => db.Masters(kind));
    public Task<long> SaveMaster(long actor, string kind, CatalogMaster value) => store.Execute(async db =>
    {
        Require(!string.IsNullOrWhiteSpace(value.Name) && value.Name.Trim().Length <= (kind == "proveedores" ? 180 : 100));
        var all = await db.Masters(kind);
        var old = all.SingleOrDefault(x => x.Id == value.Id);
        Require(value.Id == 0 || old != null, "catalog_not_found", 404);
        Require(old == null || old.Version == value.Version, "stale_version", 409);
        if (kind == "subcategorias") Require((await db.Masters("categorias")).Any(x => x.Id == value.ParentId && (x.Active || old?.ParentId == x.Id)));
        if (kind == "proveedores") Require((value.Identification?.Length ?? 0) <= 50 && (value.Phone?.Length ?? 0) <= 40);
        if (kind == "ubicaciones")
        {
            Require(!string.IsNullOrWhiteSpace(value.Code) && value.Code.Length <= 30 && new[] { "AREA", "PASILLO", "SECCION", "ESTANTE" }.Contains(value.Type));
            var visited = new HashSet<long> { value.Id };
            var parent = value.ParentId;
            while (parent.HasValue)
            {
                Require(visited.Add(parent.Value), "location_cycle");
                var p = all.SingleOrDefault(x => x.Id == parent && x.Active);
                Require(p != null); parent = p!.ParentId;
            }
            if (!value.Active) Require(!(await db.Stocks(null, value.Id)).Any(x => x.Quantity > 0) && !all.Any(x => x.ParentId == value.Id && x.Active), "location_in_use", 409);
        }
        if (old != null && kind == "subcategorias" && old.ParentId != value.ParentId)
            Require(!(await db.Products(null, null, value.Id, null, null, 0, 1)).Any(), "catalog_in_use", 409);
        var id = await db.SaveMaster(kind, value with { Name = value.Name.Trim() });
        await db.Audit(actor, $"{kind}.guardar", id); return id;
    });
    public Task<IReadOnlyList<CatalogProduct>> Products(string? search, long? categoryId, long? subcategoryId, bool? active, long? locationId, int page, int size)
    {
        Page(page, size); Require((search?.Length ?? 0) <= 180);
        return store.Execute(db => db.Products(search, categoryId, subcategoryId, active, locationId, (page - 1) * size, size));
    }
    public Task<CatalogProduct?> Product(long id) => store.Execute(db => db.Product(id));
    public Task<long> SaveProduct(long actor, CatalogProduct value) => store.Execute(async db =>
    {
        Require(!string.IsNullOrWhiteSpace(value.Sku) && value.Sku.Length <= 50 && !string.IsNullOrWhiteSpace(value.Name) && value.Name.Length <= 180 && (value.Barcode?.Length ?? 0) <= 64);
        Require(value.Price >= 0 && value.Price <= 99999999999999m && decimal.Round(value.Price, 4) == value.Price && value.Cost >= 0 && value.Cost <= 99999999999999m && decimal.Round(value.Cost, 4) == value.Cost);
        Require(value.MinimumStock >= 0 && value.MinimumStock <= 999999999999m && decimal.Round(value.MinimumStock, 3) == value.MinimumStock && new[] { "UNIDAD", "PAQUETE", "BOTELLA", "LATA", "BOLSA", "LIBRA", "LITRO", "VASO", "PORCION" }.Contains(value.Unit));
        Require(value.SupplierIds != null && value.SupplierIds.Length <= 100);
        var old = value.Id == 0 ? null : await db.Product(value.Id);
        Require(value.Id == 0 || old != null, "catalog_not_found", 404);
        Require(old == null || old.Version == value.Version, "stale_version", 409);
        Require((await db.Masters("categorias")).Any(x => x.Id == value.CategoryId && (x.Active || old?.CategoryId == x.Id)));
        if (value.SubcategoryId.HasValue) Require((await db.Masters("subcategorias")).Any(x => x.Id == value.SubcategoryId && x.ParentId == value.CategoryId && (x.Active || old?.SubcategoryId == x.Id)));
        var suppliers = await db.Masters("proveedores");
        Require(value.SupplierIds!.Distinct().Count() == value.SupplierIds!.Length && value.SupplierIds!.All(id => suppliers.Any(s => s.Id == id && (s.Active || (old?.SupplierIds.Contains(id) ?? false)))));
        if (old != null && !value.TrackInventory && old.TrackInventory) Require(!(await db.Stocks(value.Id, null)).Any(x => x.Quantity != 0), "stock_requires_tracking", 409);
        var id = await db.SaveProduct(value with { Name = value.Name.Trim(), Sku = value.Sku.Trim(), Barcode = string.IsNullOrWhiteSpace(value.Barcode) ? null : value.Barcode.Trim() });
        await db.Audit(actor, "productos.guardar", id); return id;
    });
    public Task<IReadOnlyList<StockView>> Stocks(long? productId, long? locationId) => store.Execute(db => db.Stocks(productId, locationId));
    public Task<IReadOnlyList<StockView>> LowStock() => store.Execute<IReadOnlyList<StockView>>(async db => (await db.Stocks(null, null)).GroupBy(x => x.ProductId).Select(g => g.First() with { LocationId = 0, Location = "Total tienda", Quantity = g.Sum(x => x.Quantity) }).Where(x => x.Active && x.Quantity <= x.MinimumStock).ToArray());
    public Task<IReadOnlyList<MovementView>> Movements(DateTime from, DateTime to, long? productId, string? type, int page, int size)
    {
        Page(page, size); Require(from < to && to - from <= TimeSpan.FromDays(366));
        Require(type == null || new[] { "ENTRADA", "SALIDA", "AJUSTE", "TRASLADO" }.Contains(type));
        return store.Execute(db => db.Movements(from, to, productId, type, (page - 1) * size, size));
    }
    public Task<long> Move(long actor, InventoryInput input) => store.Execute(async db =>
    {
        Require(Guid.TryParse(input.Key, out _) && new[] { "ENTRADA", "SALIDA", "AJUSTE", "TRASLADO" }.Contains(input.Type));
        Require(!string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Length <= 500);
        Require(input.Quantity is >= -999999999999m and <= 999999999999m && InventoryRules.ValidQuantity(Math.Abs(input.Quantity)) && (input.Type == "AJUSTE" || input.Quantity > 0));
        Require(input.Type == "TRASLADO" ? input.DestinationId.HasValue && input.DestinationId != input.LocationId : input.DestinationId == null);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
        var previous = await db.Previous(actor, input.Key, hash); if (previous.HasValue) return previous.Value;
        var product = await db.Product(input.ProductId);
        Require(product is { Active: true, TrackInventory: true }, "inventory_disabled", 409);
        var locations = await db.Masters("ubicaciones");
        Require(locations.Any(x => x.Id == input.LocationId && x.Active) && (!input.DestinationId.HasValue || locations.Any(x => x.Id == input.DestinationId && x.Active)));
        var id = await db.Move(actor, input, hash); await db.Audit(actor, "inventario." + input.Type, id); return id;
    });
}
