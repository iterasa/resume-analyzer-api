# Resume Analyzer API

API de Resume Analyzer en **.NET 10**, con arquitectura limpia, buenas prácticas y configuración lista para producción. Parte de la plantilla `dotnet-api-template`.

---

## 🎯 Objetivo

Proveer una base consistente y mantenible para desarrollo de APIs backend, reduciendo deuda técnica desde el inicio.

---

## 🧱 Stack Tecnológico

| Categoría        | Tecnología                         |
|-----------------|-----------------------------------|
| Runtime         | .NET 10                           |
| API             | ASP.NET Core Web API              |
| ORM             | Entity Framework Core             |
| Base de datos   | PostgreSQL (Npgsql)               |
| Documentación   | OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`) + Swagger UI |
| Versionado      | URL Path Versioning               |
| Logging         | Serilog (structured logging JSON) |
| Testing         | xUnit + FluentAssertions          |
| Arquitectura    | NetArchTest                       |
| Contenedores    | Docker (multi-stage build)        |

---

## 🧠 Principios Aplicados

| Principio                    | Descripción |
|----------------------------|------------|
| Clean Architecture          | Separación clara por capas |
| CQRS (ligero)              | Separación de commands y queries |
| Result Pattern              | Manejo explícito de errores |
| Value Objects               | Modelado rico del dominio |
| Dependency Injection        | Bajo acoplamiento |
| Fail Fast                   | Validación temprana |
| Explicit over magic         | Código claro y predecible |

---

## 📁 Estructura del Proyecto

```
src/
  Iterasa.ResumeAnalyzer.Api
  Iterasa.ResumeAnalyzer.Application
  Iterasa.ResumeAnalyzer.Domain
  Iterasa.ResumeAnalyzer.Infrastructure
  Iterasa.ResumeAnalyzer.Shared

tests/
  Iterasa.ResumeAnalyzer.UnitTests
  Iterasa.ResumeAnalyzer.IntegrationTests
  Iterasa.ResumeAnalyzer.ArchitectureTests
