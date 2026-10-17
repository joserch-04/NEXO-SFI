-- Identidad visual del cliente: una instalación corresponde a un negocio.
CREATE TABLE configuracion_negocio (
 id TINYINT UNSIGNED PRIMARY KEY,
 nombre VARCHAR(120) NOT NULL,
 lema VARCHAR(160) NOT NULL DEFAULT '',
 version INT UNSIGNED NOT NULL DEFAULT 1,
 actualizado_por BIGINT UNSIGNED NULL,
 actualizado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY(actualizado_por) REFERENCES usuarios(id),
 CHECK(id=1), CHECK(CHAR_LENGTH(TRIM(nombre)) > 0)
) ENGINE=InnoDB;
INSERT INTO configuracion_negocio(id,nombre) VALUES(1,'Mi tienda');
INSERT IGNORE INTO permisos(codigo,descripcion) VALUES('configuracion.administrar','Personalizar la identidad del negocio');
INSERT IGNORE INTO rol_permisos(rol_id,permiso_id)
 SELECT r.id,p.id FROM roles r CROSS JOIN permisos p
 WHERE r.nombre='Administrador' AND p.codigo='configuracion.administrar';
