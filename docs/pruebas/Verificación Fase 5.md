# Verificación Fase 5

Ampliación posterior de administración: véase [[Administración de usuarios frontend]] para resultados de compilación, 27 pruebas backend aprobadas (una omitida) y comprobaciones de formularios/catálogos en navegador.

Fecha: 15 de octubre de 2026. Relacionado: [[Fase 5]], [[Verificación Fase 4]].

## Ejecutado

- `npm install`: dependencias instaladas y package-lock.json generado. La primera instalación encontró restricciones de red EACCES; se repitió con autorización fuera del sandbox.
- La auditoría inicial detectó tres vulnerabilidades en dependencias de Vitest 3. Se actualizó a Vitest 5.0.5; la instalación final auditó 106 paquetes y reportó cero vulnerabilidades.
- `npm test`: Vitest 5.0.5, un archivo y **6 pruebas aprobadas**. Comprueba deduplicación de refresh, cabecera CSRF y cookie, dos recursos concurrentes con 401 y una renovación, 403 sin renovación, limpieza tras refresh rechazado, conservación de sesión ante fallo de logout, respuesta 204 y conservación de código/traceId.
- `npm run build`: comprobación TypeScript estricta y compilación Vite 6.4.4 aprobadas. 41 módulos; salida dist con HTML, CSS y JS. JS aproximado: 276 kB, 88 kB gzip.
- `git diff --check`: sin errores de whitespace; avisos normales LF/CRLF de Windows.

La primera ejecución de Vite/esbuild dentro del sandbox falló por acceso denegado al resolver la configuración desde directorios superiores. Compilación y pruebas pasaron al ejecutarse fuera del sandbox con autorización. No se cambió la aplicación para ocultar este fallo.

## Alcance de la evidencia

Las pruebas usan fetch y Web Locks simulados; no prueban cookies de navegador, CORS real, certificados, persistencia MySQL ni exclusión entre procesos/pestañas reales. No se modificó ni se volvió a probar el backend en esta fase. Los cambios previos de Fase 4 permanecen en el árbol de trabajo.

## Verificación manual pendiente

Con API HTTPS y MySQL disponibles, preparar certificado y `.env.local` siguiendo [[Fase 5]].

1. Abrir `https://localhost:5173` sin sesión; confirmar login, credenciales erróneas y acceso correcto con una cuenta existente.
2. Visitar `/cuenta` sin sesión; comprobar retorno a esa ruta tras login. Verificar `/usuarios` con y sin `usuarios.leer`, y ausencia de enlace para quien no tiene permiso.
3. Recargar página; confirmar restauración mediante cookie HttpOnly/Secure/Strict. Comprobar ausencia de tokens en almacenamiento local.
4. Esperar vencimiento del JWT; consultar usuarios y confirmar una renovación y reintento. Probar dos pestañas del mismo origen para verificar Web Locks y rotación real.
5. Detener temporalmente la API; comprobar mensaje de conexión, recuperación y que logout fallido no se presenta como revocación exitosa.
6. Cambiar contraseña: rechazar confirmación distinta/contraseña actual incorrecta y aceptar una válida. Confirmar que la contraseña anterior deja de funcionar y las sesiones anteriores quedan revocadas.
7. Revisar ancho móvil y escritorio, navegación por teclado, etiquetas, estados de carga y página inexistente. Probar paginación con más de 25 cuentas solo en un dataset autorizado.
8. Cerrar sesión, recargar y comprobar que no reaparece la sesión. Otra pestaña puede seguir mostrando su pantalla hasta solicitar un recurso; el backend debe rechazarla.

No se declara esta comprobación integral realizada. La base está implementada y verificada por compilación y pruebas unitarias del cliente; la aceptación de navegador permanece pendiente.
