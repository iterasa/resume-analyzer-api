# 0017 — Sesiones opacas en el servidor, cookie `__Host-` y antiforgery

**Estado:** aceptada (21 nov 2026). Sustituye al esquema provisional `PendingSession` del
[ADR 0016](0016-api-cerrada-por-defecto.md); la política «cerrada por defecto» no cambia.

## Contexto

La web es una SPA del mismo origen (proxy inverso) que nunca debe tocar un token: la sesión va en
una cookie `HttpOnly`. Luis pidió la opción más segura, y una sesión tiene que poder revocarse
al instante (cerrar sesión, baja, cambio de contraseña). Ver `docs/decisiones.md` de la web.

## Decisión

### Sesión

- **Token opaco**: 256 bits de un CSPRNG en base64url. La base (`user_session`) guarda solo su
  SHA-256: quien lea la base no puede usar una sesión. Un hash rápido basta porque el token es
  aleatorio, no una contraseña.
- **Validación en cada petición, sin caché**: una consulta une sesión, membresía, usuario y
  empresa. Revocar, dar de baja o cambiar el rol vale desde la petición siguiente.
- **Vencimiento**: 30 min de inactividad (se desliza, escribiendo como mucho una vez por minuto)
  y 12 h absolutas desde el inicio de sesión (`SESSION_IDLE_MINUTES`, `SESSION_ABSOLUTE_HOURS`).
  «Recordarme» solo hace persistente la cookie dentro de esos límites.
- **Rotación**: sesión nueva al registrarse, al iniciar sesión y al cambiar de empresa; la
  anterior se revoca y la nueva hereda su vencimiento absoluto (cambiar de empresa no alarga).
- **Bloqueo**: 5 intentos fallidos bloquean 15 min (`AUTH_MAX_FAILED_SIGN_INS`,
  `AUTH_LOCKOUT_MINUTES`). El contador se actualiza de forma atómica en la base: una ráfaga de
  intentos simultáneos cuenta entera. Un único error `invalid_credentials` para todo motivo.
- **Revocación atómica**: de dos peticiones simultáneas sobre la misma sesión (cerrar y cambiar
  de empresa) solo una la revoca. Iniciar sesión o registrarse con otra sesión abierta la revoca.
- **Cookie**: `__Host-criba_session`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`. El prefijo
  `__Host-` impide que un subdominio la fije o la pise.
- **Límite por IP** (IPv6 por /64), aunque la petición traiga sesión, en inicio de sesión y registro (política `auth`, [ADR 0019](0019-cache-y-limites-de-peticiones.md)).

### CSRF

- Antiforgery de ASP.NET Core, validado en **todo** POST, PUT, PATCH y DELETE por un filtro
  global (cerrado por defecto, como la autenticación). Sin token: 400 `antiforgery_invalid`, que
  la web reconoce para pedir otro y reintentar una vez.
- `GET /api/v1/antiforgery/token` deja la cookie legible `XSRF-TOKEN`; la web la reenvía en
  `X-XSRF-TOKEN`. Su pareja `HttpOnly` es `__Host-criba_af`, `SameSite=Strict`.
- El token va ligado a la identidad: abrir o cerrar sesión emite otro en la misma respuesta.
- Las llaves de Data Protection que firman los tokens viven en `data_protection_key`, con el XML
  **cifrado** por la librería de cifrado de campos ([ADR 0015](0015-cifrado-de-campos.md)):
  todas las instancias validan lo mismo y quien lea la base no puede falsificar tokens.

### Rutas anónimas

Solo `GET /antiforgery/token`, `POST /auth/register` y `POST /auth/login`, más las sondas y el
OpenAPI. La prueba de arquitectura tiene la lista exacta: abrir otra se ve en la revisión.

## Alternativas descartadas

- **JWT en la cookie**: no se revoca sin una lista de revocación, que es una sesión con más pasos.
- **Cookie de ASP.NET con `ITicketStore`**: equivale, pero cifra el ticket con Data Protection y
  añade una pieza más; el token opaco es más simple de razonar y auditar.
- **Double-submit propio**: es reinventar el antiforgery; el de ASP.NET está probado.

## Consecuencias

- **Desarrollo**: por `http://localhost` el antiforgery se niega a emitir cookies `Secure`. En
  `Development` las cookies van sin prefijo (`criba_session`, `criba_af`) y `Secure` solo por
  HTTPS (`BrowserCookies`). En cualquier otro entorno, `__Host-` y `Secure` siempre.
- **Despliegue**: tras un proxy que termina TLS hay que activar `FORWARDED_HEADERS_ENABLED`; sin
  eso la API ve HTTP y no emite tokens (el arranque lo avisa).
- **Enumeración aceptada**: el registro confirma que un correo ya tiene cuenta (409). Sin envío
  de correo no hay respuesta neutra posible; el límite por IP lo acota. Se revisa con la
  verificación de correo.
- Data Protection avisa al crear una llave de que no tiene «XML encryptor»: es esperado, el
  cifrado lo hace la columna.
- **Pendiente**: verificación de correo, recuperación de contraseña, Google, invitaciones,
  revocar las demás sesiones al cambiar la contraseña y el hub de SignalR
  (`CloseOnAuthenticationExpiration`).
