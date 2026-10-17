# Verificación sesiones compartidas

16 de octubre de 2026. Relacionado: [[Sesiones en terminales compartidas]], [[Verificación Fase 5]].

## Resultados reales

- Migración mediante `pwsh -File database/Manage-Database.ps1 Migrate`: correcta. Versiones anteriores reconocidas sin modificarlas; aplicada 004_sesion_terminal_compartida con última actividad y revocación de familias existentes.
- `npm test`: **10 pruebas aprobadas**. Incluye renovación única, 401/403, cierre local aunque falle logout, bloqueo a cinco minutos con reloj simulado, rechazo de refresh sin sesión en memoria, rechazo de renovación tardía tras bloqueo y rechazo de cambio silencioso de identidad.
- `npm run build`: TypeScript y Vite correctos, 43 módulos. Build final aproximado 289 kB JS, 91 kB gzip.
- `dotnet test tests/Nexo.IntegrationTests -c Release --filter FullyQualifiedName~SecurityTests --no-restore`, usando temporalmente NEXO_TEST_MYSQL con la conexión privada existente: **33 aprobadas, cero omitidas y cero fallos**. El entorno se retiró al terminar y no se imprimió la conexión.
- Prueba real MySQL: alta de datos de prueba, lectura/mapeo de LastActivity, comprobación de límite de ocho horas, autenticación, refresh, reutilización, revocación y administración dentro de una transacción revertida. No deja cuentas de prueba permanentes.
- Pruebas adicionales HTTP/servicio con persistencia simulada: cookie Secure/HttpOnly/Strict sin Expires ni Max-Age; inactividad invalida tanto Bearer como refresh; refresh no reanima la sesión inactiva; rotar conserva el vencimiento absoluto; un nuevo login con la cookie anterior revoca la sesión anterior.
- Navegador: abrir directamente https://localhost:5173/usuarios redirigió a /login y mostró el aviso de terminal compartida y el formulario de credenciales. No se inició una sesión de usuario real durante esta revisión.
- API reiniciada con la política nueva. El primer intento de health coincidió con la compilación Debug y todavía no estaba disponible; se comprobó nuevamente al finalizar el arranque.
- Comprobación final local: health/ready devolvió ready; schema_migrations contiene 004_sesion_terminal_compartida y había cero sesiones refresh no revocadas. Usuarios y datos de negocio conservados.

## Pendientes y límites de cobertura

No se esperaron cinco minutos reales ni ocho horas en el navegador. Los vencimientos se probaron alterando el tiempo del cliente simulado y la última actividad de las sesiones de prueba. El pulso de interacción, el aviso visual, las pestañas y pagehide requieren completar el recorrido manual siguiente en los navegadores de las terminales, incluyendo suspensión y restauración de ventanas. No se afirma una certificación de producción ni un cierre de turno de caja implementado.

## Recorrido manual de aceptación

1. Ingresar con una cuenta propia. Navegar internamente y comprobar que se conserva la sesión.
2. Recargar: debe pedir usuario y contraseña. Cerrar y volver a abrir: mismo resultado, aun si el navegador restaura pestañas/cookies.
3. Ingresar y no interactuar: al minuto cuatro aparece aviso; al minuto cinco vuelve al login. Intentar una operación con el JWT anterior debe devolver 401 por familia revocada o inactiva.
4. Ingresar e interactuar periódicamente durante más de cinco minutos: continuar trabajando sin bloqueo indebido; comprobar pulso como máximo cada minuto cuando hubo actividad y no durante abandono.
5. Abrir una segunda pestaña: debe pedir login. Iniciar sesión allí: la primera debe retirar su identidad, sin heredar silenciosamente la otra cuenta.
6. Pulsar Cerrar sesión / Cambiar usuario: retirar la vista protegida y mostrar login; un operador nuevo debe identificarse con su cuenta.
7. Interrumpir la red y cerrar sesión: debe desaparecer la información aunque falle la revocación remota. Recuperar red y volver a entrar con credenciales.
8. Suspender el equipo por más de cinco minutos y regresar: antes de continuar debe exigir autenticación. Verificar también navegación atrás/adelante con caché del navegador.

La revocación remota al cerrar abruptamente un proceso no está garantizada. La ausencia de restauración automática protege la reapertura, mientras que la inactividad y el máximo absoluto limitan sesiones abandonadas. Una ventana aún abierta dentro del plazo exige disciplina de cierre por parte del operador.
