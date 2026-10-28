using Nexo.Application;
using Nexo.Domain;
using Nexo.Infrastructure;
using Xunit;
namespace Nexo.IntegrationTests;
public class MySqlInventoryOperationsTests
{
    private sealed record Fixture(long Actor,CatalogService Catalog,InventoryOperationsService Operations,long A,long B,long Origin,long Destination,long Supplier);
    private static async Task<Fixture> Create()
    {
        var factory=new MySqlConnectionFactory(Environment.GetEnvironmentVariable("NEXO_TEST_MYSQL")!);
        var actor=await new MySqlSecurityStore(factory).Execute(async db=>(await db.Users(0,1)).First().Id);
        var store=new MySqlCatalogStore(factory);var catalog=new CatalogService(store);var operations=new InventoryOperationsService(store);
        var category=(await catalog.Masters("categorias")).First(c=>c.Active).Id;
        var suffix=Guid.NewGuid().ToString("N")[..15];
        var origin=await catalog.SaveMaster(actor,"ubicaciones",new(0,"Origen ensayo "+suffix,true,0,"IO-"+suffix,"AREA"));
        var destination=await catalog.SaveMaster(actor,"ubicaciones",new(0,"Destino ensayo "+suffix,true,0,"ID-"+suffix,"AREA"));
        var supplier=(await catalog.Masters("proveedores")).First(s=>s.Active).Id;
        async Task<long> Product(string name)=>await catalog.SaveProduct(actor,new(0,"INV-"+name+suffix,null,"Inventario operativo "+name+suffix,category,null,25,10,"UNIDAD",2,true,true,0,[]));
        return new(actor,catalog,operations,await Product("A"),await Product("B"),origin,destination,supplier);
    }
    private static InventoryBatchInput Receipt(Fixture f)=>new(Guid.NewGuid().ToString(),"ENTRADA","RECEPCION","Recepción de ensayo",f.Supplier,"DOC-"+Guid.NewGuid().ToString("N")[..12],[new(f.A,f.Origin,null,10,8),new(f.B,f.Origin,null,5,12)]);
    [MySqlFact][Trait("Category","MySql")]
    public async Task Batch_receipt_idempotency_failure_rollback_transfer_and_count_workflow()
    {
        var f=await Create();var receipt=Receipt(f);
        var id=await f.Operations.Register(f.Actor,receipt);Assert.Equal(id,await f.Operations.Register(f.Actor,receipt));
        Assert.Equal("idempotency_conflict",(await Assert.ThrowsAsync<CatalogError>(()=>f.Operations.Register(f.Actor,receipt with{Reason="Otro contenido"}))).Code);
        var exit=new InventoryBatchInput(Guid.NewGuid().ToString(),"SALIDA","DANO","Daño de ensayo",null,null,[new(f.A,f.Origin,null,2),new(f.B,f.Origin,null,100)]);
        Assert.Equal("insufficient_stock",(await Assert.ThrowsAsync<CatalogError>(()=>f.Operations.Register(f.Actor,exit))).Code);
        Assert.Equal(10,(await f.Catalog.Stocks(f.A,f.Origin)).Single().Quantity); // La primera línea también revierte.
        var history=await f.Catalog.Movements(DateTime.UtcNow.AddDays(-1),DateTime.UtcNow.AddDays(1),f.A,null,1,100);
        Assert.Single(history);Assert.Equal(0,history[0].BeforeQuantity);Assert.Equal(10,history[0].AfterQuantity);Assert.Equal(8,history[0].Cost);Assert.Equal(receipt.Reference,history[0].Reference);Assert.Equal(f.Supplier,history[0].SupplierId);
        await f.Operations.Register(f.Actor,new(Guid.NewGuid().ToString(),"TRASLADO","TRASLADO","Reposición",null,null,[new(f.A,f.Origin,f.Destination,4)]));
        await f.Operations.Register(f.Actor,new(Guid.NewGuid().ToString(),"SALIDA","PERDIDA","Pérdida verificada",null,null,[new(f.A,f.Origin,null,1)]));
        Assert.Equal(5,(await f.Catalog.Stocks(f.A,f.Origin)).Single().Quantity);Assert.Equal(4,(await f.Catalog.Stocks(f.A,f.Destination)).Single().Quantity);
        var create=new CountCreateInput(Guid.NewGuid().ToString(),f.Origin,[f.A,f.B],"Conteo de ensayo");
        var count=await f.Operations.Create(f.Actor,create);Assert.Equal(count.Count.Id,(await f.Operations.Create(f.Actor,create)).Count.Id);
        count=await f.Operations.Save(f.Actor,count.Count.Id,new(count.Count.Version,[new(f.A,3),new(f.B,null)]));
        Assert.Equal("count_incomplete",(await Assert.ThrowsAsync<SecurityError>(()=>f.Operations.Submit(f.Actor,count.Count.Id,new(count.Count.Version)))).Code);
        Assert.Equal("stale_version",(await Assert.ThrowsAsync<SecurityError>(()=>f.Operations.Save(f.Actor,count.Count.Id,new(1,[new(f.A,3),new(f.B,8)])))).Code);
        count=await f.Operations.Save(f.Actor,count.Count.Id,new(count.Count.Version,[new(f.A,3),new(f.B,8)]));
        count=await f.Operations.Submit(f.Actor,count.Count.Id,new(count.Count.Version));
        Assert.Equal("count_locked",(await Assert.ThrowsAsync<SecurityError>(()=>f.Operations.Save(f.Actor,count.Count.Id,new(count.Count.Version,[new(f.A,3),new(f.B,8)])))).Code);
        var version=count.Count.Version;
        var approved=await Task.WhenAll(f.Operations.Approve(f.Actor,count.Count.Id,new(version)),f.Operations.Approve(f.Actor,count.Count.Id,new(version)));
        Assert.Equal(approved[0].Count.OperationId,approved[1].Count.OperationId);Assert.Equal("APLICADO",approved[0].Count.State);
        Assert.Equal(3,(await f.Catalog.Stocks(f.A,f.Origin)).Single().Quantity);Assert.Equal(8,(await f.Catalog.Stocks(f.B,f.Origin)).Single().Quantity);
        Assert.Equal("count_locked",(await Assert.ThrowsAsync<SecurityError>(()=>f.Operations.Cancel(f.Actor,count.Count.Id,new(approved[0].Count.Version)))).Code);
        var adjustments=await f.Catalog.Movements(DateTime.UtcNow.AddDays(-1),DateTime.UtcNow.AddDays(1),f.A,"AJUSTE",1,100,f.Origin);
        Assert.Single(adjustments);Assert.Equal(-2,adjustments[0].Delta);Assert.Equal("CONTEO",adjustments[0].ReasonCode);
        Assert.Empty(await f.Catalog.Movements(DateTime.UtcNow.AddDays(-1),DateTime.UtcNow.AddDays(1),f.A,"AJUSTE",1,100,f.Destination));
    }
    [MySqlFact][Trait("Category","MySql")]
    public async Task Count_detects_intervening_movements_even_when_quantity_is_unchanged_and_supports_zero_difference()
    {
        var f=await Create();await f.Operations.Register(f.Actor,Receipt(f));
        var count=await f.Operations.Create(f.Actor,new(Guid.NewGuid().ToString(),f.Origin,[f.A],"Conteo debe repetirse"));
        count=await f.Operations.Save(f.Actor,count.Count.Id,new(count.Count.Version,[new(f.A,9)]));count=await f.Operations.Submit(f.Actor,count.Count.Id,new(count.Count.Version));
        await f.Catalog.Move(f.Actor,new(Guid.NewGuid().ToString(),"ENTRADA",f.A,f.Origin,null,1,"Ingreso durante conteo"));
        await f.Catalog.Move(f.Actor,new(Guid.NewGuid().ToString(),"SALIDA",f.A,f.Origin,null,1,"Salida durante conteo"));
        Assert.Equal("count_stock_changed",(await Assert.ThrowsAsync<SecurityError>(()=>f.Operations.Approve(f.Actor,count.Count.Id,new(count.Count.Version)))).Code);
        Assert.Equal(10,(await f.Catalog.Stocks(f.A,f.Origin)).Single().Quantity);
        Assert.Equal("CANCELADO",(await f.Operations.Cancel(f.Actor,count.Count.Id,new(count.Count.Version))).Count.State);
        var empty=await f.Operations.Create(f.Actor,new(Guid.NewGuid().ToString(),f.Destination,[f.A],"Conteo de saldo cero"));
        empty=await f.Operations.Save(f.Actor,empty.Count.Id,new(empty.Count.Version,[new(f.A,0)]));empty=await f.Operations.Submit(f.Actor,empty.Count.Id,new(empty.Count.Version));
        empty=await f.Operations.Approve(f.Actor,empty.Count.Id,new(empty.Count.Version));Assert.Equal("APLICADO",empty.Count.State);Assert.NotNull(empty.Count.OperationId);
        var exits=new InventoryBatchInput(Guid.NewGuid().ToString(),"SALIDA","CONSUMO_INTERNO","Retiros concurrentes",null,null,[new(f.A,f.Origin,null,10)]);
        async Task<bool> Withdraw(InventoryBatchInput request){try{await f.Operations.Register(f.Actor,request);return true;}catch(CatalogError ex)when(ex.Code=="insufficient_stock"){return false;}}
        Assert.Single(await Task.WhenAll(Withdraw(exits),Withdraw(exits with{Key=Guid.NewGuid().ToString()})),x=>x);
        Assert.Equal(0,(await f.Catalog.Stocks(f.A,f.Origin)).Single().Quantity);
        var sales=await f.Catalog.Movements(DateTime.UtcNow.AddDays(-180),DateTime.UtcNow.AddDays(180),1,"VENTA",1,100);
        Assert.NotEmpty(sales);Assert.Contains(sales,m=>m.Reference=="Venta #1");
    }
}
