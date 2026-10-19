namespace Nexo.Domain;

public sealed record Register(long Id, string Code, string Name, bool Active, bool OutOfService, uint Version, long? OpenShiftId = null)
{ public Register() : this(0,"","",true,false,0) { } }
public sealed record Shift(long Id, long RegisterId, long UserId, string Register, string Username, DateTime OpenedAt, DateTime? ClosedAt, decimal Opening, string State, decimal? Expected, decimal? Counted, decimal? Difference, string? Notes)
{ public Shift() : this(0,0,0,"","",default,null,0,"",null,null,null,null) { } }
public sealed record SaleLineInput(long ProductId, decimal Quantity, decimal Discount);
public sealed record PaymentInput(string Method, decimal Received,
 [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Reference = null);
public sealed record SaleInput(string Key, long ShiftId, string? Customer, SaleLineInput[] Lines, PaymentInput[] Payments);
public sealed record OpenInput(string Key, long RegisterId, decimal Opening);
public sealed record CloseInput(string Key, decimal Counted, string? Notes);
public sealed record CashInput(string Key, string Type, decimal Amount, string Reason);
public sealed record VoidInput(string Key, long RefundShiftId, string Reason);
public sealed record SaleLine(long Id, long ProductId, string Sku, string Name, decimal Quantity, decimal Price, decimal Rate, decimal Subtotal, decimal Discount, decimal Tax, decimal Total, bool TrackInventory)
{ public SaleLine() : this(0,0,"","",0,0,0,0,0,0,0,false) { } }
public sealed record SalePayment(string Method, decimal Applied, decimal Received, decimal Change, string? Reference = null)
{ public SalePayment() : this("",0,0,0) { } }
public sealed record Sale(long Id, long ShiftId, long RegisterId, long UserId, string Username, string Register, DateTime Date, string State, string? Customer, decimal Subtotal, decimal Discount, decimal Tax, decimal Total)
{ public Sale() : this(0,0,0,0,"","",default,"",null,0,0,0,0) { } }
public sealed record Receipt(Sale Sale, IReadOnlyList<SaleLine> Lines, IReadOnlyList<SalePayment> Payments, string Issuer, string Number, string Type, decimal PendingRefund);
public sealed record CashMovement(long Id, string Type, decimal Amount, string Reason, DateTime Date, long UserId)
{ public CashMovement() : this(0,"",0,"",default,0) { } }
public sealed record Reconciliation(Shift Shift, decimal CashSales, decimal CashIn, decimal CashOut, decimal CashRefunds, decimal Expected, decimal Card, decimal Transfer, decimal PendingNonCashRefunds, IReadOnlyList<CashMovement> Movements);
public sealed record SalesSettings(decimal Rate, string Identification, string Address)
{ public SalesSettings() : this(0,"","") { } }
public interface IPosStore { Task<T> Execute<T>(Func<IPosSession,Task<T>> action); }
public interface IPosSession
{
 Task<IReadOnlyList<Register>> Registers();
 Task<long> SaveRegister(Register value);
 Task<IReadOnlyList<Shift>> Shifts(long? actor, int offset);
 Task<Shift?> Shift(long id);
 Task<long> Open(long actor, OpenInput input);
 Task Close(long id, CloseInput input, decimal expected);
 Task<long> Cash(long actor, long shift, CashInput input);
 Task<Reconciliation> Reconcile(Shift shift);
 Task<IReadOnlyList<Sale>> Sales(long? actor, long? shift, int offset);
 Task<Receipt?> Receipt(long id);
 Task<CatalogProduct?> Product(long id);
 Task<SalesSettings> Settings();
 Task<long> Confirm(long actor, SaleInput input, IReadOnlyList<SaleLine> lines, IReadOnlyList<SalePayment> payments);
 Task<long> Void(long actor, Receipt receipt, VoidInput input);
 Task<long?> Previous(long actor, string type, string key, byte[] hash);
 Task Remember(long actor, string type, string key, byte[] hash, long id);
 Task Audit(long actor, string action, long id);
}
public interface IProductImageFiles
{
 Task<string> Write(byte[] bytes);
 Task<byte[]?> Read(string name);
 Task Delete(string name);
}
