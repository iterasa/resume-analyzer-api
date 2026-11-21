# 0002 — Configuración solo por variables de entorno, validada al arrancar

**Estado:** aceptada.

## Contexto

Contenedores, orquestadores y gestores de secretos (Parameter Store, Key Vault, Vault)
entregan configuración como variables de entorno. `appsettings.json` por entorno acaba
con secretos en el repositorio y con dos fuentes de verdad.

## Decisión

Toda opción se lee de una variable de entorno con nombre estable (`EnvironmentVariables`),
dentro de un delegado `Configure`, y se valida con `ValidateOnStart`: una variable ausente
o inválida detiene el proceso antes de aceptar tráfico, con un mensaje que nombra la
variable y el valor.

## Alternativas descartadas

- `IConfiguration` con `appsettings.*.json`: más flexible, más difícil de auditar qué
  está activo en producción.

## Consecuencias

- `.env.example` es el catálogo completo; el README, su documentación.
- Un gestor de secretos se integra fuera del proceso (inyecta variables); la aplicación no
  conoce ninguno.
- Las pruebas no pueden aislar configuración por variables (son globales al proceso): las
  variantes se configuran con `PostConfigure` en el contenedor.
