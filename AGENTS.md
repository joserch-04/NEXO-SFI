# NEXO — Instrucciones del proyecto para Codex

## 1\. Propósito

NEXO es una aplicación web para **control de inventario y facturación de una tienda de conveniencia**, desarrollada como proyecto de culminación de estudios de Ingeniería de Sistemas de Información.

El objetivo es entregar un sistema funcional, seguro, mantenible y defendible ante un jurado académico. Priorizar los requisitos obligatorios y la estabilidad sobre características adicionales.

## 2\. Alcance

NEXO funcionará sobre un **servidor local dentro de la tienda**. Las terminales acceden al sistema mediante la red local (LAN). El acceso a Internet no es requisito para las operaciones internas, siempre que servidor, red y equipos estén encendidos y operativos.

**No implementar en esta versión:** PWA, IndexedDB, modo POS sin conexión al servidor, colas de sincronización, replicación con la nube ni pagos bancarios reales. No introducir estos componentes sin aprobación explícita.

## 3\. Tecnologías obligatorias

|Componente|Tecnología|
|-|-|
|Frontend|React + TypeScript + Vite|
|Estilos|Tailwind CSS|
|Editor frontend|Visual Studio Code|
|Backend|C# + ASP.NET Core 10 Web API|
|Editor backend|Visual Studio compatible con .NET 10 (Visual Studio 2026); Visual Studio 2022 no ofrece soporte oficial para proyectos .NET 10|
|Base de datos|MySQL Server 8.4 LTS|
|Acceso a datos|Dapper + MySqlConnector, **sin Entity Framework Core**|
|Autenticación|JWT Bearer y refresh tokens revocables|
|Autorización|Roles y políticas de permisos|
|Hash de contraseñas|PasswordHasher de ASP.NET Core Identity, sin usar su proveedor EF Core|
|Contenedores|Docker Desktop + Docker Compose|
|Documentación API|OpenAPI|
|Versionado|Git|

No cambiar el stack sin autorización.

## 4\. Organización del repositorio

```text
NEXO/
├── AGENTS.md
├── README.md
├── compose.yaml
├── .env.example
├── .gitignore
├── docs/
│   ├── requisitos/
│   ├── arquitectura/
│   ├── base-de-datos/
│   └── pruebas/
├── frontend/
│   └── nexo-web/
├── backend/
│   ├── Nexo.sln
│   ├── Nexo.Api/
│   ├── Nexo.Application/
│   ├── Nexo.Domain/
│   └── Nexo.Infrastructure/
├── database/
│   ├── migrations/
│   └── seeds/
└── tests/
    ├── Nexo.UnitTests/
    └── Nexo.IntegrationTests/
```

Un solo repositorio Git, con frontend y backend como aplicaciones separadas. En desarrollo, ejecutar React desde VS Code, la API desde el IDE compatible con .NET 10 y MySQL en Docker. Configurar la comunicación por API REST (HTTP/JSON), CORS restringido y HTTPS en los entornos que lo requieran.

## 5\. Arquitectura y responsabilidades

* **Nexo.Api:** endpoints, autenticación/autorización, middleware, configuración HTTP y OpenAPI.
* **Nexo.Application:** casos de uso, DTO, validaciones y orquestación de transacciones.
* **Nexo.Domain:** entidades, contratos y reglas de negocio.
* **Nexo.Infrastructure:** repositorios Dapper, conexiones MySQL, acceso a archivos, exportaciones y respaldos.
* **nexo-web:** vistas, formularios, componentes, rutas, estado de interfaz y cliente HTTP tipado.
* **database:** scripts SQL versionados y datos de prueba no sensibles.

Reglas: el frontend no se conecta directamente a MySQL; los controladores no contienen SQL; no colocar reglas críticas solo en React; evitar sobreingeniería.

## 6\. Roles mínimos

* **Administrador:** administración general, usuarios, permisos, productos, inventario, reportes, configuración y respaldos.
* **Vendedor/Cajero:** apertura y cierre de su turno, ventas, consulta de productos y operaciones autorizadas de caja.
* **Consulta:** acceso de solo lectura a información y reportes permitidos.

El backend debe aplicar los permisos en cada endpoint relevante. No confiar en que ocultar botones sea suficiente.

## 7\. Funcionalidades requeridas

