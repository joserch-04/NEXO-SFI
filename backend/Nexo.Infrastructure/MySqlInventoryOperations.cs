using Dapper;
using MySqlConnector;
using Nexo.Domain;
namespace Nexo.Infrastructure;

public sealed partial class MySqlCatalogStore
{
    private sealed partial class Session
    {
        public Task<IReadOnlyList<MovementView>> Movements(DateTime from,DateTime to,long? productId,string? type,int offset,int limit,long? locationId = null) => List<MovementView>("""
            SELECT m.id Id,COALESCE(m.operacion_id,0) OperationId,m.producto_id ProductId,p.nombre Product,
            COALESCE(m.ubicacion_id,0) LocationId,COALESCE(u.codigo,'Histórico') Location,m.delta Delta,
            CASE WHEN o.id IS NOT NULL THEN IF(o.traslado,'TRASLADO',o.tipo) ELSE m.tipo END Type,
            COALESCE(o.motivo,IF(m.tipo='VENTA','Venta registrada','Reversión de venta')) Reason,
            m.usuario_id UserId,us.nombre_usuario Username,m.creado_en CreatedAt,
            m.cantidad_anterior BeforeQuantity,m.cantidad_posterior AfterQuantity,o.motivo_codigo ReasonCode,
            COALESCE(o.referencia,CONCAT('Venta #',dv.venta_id)) Reference,pr.nombre Supplier,o.proveedor_id SupplierId,m.costo_unitario Cost
            FROM movimientos_inventario m LEFT JOIN operaciones_inventario o ON o.id=m.operacion_id
            JOIN productos p ON p.id=m.producto_id LEFT JOIN ubicaciones u ON u.id=m.ubicacion_id
            JOIN usuarios us ON us.id=m.usuario_id LEFT JOIN proveedores pr ON pr.id=o.proveedor_id
            LEFT JOIN detalle_ventas dv ON dv.id=m.detalle_venta_id
            WHERE m.creado_en>=@from AND m.creado_en<@to AND (@productId IS NULL OR m.producto_id=@productId)
            AND (@locationId IS NULL OR m.ubicacion_id=@locationId)
            AND (@type IS NULL OR CASE WHEN o.id IS NOT NULL THEN IF(o.traslado,'TRASLADO',o.tipo) ELSE m.tipo END=@type)
            ORDER BY m.creado_en DESC,m.id DESC LIMIT @limit OFFSET @offset
            """,new{from,to,productId,type,offset,limit,locationId});
        public async Task<long?> FindOperation(long actor,string kind,string key,byte[] hash)
        {
            var old=await c.QuerySingleOrDefaultAsync<(byte[] Hash,long Id)?>("SELECT hash_solicitud,recurso_id FROM operaciones_idempotentes WHERE usuario_id=@actor AND tipo=@kind AND clave=@key",new{actor,kind,key},tx);
            if(old==null)return null;
            if(!old.Value.Hash.SequenceEqual(hash))throw new CatalogError(409,"idempotency_conflict");
            return old.Value.Id;
        }
        public async Task RememberOperation(long actor,string kind,string key,byte[] hash,long id) => await Run("INSERT INTO operaciones_idempotentes(usuario_id,tipo,clave,hash_solicitud,recurso_id,estado_http,creado_en) VALUES(@actor,@kind,@key,@hash,@id,200,UTC_TIMESTAMP(6))",new{actor,kind,key,hash,id});
        public async Task<(decimal Quantity,long LastMovement)> StockMarker(long product,long location)
        {
            var quantity=await Scalar<decimal>("SELECT COALESCE((SELECT cantidad FROM existencias_ubicacion WHERE producto_id=@product AND ubicacion_id=@location),0)",new{product,location});
            var marker=await Scalar<long>("SELECT COALESCE(MAX(id),0) FROM movimientos_inventario WHERE producto_id=@product AND ubicacion_id=@location",new{product,location});
            return (quantity,marker);
        }
        public async Task<long> MoveBatch(long actor,InventoryBatchInput input)
        {
            var type=input.Type=="TRASLADO"?"AJUSTE":input.Type;
            await Run("INSERT INTO operaciones_inventario(tipo,motivo,motivo_codigo,proveedor_id,referencia,usuario_id,creado_en,traslado) VALUES(@type,@Reason,@ReasonCode,@SupplierId,@Reference,@actor,UTC_TIMESTAMP(6),@transfer)",new{type,input.Reason,input.ReasonCode,input.SupplierId,input.Reference,actor,transfer=input.Type=="TRASLADO"});
            var id=await Scalar<long>("SELECT LAST_INSERT_ID()");
            async Task Change(InventoryLine line,long location,decimal delta)
            {
                await Run("INSERT IGNORE INTO existencias_ubicacion(producto_id,ubicacion_id,cantidad) VALUES(@ProductId,@location,0)",new{line.ProductId,location});
                var before=await Scalar<decimal>("SELECT cantidad FROM existencias_ubicacion WHERE producto_id=@ProductId AND ubicacion_id=@location FOR UPDATE",new{line.ProductId,location});
                decimal after;
                try{after=InventoryRules.Balance(before,delta);}catch(InvalidOperationException){throw new CatalogError(409,"insufficient_stock");}
                await Run("UPDATE existencias_ubicacion SET cantidad=@after WHERE producto_id=@ProductId AND ubicacion_id=@location",new{after,line.ProductId,location});
                await Run("INSERT INTO movimientos_inventario(producto_id,delta,costo_unitario,operacion_id,tipo,usuario_id,creado_en,ubicacion_id,cantidad_anterior,cantidad_posterior) VALUES(@ProductId,@delta,@Cost,@id,'MANUAL',@actor,UTC_TIMESTAMP(6),@location,@before,@after)",new{line.ProductId,delta,line.Cost,id,actor,location,before,after});
            }
            foreach(var line in input.Lines)
            {
                var delta=input.Type is "SALIDA" or "TRASLADO"?-line.Quantity:line.Quantity;
                await Change(line,line.LocationId,delta);
                if(line.DestinationId.HasValue)await Change(line,line.DestinationId.Value,line.Quantity);
            }
            foreach(var product in input.Lines.Select(l=>l.ProductId).Distinct())
                await Run("INSERT INTO existencias(producto_id,cantidad) SELECT @product,SUM(cantidad) FROM existencias_ubicacion WHERE producto_id=@product ON DUPLICATE KEY UPDATE cantidad=(SELECT SUM(e.cantidad) FROM existencias_ubicacion e WHERE e.producto_id=@product)",new{product});
            return id;
        }
        private const string CountSql="""
            SELECT co.id Id,co.ubicacion_id LocationId,u.codigo Location,co.estado State,co.motivo Reason,
            co.creado_por CreatedBy,cr.nombre_usuario Creator,co.creado_en CreatedAt,
            co.aprobado_por ApprovedBy,ap.nombre_usuario Approver,co.aprobado_en ApprovedAt,
            co.operacion_id OperationId,co.version Version
            FROM conteos_inventario co JOIN ubicaciones u ON u.id=co.ubicacion_id
            JOIN usuarios cr ON cr.id=co.creado_por LEFT JOIN usuarios ap ON ap.id=co.aprobado_por
            """;
        public Task<IReadOnlyList<InventoryCount>> Counts(int offset,int limit)=>List<InventoryCount>(CountSql+" ORDER BY co.id DESC LIMIT @limit OFFSET @offset",new{offset,limit});
        public async Task<InventoryCountDetail?> Count(long id)
        {
            var header=(await List<InventoryCount>(CountSql+" WHERE co.id=@id",new{id})).SingleOrDefault();if(header==null)return null;
            var lines=await List<InventoryCountLine>("SELECT d.producto_id ProductId,p.sku Sku,p.nombre Name,d.cantidad_sistema SystemQuantity,d.ultimo_movimiento LastMovementId,d.cantidad_contada CountedQuantity FROM detalle_conteos_inventario d JOIN productos p ON p.id=d.producto_id WHERE d.conteo_id=@id ORDER BY p.nombre,p.id",new{id});
            return new(header,lines);
        }
        public async Task<long> CreateCount(long actor,CountCreateInput input)
        {
            await Run("INSERT INTO conteos_inventario(ubicacion_id,motivo,creado_por,creado_en) VALUES(@LocationId,@Reason,@actor,UTC_TIMESTAMP(6))",new{input.LocationId,input.Reason,actor});
            var id=await Scalar<long>("SELECT LAST_INSERT_ID()");
            foreach(var product in input.ProductIds)
            {
                var marker=await StockMarker(product,input.LocationId);
                await Run("INSERT INTO detalle_conteos_inventario(conteo_id,producto_id,cantidad_sistema,ultimo_movimiento) VALUES(@id,@product,@Quantity,@LastMovement)",new{id,product,marker.Quantity,marker.LastMovement});
            }
            return id;
        }
        public async Task SaveCount(long id,CountQuantity[] lines)
        {
            foreach(var line in lines)await Run("UPDATE detalle_conteos_inventario SET cantidad_contada=@Quantity WHERE conteo_id=@id AND producto_id=@ProductId",new{id,line.ProductId,line.Quantity});
            await Run("UPDATE conteos_inventario SET version=version+1 WHERE id=@id",new{id});
        }
        public async Task SetCountState(long id,string state,long? actor=null,long? operation=null) => await Run("UPDATE conteos_inventario SET estado=@state,version=version+1,aprobado_por=@actor,aprobado_en=IF(@actor IS NULL,NULL,UTC_TIMESTAMP(6)),operacion_id=@operation WHERE id=@id",new{id,state,actor,operation});
    }
}
