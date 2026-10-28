namespace Nexo.Domain;

public sealed record InventoryLine(long ProductId, long LocationId, long? DestinationId, decimal Quantity, decimal? Cost = null);
public sealed record InventoryBatchInput(string Key, string Type, string ReasonCode, string Reason, long? SupplierId, string? Reference, InventoryLine[] Lines);
public sealed record CountCreateInput(string Key, long LocationId, long[] ProductIds, string Reason);
public sealed record CountQuantity(long ProductId, decimal? Quantity);
public sealed record CountSaveInput(uint Version, CountQuantity[] Lines);
public sealed record CountActionInput(uint Version);
public sealed record InventoryCount(long Id, long LocationId, string Location, string State, string Reason, long CreatedBy, string Creator, DateTime CreatedAt, long? ApprovedBy, string? Approver, DateTime? ApprovedAt, long? OperationId, uint Version)
{
    public InventoryCount() : this(0,0,"","","",0,"",default,null,null,null,null,0) { }
}
public sealed record InventoryCountLine(long ProductId, string Sku, string Name, decimal SystemQuantity, long LastMovementId, decimal? CountedQuantity)
{
    public InventoryCountLine() : this(0,"","",0,0,null) { }
    public decimal? Difference => CountedQuantity - SystemQuantity;
}
public sealed record InventoryCountDetail(InventoryCount Count, IReadOnlyList<InventoryCountLine> Lines);