```

## 🧭 Cómo está organizado el código

```
Api            controllers, contratos HTTP, Problem Details, OpenAPI, CORS, logging
Application    casos de uso (un handler por operación), puertos (Abstractions/), auditoría
Domain         agregados, entidades, value objects, invariantes. Sin dependencias
Infrastructure EF Core, health checks. Implementa los puertos
Shared         Result/Error, DomainException, paginación, lectura de variables
```

La regla de dependencia la vigilan los tests de arquitectura: `Api` no puede ver `Domain`
(sus tipos son `internal`), `Application` no puede ver `Infrastructure` ni `Api`, y
`Domain` no ve a nadie. La composición ocurre solo en `Program.cs` con tres extensiones:
`AddApi()`, `AddApplication()`, `AddInfrastructure()`.

Vigilan también las convenciones que este documento promete y que un refactor con prisa
rompería sin que ningún test funcional lo notara:

| Regla | Cómo se comprueba |
|------|-------------------|
| `Domain` y `Application` no leen el reloj (`DateTime*.UtcNow/Now/Today`) | Inspección del IL, incluidos los tipos anidados donde el compilador deja lambdas y máquinas de estado `async` |
| Los handlers son `internal sealed` e implementan su propia interfaz `I{Nombre}` | NetArchTest |
| Los controllers solo hablan con casos de uso: nada de `Application.Abstractions` ni `Infrastructure` | NetArchTest |
| `Domain` no depende de `Microsoft.*`; `Application` no conoce EF Core ni ASP.NET; `Shared` es hoja | NetArchTest |

### Cómo añadir un caso de uso

Ejemplo: «renombrar un usuario». Cuatro ficheros y un registro.

1. **Dominio** — la regla vive en el agregado: se añade a `User` un verbo
   `Rename(username, actorId, now)` que lance `DomainException` cuando se incumpla una
   invariante. Todo verbo que deje marca temporal recibe el instante: el dominio no
   consulta el reloj.

2. **Aplicación** — carpeta `Features/Users/Commands/RenameUser/` con:
   - `RenameUserCommand.cs` — `record RenameUserCommand(Guid UserId, string Username)`.
   - `IRenameUserHandler.cs` — `Task<Result> Handle(RenameUserCommand, CancellationToken)`.
   - `RenameUserHandler.cs` — carga con `IUserRepository.FindForUpdateAsync`, toma el
     instante una vez (`clock.GetUtcNow()`, con `TimeProvider` inyectado), invoca el verbo
     del agregado, confirma con `IUnitOfWork.SaveChangesAsync`, y registra con
     `AuditLog.Succeeded` / `Rejected`. Devuelve `Result.Failure(Error.NotFound(...))`
     para los fallos esperables; las invariantes las lanza el dominio y las traduce la API.
   - Registro en `Application/DependencyInjection.cs`.

3. **API** — acción en `UsersController` con `return result.Match(NoContent, this.ToProblem);`.

4. **Test** — uno de integración que llame al endpoint y compruebe el resultado y los
   errores esperables.

Lo que **no** hay que hacer: consultar desde un handler con `IUserQueries` para luego
modificar (las queries devuelven proyecciones o agregados sin rastrear); poner `try/catch`
de `DomainException` en el handler (lo hace el manejador global); ni devolver el agregado
desde un controller (solo DTOs).

### Cómo añadir una consulta

Igual, en `Queries/`. El puerto de lectura (`IXQueries`) devuelve un DTO proyectado con
`Select` en la propia consulta, nunca la entidad, y los listados reciben `PageQuery` y
devuelven `PagedResult<T>`.

---

## ⚙️ Configuración

La API se configura con **variables de entorno (.env)** en lugar de `appsettings.json`.

### Variables principales

| Variable                        | Por defecto | Descripción |
|--------------------------------|------------|------------|
| ASPNETCORE_URLS                | —          | URL y puerto de la API |
| HTTP_MAX_REQUEST_BODY_BYTES    | 1048576    | Tamaño máximo del cuerpo de una petición. Por encima, `413` |
| DATABASE_CONNECTION_STRING     | obligatoria | Cadena de conexión a PostgreSQL |
| LOG_LEVEL                      | Information | Nivel mínimo de log |
| LOG_FORMAT                     | json       | `json` (estructurado) o `text` (desarrollo) |
| SWAGGER_ENABLED                | true (`false` en la imagen Docker) | Expone el documento OpenAPI (`/openapi/v1.json`) y Swagger UI (`/swagger`) |
| API_TITLE                      | nombre de la app | Título publicado en OpenAPI |
| API_DESCRIPTION                | genérica   | Descripción publicada en OpenAPI |
| CORS_ENABLED                   | false      | Activa CORS. Solo con un cliente de navegador en otro origen |
| CORS_ALLOWED_ORIGINS           | —          | Obligatoria si CORS está activo. Orígenes separados por comas; admite `https://*.dominio.com`; nunca `*` |
| CORS_ALLOW_CREDENTIALS         | false      | Cookies / autenticación HTTP entre orígenes. El Bearer en `Authorization` no lo necesita |
| CORS_PREFLIGHT_MAX_AGE_SECONDS | 600        | Segundos que el navegador cachea un preflight |
| FORWARDED_HEADERS_ENABLED      | false      | Procesa `X-Forwarded-For` / `X-Forwarded-Proto` |
| FORWARDED_HEADERS_KNOWN_PROXIES | —         | IPs de proxys de confianza, separadas por comas |
| FORWARDED_HEADERS_KNOWN_NETWORKS | —        | Redes CIDR de confianza, separadas por comas |

La configuración se valida al arrancar el host (`ValidateOnStart`): con variables
ausentes o inválidas el proceso falla antes de aceptar tráfico. Las variables booleanas
aceptan `true/false`, `1/0`, `yes/no` u `on/off`; cualquier otro valor (una errata, o
unas comillas que `docker run --env-file` conserva) detiene el arranque.

---

### Ejemplo `.env`

Ver [`.env.example`](.env.example), que incluye todas las variables con valores de referencia.

---

### Carga de variables

