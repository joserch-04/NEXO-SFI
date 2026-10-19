-- Fase 7. Evolución aditiva; no altera comprobantes ni ventas históricos.
ALTER TABLE productos ADD imagen VARCHAR(80) NULL;
ALTER TABLE cajas ADD fuera_servicio BOOLEAN NOT NULL DEFAULT FALSE, ADD version INT UNSIGNED NOT NULL DEFAULT 1;
ALTER TABLE turnos_caja ADD observaciones VARCHAR(1000) NULL;
ALTER TABLE ventas ADD cliente VARCHAR(180) NULL;
ALTER TABLE detalle_ventas ADD controla_inventario BOOLEAN NOT NULL DEFAULT TRUE;
-- Localizar la restricción histórica de efectivo sin depender de su nombre automático.
SET @payment_check=(SELECT cc.constraint_name FROM information_schema.check_constraints cc JOIN information_schema.table_constraints tc ON tc.constraint_schema=cc.constraint_schema AND tc.constraint_name=cc.constraint_name WHERE cc.constraint_schema=DATABASE() AND tc.table_name='pagos' AND cc.check_clause LIKE '%metodo%');
SET @payment_sql=CONCAT('ALTER TABLE pagos DROP CHECK `',@payment_check,'`');
PREPARE payment_stmt FROM @payment_sql;
EXECUTE payment_stmt;
DEALLOCATE PREPARE payment_stmt;
ALTER TABLE pagos ADD CHECK(metodo IN ('EFECTIVO','TARJETA','TRANSFERENCIA')),
 ADD CHECK(metodo='EFECTIVO' OR cambio=0);
ALTER TABLE anulaciones ADD estado VARCHAR(30) NOT NULL DEFAULT 'REGISTRADA',
 ADD pendiente_no_efectivo DECIMAL(18,2) NOT NULL DEFAULT 0;
CREATE TABLE configuracion_ventas (
 id INT PRIMARY KEY, tasa_impuesto DECIMAL(7,4) NOT NULL DEFAULT 0,
 identificacion VARCHAR(100) NOT NULL DEFAULT '', direccion VARCHAR(250) NOT NULL DEFAULT '',
 CHECK(tasa_impuesto>=0 AND tasa_impuesto<=1)
);
INSERT INTO configuracion_ventas(id) VALUES(1);
INSERT IGNORE INTO permisos(codigo,descripcion) VALUES
 ('cajas.leer','Consultar cajas y turnos propios'),('cajas.administrar','Administrar cajas'),
 ('cajas.abrir','Abrir turno propio'),('cajas.cerrar','Cerrar turno propio'),
 ('cajas.movimientos','Registrar entradas y salidas de efectivo propias'),('cajas.arquear','Consultar arqueo propio'),
 ('cajas.historial','Consultar turnos y ventas de todos los cajeros'),
 ('ventas.registrar','Confirmar ventas en turno propio'),('ventas.leer','Consultar ventas propias y comprobantes'),
 ('ventas.descuentos','Aplicar descuentos'),('ventas.anular','Anular y devolver efectivo autorizado');
INSERT IGNORE INTO rol_permisos SELECT r.id,p.id FROM roles r CROSS JOIN permisos p WHERE r.nombre='Administrador';
INSERT IGNORE INTO rol_permisos SELECT r.id,p.id FROM roles r CROSS JOIN permisos p
 WHERE r.nombre='Vendedor/Cajero' AND p.codigo IN ('cajas.leer','cajas.abrir','cajas.cerrar','cajas.arquear','ventas.registrar','ventas.leer');
