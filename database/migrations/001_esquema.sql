-- MySQL 8.4. Aplicar una sola vez mediante database/Manage-Database.ps1.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
CREATE TABLE roles (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, nombre VARCHAR(60) NOT NULL UNIQUE,
 activo BOOLEAN NOT NULL DEFAULT TRUE
) ENGINE=InnoDB;
CREATE TABLE permisos (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, codigo VARCHAR(100) NOT NULL UNIQUE,
 descripcion VARCHAR(255) NOT NULL
) ENGINE=InnoDB;
CREATE TABLE usuarios (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, nombre_usuario VARCHAR(80) NOT NULL UNIQUE,
 nombre VARCHAR(160) NOT NULL, password_hash VARCHAR(512) NOT NULL, activo BOOLEAN NOT NULL DEFAULT TRUE,
 version_sesion INT UNSIGNED NOT NULL DEFAULT 1, creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB;
CREATE TABLE usuario_roles (
 usuario_id BIGINT UNSIGNED NOT NULL, rol_id BIGINT UNSIGNED NOT NULL, PRIMARY KEY(usuario_id,rol_id),
 FOREIGN KEY(usuario_id) REFERENCES usuarios(id), FOREIGN KEY(rol_id) REFERENCES roles(id)
) ENGINE=InnoDB;
CREATE TABLE rol_permisos (
 rol_id BIGINT UNSIGNED NOT NULL, permiso_id BIGINT UNSIGNED NOT NULL, PRIMARY KEY(rol_id,permiso_id),
 FOREIGN KEY(rol_id) REFERENCES roles(id), FOREIGN KEY(permiso_id) REFERENCES permisos(id)
) ENGINE=InnoDB;
CREATE TABLE sesiones_refresh (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, usuario_id BIGINT UNSIGNED NOT NULL,
 familia CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, hash BINARY(32) NOT NULL UNIQUE,
 creado_en DATETIME(6) NOT NULL, expira_en DATETIME(6) NOT NULL, revocado_en DATETIME(6),
 reemplazo_id BIGINT UNSIGNED, FOREIGN KEY(usuario_id) REFERENCES usuarios(id),
 FOREIGN KEY(reemplazo_id) REFERENCES sesiones_refresh(id), INDEX(usuario_id,familia),
 CHECK(expira_en > creado_en), CHECK(revocado_en IS NULL OR revocado_en >= creado_en)
) ENGINE=InnoDB;
CREATE TABLE categorias (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, nombre VARCHAR(100) NOT NULL UNIQUE, activo BOOLEAN NOT NULL DEFAULT TRUE
) ENGINE=InnoDB;
CREATE TABLE proveedores (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, nombre VARCHAR(180) NOT NULL,
 identificacion VARCHAR(50) UNIQUE, telefono VARCHAR(40), activo BOOLEAN NOT NULL DEFAULT TRUE
) ENGINE=InnoDB;
CREATE TABLE productos (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, categoria_id BIGINT UNSIGNED NOT NULL,
 sku VARCHAR(50) NOT NULL UNIQUE, codigo_barras VARCHAR(64) UNIQUE, nombre VARCHAR(180) NOT NULL,
 precio DECIMAL(18,4) NOT NULL, stock_minimo DECIMAL(18,3) NOT NULL DEFAULT 0,
 activo BOOLEAN NOT NULL DEFAULT TRUE, FOREIGN KEY(categoria_id) REFERENCES categorias(id),
 CHECK(precio >= 0), CHECK(stock_minimo >= 0)
) ENGINE=InnoDB;
CREATE TABLE producto_proveedores (
 producto_id BIGINT UNSIGNED NOT NULL, proveedor_id BIGINT UNSIGNED NOT NULL,
 PRIMARY KEY(producto_id,proveedor_id), FOREIGN KEY(producto_id) REFERENCES productos(id),
 FOREIGN KEY(proveedor_id) REFERENCES proveedores(id)
) ENGINE=InnoDB;
CREATE TABLE existencias (
 producto_id BIGINT UNSIGNED PRIMARY KEY, cantidad DECIMAL(18,3) NOT NULL DEFAULT 0,
 FOREIGN KEY(producto_id) REFERENCES productos(id), CHECK(cantidad >= 0)
) ENGINE=InnoDB;
CREATE TABLE cajas (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, codigo VARCHAR(30) NOT NULL UNIQUE,
 nombre VARCHAR(100) NOT NULL, activo BOOLEAN NOT NULL DEFAULT TRUE
) ENGINE=InnoDB;
CREATE TABLE turnos_caja (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, caja_id BIGINT UNSIGNED NOT NULL,
 usuario_id BIGINT UNSIGNED NOT NULL, apertura_en DATETIME(6) NOT NULL, cierre_en DATETIME(6),
 fondo_inicial DECIMAL(18,2) NOT NULL, esperado DECIMAL(18,2), contado DECIMAL(18,2),
 diferencia DECIMAL(18,2) GENERATED ALWAYS AS (contado - esperado) STORED,
 estado VARCHAR(10) NOT NULL,
 caja_abierta BIGINT UNSIGNED GENERATED ALWAYS AS (CASE WHEN estado='ABIERTO' THEN caja_id END) STORED,
 UNIQUE(caja_abierta), UNIQUE(id,caja_id,usuario_id),
 FOREIGN KEY(caja_id) REFERENCES cajas(id), FOREIGN KEY(usuario_id) REFERENCES usuarios(id),
 CHECK(fondo_inicial >= 0), CHECK(contado IS NULL OR contado >= 0),
 CHECK((estado='ABIERTO' AND cierre_en IS NULL AND esperado IS NULL AND contado IS NULL)
 OR (estado='CERRADO' AND cierre_en IS NOT NULL AND cierre_en >= apertura_en AND esperado IS NOT NULL AND contado IS NOT NULL))
) ENGINE=InnoDB;
CREATE TABLE ventas (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, turno_id BIGINT UNSIGNED NOT NULL,
 caja_id BIGINT UNSIGNED NOT NULL, usuario_id BIGINT UNSIGNED NOT NULL,
 fecha DATETIME(6) NOT NULL, estado VARCHAR(12) NOT NULL,
 subtotal DECIMAL(18,2) NOT NULL, descuento DECIMAL(18,2) NOT NULL DEFAULT 0,
 impuesto DECIMAL(18,2) NOT NULL DEFAULT 0, total DECIMAL(18,2) NOT NULL,
 FOREIGN KEY(turno_id,caja_id,usuario_id) REFERENCES turnos_caja(id,caja_id,usuario_id),
 UNIQUE(id,turno_id), INDEX(fecha,id), INDEX(turno_id,fecha),
 CHECK(estado IN ('CONFIRMADA','ANULADA')),
 CHECK(subtotal >= 0 AND descuento >= 0 AND descuento <= subtotal AND impuesto >= 0),
 CHECK(total = subtotal - descuento + impuesto)
) ENGINE=InnoDB;
CREATE TABLE detalle_ventas (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, venta_id BIGINT UNSIGNED NOT NULL,
 producto_id BIGINT UNSIGNED NOT NULL, sku_snapshot VARCHAR(50) NOT NULL, nombre_snapshot VARCHAR(180) NOT NULL,
 cantidad DECIMAL(18,3) NOT NULL, precio_unitario DECIMAL(18,4) NOT NULL,
 tasa_impuesto DECIMAL(7,4) NOT NULL DEFAULT 0, subtotal DECIMAL(18,2) NOT NULL,
 descuento DECIMAL(18,2) NOT NULL DEFAULT 0, impuesto DECIMAL(18,2) NOT NULL DEFAULT 0, total DECIMAL(18,2) NOT NULL,
 UNIQUE(id,venta_id), UNIQUE(id,producto_id),
 FOREIGN KEY(venta_id) REFERENCES ventas(id), FOREIGN KEY(producto_id) REFERENCES productos(id),
 CHECK(cantidad > 0 AND precio_unitario >= 0 AND tasa_impuesto >= 0),
 CHECK(subtotal >= 0 AND descuento >= 0 AND descuento <= subtotal AND impuesto >= 0),
 CHECK(total = subtotal - descuento + impuesto)
) ENGINE=InnoDB;
CREATE TABLE pagos (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, venta_id BIGINT UNSIGNED NOT NULL,
 metodo VARCHAR(30) NOT NULL, importe_aplicado DECIMAL(18,2) NOT NULL,
 recibido DECIMAL(18,2) NOT NULL, cambio DECIMAL(18,2) NOT NULL,
 FOREIGN KEY(venta_id) REFERENCES ventas(id), INDEX(venta_id,metodo),
 CHECK(metodo='EFECTIVO'), CHECK(importe_aplicado > 0 AND cambio >= 0),
 CHECK(recibido = importe_aplicado + cambio)
) ENGINE=InnoDB;
CREATE TABLE facturas (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, venta_id BIGINT UNSIGNED NOT NULL UNIQUE,
 serie VARCHAR(20) NOT NULL, numero BIGINT UNSIGNED NOT NULL, emitido_en DATETIME(6) NOT NULL,
 tipo VARCHAR(20) NOT NULL, datos_emisor_snapshot TEXT NOT NULL,
 UNIQUE(serie,numero), FOREIGN KEY(venta_id) REFERENCES ventas(id), CHECK(numero > 0), CHECK(tipo='ACADEMICO')
) ENGINE=InnoDB;
CREATE TABLE anulaciones (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, venta_id BIGINT UNSIGNED NOT NULL UNIQUE,
 autorizado_por BIGINT UNSIGNED NOT NULL, turno_reembolso_id BIGINT UNSIGNED NOT NULL,
 motivo VARCHAR(500) NOT NULL, creado_en DATETIME(6) NOT NULL, reembolso DECIMAL(18,2) NOT NULL,
 FOREIGN KEY(venta_id) REFERENCES ventas(id), FOREIGN KEY(autorizado_por) REFERENCES usuarios(id),
 FOREIGN KEY(turno_reembolso_id) REFERENCES turnos_caja(id), CHECK(reembolso >= 0), CHECK(CHAR_LENGTH(TRIM(motivo)) > 0)
) ENGINE=InnoDB;
CREATE TABLE devoluciones (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, venta_id BIGINT UNSIGNED NOT NULL,
 turno_id BIGINT UNSIGNED NOT NULL, autorizado_por BIGINT UNSIGNED NOT NULL,
 motivo VARCHAR(500) NOT NULL, creado_en DATETIME(6) NOT NULL, total_reembolso DECIMAL(18,2) NOT NULL,
 UNIQUE(id,venta_id), FOREIGN KEY(venta_id) REFERENCES ventas(id), FOREIGN KEY(turno_id) REFERENCES turnos_caja(id),
 FOREIGN KEY(autorizado_por) REFERENCES usuarios(id), CHECK(total_reembolso >= 0), CHECK(CHAR_LENGTH(TRIM(motivo)) > 0)
) ENGINE=InnoDB;
CREATE TABLE detalle_devoluciones (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, devolucion_id BIGINT UNSIGNED NOT NULL,
 venta_id BIGINT UNSIGNED NOT NULL, detalle_venta_id BIGINT UNSIGNED NOT NULL,
 cantidad DECIMAL(18,3) NOT NULL, importe DECIMAL(18,2) NOT NULL, reintegrar_stock BOOLEAN NOT NULL,
 UNIQUE(devolucion_id,detalle_venta_id),
 FOREIGN KEY(devolucion_id,venta_id) REFERENCES devoluciones(id,venta_id),
 FOREIGN KEY(detalle_venta_id,venta_id) REFERENCES detalle_ventas(id,venta_id),
 CHECK(cantidad > 0 AND importe >= 0)
) ENGINE=InnoDB;
CREATE TABLE operaciones_inventario (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, tipo VARCHAR(24) NOT NULL,
 motivo VARCHAR(500) NOT NULL, proveedor_id BIGINT UNSIGNED, referencia VARCHAR(100),
 usuario_id BIGINT UNSIGNED NOT NULL, creado_en DATETIME(6) NOT NULL,
 FOREIGN KEY(proveedor_id) REFERENCES proveedores(id), FOREIGN KEY(usuario_id) REFERENCES usuarios(id),
 CHECK(tipo IN ('ENTRADA','SALIDA','AJUSTE')), CHECK(CHAR_LENGTH(TRIM(motivo)) > 0)
) ENGINE=InnoDB;
CREATE TABLE movimientos_inventario (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, producto_id BIGINT UNSIGNED NOT NULL,
 delta DECIMAL(18,3) NOT NULL, costo_unitario DECIMAL(18,4), operacion_id BIGINT UNSIGNED,
 detalle_venta_id BIGINT UNSIGNED, detalle_devolucion_id BIGINT UNSIGNED,
 tipo VARCHAR(24) NOT NULL, usuario_id BIGINT UNSIGNED NOT NULL, creado_en DATETIME(6) NOT NULL,
 FOREIGN KEY(producto_id) REFERENCES productos(id), FOREIGN KEY(operacion_id) REFERENCES operaciones_inventario(id),
 FOREIGN KEY(detalle_venta_id,producto_id) REFERENCES detalle_ventas(id,producto_id),
 FOREIGN KEY(detalle_devolucion_id) REFERENCES detalle_devoluciones(id), FOREIGN KEY(usuario_id) REFERENCES usuarios(id),
 INDEX(producto_id,creado_en,id), CHECK(delta <> 0), CHECK(costo_unitario IS NULL OR costo_unitario >= 0),
 CHECK((tipo='MANUAL' AND operacion_id IS NOT NULL AND detalle_venta_id IS NULL AND detalle_devolucion_id IS NULL)
 OR (tipo='VENTA' AND delta < 0 AND operacion_id IS NULL AND detalle_venta_id IS NOT NULL AND detalle_devolucion_id IS NULL)
 OR (tipo='ANULACION' AND delta > 0 AND operacion_id IS NULL AND detalle_venta_id IS NOT NULL AND detalle_devolucion_id IS NULL)
 OR (tipo='DEVOLUCION' AND delta > 0 AND operacion_id IS NULL AND detalle_venta_id IS NULL AND detalle_devolucion_id IS NOT NULL))
) ENGINE=InnoDB;
CREATE TABLE movimientos_caja (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, turno_id BIGINT UNSIGNED NOT NULL,
 usuario_id BIGINT UNSIGNED NOT NULL, tipo VARCHAR(10) NOT NULL, importe DECIMAL(18,2) NOT NULL,
 motivo VARCHAR(500) NOT NULL, creado_en DATETIME(6) NOT NULL,
 FOREIGN KEY(turno_id) REFERENCES turnos_caja(id), FOREIGN KEY(usuario_id) REFERENCES usuarios(id),
 CHECK(tipo IN ('INGRESO','EGRESO')), CHECK(importe > 0), CHECK(CHAR_LENGTH(TRIM(motivo)) > 0)
) ENGINE=InnoDB;
CREATE TABLE operaciones_idempotentes (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, usuario_id BIGINT UNSIGNED NOT NULL,
 tipo VARCHAR(40) NOT NULL, clave VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 hash_solicitud BINARY(32) NOT NULL, recurso_id BIGINT UNSIGNED NOT NULL, estado_http SMALLINT UNSIGNED NOT NULL,
 creado_en DATETIME(6) NOT NULL, UNIQUE(usuario_id,tipo,clave),
 FOREIGN KEY(usuario_id) REFERENCES usuarios(id), CHECK(estado_http BETWEEN 200 AND 299)
) ENGINE=InnoDB;
CREATE TABLE auditoria (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, actor_id BIGINT UNSIGNED,
 accion VARCHAR(100) NOT NULL, recurso_tipo VARCHAR(60) NOT NULL, recurso_id BIGINT UNSIGNED,
 fecha DATETIME(6) NOT NULL, resultado VARCHAR(20) NOT NULL,
 correlacion CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, resumen TEXT NOT NULL,
 FOREIGN KEY(actor_id) REFERENCES usuarios(id), INDEX(fecha,id)
) ENGINE=InnoDB;
CREATE TABLE respaldos (
 id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, solicitado_por BIGINT UNSIGNED NOT NULL,
 nombre_interno VARCHAR(180) NOT NULL UNIQUE, hash_archivo BINARY(32), estado VARCHAR(20) NOT NULL,
 creado_en DATETIME(6) NOT NULL, finalizado_en DATETIME(6), error_saneado VARCHAR(500),
 FOREIGN KEY(solicitado_por) REFERENCES usuarios(id), CHECK(estado IN ('PENDIENTE','EN_PROCESO','COMPLETADO','FALLIDO'))
) ENGINE=InnoDB;
