-- Fase 6: conserva el saldo y el historial anteriores en B-01.
CREATE TABLE catalogo_bloqueo(id INT PRIMARY KEY);
INSERT INTO catalogo_bloqueo VALUES(1);
ALTER TABLE categorias ADD version INT UNSIGNED NOT NULL DEFAULT 1;
ALTER TABLE proveedores ADD version INT UNSIGNED NOT NULL DEFAULT 1;
CREATE TABLE subcategorias (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, categoria_id BIGINT UNSIGNED NOT NULL,
 nombre VARCHAR(100) NOT NULL, activo BOOLEAN NOT NULL DEFAULT TRUE, version INT UNSIGNED NOT NULL DEFAULT 1,
 UNIQUE(categoria_id,nombre), UNIQUE(id,categoria_id), FOREIGN KEY(categoria_id) REFERENCES categorias(id)
);
CREATE TABLE ubicaciones (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, codigo VARCHAR(30) NOT NULL UNIQUE,
 nombre VARCHAR(100) NOT NULL, tipo VARCHAR(20) NOT NULL, padre_id BIGINT UNSIGNED,
 activo BOOLEAN NOT NULL DEFAULT TRUE, version INT UNSIGNED NOT NULL DEFAULT 1,
 FOREIGN KEY(padre_id) REFERENCES ubicaciones(id), CHECK(tipo IN ('AREA','PASILLO','SECCION','ESTANTE'))
);
INSERT INTO ubicaciones(codigo,nombre,tipo) VALUES('B-01','Bodega','AREA');
ALTER TABLE productos
 ADD subcategoria_id BIGINT UNSIGNED NULL,
 ADD costo DECIMAL(18,4) NOT NULL DEFAULT 0,
 ADD unidad VARCHAR(20) NOT NULL DEFAULT 'UNIDAD',
 ADD controla_inventario BOOLEAN NOT NULL DEFAULT TRUE,
 ADD version INT UNSIGNED NOT NULL DEFAULT 1,
 ADD FOREIGN KEY(subcategoria_id,categoria_id) REFERENCES subcategorias(id,categoria_id),
 ADD CHECK(costo >= 0);
CREATE TABLE existencias_ubicacion (
 producto_id BIGINT UNSIGNED NOT NULL, ubicacion_id BIGINT UNSIGNED NOT NULL,
 cantidad DECIMAL(18,3) NOT NULL DEFAULT 0, PRIMARY KEY(producto_id,ubicacion_id),
 FOREIGN KEY(producto_id) REFERENCES productos(id), FOREIGN KEY(ubicacion_id) REFERENCES ubicaciones(id),
 CHECK(cantidad >= 0)
);
INSERT INTO existencias_ubicacion SELECT producto_id,(SELECT id FROM ubicaciones WHERE codigo='B-01'),cantidad FROM existencias;
ALTER TABLE movimientos_inventario ADD ubicacion_id BIGINT UNSIGNED NULL, ADD FOREIGN KEY(ubicacion_id) REFERENCES ubicaciones(id);
UPDATE movimientos_inventario SET ubicacion_id=(SELECT id FROM ubicaciones WHERE codigo='B-01');
ALTER TABLE operaciones_inventario ADD traslado BOOLEAN NOT NULL DEFAULT FALSE;
INSERT IGNORE INTO permisos(codigo,descripcion) VALUES
 ('catalogo.leer','Consultar catálogo'),('catalogo.administrar','Administrar catálogo y ubicaciones'),
 ('proveedores.leer','Consultar proveedores'),('proveedores.administrar','Administrar proveedores'),
 ('inventario.leer','Consultar saldos y movimientos'),('inventario.entradas','Registrar entradas'),
 ('inventario.ajustar','Registrar salidas, ajustes y traslados');
INSERT IGNORE INTO rol_permisos SELECT r.id,p.id FROM roles r CROSS JOIN permisos p WHERE r.nombre='Administrador';
INSERT IGNORE INTO rol_permisos SELECT r.id,p.id FROM roles r CROSS JOIN permisos p
 WHERE r.nombre IN ('Consulta','Vendedor/Cajero') AND p.codigo IN ('catalogo.leer','inventario.leer');

