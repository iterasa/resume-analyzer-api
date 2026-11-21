# 0019 — Caché local sin datos de autorización y límites de peticiones por política

**Estado:** aceptada (21 nov 2026). Viene de la rama `full` de la plantilla, ajustada a Criba.

## Contexto

Algunas lecturas se repiten en cada pantalla (la empresa en `GET /me`, los responsables
posibles de una vacante). Y una API con inicio de sesión necesita acotar la fuerza bruta.

## Decisión

### Caché

- `ICacheService` sobre `HybridCache` (Microsoft), hoy solo en memoria del proceso. Evita la
  estampida: una sola fábrica por clave a la vez.
- **Nunca se cachea lo que decide un acceso**: sesión, rol y membresía se leen de la base en cada
  petición ([ADR 0017](0017-sesiones-en-el-servidor-y-antiforgery.md)).
- Se cachea: resumen de la empresa (5 min) y responsables posibles (60 s). Claves con la empresa.
- Una entrada con datos personales ya descifrados se marca `ContainsPersonalData`: si mañana se
  añade una caché distribuida (Redis), esa entrada nunca sale del proceso.
- La invalidación solo alcanza a la instancia que la hace; las demás esperan a que caduque. Por
  eso las vigencias son cortas.

### Límites de peticiones

- Limitador de ASP.NET Core, ventana fija. Cada controller declara su política (lo vigila una
  prueba de arquitectura):
  - `per-client`: 100 peticiones por minuto y usuario (por IP si es anónima);
  - `auth`: 10 por minuto e IP en inicio de sesión y registro.
- Rechazo: 429 Problem Details con `Retry-After` (expuesta por CORS).

## Alternativas descartadas

- **Output caching de respuestas**: con sesión y datos por empresa, cachear respuestas HTTP es
  una fuga esperando a ocurrir.
- **Limitador distribuido**: con varias instancias cada una cuenta lo suyo; se acepta hasta que
  haya varias réplicas o abuso real.

## Consecuencias

- Las vigencias de la caché van en código junto a cada clave (`CacheKeys`): son parte de la
  decisión de qué se cachea. Los límites se ajustan con `RATE_LIMIT_*`.
- Tras un proxy, la IP del cliente solo es real con `FORWARDED_HEADERS_*` configurado; si no,
  todos comparten la cuota `auth` del proxy.
