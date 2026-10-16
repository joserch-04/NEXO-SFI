# Administración de usuarios frontend

15 de octubre de 2026. Extensión autorizada de [[Fase 5]], sobre los contratos de [[Fase 4]]. Ver también [[Verificación Fase 5]].

## Objetivo y tecnologías

Permitir que el administrador cree cuentas de Vendedor/Cajero y Consulta y configure sus accesos desde React. Se reutilizan React, TypeScript, Tailwind, React Router y ApiClient; no se agregan dependencias, migraciones ni endpoints.

## Flujo para el administrador

1. En Usuarios, pulsar Crear usuario e introducir nombre, usuario, contraseña inicial y estado. Usuario admite 3–80 caracteres alfanuméricos, punto, guion y guion bajo. Contraseña: 12–128 caracteres, letras y números.
2. Crear cuenta llama POST `/usuarios`. Tras confirmación del servidor, aparece la selección de roles. Marcar Vendedor/Cajero, Consulta u otros roles activos y guardar mediante PUT `/usuarios/{id}/roles`.
3. Las etapas son independientes porque la API ofrece dos operaciones. Si se cancela o falla la segunda, la cuenta ya existe sin los nuevos roles: utilizar el botón Roles de esa fila, sin crearla otra vez. La interfaz informa que falta asignar roles. Nunca anuncia éxito conjunto antes de ambas operaciones.
4. Editar cambia nombre, usuario o estado mediante PUT `/usuarios/{id}`. Desmarcar Cuenta activa desactiva; volver a marcarla reactiva. No hay borrado físico. Se confirma antes de actualizar, porque se revocan las sesiones del usuario. Editar la propia cuenta termina la sesión local tras éxito.
5. En Roles y permisos elegir un rol y marcar los permisos del catálogo. Guardar llama PUT `/roles/{id}/permisos`. Afecta a todos los usuarios de ese rol; se solicita confirmación. Administrador está protegido y se muestra como solo lectura.

## Responsabilidades y permisos

`src/administration.tsx` contiene Users, UserEditor, UserRoles y Roles. Sus efectos cancelan actualizaciones de estado de solicitudes anteriores al cambiar usuario/rol. Los formularios deshabilitan controles durante guardado y manejan errores, carga y reintento. `main.tsx` registra `/roles` protegida por `roles.leer` y el enlace correspondiente. `api.ts` traduce conflictos y errores de validación/seguridad a español.

- `usuarios.leer`: listado paginado.
- `usuarios.administrar`: creación y edición/estado.
- `roles.asignar`, `roles.leer` y rol Administrador: selector de asignación. No aparece para la propia cuenta, coherente con el backend.
- `roles.leer`: consulta de catálogo de roles y permisos.
- `roles.administrar` y rol Administrador: edición de permisos de roles distintos de Administrador.

Los permisos se asignan a roles y se suman para cada usuario. No hay permisos individuales directos. El servidor aplica siempre sus políticas y protege el último administrador activo. Las tablas siguen siendo usuarios, roles, permisos, usuario_roles, rol_permisos, sesiones refresh y auditoría de Fase 4. No se modifican los permisos existentes automáticamente.

Vendedor/Cajero tiene actualmente catalogo.leer y ventas.crear; Consulta tiene catalogo.leer y reportes.leer. Estos permisos del catálogo preparan los accesos: las pantallas de inventario, ventas, caja y reportes continúan en sus fases correspondientes. Esta entrega no crea esos módulos ni permite crear códigos de permiso fuera del catálogo SQL. No incluye formularios para crear o renombrar roles. El cambio de contraseñas ajenas por el administrador se incorporó posteriormente: [[Política de contraseñas]].

## Verificación ejecutada y límites

- `npm run build`: TypeScript y Vite correctos, 42 módulos.
- `npm test`: seis pruebas del cliente aprobadas.
- `dotnet test tests/Nexo.IntegrationTests -c Release --filter FullyQualifiedName~SecurityTests --no-restore`: 27 aprobadas y una omitida (MySQL real sin NEXO_TEST_MYSQL). Incluye administración, políticas, revocación y protección del último administrador con persistencia simulada. El SDK emite aviso por versión preliminar; no hubo fallos.
- Navegador local autenticado: listado real, controles Crear usuario/Editar/Roles, formulario de creación y carga del selector de roles de demo.cajero1 con Vendedor/Cajero seleccionado. Se comprobaron catálogos de Vendedor/Cajero y Consulta contra API local; mostraron permisos y botón Guardar permisos. No se guardaron cambios a cuentas o permisos existentes durante esta comprobación.

Pendiente: envío completo de los formularios en navegador con una cuenta de prueba expresamente elegida, así como revisión móvil y navegación sin privilegios. La evidencia HTTP de políticas está en las pruebas backend, pero no sustituye esos recorridos de interfaz. No se crean cuentas permanentes de prueba para verificar la pantalla.

## Preguntas del jurado

- ¿Por qué dos pasos para crear usuario? Los contratos actuales separan alta y asignación; la interfaz comunica el avance real y conserva la cuenta si falla la segunda operación.
- ¿Consulta puede vender? Con sus permisos actuales no tiene ventas.crear. El backend de cada módulo deberá aplicar la política correspondiente cuando se implemente.
- ¿Ocultar el botón basta? No, el servidor autentica y valida permisos y reglas antes de cualquier cambio.
- ¿Qué pasa al desactivar el último administrador? El backend rechaza y revierte; la interfaz muestra el conflicto y mantiene los datos anteriores.
