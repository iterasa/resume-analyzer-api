# 0016 — API cerrada por defecto: toda ruta exige sesión

**Estado:** aceptada (21 nov 2026).

## Contexto

La API no tenía autenticación: cualquiera podía listar los usuarios con su correo (ya
descifrado), crear usuarios y dar de baja a cualquiera. El cifrado de campos (ADR 0015)
protege la base, no lo que la API entrega. El inicio de sesión con cookie todavía no existe.

## Decisión

- **Deny by default.** Una política de autorización por defecto (`FallbackPolicy`) exige un
  usuario autenticado en **toda** ruta. Solo son anónimas, de forma explícita, las sondas de
  salud y el documento OpenAPI, que solo se publica con `SWAGGER_ENABLED`.
- **Esquema provisional.** Mientras no haya inicio de sesión, el esquema
  `PendingSession` no autentica a nadie, así que toda ruta protegida responde 401 en
  formato Problem Details. El inicio de sesión con cookie lo sustituirá; la política no
  cambia.
- **Prueba de arquitectura.** Ningún controller puede llevar `[AllowAnonymous]`: abrir una
  ruta es una decisión de seguridad que se ve en la revisión.
- **Sin sesión, una ruta inexistente de la API también da 401.** Quien no inició sesión no
  puede averiguar qué rutas existen.

## Alternativas descartadas

- **`[Authorize]` en cada controller.** Un controller nuevo sin el atributo quedaría abierto.

## Consecuencias

- **Uso de la API:** hasta que exista el inicio de sesión, la API no se puede usar fuera de
  las pruebas. Es intencional: es preferible cerrada a abierta.
- **Pruebas:** se autentican con un esquema de prueba (cabecera `X-Test-User`). El desafío
  sigue siendo el de producción.
- **Pendiente:**
  - con el inicio de sesión, la protección antiforgery (CSRF) en cada POST, PUT, PATCH y
    DELETE, que el frontend ya envía (`X-XSRF-TOKEN`);
  - autorizar por permiso (por ejemplo, que solo un administrador gestione usuarios), no
    solo por estar autenticado.