| Entorno     | Configuración |
|------------|--------------|
| VS Code     | `envFile` en `launch.json` |
| Docker      | `--env-file .env` |
| Compose     | `env_file:` |
| Shell       | `export $(grep -v '^#' .env \| xargs)` |

> El connection string contiene `;`. Nunca cargues el `.env` con `source .env`: bash
> partiría el valor en varios comandos. Usa `--env-file`, `envFile` o el `export` de arriba.

---

### Uso en código

```csharp
Environment.GetEnvironmentVariable("VARIABLE_NAME");
```

Helper incluido:

```
Shared/Configuration/EnvironmentVariables.cs
Shared/Configuration/EnvironmentVariableReader.cs
```

Toda opción se lee dentro de un delegado `Configure` y se valida con `ValidateOnStart`,
de modo que un valor ausente o inválido detiene el arranque en lugar de fallar en la
primera petición.

---

## ⚠️ Errores

Toda respuesta de error es un **Problem Details** (RFC 9457) con
`Content-Type: application/problem+json`, la construya quien la construya: un controller,
el manejador global de excepciones o el propio framework (ruta inexistente, método no
admitido).

| Miembro | Qué es | Ejemplo |
|--------|--------|---------|
| `type` | Identificador **estable** del tipo de error. Es lo que un cliente compara | `urn:problem-type:not_found`, `urn:problem-type:conflict` |
| `title` | Resumen legible del tipo. No cambia entre ocurrencias | `Not Found` |
| `status` | Código HTTP | `404` |
| `detail` | Explicación de esta ocurrencia | `User not found.` |
| `errors` | Solo en validación: mensajes por campo. La clave vacía agrupa los que no señalan un campo | `{"Email": ["The Email field is required."]}` |
| `correlationId`, `traceId` | Para localizar la petición en el log | — |

Un error de validación tiene **siempre la misma forma**, venga de donde venga: un campo
obligatorio ausente (validación del contrato HTTP), una regla del caso de uso
(`Error.Validation(...)`) o una invariante del dominio (`DomainException`). Los tres salen
como `type: urn:problem-type:validation_error` con `errors`.

En el código, el caso de uso devuelve `Result` / `Result<T>` con un `Error` que lleva un
`Code` (el sufijo del `type`), un `Message` (el `detail`) y un `ErrorKind` (del que la capa
de API deduce el código HTTP). `Error.Validation("field", "message")` señala un campo. El
controller resuelve el resultado con `result.Match(Ok, this.ToProblem)`.

El prefijo `urn:problem-type:` es una constante (`ProblemTypes.Prefix`); cada proyecto
puede sustituirlo por una URL que documente sus errores.

---

## 📝 Logging

| Aspecto      | Descripción |
|-------------|------------|
| Motor       | Serilog |
| Formato     | JSON compacto (`LOG_FORMAT=json`) o texto legible (`text`) |
| Por petición | Un evento con método, ruta, estado, duración, usuario (si la petición está autenticada), IP y versión de API |
| Correlación | Cabecera `X-Correlation-Id`, aceptada y devuelta; presente en todos los eventos y en cada Problem Details |
| Auditoría | Un evento por operación de negocio (`AuditAction`, `AuditTarget`, `ActorId`, `AuditOutcome`) |

---

## 🩺 Health Checks

| Endpoint        | Comprueba | Uso |
|----------------|-----------|-----|
| `/health`      | Proceso vivo | Sonda general |
| `/health/live` | Proceso vivo | Liveness probe |
| `/health/ready`| Conectividad con PostgreSQL | Readiness probe |

Las sondas responden JSON y son anónimas.
La liveness no depende de la base de datos: un fallo de PostgreSQL no debe provocar
el reinicio del contenedor, solo sacar la instancia del balanceo hasta que se recupere.

La respuesta se limita al estado agregado (`{"status":"Healthy"}`); qué dependencia
falló y por qué solo viaja al log estructurado.

---

## 🔀 Versionado de API

| Estrategia | Ejemplo |
|-----------|--------|
| URL Path  | `/api/v1/users` |

