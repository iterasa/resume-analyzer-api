# 0003 — `Result` en casos de uso, `DomainException` en invariantes, Problem Details con catálogo

**Estado:** aceptada.

## Contexto

Había tres formas de 400 (validación del contrato, `Error.Validation`, `DomainException`),
`title` hacía de código máquina y los controllers acababan en `result.Value!`.

## Decisión

- Un fallo esperable (no encontrado, conflicto, regla incumplida) viaja como `Result` /
  `Error` con `Code`, `Message`, `ErrorKind` y, opcionalmente, mensajes por campo.
- Una invariante del dominio lanza `DomainException`; el manejador global la traduce.
  Es un error del llamante (400), no un fallo del servidor.
- Toda respuesta de error es un Problem Details (RFC 9457) con `type =
  urn:problem-type:<code>` como identificador estable, `title` por tipo, `detail` por
  ocurrencia y `errors` por campo en validación. Un único hook (`CustomizeProblemDetails`)
  normaliza también los errores del framework (401, 403, 404, 405).
- El controller resuelve con `result.Match(Ok, this.ToProblem)`.

## Alternativas descartadas

- Excepciones para todo fallo esperable: flujo de control por excepción y 500 por defecto.
- `Result` también en el dominio: los agregados dejarían de garantizar sus invariantes.
- `type` como URL: la plantilla no tiene dominio; el prefijo URN es una constante que cada
  proyecto puede sustituir por una URL documentada.

## Consecuencias

- Los controllers no llevan `[Produces]`: ese atributo pisa el `application/problem+json`.
  El formateador se acota una vez y una convención documenta el tipo de error en OpenAPI.
- Un cliente compara `type`, nunca `title` ni `detail`.
