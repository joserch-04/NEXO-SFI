-- Inventario operativo: documentos de recepción y conteos físicos trazables.
ALTER TABLE operaciones_inventario ADD motivo_codigo VARCHAR(30) NOT NULL DEFAULT 'OTRO';
ALTER TABLE movimientos_inventario
 ADD cantidad_anterior DECIMAL(18,3) NULL,
 ADD cantidad_posterior DECIMAL(18,3) NULL,
 ADD CHECK(cantidad_anterior IS NULL OR cantidad_anterior>=0),
 ADD CHECK(cantidad_posterior IS NULL OR cantidad_posterior>=0),
 ADD CHECK(cantidad_anterior IS NULL OR cantidad_posterior IS NULL OR cantidad_posterior=cantidad_anterior+delta);
CREATE TABLE conteos_inventario (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
 ubicacion_id BIGINT UNSIGNED NOT NULL,
 estado VARCHAR(15) NOT NULL DEFAULT 'BORRADOR',
 motivo VARCHAR(500) NOT NULL,
 creado_por BIGINT UNSIGNED NOT NULL,
 creado_en DATETIME(6) NOT NULL,
 aprobado_por BIGINT UNSIGNED NULL,
 aprobado_en DATETIME(6) NULL,
 operacion_id BIGINT UNSIGNED NULL UNIQUE,
 version INT UNSIGNED NOT NULL DEFAULT 1,
 FOREIGN KEY(ubicacion_id) REFERENCES ubicaciones(id),
 FOREIGN KEY(creado_por) REFERENCES usuarios(id),
 FOREIGN KEY(aprobado_por) REFERENCES usuarios(id),
 FOREIGN KEY(operacion_id) REFERENCES operaciones_inventario(id),
 CHECK(estado IN ('BORRADOR','PENDIENTE','APLICADO','CANCELADO')),
 CHECK((estado='APLICADO' AND aprobado_por IS NOT NULL AND aprobado_en IS NOT NULL AND operacion_id IS NOT NULL)
 OR (estado<>'APLICADO' AND aprobado_por IS NULL AND aprobado_en IS NULL AND operacion_id IS NULL)),
 INDEX(creado_en,id)
) ENGINE=InnoDB;
CREATE TABLE detalle_conteos_inventario (
 conteo_id BIGINT UNSIGNED NOT NULL,
 producto_id BIGINT UNSIGNED NOT NULL,
 cantidad_sistema DECIMAL(18,3) NOT NULL,
 ultimo_movimiento BIGINT UNSIGNED NOT NULL,
 cantidad_contada DECIMAL(18,3) NULL,
 PRIMARY KEY(conteo_id,producto_id),
 FOREIGN KEY(conteo_id) REFERENCES conteos_inventario(id),
 FOREIGN KEY(producto_id) REFERENCES productos(id),
 CHECK(cantidad_sistema>=0), CHECK(cantidad_contada IS NULL OR cantidad_contada>=0)
) ENGINE=InnoDB;
INSERT IGNORE INTO permisos(codigo,descripcion) VALUES
 ('inventario.conteos','Crear y capturar conteos físicos'),
 ('inventario.aprobar','Aprobar ajustes de conteos físicos');
INSERT IGNORE INTO rol_permisos(rol_id,permiso_id)
 SELECT r.id,p.id FROM roles r CROSS JOIN permisos p
 WHERE r.nombre='Administrador' AND p.codigo IN ('inventario.conteos','inventario.aprobar');