1. Inicio y cierre de sesión.
2. Administración de usuarios.
3. Administración de roles y permisos.
4. CRUD de productos.
5. CRUD de categorías.
6. CRUD de proveedores.
7. Entradas de inventario.
8. Salidas y ajustes de inventario.
9. Alertas de stock bajo.
10. Gestión de cajas registradoras.
11. Apertura y cierre de turnos de caja.
12. Registro de ventas y detalle de venta.
13. Cálculo de subtotales, descuentos, impuestos y totales según reglas aprobadas.
14. Generación de comprobantes/facturas imprimibles.
15. Registro de pagos, con efectivo como mínimo; otros medios solo si son simulados o están claramente delimitados.
16. Anulaciones y devoluciones con autorización y trazabilidad.
17. Movimientos de efectivo de caja.
18. Arqueo y conciliación de caja.
19. Historial y consulta de ventas.
20. Registro de auditoría de acciones críticas.
21. Dashboard con al menos tres gráficos basados en datos reales.
22. Al menos ocho reportes, incluidos reportes parametrizados y maestro-detalle.
23. Exportación a Excel.
24. Respaldo y restauración de MySQL desde una interfaz administrativa protegida.

Documentar al menos 15 requisitos funcionales y los requisitos no funcionales. La base de datos debe contener al menos 10 tablas relacionadas.

## 8\. Reglas de negocio esenciales

* Una caja no puede tener dos turnos abiertos simultáneamente.
* Cada venta debe vincularse a usuario, caja y turno.
* El servidor calcula y valida precios, impuestos, descuentos y totales; no confía en los cálculos del navegador.
* La confirmación de una venta debe ser **atómica**: cabecera, detalle, pagos y movimientos de inventario se confirman o revierten juntos.
* Evitar ventas duplicadas mediante un identificador único de operación y reglas de idempotencia.
* No permitir existencias negativas salvo autorización explícita de una regla de negocio documentada.
* Proteger la actualización concurrente del inventario cuando venden varias cajas.
* No eliminar físicamente ventas históricas; registrar anulaciones y devoluciones como operaciones trazables.
* Utilizar tipos decimales apropiados para importes monetarios.
* Definir con el tutor el alcance fiscal y el formato del comprobante antes de declararlo factura legal.

## 9\. Seguridad

* Almacenar únicamente hashes seguros de contraseñas, nunca texto plano.
* Usar consultas SQL parametrizadas con Dapper.
* Validar entrada de datos en el backend.
* Configurar correctamente firma, emisor, audiencia y expiración de JWT.
* Utilizar access tokens de duración limitada y refresh tokens revocables y protegidos.
* Evitar almacenar tokens sensibles en `localStorage`; definir una estrategia segura para cookies y protección CSRF cuando corresponda.
* No incluir secretos en código, commits ni archivos de ejemplo.
* Registrar auditoría de cambios críticos sin exponer contraseñas o tokens.
* Limitar respaldos y restauraciones a usuarios autorizados, con validaciones y confirmación explícita.
* Utilizar HTTPS para las comunicaciones con credenciales, incluso en la LAN, mediante certificados confiables.

## 10\. Modelo de datos preliminar

Diseñar y revisar un modelo relacional que contemple, al menos: `usuarios`, `roles`, `permisos`, `usuario\_roles`, `rol\_permisos`, `categorias`, `productos`, `proveedores`, `movimientos\_inventario`, `cajas`, `turnos\_caja`, `ventas`, `detalle\_ventas`, `pagos`, `facturas` y `auditoria`.

Agregar tablas justificadas para devoluciones, movimientos de caja, compras u otras necesidades aprobadas. Definir PK, FK, índices, restricciones, tipos de datos, unicidad y política de eliminación. Mantener scripts SQL de migración versionados. No utilizar migraciones de Entity Framework.

## 11\. Reportes y dashboard

Reportes mínimos propuestos:

1. Inventario actual.
2. Movimientos de inventario por período.
3. Ventas por rango de fechas.
4. Ventas con detalle de productos (maestro-detalle).
5. Ventas por cajero y turno.
6. Cierres y diferencias de caja.
7. Ventas por método de pago.
8. Productos más vendidos.

El dashboard debe incluir al menos tres gráficos con consultas reales a MySQL. Implementar filtros, validación de parámetros y exportación a Excel donde corresponda.

## 12\. Docker y operación local

* Usar Docker Compose para MySQL durante el desarrollo; posteriormente se puede incluir API y frontend en contenedores.
* Persistir los datos de MySQL en un volumen.
* Configurar variables de entorno y un `.env.example` sin contraseñas reales.
* No exponer MySQL a redes externas sin necesidad; restringir puertos y accesos.
* Probar respaldos y restauraciones; el volumen Docker **no sustituye** un backup.
* Documentar requisitos de energía y LAN para el servidor de la tienda.

