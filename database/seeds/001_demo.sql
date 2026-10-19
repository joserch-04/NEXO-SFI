-- Solo desarrollo. Dataset fijo, sin credenciales ni cuentas habilitadas.
-- Una segunda ejecución falla por PK; la transacción conserva el dataset original.
SET time_zone='+00:00';
START TRANSACTION;
INSERT IGNORE INTO roles(id,nombre) VALUES(1,'Administrador'),(2,'Vendedor/Cajero'),(3,'Consulta');
INSERT IGNORE INTO permisos(codigo,descripcion) VALUES
 ('catalogo.leer','Consultar catálogo'),('ventas.crear','Registrar venta'),
 ('reportes.leer','Consultar reportes');
-- Resolver por código: las migraciones de seguridad ya pueden haber creado permisos.
INSERT IGNORE INTO rol_permisos(rol_id,permiso_id)
 SELECT r.id,p.id FROM roles r CROSS JOIN permisos p
 WHERE (r.nombre='Administrador' AND p.codigo IN ('catalogo.leer','ventas.crear','reportes.leer'))
 OR (r.nombre='Vendedor/Cajero' AND p.codigo IN ('catalogo.leer','ventas.crear'))
 OR (r.nombre='Consulta' AND p.codigo IN ('catalogo.leer','reportes.leer'));
INSERT INTO usuarios(id,nombre_usuario,nombre,password_hash,activo) VALUES
 (1,'demo.admin','Administrador de prueba','',FALSE),
 (2,'demo.cajero1','Cajero uno de prueba','',FALSE),
 (3,'demo.cajero2','Cajero dos de prueba','',FALSE),
 (4,'demo.consulta','Consulta de prueba','',FALSE);
INSERT INTO usuario_roles VALUES(1,1),(2,2),(3,2),(4,3);
INSERT INTO categorias(id,nombre) VALUES(1,'Bebidas'),(2,'Alimentos');
INSERT INTO proveedores(id,nombre) VALUES(1,'Distribuidora ficticia');
INSERT INTO productos(id,categoria_id,sku,nombre,precio,stock_minimo,activo) VALUES
 (1,1,'AGUA-01','Agua demo',25,5,TRUE),
 (2,2,'ARROZ-01','Arroz demo',30,2,TRUE),
 (3,1,'JUGO-01','Jugo demo',18.1250,2,TRUE),
 (4,2,'BAJA-01','Producto inactivo demo',10,0,FALSE);
INSERT INTO producto_proveedores VALUES(1,1),(2,1),(3,1);
INSERT INTO cajas(id,codigo,nombre) VALUES(1,'CAJA-01','Caja principal'),(2,'CAJA-02','Caja auxiliar');
INSERT INTO turnos_caja(id,caja_id,usuario_id,apertura_en,cierre_en,fondo_inicial,esperado,contado,estado) VALUES
 (1,1,2,'2026-10-14 14:00:00','2026-10-14 22:00:00',100,150,150,'CERRADO'),
 (2,2,3,'2026-10-15 14:00:00',NULL,100,NULL,NULL,'ABIERTO');
-- Importes ilustrativos con impuesto/descuento cero, sin política fiscal implícita.
INSERT INTO ventas(id,turno_id,caja_id,usuario_id,fecha,estado,subtotal,descuento,impuesto,total) VALUES(1,1,1,2,'2026-10-14 15:00:00','CONFIRMADA',50,0,0,50);
INSERT INTO detalle_ventas(id,venta_id,producto_id,sku_snapshot,nombre_snapshot,cantidad,precio_unitario,tasa_impuesto,subtotal,descuento,impuesto,total) VALUES(1,1,1,'AGUA-01','Agua demo',2,25,0,50,0,0,50);
INSERT INTO pagos(id,venta_id,metodo,importe_aplicado,recibido,cambio) VALUES(1,1,'EFECTIVO',50,100,50);
INSERT INTO facturas VALUES(1,1,'DEMO',1,'2026-10-14 15:00:00','ACADEMICO','NEXO DEMO - COMPROBANTE DE VENTA');
INSERT INTO operaciones_inventario(id,tipo,motivo,proveedor_id,referencia,usuario_id,creado_en) VALUES(1,'ENTRADA','Carga inicial demo',1,'DEMO-001',1,'2026-10-14 14:00:00');
INSERT INTO movimientos_inventario(id,producto_id,delta,costo_unitario,operacion_id,tipo,usuario_id,creado_en) VALUES
 (1,1,12,15,1,'MANUAL',1,'2026-10-14 14:00:00'),
 (2,2,1,20,1,'MANUAL',1,'2026-10-14 14:00:00'),
 (3,4,5,5,1,'MANUAL',1,'2026-10-14 14:00:00');
INSERT INTO movimientos_inventario(id,producto_id,delta,detalle_venta_id,tipo,usuario_id,creado_en)
 VALUES(4,1,-2,1,'VENTA',2,'2026-10-14 15:00:00');
INSERT INTO existencias VALUES(1,10),(2,1),(3,0),(4,5);
INSERT INTO existencias_ubicacion SELECT producto_id,(SELECT id FROM ubicaciones WHERE codigo='B-01'),cantidad FROM existencias;
UPDATE movimientos_inventario SET ubicacion_id=(SELECT id FROM ubicaciones WHERE codigo='B-01') WHERE ubicacion_id IS NULL;
INSERT INTO operaciones_idempotentes VALUES(1,2,'VENTA','demo-venta-001',UNHEX(SHA2('demo-venta-001',256)),1,201,'2026-10-14 15:00:00');
INSERT INTO auditoria VALUES(1,1,'CARGA_DEMO','dataset',NULL,'2026-10-15 00:00:00','EXITO','00000000-0000-4000-8000-000000000001','Datos sintéticos, usuarios deshabilitados');
COMMIT;
