-- Serializa mutaciones de seguridad: rotación, bootstrap y último administrador.
CREATE TABLE seguridad_bloqueo (id INT PRIMARY KEY);
INSERT INTO seguridad_bloqueo VALUES (1);
INSERT IGNORE INTO roles(nombre) VALUES ('Administrador'),('Vendedor/Cajero'),('Consulta');
INSERT IGNORE INTO permisos(codigo,descripcion) VALUES
 ('usuarios.leer','Consultar usuarios'),('usuarios.administrar','Administrar usuarios'),
 ('roles.leer','Consultar roles y permisos'),('roles.administrar','Administrar roles y permisos'),
 ('roles.asignar','Asignar roles a usuarios');
INSERT IGNORE INTO rol_permisos(rol_id,permiso_id)
 SELECT r.id,p.id FROM roles r CROSS JOIN permisos p WHERE r.nombre='Administrador';
