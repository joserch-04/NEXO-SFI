using System.Data;
using System.Text.Json;
using Dapper;
using MySqlConnector;
using Nexo.Domain;
namespace Nexo.Infrastructure;

public sealed class MySqlPosStore(MySqlConnectionFactory factory) : IPosStore
{
 public async Task<T> Execute<T>(Func<IPosSession,Task<T>> action)
 {
  await using var c=await factory.OpenAsync();await using var tx=await c.BeginTransactionAsync(IsolationLevel.ReadCommitted);
  try
  {
   // Mismo orden y mismo bloqueo que Fase 6, incluso en procesos API diferentes.
   await c.ExecuteScalarAsync<int>("SELECT id FROM catalogo_bloqueo WHERE id=1 FOR UPDATE",transaction:tx);
   var result=await action(new Session(c,tx));await tx.CommitAsync();return result;
  }
  catch(MySqlException e) when(e.Number is 1062 or 1451 or 1452 or 3819 or 1213 or 1205)
  {throw new CatalogError(409,e.Number is 1213 or 1205?"transaction_retry":"pos_conflict");}
 }
 private sealed class Session(MySqlConnection c,MySqlTransaction tx):IPosSession
 {
  private Task<int> Run(string sql,object? args=null)=>c.ExecuteAsync(sql,args,tx);
  private Task<T> Scalar<T>(string sql,object? args=null)=>c.ExecuteScalarAsync<T>(sql,args,tx)!;
  private async Task<IReadOnlyList<T>> List<T>(string sql,object? args=null)=>(await c.QueryAsync<T>(sql,args,tx)).AsList();
  public Task<IReadOnlyList<Register>> Registers()=>List<Register>("SELECT c.id Id,c.codigo Code,c.nombre Name,c.activo Active,c.fuera_servicio OutOfService,c.version Version,t.id OpenShiftId FROM cajas c LEFT JOIN turnos_caja t ON t.caja_id=c.id AND t.estado='ABIERTO' ORDER BY c.codigo");
  public async Task<long> SaveRegister(Register v)
  {
   if(v.Id==0){await Run("INSERT INTO cajas(codigo,nombre,activo,fuera_servicio) VALUES(@Code,@Name,@Active,@OutOfService)",v);return await Scalar<long>("SELECT LAST_INSERT_ID()");}
   await Run("UPDATE cajas SET codigo=@Code,nombre=@Name,activo=@Active,fuera_servicio=@OutOfService,version=version+1 WHERE id=@Id",v);return v.Id;
  }
  private const string ShiftSql="SELECT t.id Id,t.caja_id RegisterId,t.usuario_id UserId,c.nombre Register,u.nombre_usuario Username,t.apertura_en OpenedAt,t.cierre_en ClosedAt,t.fondo_inicial Opening,t.estado State,t.esperado Expected,t.contado Counted,t.diferencia Difference,t.observaciones Notes FROM turnos_caja t JOIN cajas c ON c.id=t.caja_id JOIN usuarios u ON u.id=t.usuario_id";
  public Task<IReadOnlyList<Shift>> Shifts(long? actor,int offset)=>List<Shift>(ShiftSql+" WHERE (@actor IS NULL OR t.usuario_id=@actor) ORDER BY (t.estado='ABIERTO') DESC,t.id DESC LIMIT 50 OFFSET @offset",new{actor,offset});
  public async Task<Shift?> Shift(long id)=>(await List<Shift>(ShiftSql+" WHERE t.id=@id",new{id})).SingleOrDefault();
  public async Task<long> Open(long actor,OpenInput v)
  {await Run("INSERT INTO turnos_caja(caja_id,usuario_id,apertura_en,fondo_inicial,estado) VALUES(@RegisterId,@actor,UTC_TIMESTAMP(6),@Opening,'ABIERTO')",new{v.RegisterId,actor,v.Opening});return await Scalar<long>("SELECT LAST_INSERT_ID()");}
  public async Task Close(long id,CloseInput input,decimal expected)=>await Run("UPDATE turnos_caja SET estado='CERRADO',cierre_en=UTC_TIMESTAMP(6),esperado=@expected,contado=@Counted,observaciones=@Notes WHERE id=@id AND estado='ABIERTO'",new{id,expected,input.Counted,input.Notes});
  public async Task<long> Cash(long actor,long shift,CashInput input)
  {await Run("INSERT INTO movimientos_caja(turno_id,usuario_id,tipo,importe,motivo,creado_en) VALUES(@shift,@actor,@Type,@Amount,@Reason,UTC_TIMESTAMP(6))",new{shift,actor,input.Type,input.Amount,input.Reason});return await Scalar<long>("SELECT LAST_INSERT_ID()");}
  public async Task<Reconciliation> Reconcile(Shift s)
  {
   // El efectivo cobrado permanece hasta una devolución explícita: anular no borra pagos.
   var payments=await List<SalePayment>("SELECT p.metodo Method,SUM(p.importe_aplicado) Applied,SUM(p.recibido) Received,SUM(p.cambio) `Change` FROM pagos p JOIN ventas v ON v.id=p.venta_id WHERE v.turno_id=@Id GROUP BY p.metodo",s);
   var movements=await List<CashMovement>("SELECT id Id,tipo Type,importe Amount,motivo Reason,creado_en Date,usuario_id UserId FROM movimientos_caja WHERE turno_id=@Id ORDER BY id",s);
   var refunds=await Scalar<decimal>("SELECT COALESCE(SUM(reembolso),0) FROM anulaciones WHERE turno_reembolso_id=@Id",s);
   var pending=await Scalar<decimal>("SELECT COALESCE(SUM(a.pendiente_no_efectivo),0) FROM anulaciones a JOIN ventas v ON v.id=a.venta_id WHERE v.turno_id=@Id",s);
   decimal Paid(string method)=>payments.Where(x=>x.Method==method).Sum(x=>x.Applied);
   var cash=Paid("EFECTIVO");var incoming=movements.Where(x=>x.Type=="INGRESO").Sum(x=>x.Amount);var outgoing=movements.Where(x=>x.Type=="EGRESO").Sum(x=>x.Amount);
   return new(s,cash,incoming,outgoing,refunds,s.Opening+cash+incoming-outgoing-refunds,Paid("TARJETA"),Paid("TRANSFERENCIA"),pending,movements);
  }
  private const string SaleSql="SELECT v.id Id,v.turno_id ShiftId,v.caja_id RegisterId,v.usuario_id UserId,u.nombre_usuario Username,c.nombre Register,v.fecha Date,v.estado State,v.cliente Customer,v.subtotal Subtotal,v.descuento Discount,v.impuesto Tax,v.total Total FROM ventas v JOIN usuarios u ON u.id=v.usuario_id JOIN cajas c ON c.id=v.caja_id";
  public Task<IReadOnlyList<Sale>> Sales(long? actor,long? shift,int offset)=>List<Sale>(SaleSql+" WHERE (@actor IS NULL OR v.usuario_id=@actor) AND (@shift IS NULL OR v.turno_id=@shift) ORDER BY v.id DESC LIMIT 50 OFFSET @offset",new{actor,shift,offset});
  public async Task<Receipt?> Receipt(long id)
  {
   var sale=(await List<Sale>(SaleSql+" WHERE v.id=@id",new{id})).SingleOrDefault();if(sale==null)return null;
   var lines=await List<SaleLine>("SELECT id Id,producto_id ProductId,sku_snapshot Sku,nombre_snapshot Name,cantidad Quantity,precio_unitario Price,tasa_impuesto Rate,subtotal Subtotal,descuento Discount,impuesto Tax,total Total,controla_inventario TrackInventory FROM detalle_ventas WHERE venta_id=@id ORDER BY id",new{id});
   var payments=await List<SalePayment>("SELECT metodo Method,importe_aplicado Applied,recibido Received,cambio `Change`,referencia Reference FROM pagos WHERE venta_id=@id ORDER BY id",new{id});
   var issuer=await Scalar<string>("SELECT datos_emisor_snapshot FROM facturas WHERE venta_id=@id",new{id});
   var number=await Scalar<string>("SELECT CONCAT(serie,'-',LPAD(numero,10,'0')) FROM facturas WHERE venta_id=@id",new{id});
   var pending=await Scalar<decimal>("SELECT COALESCE(SUM(pendiente_no_efectivo),0) FROM anulaciones WHERE venta_id=@id",new{id});
   return new(sale,lines,payments,issuer??"{}",number??$"HIST-{id}","COMPROBANTE DE VENTA",pending);
  }
  public async Task<CatalogProduct?> Product(long id)
  {
   var rows=await c.QueryAsync("SELECT * FROM productos WHERE id=@id",new{id},tx);var p=rows.SingleOrDefault();if(p==null)return null;
   return new((long)p.id,(string)p.sku,(string?)p.codigo_barras,(string)p.nombre,(long)p.categoria_id,(long?)p.subcategoria_id,(decimal)p.precio,(decimal)p.costo,(string)p.unidad,(decimal)p.stock_minimo,Convert.ToBoolean(p.controla_inventario),Convert.ToBoolean(p.activo),(uint)p.version,Array.Empty<long>());
  }
  public async Task<SalesSettings> Settings()=> (await List<SalesSettings>("SELECT tasa_impuesto Rate,identificacion Identification,direccion Address FROM configuracion_ventas WHERE id=1")).Single();
  public async Task<long> Confirm(long actor,SaleInput input,IReadOnlyList<SaleLine> lines,IReadOnlyList<SalePayment> payments)
  {
   var shift=(await Shift(input.ShiftId))!;
   await Run("INSERT INTO ventas(turno_id,caja_id,usuario_id,fecha,estado,cliente,subtotal,descuento,impuesto,total) VALUES(@Id,@RegisterId,@actor,UTC_TIMESTAMP(6),'CONFIRMADA',@Customer,@subtotal,@discount,@tax,@total)",new{shift.Id,shift.RegisterId,actor,input.Customer,subtotal=lines.Sum(x=>x.Subtotal),discount=lines.Sum(x=>x.Discount),tax=lines.Sum(x=>x.Tax),total=lines.Sum(x=>x.Total)});
   var sale=await Scalar<long>("SELECT LAST_INSERT_ID()");
   foreach(var line in lines)
   {
    await Run("INSERT INTO detalle_ventas(venta_id,producto_id,sku_snapshot,nombre_snapshot,cantidad,precio_unitario,tasa_impuesto,subtotal,descuento,impuesto,total,controla_inventario) VALUES(@sale,@ProductId,@Sku,@Name,@Quantity,@Price,@Rate,@Subtotal,@Discount,@Tax,@Total,@TrackInventory)",new{sale,line.ProductId,line.Sku,line.Name,line.Quantity,line.Price,line.Rate,line.Subtotal,line.Discount,line.Tax,line.Total,line.TrackInventory});
    var detail=await Scalar<long>("SELECT LAST_INSERT_ID()");
    if(line.TrackInventory)
    {
     var stocks=(await c.QueryAsync<(long Location,decimal Quantity)>("SELECT ubicacion_id,cantidad FROM existencias_ubicacion WHERE producto_id=@ProductId AND cantidad>0 ORDER BY ubicacion_id FOR UPDATE",line,tx)).ToArray();
     if(stocks.Sum(x=>x.Quantity)<line.Quantity)throw new CatalogError(409,"insufficient_stock");
     var remaining=line.Quantity;
     foreach(var stock in stocks)
     {
      var quantity=Math.Min(remaining,stock.Quantity);if(quantity<=0)break;
      await Change(actor,line.ProductId,stock.Location,-quantity,detail,"VENTA");remaining-=quantity;
     }
    }
   }
   foreach(var p in payments)await Run("INSERT INTO pagos(venta_id,metodo,importe_aplicado,recibido,cambio,referencia) VALUES(@sale,@Method,@Applied,@Received,@Change,@Reference)",new{sale,p.Method,p.Applied,p.Received,p.Change,p.Reference});
   var settings=await Settings();var name=await Scalar<string>("SELECT nombre FROM configuracion_negocio WHERE id=1");
   var issuer=JsonSerializer.Serialize(new{name,settings.Identification,settings.Address,currency="NIO"});
   await Run("INSERT INTO facturas(venta_id,serie,numero,emitido_en,tipo,datos_emisor_snapshot) VALUES(@sale,'INT',@sale,UTC_TIMESTAMP(6),'ACADEMICO',@issuer)",new{sale,issuer});return sale;
  }
  private async Task Change(long actor,long product,long location,decimal delta,long detail,string type)
  {
   var before=await Scalar<decimal>("SELECT cantidad FROM existencias_ubicacion WHERE producto_id=@product AND ubicacion_id=@location FOR UPDATE",new{product,location});
   await Run("UPDATE existencias_ubicacion SET cantidad=cantidad+@delta WHERE producto_id=@product AND ubicacion_id=@location; UPDATE existencias SET cantidad=cantidad+@delta WHERE producto_id=@product; INSERT INTO movimientos_inventario(producto_id,delta,detalle_venta_id,tipo,usuario_id,creado_en,ubicacion_id,cantidad_anterior,cantidad_posterior) VALUES(@product,@delta,@detail,@type,@actor,UTC_TIMESTAMP(6),@location,@before,@after)",new{actor,product,location,delta,detail,type,before,after=before+delta});
  }
  public async Task<long> Void(long actor,Receipt receipt,VoidInput input)
  {
   var sale=receipt.Sale.Id;
   if(await Scalar<int>("SELECT COUNT(*) FROM devoluciones WHERE venta_id=@sale",new{sale})>0)throw new CatalogError(409,"sale_has_returns");
   var original=(await c.QueryAsync<(long Product,long Location,decimal Delta,long Detail)>("SELECT m.producto_id,m.ubicacion_id,m.delta,m.detalle_venta_id FROM movimientos_inventario m JOIN detalle_ventas d ON d.id=m.detalle_venta_id WHERE d.venta_id=@sale AND m.tipo='VENTA' ORDER BY m.id",new{sale},tx)).ToArray();
   foreach(var m in original)await Change(actor,m.Product,m.Location,-m.Delta,m.Detail,"ANULACION");
   var cash=receipt.Payments.Where(x=>x.Method=="EFECTIVO").Sum(x=>x.Applied);var pending=receipt.Payments.Where(x=>x.Method!="EFECTIVO").Sum(x=>x.Applied);
   await Run("INSERT INTO anulaciones(venta_id,autorizado_por,turno_reembolso_id,motivo,creado_en,reembolso,estado,pendiente_no_efectivo) VALUES(@sale,@actor,@RefundShiftId,@Reason,UTC_TIMESTAMP(6),@cash,@state,@pending); UPDATE ventas SET estado='ANULADA' WHERE id=@sale",new{sale,actor,input.RefundShiftId,input.Reason,cash,pending,state=pending>0?"REEMBOLSO_PENDIENTE":"REGISTRADA"});return sale;
  }
  public async Task<long?> Previous(long actor,string type,string key,byte[] hash)
  {
   var old=await c.QuerySingleOrDefaultAsync<(byte[] Hash,long Id)?>("SELECT hash_solicitud,recurso_id FROM operaciones_idempotentes WHERE usuario_id=@actor AND tipo=@type AND clave=@key",new{actor,type,key},tx);
   if(old==null)return null;if(!old.Value.Hash.SequenceEqual(hash))throw new CatalogError(409,"idempotency_conflict");return old.Value.Id;
  }
  public async Task Remember(long actor,string type,string key,byte[] hash,long id)=>await Run("INSERT INTO operaciones_idempotentes(usuario_id,tipo,clave,hash_solicitud,recurso_id,estado_http,creado_en) VALUES(@actor,@type,@key,@hash,@id,200,UTC_TIMESTAMP(6))",new{actor,type,key,hash,id});
  public async Task Audit(long actor,string action,long id)=>await Run("INSERT INTO auditoria(actor_id,accion,recurso_tipo,recurso_id,fecha,resultado,correlacion,resumen) VALUES(@actor,@action,'pos',@id,UTC_TIMESTAMP(6),'EXITO',UUID(),'Operación POS confirmada; consultar recurso para detalle')",new{actor,action,id});
 }
}
