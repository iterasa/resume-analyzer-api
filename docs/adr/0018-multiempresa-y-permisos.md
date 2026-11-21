# 0018 — Multiempresa: empresa por petición, filtros obligatorios y permisos por rol

**Estado:** aceptada (21 nov 2026). Resuelve el pendiente «contexto de empresa por petición» del
[ADR 0015](0015-cifrado-de-campos.md).

## Contexto

Criba guarda vacantes y candidatos de muchas empresas en la misma base. Una consulta que olvide
su `Where tenant_id = …` mostraría datos de otra empresa: el aislamiento no puede depender de
que cada consulta se acuerde.

## Decisión

### Empresa de la operación

- La sesión lleva la empresa activa (claim `tid`). Un middleware tras la autenticación la abre
  para el resto de la petición con `TenantCryptoScope.Enter`: el mismo ámbito que ya elegía las
  llaves de cifrado. Sin sesión no se abre ninguna.
- Fuera de una petición (registro de una empresa, jobs) se abre explícitamente con `ITenantScope`.

### Aislamiento en la base

- Toda entidad de empresa implementa `ITenantOwned` y el contexto le pone un **filtro de
  consulta** `tenant_id = empresa de la operación`. Sin empresa abierta no ve ninguna fila.
- **Escritura**: un interceptor (`TenantOwnershipGuard`) rechaza guardar una entidad de empresa
  que no sea de la empresa abierta, o sin empresa. Así un `TenantId` sacado de un cuerpo de
  petición no llega a la base.
- Las lecturas que cruzan empresas (las membresías de un usuario, la validación de su sesión) lo
  declaran con `IgnoreQueryFilters()` y su propio filtro explícito. Son pocas y se revisan.
- Una vacante de otra empresa responde 404, igual que una que no existe.
- Una prueba de arquitectura exige el filtro en toda entidad `ITenantOwned`.

### Roles y permisos

- Cuatro roles fijos (`Admin`, `Recruiter`, `HiringManager`, `ReadOnly`) y el catálogo de
  permisos que ya usa la web (`vacancies.manage`, `candidates.reveal`, …), en código
  (`Shared/Tenancy/RolePermissions`). El rol se lee de la base en cada petición con la sesión.
- Cada permiso es una política con su mismo nombre: `[Authorize(Policy = Permissions.VacanciesManage)]`.
  Cada 403 queda en el log con el permiso exigido y la empresa.
- Un rol sin `candidates.reveal` evalúa siempre de forma anónima: invariante de `Membership`.

### Caché

Toda clave lleva la empresa. Una fábrica de la caché puede ejecutarse fuera del contexto de la
petición, así que recibe la empresa explícita en lugar de leer el ámbito.

## Alternativas descartadas

- **Una base o un esquema por empresa**: aislamiento físico, pero migraciones y conexiones por
  empresa. Excesivo para el volumen previsto; las llaves por empresa ya separan los datos cifrados.
- **Row-Level Security de PostgreSQL**: segunda barrera válida, pero exige fijar una variable de
  sesión en cada conexión del pool. Se reconsidera si aparecen consultas SQL a mano.
- **Roles configurables por empresa**: la web no los pide; un catálogo fijo es más fácil de auditar.

## Consecuencias

- Un job que lea datos de empresa debe abrir el ámbito de la fila; si no, no ve nada y no puede
  escribir (falla cerrado).
- El usuario es de plataforma (puede estar en varias empresas): su nombre y correo van con las
  llaves de plataforma (ADR 0015, decisión 9).
- **Pendiente**: invitaciones y gestión del equipo, evaluación anónima configurable por un
  administrador, y la lista corta del gerente de contratación cuando existan las postulaciones.
