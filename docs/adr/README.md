# Decisiones de arquitectura (ADR)

Una decisión por fichero, numerada y nunca editada una vez aceptada: para cambiarla se
escribe otra que la sustituye y se enlazan. Formato: contexto, decisión, alternativas
descartadas, consecuencias.

<!-- plantilla:inicio -->
La numeración es común a las tres ramas de la plantilla (ver «Ramas» en el README): los
números que faltan aquí son decisiones de features que viven en `full` o `multitenant`.

<!-- plantilla:fin -->
| Nº | Decisión | Estado |
|---|---|---|
| [0001](0001-capas-y-shared-kernel.md) | Cinco proyectos, dominio `internal`, `Shared` como núcleo compartido | Aceptada |
| [0002](0002-configuracion-por-variables-de-entorno.md) | Configuración solo por variables de entorno, validada al arrancar | Aceptada |
| [0003](0003-contrato-de-error.md) | `Result` en casos de uso, `DomainException` en invariantes, Problem Details con catálogo de tipos | Aceptada |
| [0004](0004-reloj-explicito.md) | El dominio no lee el reloj: `TimeProvider` en Application y `now` en cada verbo | Aceptada |
| [0006](0006-cors-lista-explicita.md) | CORS desactivado por defecto, lista explícita, sin credenciales | Aceptada |
| [0008](0008-openapi-nativo.md) | OpenAPI generado por ASP.NET Core; Swagger UI solo como interfaz | Aceptada |
| [0009](0009-fluentassertions-7.md) | FluentAssertions fijado en 7.x por licencia | Aceptada |
| [0010](0010-contenedor-sin-root-y-sdk-fijado.md) | Contenedor sin root con `HEALTHCHECK`; SDK fijado con `global.json` | Aceptada |
| [0014](0014-hooks-de-pre-commit.md) | Hooks de pre-commit sin dependencias: gitleaks y formato | Aceptada |
