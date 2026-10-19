using Dapper;
using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
using Xunit;
using Xunit.Abstractions;
namespace Nexo.IntegrationTests;

public class MySqlPosTests(ITestOutputHelper output)
{
 private static string Key()=>Guid.NewGuid().ToString();
 private sealed class Stop:Exception;
 private sealed class Existing(IPosSession db):IPosStore {public Task<T> Execute<T>(Func<IPosSession,Task<T>> action)=>action(db);}
 private static MySqlConnectionFactory Factory()=>new(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!);
 private static PosService Service()=>new(new MySqlPosStore(Factory()));
 private static async Task<(long Actor,long Other)> Actors()
 {await using var c=await Factory().OpenAsync();var ids=(await c.QueryAsync<long>("SELECT id FROM usuarios ORDER BY id LIMIT 2")).ToArray();return(ids[0],ids[1]);}
 private static async Task<long> Register(PosService s,long actor)=>await s.SaveRegister(actor,new(0,"F7-"+Guid.NewGuid().ToString("N")[..20],"Caja de prueba F7",true,false,0));
 private static async Task<long> Product(long actor,bool tracking=true,decimal quantity=5)
 {
  var catalog=new CatalogService(new MySqlCatalogStore(Factory()));var category=(await catalog.Masters("categorias")).First(x=>x.Active).Id;
  var id=await catalog.SaveProduct(actor,new(0,"F7-"+Guid.NewGuid().ToString("N"),null,"Producto F7",category,null,25,10,"UNIDAD",0,tracking,true,0,[]));
  if(tracking&&quantity>0)await catalog.Move(actor,new(Key(),"ENTRADA",id,(await catalog.Masters("ubicaciones")).First(x=>x.Active).Id,null,quantity,"Fixture F7"));return id;
 }
 private static SaleInput Sale(long shift,long product,string method="EFECTIVO",decimal amount=25)=>new(Key(),shift,null,[new(product,1,0)],[new(method,amount,method=="EFECTIVO"?null:"REF-"+Key())]);
 private static async Task<decimal> Stock(long id){await using var c=await Factory().OpenAsync();return await c.ExecuteScalarAsync<decimal>("SELECT cantidad FROM existencias WHERE producto_id=@id",new{id});}
 private static async Task<string> Error(Func<Task<long>> action){try{await action();return "ok";}catch(CatalogError e){return e.Code;}}
 [MySqlFact]
 public async Task References_roundtrip_idempotency_void_and_legacy_replay()
 {
  var (actor,_)=await Actors();var product=await Product(actor,false);
  await Assert.ThrowsAsync<Stop>(()=>new MySqlPosStore(Factory()).Execute<bool>(async db=>
  {
   var s=new PosService(new Existing(db));var shift=await s.Open(actor,new(Key(),await Register(s,actor),100));
   foreach(var method in new[]{"TARJETA","TRANSFERENCIA"})
   {
    var invalid=Sale(shift,product,method) with{Payments=[new(method,25)]};
    Assert.Equal("payment_reference_required",await Error(()=>s.Confirm(actor,false,invalid)));
   }
   Assert.Empty(await s.Sales(actor,false,shift,1));
   var input=new SaleInput(Key(),shift,null,[new(product,1,0)],[new("EFECTIVO",5),new("TARJETA",10," 0000-NFC-123 "),new("TRANSFERENCIA",10,"0000-TRF-456")]);
   var sale=await s.Confirm(actor,false,input);Assert.Equal(sale,await s.Confirm(actor,false,input));
   var receipt=await s.Receipt(actor,false,sale);
   Assert.Null(receipt.Payments.Single(x=>x.Method=="EFECTIVO").Reference);
   Assert.Equal("0000-NFC-123",receipt.Payments.Single(x=>x.Method=="TARJETA").Reference);
   Assert.Equal("0000-TRF-456",receipt.Payments.Single(x=>x.Method=="TRANSFERENCIA").Reference);
   Assert.Equal("idempotency_conflict",await Error(()=>s.Confirm(actor,false,input with{Payments=[new("TARJETA",25,"OTRA-REFERENCIA")]})));
   await s.Void(actor,sale,new(Key(),shift,"Anulación conserva referencias"));
   Assert.Equal("0000-NFC-123",(await s.Receipt(actor,false,sale)).Payments.Single(x=>x.Method=="TARJETA").Reference);
   // Emular una venta confirmada antes de 009 y su hash exacto sin propiedad Reference.
   var legacy=new SaleInput(Key(),shift,null,[new(product,1,0)],[new("TARJETA",25)]);
   var p=(await db.Product(product))!;
   var legacyId=await db.Confirm(actor,legacy,[PosService.CalculateLine(p,legacy.Lines[0],0)],[new("TARJETA",25,25,0)]);
   var oldBody=new{legacy.Key,legacy.ShiftId,legacy.Customer,legacy.Lines,Payments=new[]{new{Method="TARJETA",Received=25m}}};
   var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(oldBody));
   await db.Remember(actor,"VENTA",legacy.Key,hash,legacyId);
   Assert.Equal(legacyId,await s.Confirm(actor,false,legacy));
   Assert.Null((await s.Receipt(actor,false,legacyId)).Payments.Single().Reference);
   Assert.Equal(2,(await s.Sales(actor,false,shift,1)).Count);
   throw new Stop();
  }));
 }
 [MySqlFact]
 public async Task Sales_payments_prepared_discounts_receipts_reconciliation_void_and_close()
 {
  var (actor,_)=await Actors();var store=new MySqlPosStore(Factory());var regular=await Product(actor,true,20);var prepared=await Product(actor,false);
  await Assert.ThrowsAsync<Stop>(()=>store.Execute<bool>(async db=>
  {
   var s=new PosService(new Existing(db));var register=await Register(s,actor);var opening=new OpenInput(Key(),register,100);
   var shift=await s.Open(actor,opening);Assert.Equal(shift,await s.Open(actor,opening));
   var first=Sale(shift,regular,"EFECTIVO",50);var sale=await s.Confirm(actor,false,first);Assert.Equal(sale,await s.Confirm(actor,false,first));
   Assert.Equal("idempotency_conflict",await Error(()=>s.Confirm(actor,false,first with{Customer="Otro"})));
   var receipt=await s.Receipt(actor,false,sale);Assert.Equal(25,receipt.Payments.Single().Change);Assert.Contains("INT-",receipt.Number);Assert.Equal("COMPROBANTE DE VENTA",receipt.Type);
   await s.Confirm(actor,false,Sale(shift,regular,"TARJETA"));await s.Confirm(actor,false,Sale(shift,regular,"TRANSFERENCIA"));
   var combined=new SaleInput(Key(),shift,"Cliente de prueba",[new(regular,2,5),new(prepared,1,0)],[new("EFECTIVO",50),new("TARJETA",20,"NFC-000020")]);
   Assert.Equal("discount_forbidden",await Error(()=>s.Confirm(actor,false,combined)));
   var multi=await s.Confirm(actor,true,combined);Assert.Equal(70,(await s.Receipt(actor,false,multi)).Sale.Total);
   var voidInput=new VoidInput(Key(),shift,"Error en cobro");Assert.Equal(multi,await s.Void(actor,multi,voidInput));Assert.Equal(multi,await s.Void(actor,multi,voidInput));
   Assert.Equal("sale_already_void",await Error(()=>s.Void(actor,multi,voidInput with{Key=Key()})));
   receipt=await s.Receipt(actor,false,multi);Assert.Equal("ANULADA",receipt.Sale.State);Assert.Equal(20,receipt.PendingRefund);
   Assert.Equal("NFC-000020",receipt.Payments.Single(x=>x.Method=="TARJETA").Reference);
   await s.Cash(actor,shift,new(Key(),"INGRESO",10,"Reposición"));await s.Cash(actor,shift,new(Key(),"EGRESO",5,"Retiro autorizado"));
   var r=await s.Reconcile(actor,false,shift);Assert.Equal(130,r.Expected);Assert.Equal(45,r.Card);Assert.Equal(25,r.Transfer);Assert.Equal(50,r.CashRefunds);
   var close=new CloseInput(Key(),135,"Sobrante de cinco");Assert.Equal(shift,await s.Close(actor,shift,close));Assert.Equal(shift,await s.Close(actor,shift,close));
   r=await s.Reconcile(actor,false,shift);Assert.Equal(5,r.Shift.Difference);
   Assert.Equal("shift_closed",await Error(()=>s.Close(actor,shift,close with{Key=Key()})));
   Assert.Equal("shift_closed",await Error(()=>s.Confirm(actor,false,Sale(shift,prepared))));
   var next=await s.Open(actor,new(Key(),register,100));await s.Close(actor,next,new(Key(),90,"Faltante"));Assert.Equal(-10,(await s.Reconcile(actor,false,next)).Shift.Difference);
   throw new Stop();
  }));
  Assert.Equal(20,await Stock(regular));Assert.Equal(0,await Stock(prepared));
 }
 [MySqlFact]
 public async Task Insufficient_stock_and_injected_failure_rollback_all_sale_records()
 {
  var (actor,_)=await Actors();var good=await Product(actor,true,2);var empty=await Product(actor,true,0);var s=Service();var register=await Register(s,actor);var shift=await s.Open(actor,new(Key(),register,100));
  try
  {
   var input=new SaleInput(Key(),shift,null,[new(good,1,0),new(empty,1,0)],[new("EFECTIVO",50)]);
   Assert.Equal("insufficient_stock",await Error(()=>s.Confirm(actor,false,input)));Assert.Equal(2,await Stock(good));Assert.Empty(await s.Sales(actor,false,shift,1));
   input=Sale(shift,good);var failed=input;
   await Assert.ThrowsAsync<Stop>(()=>new MySqlPosStore(Factory()).Execute<long>(async db=>{await new PosService(new Existing(db)).Confirm(actor,false,failed);throw new Stop();}));
   Assert.Equal(2,await Stock(good));Assert.Empty(await s.Sales(actor,false,shift,1));
   var sale=await s.Confirm(actor,false,input);Assert.Equal(1,await Stock(good));Assert.Single(await s.Sales(actor,false,shift,1));
   await s.Void(actor,sale,new(Key(),shift,"Reversión del ensayo"));
  }
  finally {await s.Close(actor,shift,new(Key(),100,"Prueba finalizada"));}
 }
 [MySqlFact]
 public async Task Concurrent_last_stock_duplicate_sale_void_open_and_close()
 {
  var (actor,other)=await Actors();var s=Service();var p=await Product(actor,true,1);var prepared=await Product(actor,false);var register=await Register(s,actor);
  var openResults=await Task.WhenAll(Error(()=>s.Open(actor,new(Key(),register,100))),Error(()=>s.Open(other,new(Key(),register,100))));Assert.Single(openResults,x=>x=="ok");
  var open=(await s.Shifts(actor,true,1)).Single(x=>x.RegisterId==register&&x.State=="ABIERTO");var initialActor=actor;actor=open.UserId;other=actor==initialActor?other:initialActor;var shift=open.Id;
  var otherRegister=await Register(s,other);var otherShift=await s.Open(other,new(Key(),otherRegister,100));
  var a=Sale(shift,p);var b=Sale(otherShift,p);
  var results=await Task.WhenAll(Error(()=>s.Confirm(actor,false,a)),Error(()=>s.Confirm(other,false,b)));Assert.Single(results,x=>x=="ok");Assert.Contains("insufficient_stock",results);Assert.Equal(0,await Stock(p));
  if(results[1]=="ok"){(actor,other)=(other,actor);(shift,otherShift)=(otherShift,shift);}
  var sale=(await s.Sales(actor,false,shift,1)).Single().Id;var original=results[0]=="ok"?a:b;
  var duplicates=await Task.WhenAll(s.Confirm(actor,false,original),s.Confirm(actor,false,original));Assert.All(duplicates,x=>Assert.Equal(sale,x));Assert.Equal(0,await Stock(p));
  var fresh=Sale(shift,prepared);var raced=await Task.WhenAll(s.Confirm(actor,false,fresh),s.Confirm(actor,false,fresh));Assert.Equal(raced[0],raced[1]);Assert.Equal(2,(await s.Sales(actor,false,shift,1)).Count);
  await s.Void(actor,raced[0],new(Key(),shift,"Preparado duplicado solo una vez"));Assert.Equal(0,await Stock(prepared));
  var voids=await Task.WhenAll(Error(()=>s.Void(actor,sale,new(Key(),shift,"Ensayo concurrente"))),Error(()=>s.Void(actor,sale,new(Key(),shift,"Segundo intento"))));Assert.Single(voids,x=>x=="ok");Assert.Equal(1,await Stock(p));
  var closures=await Task.WhenAll(Error(()=>s.Close(actor,shift,new(Key(),100,null))),Error(()=>s.Close(actor,shift,new(Key(),100,null))));Assert.Single(closures,x=>x=="ok");
  await s.Close(other,otherShift,new(Key(),100,"Segundo vendedor"));
 }
 [MySqlFact]
 public async Task Representative_products_cover_each_category_and_subcategory_without_selling_entire_catalog()
 {
  var (actor,_)=await Actors();var catalog=new CatalogService(new MySqlCatalogStore(Factory()));var categories=(await catalog.Masters("categorias")).Where(x=>x.Active).ToArray();
  var balances=(await catalog.Stocks(null,null)).GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
  bool Available(CatalogProduct p)=>!p.TrackInventory||balances.GetValueOrDefault(p.Id)>=1;
  var sample=new Dictionary<long,CatalogProduct>();
  foreach(var c in categories)
  {
   var products=(await catalog.Products(null,c.Id,null,true,null,1,100)).OrderByDescending(Available).ToArray();if(products.Length>0)sample.TryAdd(products[0].Id,products[0]);
   foreach(var group in products.Where(p=>p.SubcategoryId!=null).GroupBy(p=>p.SubcategoryId))sample.TryAdd(group.First().Id,group.First());
  }
  Assert.NotEmpty(sample);
  output.WriteLine($"Muestra: {sample.Count} productos, {sample.Values.Select(p=>p.CategoryId).Distinct().Count()} categorías, {sample.Values.Where(p=>p.SubcategoryId!=null).Select(p=>p.SubcategoryId).Distinct().Count()} subcategorías.");
  await Assert.ThrowsAsync<Stop>(()=>new MySqlPosStore(Factory()).Execute<bool>(async db=>
  {
   var s=new PosService(new Existing(db));var register=await Register(s,actor);var shift=await s.Open(actor,new(Key(),register,0));
   foreach(var p in sample.Values)
   {
    var amount=PosService.Round(p.Price);if(amount<=0)continue;
    var input=Sale(shift,p.Id,"EFECTIVO",amount);
    if(!Available(p)){Assert.Equal("insufficient_stock",await Error(()=>s.Confirm(actor,false,input)));continue;}
    var id=await s.Confirm(actor,false,input);Assert.Equal(p.Name,(await s.Receipt(actor,false,id)).Lines.Single().Name);
   }
   throw new Stop();
  }));
 }
}
