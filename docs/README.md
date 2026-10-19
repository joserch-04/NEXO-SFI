# NEXO — documentación del proyecto


## Fase actual: punto de venta y caja

La Fase 7 integra imágenes de productos, cajas, turnos, POS, ventas, pagos combinados, comprobantes internos, anulaciones y arqueos. Véanse [[Fase 7]], [[Requisitos POS y caja]], [[Modelo POS y caja]], [[Manual POS y caja]] y [[Verificación Fase 7]].

Personalización del cliente: [[Identidad y configuración del negocio]] y [[Verificación identidad del negocio]]. NEXO es el software; el nombre de la tienda se administra desde Configuración.

La Fase 6 incorpora categorías, subcategorías, proveedores, ubicaciones, productos, movimientos atómicos y alertas. Véanse [[Fase 6]], [[Manual catálogo e inventario]] y [[Verificación Fase 6]].

La Fase 5 incorpora React/TypeScript, Tailwind, rutas protegidas, formularios y cliente de sesión. Véanse [[Fase 5]] y [[Verificación Fase 5]] para instalación, funcionamiento y límites de verificación.

Configuración aplicada de certificados, VS Code y Visual Studio 2026: [[Ejecución local IDE]].

Alta/edición de cuentas de cajeros y consulta, roles y permisos: [[Administración de usuarios frontend]].

Cambios de contraseña exclusivos del administrador: [[Política de contraseñas]].

Política vigente de acceso en cajas: [[Sesiones en terminales compartidas]] y [[Verificación sesiones compartidas]]. Sustituye la restauración automática y las cookies persistentes de siete días descritas en las notas históricas.

La Fase 4 implementa sesión JWT, refresh revocable, usuarios, roles y políticas. Véanse [[Fase 4]] y [[Verificación Fase 4]]. La Fase 3 incorpora la solución ASP.NET Core 10, capas, conexión Dapper/MySQL, errores, OpenAPI y pruebas iniciales. Véanse [[Fase 3]] y [[Verificación Fase 3]]. La solicitud de Fase 2 autorizó implementar el modelo preliminar. Ya existen SQL, semillas y Compose; las notas de Fase 1 siguientes conservan el contexto del diseño. El archivo real de instrucciones actual es `AGENTS.md`.

- [[Fase 2]] — implementación, comandos, tablas y defensa técnica.
- [[Diagrama entidad-relación]] — relaciones del SQL implementado.
- [[Verificación Fase 2]] — evidencia real y límites.

## Registro histórico de Fase 1
Fecha: 15 de octubre de 2026. Estado original: propuesta pendiente de aprobación.

Fuente: `../AGENTS.md.md`, único archivo preexistente encontrado. Se conserva su nombre; no existe `AGENTS.md` en la raíz del espacio de trabajo. No se han creado aplicaciones, migraciones, contenedores ni instalado dependencias.

## Objetivo y alcance

Diseñar un sistema de inventario y facturación para una tienda de conveniencia, operado en un servidor local y utilizado desde terminales LAN. Debe funcionar sin Internet mientras servidor, red y equipos estén disponibles. Este documento y sus notas describen propuestas, no capacidades implementadas.

Fuera de alcance: PWA, IndexedDB, ventas sin conexión al servidor, sincronización, nube y pagos bancarios reales. Se propone una tienda, una ubicación de inventario y moneda NIO; estas tres suposiciones requieren aprobación.

## Lectura del diseño

1. [[Requisitos]] — [requisitos y aceptación](requisitos/Requisitos.md).
2. [[Arquitectura]] — [módulos, capas, repositorio y seguridad](arquitectura/Arquitectura.md).
3. [[Modelo preliminar]] — [tablas, relaciones e integridad](base-de-datos/Modelo%20preliminar.md).
4. [[API REST]] — [contratos y permisos](arquitectura/API%20REST.md).
5. [[Docker local]] — [desarrollo y operación](arquitectura/Docker%20local.md).
6. [[Plan de pruebas]] — [validaciones previstas](pruebas/Plan%20de%20pruebas.md).
7. [[Decisiones pendientes]] — [aprobaciones y preguntas del jurado](arquitectura/Decisiones%20pendientes.md).

## Límite histórico de Fase 1

Solo se producen notas Markdown en `docs/`. No existe código cuyo funcionamiento pueda explicarse o probarse aún. Las clases, métodos, endpoints y tablas descritos son diseños. Tras la aprobación corresponde exclusivamente iniciar la Fase 2: base de datos y Docker.

## Verificación e incidencias históricas de Fase 1

Se leyó el archivo de instrucciones y se inspeccionó el árbol existente. La búsqueda inicial de `AGENTS.md` falló porque el archivo real se llama `AGENTS.md.md`; se localizó y leyó sin renombrarlo. La revisión documental verifica cobertura del alcance y coherencia entre notas; no equivale a pruebas funcionales, ejecución SQL, validación de Docker ni compilación. Estas permanecen pendientes.

Ampliación de cobros de Fase 7: [[Referencias de pagos]] explica el registro manual obligatorio de referencia de tarjeta/transferencia y su relación con terminales NFC de producción.

Organización actual de la interfaz: [[Navegación por secciones]] — POS independiente, caja e historial derivados y menú agrupado.
