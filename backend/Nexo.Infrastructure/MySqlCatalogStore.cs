using System.Data;
using Dapper;
using MySqlConnector;

using Nexo.Domain;
namespace Nexo.Infrastructure;

// Un bloqueo corto del módulo serializa escrituras y reintentos, incluso entre procesos API.
// Las lecturas comparten una instantánea; saldos, historial, idempotencia y auditoría confirman juntos.
public sealed class MySqlCatalogStore(MySqlConnectionFactory factory) : ICatalogStore
{
    public async Task<T> Execute<T>(Func<ICatalogSession, Task<T>> action)
    {
        await using var connection = await factory.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await connection.ExecuteScalarAsync<int>("SELECT id FROM catalogo_bloqueo WHERE id=1 FOR UPDATE", transaction: transaction);
            var result = await action(new Session(connection, transaction));
            await transaction.CommitAsync(); return result;
        }
        catch (MySqlException ex) when (ex.Number is 1062 or 1451 or 1452 or 3819)
        { throw new CatalogError(409, ex.Number == 1062 ? "catalog_duplicate" : "catalog_conflict"); }
    }
    private sealed class ProductRow
    {
        public long Id { get; set; }
        public string Sku { get; set; } = "";
        public string? Barcode { get; set; }
        public string Name { get; set; } = "";
        public long CategoryId { get; set; }
        public long? SubcategoryId { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public string Unit { get; set; } = "";
        public decimal MinimumStock { get; set; }
        public bool TrackInventory { get; set; }
        public bool Active { get; set; }
        public uint Version { get; set; }
        public string? ImageRef { get; set; }
        public CatalogProduct View(long[] suppliers) => new(Id, Sku, Barcode, Name, CategoryId, SubcategoryId, Price, Cost, Unit, MinimumStock, TrackInventory, Active, Version, suppliers, ImageRef);
    }
    private sealed class MasterRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public bool Active { get; set; }
        public uint Version { get; set; }
        public string? Code { get; set; }
        public string? Type { get; set; }
        public long? ParentId { get; set; }
        public string? Identification { get; set; }
        public string? Phone { get; set; }
        public CatalogMaster View() => new(Id, Name, Active, Version, Code, Type, ParentId, Identification, Phone);
    }
    private sealed class Session(MySqlConnection c, MySqlTransaction tx) : ICatalogSession
    {
        private Task<int> Run(string sql, object? args = null) => c.ExecuteAsync(sql, args, tx);
        private Task<T> Scalar<T>(string sql, object? args = null) => c.ExecuteScalarAsync<T>(sql, args, tx)!;
        private async Task<IReadOnlyList<T>> List<T>(string sql, object? args = null) => (await c.QueryAsync<T>(sql, args, tx)).AsList();
        private static string Table(string kind) => kind switch { "categorias" => "categorias", "subcategorias" => "subcategorias", "proveedores" => "proveedores", "ubicaciones" => "ubicaciones", _ => throw new CatalogError(404, "catalog_not_found") };
        public async Task<IReadOnlyList<CatalogMaster>> Masters(string kind)
        {
            var extra = kind switch { "subcategorias" => ",categoria_id ParentId", "ubicaciones" => ",codigo Code,tipo Type,padre_id ParentId", "proveedores" => ",identificacion Identification,telefono Phone", _ => "" };
            return (await List<MasterRow>($"SELECT id Id,nombre Name,activo Active,version Version{extra} FROM {Table(kind)} ORDER BY nombre,id")).Select(x => x.View()).ToArray();
        }
        public async Task<long> SaveMaster(string kind, CatalogMaster v)
        {
            var table = Table(kind);
            var extra = kind switch { "subcategorias" => ",categoria_id=@ParentId", "ubicaciones" => ",codigo=@Code,tipo=@Type,padre_id=@ParentId", "proveedores" => ",identificacion=@Identification,telefono=@Phone", _ => "" };
            if (v.Id == 0)
            {
                await Run($"INSERT INTO {table} SET nombre=@Name,activo=@Active{extra}", v);
                return await Scalar<long>("SELECT LAST_INSERT_ID()");
            }
            await Run($"UPDATE {table} SET nombre=@Name,activo=@Active,version=version+1{extra} WHERE id=@Id", v); return v.Id;
        }
        private const string ProductColumns = "p.id Id,p.sku Sku,p.codigo_barras Barcode,p.nombre Name,p.categoria_id CategoryId,p.subcategoria_id SubcategoryId,p.precio Price,p.costo Cost,p.unidad Unit,p.stock_minimo MinimumStock,p.controla_inventario TrackInventory,p.activo Active,p.version Version,p.imagen ImageRef";
        public async Task SetImage(long id, string? reference) => await Run("UPDATE productos SET imagen=@reference,version=version+1 WHERE id=@id", new { id, reference });
        private async Task<IReadOnlyList<CatalogProduct>> ReadProducts(string where, object args, string tail)
        {
            var rows = await List<ProductRow>($"SELECT {ProductColumns} FROM productos p WHERE {where} {tail}", args);
            if (rows.Count == 0) return [];
            var suppliers = await List<(long ProductId, long SupplierId)>("SELECT producto_id,proveedor_id FROM producto_proveedores WHERE producto_id IN @Ids", new { Ids = rows.Select(x => x.Id).ToArray() });
            return rows.Select(p => p.View(suppliers.Where(s => s.ProductId == p.Id).Select(s => s.SupplierId).ToArray())).ToArray();
        }
        public async Task<CatalogProduct?> Product(long id) => (await ReadProducts("p.id=@id", new { id }, "")).SingleOrDefault();
        public Task<IReadOnlyList<CatalogProduct>> Products(string? search, long? categoryId, long? subcategoryId, bool? active, long? locationId, int offset, int limit) => ReadProducts("(@search IS NULL OR LOCATE(@search,p.nombre)>0 OR LOCATE(@search,p.sku)>0 OR LOCATE(@search,p.codigo_barras)>0) AND (@categoryId IS NULL OR p.categoria_id=@categoryId) AND (@subcategoryId IS NULL OR p.subcategoria_id=@subcategoryId) AND (@active IS NULL OR p.activo=@active) AND (@locationId IS NULL OR EXISTS(SELECT 1 FROM existencias_ubicacion e WHERE e.producto_id=p.id AND e.ubicacion_id=@locationId))", new { search, categoryId, subcategoryId, active, locationId, offset, limit }, "ORDER BY p.nombre,p.id LIMIT @limit OFFSET @offset");
        public async Task<long> SaveProduct(CatalogProduct v)
        {
            const string fields = "sku=@Sku,codigo_barras=@Barcode,nombre=@Name,categoria_id=@CategoryId,subcategoria_id=@SubcategoryId,precio=@Price,costo=@Cost,unidad=@Unit,stock_minimo=@MinimumStock,controla_inventario=@TrackInventory,activo=@Active";
            long id = v.Id;
            if (id == 0) { await Run("INSERT INTO productos SET " + fields, v); id = await Scalar<long>("SELECT LAST_INSERT_ID()"); await Run("INSERT INTO existencias(producto_id,cantidad) VALUES(@id,0)", new { id }); }
            else await Run("UPDATE productos SET " + fields + ",version=version+1 WHERE id=@Id", v);
            // DELETE está concedido únicamente a la tabla puente; los maestros se desactivan.
            var existing = await List<long>("SELECT proveedor_id FROM producto_proveedores WHERE producto_id=@id", new { id });
            foreach (var supplier in existing.Except(v.SupplierIds)) await Run("DELETE FROM producto_proveedores WHERE producto_id=@id AND proveedor_id=@supplier", new { id, supplier });
            foreach (var supplier in v.SupplierIds.Except(existing)) await Run("INSERT INTO producto_proveedores VALUES(@id,@supplier)", new { id, supplier });
            return id;
        }
        public Task<IReadOnlyList<StockView>> Stocks(long? productId, long? locationId) => List<StockView>("""
            SELECT p.id ProductId,p.sku Sku,p.nombre Name,p.categoria_id CategoryId,
            COALESCE(e.ubicacion_id,0) LocationId,COALESCE(u.codigo,'Sin existencias') Location,
            COALESCE(e.cantidad,0) Quantity,p.stock_minimo MinimumStock,p.activo Active
            FROM productos p LEFT JOIN existencias_ubicacion e ON e.producto_id=p.id
            LEFT JOIN ubicaciones u ON u.id=e.ubicacion_id
            WHERE p.controla_inventario=1 AND (@productId IS NULL OR p.id=@productId)
            AND (@locationId IS NULL OR e.ubicacion_id=@locationId) ORDER BY p.nombre,p.id,u.codigo
            """, new { productId, locationId });
        public Task<IReadOnlyList<MovementView>> Movements(DateTime from, DateTime to, long? productId, string? type, int offset, int limit) => List<MovementView>("""
            SELECT m.id Id,m.operacion_id OperationId,m.producto_id ProductId,p.nombre Product,
            m.ubicacion_id LocationId,u.codigo Location,m.delta Delta,
            IF(o.traslado,'TRASLADO',o.tipo) Type,o.motivo Reason,m.usuario_id UserId,us.nombre_usuario Username,m.creado_en CreatedAt
            FROM movimientos_inventario m JOIN operaciones_inventario o ON o.id=m.operacion_id
            JOIN productos p ON p.id=m.producto_id JOIN ubicaciones u ON u.id=m.ubicacion_id JOIN usuarios us ON us.id=m.usuario_id
            WHERE m.creado_en>=@from AND m.creado_en<@to AND (@productId IS NULL OR m.producto_id=@productId)
            AND (@type IS NULL OR IF(o.traslado,'TRASLADO',o.tipo)=@type)
            ORDER BY m.creado_en DESC,m.id DESC LIMIT @limit OFFSET @offset
            """, new { from, to, productId, type, offset, limit });
        public async Task<long?> Previous(long actor, string key, byte[] hash)
        {
            var old = await c.QuerySingleOrDefaultAsync<(byte[] Hash, long Id)?>("SELECT hash_solicitud,recurso_id FROM operaciones_idempotentes WHERE usuario_id=@actor AND tipo='INVENTARIO' AND clave=@key", new { actor, key }, tx);
            if (old == null) return null;
            if (!old.Value.Hash.SequenceEqual(hash)) throw new CatalogError(409, "idempotency_conflict");
            return old.Value.Id;
        }
        public async Task<long> Move(long actor, InventoryInput input, byte[] hash)
        {
            var type = input.Type == "TRASLADO" ? "AJUSTE" : input.Type;
            await Run("INSERT INTO operaciones_inventario(tipo,motivo,usuario_id,creado_en,traslado) VALUES(@type,@Reason,@actor,UTC_TIMESTAMP(6),@transfer)", new { type, input.Reason, actor, transfer = input.Type == "TRASLADO" });
            var id = await Scalar<long>("SELECT LAST_INSERT_ID()");
            async Task Change(long location, decimal delta)
            {
                await Run("INSERT IGNORE INTO existencias_ubicacion(producto_id,ubicacion_id,cantidad) VALUES(@ProductId,@location,0)", new { input.ProductId, location });
                var current = await Scalar<decimal>("SELECT cantidad FROM existencias_ubicacion WHERE producto_id=@ProductId AND ubicacion_id=@location FOR UPDATE", new { input.ProductId, location });
                decimal balance;
                try { balance = InventoryRules.Balance(current, delta); } catch (InvalidOperationException) { throw new CatalogError(409, "insufficient_stock"); }
                await Run("UPDATE existencias_ubicacion SET cantidad=@balance WHERE producto_id=@ProductId AND ubicacion_id=@location", new { balance, input.ProductId, location });
                await Run("INSERT INTO movimientos_inventario(producto_id,delta,operacion_id,tipo,usuario_id,creado_en,ubicacion_id) VALUES(@ProductId,@delta,@id,'MANUAL',@actor,UTC_TIMESTAMP(6),@location)", new { input.ProductId, delta, id, actor, location });
            }
            var delta = input.Type is "SALIDA" or "TRASLADO" ? -input.Quantity : input.Quantity;
            await Change(input.LocationId, delta);
            if (input.DestinationId.HasValue) await Change(input.DestinationId.Value, input.Quantity);
            await Run("INSERT INTO existencias(producto_id,cantidad) SELECT @ProductId,SUM(cantidad) FROM existencias_ubicacion WHERE producto_id=@ProductId ON DUPLICATE KEY UPDATE cantidad=(SELECT SUM(e.cantidad) FROM existencias_ubicacion e WHERE e.producto_id=@ProductId)", new { input.ProductId });
            await Run("INSERT INTO operaciones_idempotentes(usuario_id,tipo,clave,hash_solicitud,recurso_id,estado_http,creado_en) VALUES(@actor,'INVENTARIO',@Key,@hash,@id,200,UTC_TIMESTAMP(6))", new { actor, input.Key, hash, id });
            return id;
        }
        public async Task Audit(long actor, string action, long id) => await Run("INSERT INTO auditoria(actor_id,accion,recurso_tipo,recurso_id,fecha,resultado,correlacion,resumen) VALUES(@actor,@action,'catalogo',@id,UTC_TIMESTAMP(6),'EXITO',UUID(),'Cambio de catálogo o inventario')", new { actor, action, id });
    }
}
