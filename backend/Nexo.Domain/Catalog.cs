namespace Nexo.Domain;

public sealed record CatalogMaster(long Id, string Name, bool Active, uint Version, string? Code = null, string? Type = null, long? ParentId = null, string? Identification = null, string? Phone = null);
public sealed record CatalogProduct(long Id, string Sku, string? Barcode, string Name, long CategoryId, long? SubcategoryId, decimal Price, decimal Cost, string Unit, decimal MinimumStock, bool TrackInventory, bool Active, uint Version, long[] SupplierIds, string? ImageRef = null);
public sealed record StockView(long ProductId, string Sku, string Name, long CategoryId, long LocationId, string Location, decimal Quantity, decimal MinimumStock, bool Active) { public StockView() : this(0, "", "", 0, 0, "", 0, 0, false) { } }
public sealed record MovementView(long Id, long OperationId, long ProductId, string Product, long LocationId, string Location, decimal Delta, string Type, string Reason, long UserId, string Username, DateTime CreatedAt) { public decimal? BeforeQuantity { get; init; } public decimal? AfterQuantity { get; init; } public string? ReasonCode { get; init; } public string? Reference { get; init; } public string? Supplier { get; init; } public long? SupplierId { get; init; } public decimal? Cost { get; init; } public MovementView() : this(0, 0, 0, "", 0, "", 0, "", "", 0, "", default) { } }
public sealed record InventoryInput(string Key, string Type, long ProductId, long LocationId, long? DestinationId, decimal Quantity, string Reason);
public interface ICatalogStore
{
    Task<T> Execute<T>(Func<ICatalogSession, Task<T>> action);
}
public interface ICatalogSession
{
    Task<IReadOnlyList<CatalogMaster>> Masters(string kind);
    Task<long> SaveMaster(string kind, CatalogMaster value);
    Task<CatalogProduct?> Product(long id);
    Task<IReadOnlyList<CatalogProduct>> Products(string? search, long? categoryId, long? subcategoryId, bool? active, long? locationId, int offset, int limit);
    Task<long> SaveProduct(CatalogProduct product);
    Task SetImage(long id, string? reference);
    Task<IReadOnlyList<StockView>> Stocks(long? productId, long? locationId);
    Task<IReadOnlyList<MovementView>> Movements(DateTime from, DateTime to, long? productId, string? type, int offset, int limit, long? locationId = null);
    Task<long?> Previous(long actor, string key, byte[] hash);
    Task<long> Move(long actor, InventoryInput input, byte[] hash);
    Task<long?> FindOperation(long actor, string kind, string key, byte[] hash);
    Task RememberOperation(long actor, string kind, string key, byte[] hash, long id);
    Task<long> MoveBatch(long actor, InventoryBatchInput input);
    Task<IReadOnlyList<InventoryCount>> Counts(int offset, int limit);
    Task<InventoryCountDetail?> Count(long id);
    Task<long> CreateCount(long actor, CountCreateInput input);
    Task SaveCount(long id, CountQuantity[] lines);
    Task SetCountState(long id, string state, long? actor = null, long? operation = null);
    Task<(decimal Quantity, long LastMovement)> StockMarker(long product, long location);
    Task Audit(long actor, string action, long id);
}
public static class InventoryRules
{
    public static bool ValidQuantity(decimal quantity) => quantity > 0 && quantity <= 999999999999m && decimal.Round(quantity, 3) == quantity;
    public static decimal Balance(decimal current, decimal delta)
    {
        var result = current + delta;
        if (result < 0 || result > 999999999999999m) throw new InvalidOperationException("insufficient_stock");
        return result;
    }
}

public sealed class CatalogError(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
