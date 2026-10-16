-- Solo desarrollo. Dataset fijo, sin credenciales ni cuentas habilitadas.
-- Una segunda ejecución falla por PK; la transacción conserva el dataset original.
SET time_zone='+00:00';
START TRANSACTION;
INSERT INTO roles(id,nombre) VALUES(1,'Administrador'),(2,'Vendedor/Cajero'),(3,'Consulta');
INSERT INTO permisos(id,codigo,descripcion) VALUES
 (1,'catalogo.leer','Consultar catálogo'),(2,'ventas.crear','Registrar venta'),
 (3,'reportes.leer','Consultar reportes');
-- Matriz ilustrativa, no autoriza endpoints; completar en Fase 4.
INSERT INTO rol_permisos VALUES(1,1),(1,2),(1,3),(2,1),(2,2),(3,1),(3,3);
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
INSERT INTO ventas VALUES(1,1,1,2,'2026-10-14 15:00:00','CONFIRMADA',50,0,0,50);
INSERT INTO detalle_ventas VALUES(1,1,1,'AGUA-01','Agua demo',2,25,0,50,0,0,50);
INSERT INTO pagos VALUES(1,1,'EFECTIVO',50,100,50);
INSERT INTO facturas VALUES(1,1,'DEMO',1,'2026-10-14 15:00:00','ACADEMICO','NEXO DEMO - SIN VALIDEZ FISCAL');
INSERT INTO operaciones_inventario VALUES(1,'ENTRADA','Carga inicial demo',1,'DEMO-001',1,'2026-10-14 14:00:00');
INSERT INTO movimientos_inventario(id,producto_id,delta,costo_unitario,operacion_id,tipo,usuario_id,creado_en) VALUES
 (1,1,12,15,1,'MANUAL',1,'2026-10-14 14:00:00'),
 (2,2,1,20,1,'MANUAL',1,'2026-10-14 14:00:00'),
 (3,4,5,5,1,'MANUAL',1,'2026-10-14 14:00:00');
INSERT INTO movimientos_inventario(id,producto_id,delta,detalle_venta_id,tipo,usuario_id,creado_en)
 VALUES(4,1,-2,1,'VENTA',2,'2026-10-14 15:00:00');
INSERT INTO existencias VALUES(1,10),(2,1),(3,0),(4,5);
INSERT INTO operaciones_idempotentes VALUES(1,2,'VENTA','demo-venta-001',UNHEX(SHA2('demo-venta-001',256)),1,201,'2026-10-14 15:00:00');
INSERT INTO auditoria VALUES(1,1,'CARGA_DEMO','dataset',NULL,'2026-10-15 00:00:00','EXITO','00000000-0000-4000-8000-000000000001','Datos sintéticos, usuarios deshabilitados');
COMMIT;
