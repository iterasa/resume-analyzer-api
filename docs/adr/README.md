# Decisiones de arquitectura (ADR)

Una decisión por fichero, numerada y nunca editada una vez aceptada: para cambiarla se
escribe otra que la sustituye y se enlazan. Formato: contexto, decisión, alternativas
descartadas, consecuencias.

| Nº | Decisión | Estado |
|---|---|---|
| [0001](0001-capas-y-shared-kernel.md) | Cinco proyectos, dominio `internal`, `Shared` como núcleo compartido | Aceptada |
| [0002](0002-configuracion-por-variables-de-entorno.md) | Configuración solo por variables de entorno, validada al arrancar | Aceptada |
| [0003](0003-contrato-de-error.md) | `Result` en casos de uso, `DomainException` en invariantes, Problem Details con catálogo de tipos | Aceptada |
| [0004](0004-reloj-explicito.md) | El dominio no lee el reloj: `TimeProvider` en Application y `now` en cada verbo | Aceptada |
| [0006](0006-cors-lista-explicita.md) | CORS desactivado por defecto, lista explícita, sin credenciales | Aceptada |
| [0007](0007-idempotencia-transaccional.md) | `Idempotency-Key` por empresa y usuario, en una sola transacción | Aceptada |
| [0008](0008-openapi-nativo.md) | OpenAPI generado por ASP.NET Core; Swagger UI solo como interfaz | Aceptada |
| [0009](0009-fluentassertions-7.md) | FluentAssertions fijado en 7.x por licencia | Aceptada |
| [0010](0010-contenedor-sin-root-y-sdk-fijado.md) | Contenedor sin root con `HEALTHCHECK`; SDK fijado con `global.json` | Aceptada |
| [0013](0013-argon2id-por-defecto.md) | Argon2id por defecto, PBKDF2 para FIPS, rehash al iniciar sesión | Aceptada |
| [0014](0014-hooks-de-pre-commit.md) | Hooks de pre-commit sin dependencias: gitleaks y formato | Aceptada |
| [0015](0015-cifrado-de-campos.md) | Cifrado de campos en la aplicación: llave por empresa en AWS KMS e índices ciegos | Aceptada |
| [0016](0016-api-cerrada-por-defecto.md) | API cerrada por defecto: toda ruta exige sesión salvo las declaradas anónimas | Aceptada |
| [0017](0017-sesiones-en-el-servidor-y-antiforgery.md) | Sesiones opacas en el servidor, cookie `__Host-` y antiforgery | Aceptada |
| [0018](0018-multiempresa-y-permisos.md) | Multiempresa: empresa por petición, filtros obligatorios y permisos por rol | Aceptada |
| [0019](0019-cache-y-limites-de-peticiones.md) | Caché local sin datos de autorización; límites de peticiones por política | Aceptada |