Un documento OpenAPI por versión, en `/openapi/{v}.json`, generado por ASP.NET Core
(`Microsoft.AspNetCore.OpenApi`) con la integración de `Asp.Versioning.OpenApi`, a partir
de los controllers, los atributos `[ProducesResponseType]` y los comentarios XML de `Api`,
`Application` y `Shared`. Las versiones salen de los atributos `[ApiVersion]`: un
controller con una versión nueva hace aparecer su documento sin más configuración. Swagger
UI (`/swagger`) es solo la interfaz; cualquier otra que lea el documento sirve igual.

| Endpoint | Descripción |
|---------|-------------|
| `GET /users`, `GET /users/{id}` | Listado paginado y detalle |
| `POST /users` | Alta |
| `DELETE /users/{id}`, `POST /users/{id}/activation` | Baja y alta lógica |

Los listados aceptan `?page=` y `?pageSize=` y devuelven `items`, `page`, `pageSize`,
`totalCount`, `totalPages` y `hasNextPage`. Los valores fuera de rango se ajustan en
lugar de rechazarse: `pageSize` entre 1 y 200, `page` entre 1 y el máximo con el que el
desplazamiento cabe en un entero.

---

## 🗄️ Migraciones

Viven en `src/Iterasa.ResumeAnalyzer.Infrastructure/Migrations`. La capa de API no referencia EF
Core Tools: el contexto de diseño lo construye `AppDbContextFactory`, que solo necesita
`DATABASE_CONNECTION_STRING`. La herramienta `dotnet-ef` está fijada en
`.config/dotnet-tools.json`, a la misma versión que el runtime de EF: `dotnet tool restore`
la deja lista sin instalar nada global.

```bash
dotnet tool restore
export DATABASE_CONNECTION_STRING='Host=localhost;Port=5432;Database=...;Username=...;Password=...'
dotnet ef migrations add <Nombre> --project src/Iterasa.ResumeAnalyzer.Infrastructure --startup-project src/Iterasa.ResumeAnalyzer.Infrastructure
dotnet ef database update --project src/Iterasa.ResumeAnalyzer.Infrastructure --startup-project src/Iterasa.ResumeAnalyzer.Infrastructure
```

La concurrencia optimista usa `xmin`, columna de sistema de PostgreSQL: si al generar
una migración EF incluye `xmin` en un `CreateTable`, hay que retirarlo a mano, porque
crearla falla.

---

## 🐳 Docker

```bash
docker build -t api .
docker run --env-file .env -p 8080:8080 api
```

El proceso corre como el usuario `app` (uid 1654) de la imagen base, nunca como root, y la
imagen declara un `HEALTHCHECK` contra `/health/live` para Docker, Compose y ECS
(Kubernetes lo ignora y usa sus propias probes). El SDK que compila lo fija `global.json`
(`10.0.x`, cualquier banda de características): la misma versión en cada máquina y en el CI.

La imagen arranca con `SWAGGER_ENABLED=false`: lo que se despliega no publica la
documentación salvo que el entorno la pida. El `.env` de ejemplo la activa, así que
`docker run --env-file .env` la sigue mostrando en local.

El Dockerfile no fija arquitectura: la misma receta produce imágenes `amd64` y `arm64`.

```bash
docker buildx build --platform linux/amd64,linux/arm64 -t registry/api:1.2.3 --push .
```

---

## 🧪 Testing

| Tipo               | Descripción |
|-------------------|------------|
| Unit Tests         | Dominio y lógica pura |
| Integration Tests  | API real con WebApplicationFactory sobre SQLite en memoria; no necesita Docker |
| Architecture Tests | Dependencias entre capas y convenciones (reloj, handlers, controllers) |

### Hooks de pre-commit

