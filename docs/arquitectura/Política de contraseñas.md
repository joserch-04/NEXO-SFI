# Política de contraseñas

Regla solicitada el 15 de octubre de 2026. Relacionado: [[Fase 4]], [[Fase 5]], [[Administración de usuarios frontend]]. Esta regla sustituye el autoservicio anterior para personal no administrador.

Solo un usuario activo con rol Administrador y permiso usuarios.administrar puede cambiar contraseñas. El administrador cambia la propia desde Mi cuenta aportando la contraseña actual. Para otra cuenta, abre Usuarios → Cambiar contraseña, introduce y confirma la nueva, y confirma el cambio. No necesita la contraseña anterior ajena.

Vendedor/Cajero y Consulta no disponen de formulario de cambio propio: deben pedirlo al administrador. La autorización consiste en que el administrador autenticado realiza el cambio; no se crea una aprobación temporal ni un trámite de solicitudes. No se alteran contraseñas existentes por aplicar esta regla.

`SecurityService.ChangePassword` comprueba permiso y rol actual desde la BD antes de validar la entrada. También rechaza con 403 a un usuario no administrador que haya recibido usuarios.administrar. Continúa usando PasswordHasher, política de 12–128 caracteres con letras y números, revocación de todas las sesiones y auditoría PASSWORD_CAMBIAR. POST `/usuarios/{id}/cambio-contrasena` conserva su contrato; no hay cambios SQL.

`main.tsx` presenta el cambio propio únicamente al administrador. `administration.tsx` ofrece AdminPassword para cuentas ajenas; valida confirmación, no muestra contraseñas existentes y no almacena la nueva fuera de la solicitud. El backend decide el acceso incluso ante llamadas directas.

Pruebas de seguridad actualizadas: un trabajador no puede cambiarse la contraseña; el administrador sí puede cambiar la suya y la ajena, revocando sesiones. Dos casos adicionales cubren Vendedor/Cajero y Consulta con permiso administrativo concedido y verifican 403 y conservación de la contraseña anterior. Las pruebas usan persistencia simulada, sin cambiar cuentas reales.

Verificación ejecutada: compilación frontend TypeScript/Vite correcta; pruebas de seguridad backend Release: 29 aprobadas, una MySQL omitida por ausencia de NEXO_TEST_MYSQL. No se cambió ninguna contraseña real ni se envió el formulario de cambio en el navegador. Se reinició la API local para aplicar la regla.

Defensa: ocultar el formulario evita confusión; la comprobación de rol y permiso del servidor impide eludir la regla mediante peticiones HTTP. El registro de auditoría identifica al administrador que autorizó y efectuó el cambio.
