-- Las aserciones dividen por cero no son fiables en SELECT; SIGNAL falla explícitamente.
DROP PROCEDURE IF EXISTS nexo_verificar;
DELIMITER //
CREATE PROCEDURE nexo_verificar()
BEGIN
 IF (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('productos','categorias','subcategorias','ubicaciones','existencias_ubicacion','catalogo_bloqueo','schema_migrations')) <> 7 THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Faltan tablas de catálogo F6'; END IF;
 IF (SELECT COUNT(*) FROM productos) < 4 OR (SELECT COUNT(*) FROM ventas) < 1 THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Dataset demo incompleto'; END IF;
 IF EXISTS(SELECT 1 FROM existencias e LEFT JOIN movimientos_inventario m ON m.producto_id=e.producto_id
 GROUP BY e.producto_id,e.cantidad HAVING e.cantidad <> COALESCE(SUM(m.delta),0)) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Stock no coincide con libro'; END IF;
 IF EXISTS(SELECT 1 FROM ventas v LEFT JOIN pagos p ON p.venta_id=v.id GROUP BY v.id,v.total HAVING COALESCE(SUM(p.importe_aplicado),0) <> v.total) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Pagos no coinciden con venta'; END IF;
 IF EXISTS(SELECT 1 FROM ventas v LEFT JOIN detalle_ventas d ON d.venta_id=v.id GROUP BY v.id,v.total HAVING COALESCE(SUM(d.total),0) <> v.total) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Detalle no coincide con venta'; END IF;
 IF EXISTS(SELECT 1 FROM usuarios WHERE activo=TRUE AND nombre_usuario LIKE 'demo.%') THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Usuarios demo deben estar deshabilitados'; END IF;
 IF EXISTS(SELECT 1 FROM existencias e LEFT JOIN existencias_ubicacion u ON u.producto_id=e.producto_id
 GROUP BY e.producto_id,e.cantidad HAVING e.cantidad <> COALESCE(SUM(u.cantidad),0)) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Saldo global no coincide con ubicaciones'; END IF;
 IF EXISTS(SELECT 1 FROM existencias_ubicacion e LEFT JOIN movimientos_inventario m
 ON m.producto_id=e.producto_id AND m.ubicacion_id=e.ubicacion_id
 GROUP BY e.producto_id,e.ubicacion_id,e.cantidad HAVING e.cantidad <> COALESCE(SUM(m.delta),0)) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Saldo por ubicación no coincide con libro'; END IF;
 SELECT VERSION() AS version_mysql, 'Aserciones SQL correctas' AS resultado;
END//
DELIMITER ;
CALL nexo_verificar();
DROP PROCEDURE nexo_verificar;