`.githooks/pre-commit` pasa **gitleaks** sobre lo que entra en el commit y `dotnet format
--verify-no-changes` sobre los `.cs` modificados. No necesita más que bash, gitleaks y el
SDK, y se activa una vez por clon (ver [Puesta en marcha](#-puesta-en-marcha)). Es una red local: las mismas dos comprobaciones
corren en el CI, que es lo que bloquea de verdad. `git commit --no-verify` lo salta a
sabiendas.

### CI y dependencias

`.github/workflows/ci.yml` corre en cada PR y en cada push a `main`. Pasa gitleaks sobre todo
el historial, con el binario verificado por SHA-256, y después `dotnet format
--verify-no-changes`, la compilación y las pruebas. Las acciones van fijadas por SHA, y
`.github/dependabot.yml` propone cada semana las subidas de NuGet, de las imágenes base y de
las acciones. Las versiones mayores de NuGet y de Docker quedan fuera, porque van con el
`TargetFramework` y se suben a mano.

---

## 🚀 Puesta en marcha

### 1. Activar los hooks de pre-commit

```bash
git config core.hooksPath .githooks
```

### 2. Configurar

```bash
cp .env.example .env
```

### 3. Ejecutar

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/Iterasa.ResumeAnalyzer.Api
```

---

## 📌 Convenciones Clave

| Regla | Descripción |
|------|------------|
| internal por defecto | Encapsulación estricta |
| public mínimo        | Solo contratos externos |
| XML Docs             | Obligatorios en públicos |
| Controllers          | Sin lógica de negocio |
| Handlers             | Un caso de uso por clase |

---

## 🛡️ Cabeceras y transporte

Toda respuesta lleva `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
`Content-Security-Policy: frame-ancestors 'none'`, `Referrer-Policy: no-referrer` y, si el
endpoint no dice otra cosa, `Cache-Control: no-store`. La cabecera `Server` no se emite.
El cuerpo de una petición se limita a `HTTP_MAX_REQUEST_BODY_BYTES`.

La API asume que **el TLS termina en un proxy inverso**: no redirige a HTTPS ni
emite `Strict-Transport-Security`; es el proxy quien debe hacerlo. Si la API se expone
directamente, añade ambas cosas en `Program.cs`.

### CORS

Desactivado por defecto: una API sin cliente de navegador no debe tener política alguna.
Con `CORS_ENABLED=true`:

| Aspecto | Política |
|--------|---------|
| Orígenes | Solo los de `CORS_ALLOWED_ORIGINS`, comparados literalmente: esquema, host y puerto. `https://*.dominio.com` admite cualquier subdominio, no el dominio raíz. `*` y `null` se rechazan |
| Validación | Al arrancar. Un origen mal escrito (sin esquema, con ruta, `*`) detiene el proceso con un mensaje que nombra la variable y el valor |
| Métodos | `GET POST PUT DELETE`: los que usa la API. Se amplían en `CorsPolicyOptions`, en un sitio |
| Cabeceras admitidas | `Authorization`, `Content-Type`, `X-Correlation-Id` |
| Cabeceras expuestas | `X-Correlation-Id` (para reportar un error), `Retry-After` (tras un 429), `Location` (tras un 201). Sin exponerlas, el script del navegador no puede leerlas |
| Credenciales | No, salvo `CORS_ALLOW_CREDENTIALS=true`. El token Bearer viaja en `Authorization` y **no** es una credencial CORS; solo las cookies lo son. Con comodín de subdominio y credenciales, el arranque avisa |
| Preflight | Respondido antes que el resto del pipeline; cacheable `CORS_PREFLIGHT_MAX_AGE_SECONDS` |
| Errores | Las cabeceras CORS acompañan también a las respuestas de error: sin ellas, el navegador oculta el Problem Details al script |

---

## ❗ Decisiones Intencionales

No incluye:

- Autenticación y autorización (rama `full`)
- Idempotencia, cache, rate limiting y OpenTelemetry (rama `full`)
- Multitenancy (rama `multitenant`)
- Cookies / sesiones
- CSRF
- HTTPS redirection / HSTS (responsabilidad del proxy que termina TLS)

---

## 🗒️ Decisiones de arquitectura

Las decisiones que no se deducen del código están en [`docs/adr`](docs/adr/README.md): una
por fichero, con el contexto, la alternativa descartada y lo que implica. Cambiar una es
escribir la siguiente que la sustituye, no editar la anterior.

---

## 📈 Próximas Extensiones

- Feature flags
- Background jobs

---

## 🧩 Filosofía

> Base sólida, no sobreingeniería.

---

## 📄 Licencia

Uso interno.