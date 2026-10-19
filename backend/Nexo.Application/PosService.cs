using System.Security.Cryptography;
using System.Text.Json;
using Nexo.Domain;
namespace Nexo.Application;

public sealed class PosService(IPosStore store)
{
 public static readonly string[] Permissions=["cajas.leer","cajas.administrar","cajas.abrir","cajas.cerrar","cajas.movimientos","cajas.arquear","cajas.historial","ventas.registrar","ventas.leer","ventas.descuentos","ventas.anular"];
 private static void Need(bool ok,string code="invalid_pos",int status=400) { if(!ok) throw new CatalogError(status,code); }
 public static decimal Round(decimal amount)=>decimal.Round(amount,2,MidpointRounding.AwayFromZero);
 public static bool Money(decimal value)=>value>=0 && value<=999999999999m && Round(value)==value;
 public static SaleLine CalculateLine(CatalogProduct product,SaleLineInput input,decimal rate)
 {
  Need(InventoryRules.ValidQuantity(input.Quantity)&&Money(input.Discount));
  Need(rate>=0&&rate<=1&&decimal.Round(rate,4)==rate);
  var subtotal=Round(product.Price*input.Quantity);Need(Money(subtotal)&&input.Discount<=subtotal);
  var tax=Round((subtotal-input.Discount)*rate);
  return new(0,product.Id,product.Sku,product.Name,input.Quantity,product.Price,rate,subtotal,input.Discount,tax,subtotal-input.Discount+tax,product.TrackInventory);
 }
 private static void Reason(string? value,int max=500)=>Need(!string.IsNullOrWhiteSpace(value)&&value.Length<=max);
 private static void Owner(Shift shift,long actor)=>Need(shift.UserId==actor,"shift_owner",403);
 private static void Opened(Shift shift)=>Need(shift.State=="ABIERTO","shift_closed",409);
 private static async Task<Shift> Get(IPosSession db,long id)=>await db.Shift(id)??throw new CatalogError(404,"shift_not_found");
 private Task<long> Once<T>(long actor,string type,string key,T input,Func<IPosSession,Task<long>> action)=>store.Execute(async db=>
 {
  Need(Guid.TryParse(key,out _)); var hash=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
  var old=await db.Previous(actor,type,key,hash); if(old.HasValue)return old.Value;
  var id=await action(db); await db.Remember(actor,type,key,hash,id); await db.Audit(actor,type,id); return id;
 });
 public Task<IReadOnlyList<Register>> Registers()=>store.Execute(db=>db.Registers());
 public Task<SalesSettings> Settings()=>store.Execute(db=>db.Settings());
 public Task<long> SaveRegister(long actor,Register input)=>store.Execute(async db=>
 {
  Need(!string.IsNullOrWhiteSpace(input.Code)&&input.Code.Length<=30&&!string.IsNullOrWhiteSpace(input.Name)&&input.Name.Length<=100);
  var old=(await db.Registers()).SingleOrDefault(x=>x.Id==input.Id);
  Need(input.Id==0||old!=null,"register_not_found",404); Need(old==null||old.Version==input.Version,"stale_version",409);
  Need(old?.OpenShiftId==null || (input.Active&&!input.OutOfService),"register_open",409);
  var id=await db.SaveRegister(input);await db.Audit(actor,"CAJA_GUARDAR",id);return id;
 });
 public Task<IReadOnlyList<Shift>> Shifts(long actor,bool history,int page) {Need(page is >=1 and <=100000);return store.Execute(db=>db.Shifts(history?null:actor,(page-1)*50));}
 public Task<long> Open(long actor,OpenInput input)=>Once(actor,"TURNO_ABRIR",input.Key,input,async db=>
 {
  Need(Money(input.Opening));var register=(await db.Registers()).SingleOrDefault(x=>x.Id==input.RegisterId);
  Need(register is {Active:true,OutOfService:false},"register_unavailable",409);Need(register!.OpenShiftId==null,"register_open",409);
  Need(!(await db.Shifts(actor,0)).Any(x=>x.State=="ABIERTO"),"user_shift_open",409);
  return await db.Open(actor,input);
 });
 public Task<Reconciliation> Reconcile(long actor,bool canViewOtherShifts,long shift)=>store.Execute(async db=>{var s=await Get(db,shift);if(!canViewOtherShifts)Owner(s,actor);return await db.Reconcile(s);});
 public Task<long> Close(long actor,long shift,CloseInput input)=>Once(actor,"TURNO_CERRAR",input.Key,new {shift,input},async db=>
 {Need(Money(input.Counted)&&(input.Notes?.Length??0)<=1000);var s=await Get(db,shift);Owner(s,actor);Opened(s);var r=await db.Reconcile(s);await db.Close(shift,input,r.Expected);return shift;});
 public Task<long> Cash(long actor,long shift,CashInput input)=>Once(actor,"CAJA_MOVIMIENTO",input.Key,new {shift,input},async db=>
 {Need(input.Type is "INGRESO" or "EGRESO"&&Money(input.Amount)&&input.Amount>0);Reason(input.Reason);var s=await Get(db,shift);Owner(s,actor);Opened(s);Need(input.Type!="EGRESO"||(await db.Reconcile(s)).Expected>=input.Amount,"cash_insufficient",409);return await db.Cash(actor,shift,input);});
 public Task<IReadOnlyList<Sale>> Sales(long actor,bool history,long? shift,int page) {Need(page is >=1 and <=100000);return store.Execute(db=>db.Sales(history?null:actor,shift,(page-1)*50));}
 public Task<Receipt> Receipt(long actor,bool history,long id)=>store.Execute(async db=>{var r=await db.Receipt(id)??throw new CatalogError(404,"sale_not_found");Need(history||r.Sale.UserId==actor,"sale_owner",403);return r;});
 public Task<long> Confirm(long actor,bool discounts,SaleInput input)=>Once(actor,"VENTA",input.Key,input,async db=>
 {
  Need(input.Lines is {Length:>0 and <=100}&&input.Payments is {Length:>0 and <=3}&&(input.Customer?.Length??0)<=180);
  Need(input.Lines.All(x=>x!=null)&&input.Payments.All(x=>x!=null));
  var shift=await Get(db,input.ShiftId);Owner(shift,actor);Opened(shift);
  Need(input.Lines.Select(x=>x.ProductId).Distinct().Count()==input.Lines.Length);
  var settings=await db.Settings();var lines=new List<SaleLine>();
  foreach(var l in input.Lines.OrderBy(x=>x.ProductId))
  {
   Need(InventoryRules.ValidQuantity(l.Quantity)&&Money(l.Discount));Need(l.Discount==0||discounts,"discount_forbidden",403);
   var p=await db.Product(l.ProductId);Need(p is {Active:true},"product_unavailable",409);
   lines.Add(CalculateLine(p!,l,settings.Rate));
  }
  var amount=lines.Sum(x=>x.Total);Need(Money(amount)&&amount>0);
  var payments=Allocate(amount,input.Payments);
  return await db.Confirm(actor,input,lines,payments);
 });
 public static IReadOnlyList<SalePayment> Allocate(decimal total,PaymentInput[] input)
 {
  Need(input is {Length:>0 and <=3}&&input.All(x=>x!=null)&&input.Select(x=>x.Method).Distinct().Count()==input.Length);
  Need(input.All(x=>x.Method is "EFECTIVO" or "TARJETA" or "TRANSFERENCIA" && Money(x.Received)&&x.Received>0));
  foreach(var payment in input)
  {
   if(payment.Method=="EFECTIVO") Need(string.IsNullOrWhiteSpace(payment.Reference),"cash_reference_not_allowed");
   else
   {
    Need(!string.IsNullOrWhiteSpace(payment.Reference),"payment_reference_required");
    var reference=payment.Reference!.Trim();
    Need(payment.Reference.Length<=100&&reference.Length<=100&&!payment.Reference.Any(char.IsControl)&&reference.All(c=>char.IsAsciiLetterOrDigit(c)||" -_./:".Contains(c)),"invalid_payment_reference");
   }
  }
  var paid=input.Sum(x=>x.Received);Need(paid>=total,"payment_incomplete");var change=paid-total;
  var cash=input.SingleOrDefault(x=>x.Method=="EFECTIVO");Need(change==0||(cash!=null&&cash.Received>change),"invalid_change");
  return input.Select(x=>new SalePayment(x.Method,x.Received-(x.Method=="EFECTIVO"?change:0),x.Received,x.Method=="EFECTIVO"?change:0,x.Method=="EFECTIVO"?null:x.Reference!.Trim())).ToArray();
 }
 public Task<long> Void(long actor,long sale,VoidInput input)=>Once(actor,"VENTA_ANULAR",input.Key,new {sale,input},async db=>
 {
  Reason(input.Reason);var receipt=await db.Receipt(sale)??throw new CatalogError(404,"sale_not_found");Need(receipt.Sale.State=="CONFIRMADA","sale_already_void",409);
  var shift=await Get(db,input.RefundShiftId);Owner(shift,actor);Opened(shift);
  var refund=receipt.Payments.Where(x=>x.Method=="EFECTIVO").Sum(x=>x.Applied);
  Need((await db.Reconcile(shift)).Expected>=refund,"cash_insufficient",409);
  return await db.Void(actor,receipt,input);
 });
}
