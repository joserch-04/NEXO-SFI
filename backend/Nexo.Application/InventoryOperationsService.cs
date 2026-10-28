using System.Security.Cryptography;
using System.Text.Json;
using Nexo.Domain;
namespace Nexo.Application;

public sealed class InventoryOperationsService(ICatalogStore store)
{
    private static void Check(bool condition, string code = "invalid_inventory_operation", int status = 400) => CatalogService.Require(condition, code, status);
    private static byte[] Hash<T>(T value) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value));
    public static readonly string[] ExitReasons = ["DANO", "VENCIMIENTO", "PERDIDA", "CONSUMO_INTERNO", "OTRO"];
    public static void ValidateBatch(InventoryBatchInput input)
    {
        Check(Guid.TryParse(input.Key, out _) && input.Type is "ENTRADA" or "SALIDA" or "TRASLADO");
        Check(!string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Length <= 500 && (input.Reference?.Length ?? 0) <= 100);
        Check(input.Lines is { Length: >= 1 and <= 100 });
        Check(input.Type == "ENTRADA" ? input.SupplierId > 0 && !string.IsNullOrWhiteSpace(input.Reference) && input.ReasonCode == "RECEPCION"
            : input.SupplierId == null && (input.Type == "SALIDA" ? ExitReasons.Contains(input.ReasonCode) : input.ReasonCode == "TRASLADO"));
        Check(input.Lines!.All(l=>l!=null));
        Check(input.Lines!.Select(x => (x.ProductId,x.LocationId,x.DestinationId)).Distinct().Count() == input.Lines.Length);
        foreach (var line in input.Lines)
        {
            Check(line.ProductId > 0 && line.LocationId > 0 && InventoryRules.ValidQuantity(line.Quantity));
            Check(input.Type == "TRASLADO" ? line.DestinationId > 0 && line.DestinationId != line.LocationId : line.DestinationId == null);
            Check(input.Type == "ENTRADA" ? line.Cost is >= 0 and <= 99999999999999m && decimal.Round(line.Cost.Value,4) == line.Cost : line.Cost == null);
        }
    }
    public Task<long> Register(long actor, InventoryBatchInput input) => store.Execute(async db =>
    {
        ValidateBatch(input);
        var hash = Hash(input); var old = await db.FindOperation(actor,"INVENTARIO_LOTE",input.Key,hash); if (old.HasValue) return old.Value;
        await ValidateReferences(db,input);
        var id = await db.MoveBatch(actor,input);
        await db.RememberOperation(actor,"INVENTARIO_LOTE",input.Key,hash,id);
        await db.Audit(actor,"inventario." + input.Type,id); return id;
    });
    private static async Task ValidateReferences(ICatalogSession db, InventoryBatchInput input)
    {
        var locations = await db.Masters("ubicaciones");
        if(input.SupplierId.HasValue) Check((await db.Masters("proveedores")).Any(s => s.Id == input.SupplierId && s.Active));
        foreach(var line in input.Lines)
        {
            Check(await db.Product(line.ProductId) is { Active:true, TrackInventory:true },"inventory_disabled",409);
            Check(locations.Any(l => l.Id == line.LocationId && l.Active) && (!line.DestinationId.HasValue || locations.Any(l => l.Id == line.DestinationId && l.Active)));
        }
    }
    public Task<IReadOnlyList<InventoryCount>> Counts(int page, int size)
    {
        Check(page is >=1 and <=100000 && size is >=1 and <=100);
        return store.Execute(db => db.Counts((page-1)*size,size));
    }
    public Task<InventoryCountDetail> Count(long id) => store.Execute(async db => await Required(db,id));
    private static async Task<InventoryCountDetail> Required(ICatalogSession db,long id) => await db.Count(id) ?? throw new SecurityError(404,"count_not_found");
    public Task<InventoryCountDetail> Create(long actor,CountCreateInput input) => store.Execute(async db =>
    {
        Check(Guid.TryParse(input.Key,out _) && !string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Length <= 500 && input.ProductIds is {Length:>=1 and <=100});
        Check(input.ProductIds!.Distinct().Count() == input.ProductIds.Length);
        Check((await db.Masters("ubicaciones")).Any(l => l.Id == input.LocationId && l.Active));
        var hash=Hash(input); var old=await db.FindOperation(actor,"CONTEO_CREAR",input.Key,hash); if(old.HasValue)return await Required(db,old.Value);
        foreach(var id in input.ProductIds) Check(await db.Product(id) is {Active:true,TrackInventory:true},"inventory_disabled",409);
        var count=await db.CreateCount(actor,input); await db.RememberOperation(actor,"CONTEO_CREAR",input.Key,hash,count);
        await db.Audit(actor,"inventario.conteo_crear",count); return await Required(db,count);
    });
    public Task<InventoryCountDetail> Save(long actor,long id,CountSaveInput input) => store.Execute(async db =>
    {
        var current=await Required(db,id); Check(current.Count.State=="BORRADOR","count_locked",409); Check(current.Count.Version==input.Version,"stale_version",409);
        Check(input.Lines != null && input.Lines.Length==current.Lines.Count && input.Lines.All(l=>l!=null) && input.Lines.Select(x=>x.ProductId).Distinct().Count()==input.Lines.Length);
        Check(input.Lines!.All(l=>l!=null));
        Check(input.Lines!.All(l=>current.Lines.Any(x=>x.ProductId==l.ProductId) && (l.Quantity==null || (l.Quantity is >=0 and <=999999999999m && decimal.Round(l.Quantity.Value,3)==l.Quantity))));
        await db.SaveCount(id,input.Lines!); await db.Audit(actor,"inventario.conteo_capturar",id);return await Required(db,id);
    });
    public Task<InventoryCountDetail> Submit(long actor,long id,CountActionInput input) => store.Execute(async db =>
    {
        var current=await Required(db,id); if(current.Count.State=="PENDIENTE")return current;
        Check(current.Count.State=="BORRADOR","count_locked",409); Check(current.Count.Version==input.Version,"stale_version",409);
        Check(current.Lines.All(l=>l.CountedQuantity.HasValue),"count_incomplete");
        await db.SetCountState(id,"PENDIENTE");await db.Audit(actor,"inventario.conteo_enviar",id);return await Required(db,id);
    });
    public Task<InventoryCountDetail> Cancel(long actor,long id,CountActionInput input) => store.Execute(async db =>
    {
        var current=await Required(db,id);if(current.Count.State=="CANCELADO")return current;
        Check(current.Count.State is "BORRADOR" or "PENDIENTE","count_locked",409);Check(current.Count.Version==input.Version,"stale_version",409);
        await db.SetCountState(id,"CANCELADO");await db.Audit(actor,"inventario.conteo_cancelar",id);return await Required(db,id);
    });
    public Task<InventoryCountDetail> Approve(long actor,long id,CountActionInput input) => store.Execute(async db =>
    {
        var current=await Required(db,id);if(current.Count.State=="APLICADO")return current;
        Check(current.Count.State=="PENDIENTE","count_locked",409);Check(current.Count.Version==input.Version,"stale_version",409);
        Check((await db.Masters("ubicaciones")).Any(l=>l.Id==current.Count.LocationId && l.Active));
        var changes=new List<InventoryLine>();
        foreach(var line in current.Lines)
        {
            Check(await db.Product(line.ProductId) is {Active:true,TrackInventory:true},"inventory_disabled",409);
            var stock=await db.StockMarker(line.ProductId,current.Count.LocationId);
            Check(stock.Quantity==line.SystemQuantity && stock.LastMovement==line.LastMovementId,"count_stock_changed",409);
            Check(line.CountedQuantity.HasValue,"count_incomplete");
            if(line.Difference != 0)changes.Add(new(line.ProductId,current.Count.LocationId,null,line.Difference!.Value));
        }
        var operation=await db.MoveBatch(actor,new("","AJUSTE","CONTEO",current.Count.Reason,null,$"Conteo #{id}",changes.ToArray()));
        await db.SetCountState(id,"APLICADO",actor,operation);await db.Audit(actor,"inventario.conteo_aprobar",id);
        return await Required(db,id);
    });
}
