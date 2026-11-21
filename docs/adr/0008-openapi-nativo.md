# 0008 — OpenAPI generado por ASP.NET Core; Swagger UI solo como interfaz

**Estado:** aceptada.

## Contexto

Swashbuckle 6.6 quedó atrás de .NET 10 y Microsoft lo retiró de las plantillas en favor
de `Microsoft.AspNetCore.OpenApi`. Además `[Produces]` en los controllers hacía que la
documentación y la realidad discreparan en el tipo de contenido de los errores.

## Decisión

- Documento por versión generado por ASP.NET Core con la integración de
  `Asp.Versioning.OpenApi`: las versiones salen de `[ApiVersion]`.
- Los filtros pasan a *transformers*: identidad y esquema Bearer, requisito de seguridad
  por operación, sondas de salud, cabecera de idempotencia.
- Comentarios XML de `Api` (los carga la integración), `Application` y `Shared` (se
  registran), más un *transformer* que resuelve por `MemberInfo` las propiedades cuyo
  nombre JSON no coincide con el C# (`access_token`).
- Swagger UI se mantiene como interfaz por familiaridad; es reemplazable por cualquiera
  que lea `/openapi/{v}.json`.

## Alternativas descartadas

- Swashbuckle 10: mantenido, pero fuera de la dirección de la plataforma.
- Scalar como interfaz: igual de válido; se pospone para no cambiar rutas ni variables.

## Consecuencias

- `SWAGGER_ENABLED` gobierna documento e interfaz. Desactivar en producción.
- Los tipos de contenido documentados coinciden con los servidos (2xx `application/json`,
  4xx/5xx `application/problem+json`).