## 13\. Fases de implementación

### Fase 1 — Análisis y arquitectura

Entregables: requisitos funcionales/no funcionales, diagramas, estructura del repositorio, diseño de API y plan de pruebas. **No implementar módulos antes de aprobar el diseño.**

### Fase 2 — Base de datos y Docker

Entregables: diagrama entidad-relación, migraciones SQL, datos de prueba y contenedor MySQL verificado.

### Fase 3 — Backend base

Entregables: solución ASP.NET Core 10, separación por capas, Dapper, conexión MySQL, manejo de errores, OpenAPI y pruebas iniciales.

### Fase 4 — Seguridad y usuarios

Entregables: login, JWT, refresh tokens, gestión de usuarios, roles, permisos y pruebas de autorización.

### Fase 5 — Frontend base

Entregables: React/TypeScript, diseño general, rutas protegidas, formularios, cliente API y gestión de sesión.

### Fase 6 — Catálogo e inventario

Entregables: productos, categorías, proveedores, movimientos, validaciones y stock bajo.

### Fase 7 — Punto de venta y caja

Entregables: cajas, turnos, ventas, pagos, comprobantes, anulaciones y arqueos; pruebas de concurrencia y transacciones.

### Fase 8 — Reportes y dashboard

Entregables: ocho reportes, tres gráficos y exportación a Excel.

### Fase 9 — Respaldos y auditoría

Entregables: backup/restore protegido, registro de auditoría y prueba de recuperación.

### Fase 10 — Integración y defensa

Entregables: pruebas completas, corrección de errores, despliegue local, manual técnico, manual de usuario y guion de demostración.

Cada fase debe probarse antes de continuar. No desarrollar todas las fases en una sola solicitud.

## 14\. Instrucciones permanentes para Codex

Antes de modificar código:

1. Leer este archivo y la documentación existente.
2. Explicar qué se implementará y por qué.
3. Identificar los archivos afectados y dependencias.
4. Señalar decisiones que requieren aprobación.

Durante el desarrollo:

1. Trabajar en cambios pequeños y verificables.
2. Mantener código legible y nombres consistentes.
3. Escribir pruebas pertinentes y ejecutar las que estén disponibles.
4. No agregar bibliotecas o módulos ajenos al alcance sin autorización.
5. No modificar otras funcionalidades sin una razón documentada.

Al terminar:

1. Resumir cambios y archivos modificados.
2. Indicar cómo ejecutar y probar.
3. Diferenciar pruebas ejecutadas de pruebas pendientes.
4. Informar errores, riesgos y limitaciones.
5. Explicar el flujo técnico para que el estudiante pueda defenderlo.

**No declarar una funcionalidad terminada sin verificación.**





**15. Documentación técnica en Obsidian**



**El proyecto NEXO utiliza Obsidian para organizar su documentación técnica y académica.**



**Toda la documentación deberá almacenarse en la carpeta `docs/`, utilizando archivos Markdown (.md).**



**Por cada etapa de desarrollo, Codex deberá:**



**1. Crear una nota que explique los objetivos de la etapa.**

**2. Documentar las tecnologías utilizadas.**

**3. Explicar cómo funciona el código implementado.**

**4. Describir las clases, métodos, endpoints y tablas involucradas.**

**5. Registrar las decisiones técnicas y sus justificaciones.**

**6. Documentar los errores encontrados y cómo se solucionaron.**

**7. Incluir ejemplos prácticos y diagramas Mermaid cuando sean útiles.**

**8. Preparar posibles preguntas del jurado con respuestas.**

**9. Explicar los conceptos técnicos con lenguaje sencillo.**

**10. Relacionar notas mediante enlaces internos de Obsidian, por ejemplo \[\[Autenticación JWT]].**



**La documentación debe estar redactada en español y corresponder al funcionamiento real del proyecto.**



**No inventar pruebas, resultados ni funcionalidades que todavía no se hayan implementado.**


## 16. Autoría de commits solicitada por el usuario

El usuario solicita que los commits realizados para este proyecto usen siempre su identidad Git configurada: nombre `Jose`, correo `joserochaxd1@gmail.com`, tanto para autor como para committer. Aplicar esta identidad también al preparar trabajo en entornos cloud. Esto se refiere a la identidad del commit; la firma criptográfica GPG/SSH requiere una clave configurada y no debe declararse realizada sin verificación. No publicar secretos ni configuración privada local.
