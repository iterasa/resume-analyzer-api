# 0015 — Cifrado de campos en la aplicación: llave por empresa en AWS KMS e índices ciegos

**Estado:** aceptada (21 nov 2026).

## Contexto

Criba guarda datos personales de candidatos de varias empresas: nombre, contacto, CV y
conversaciones de WhatsApp. El cifrado del disco y de las copias protege contra el robo
del soporte físico, pero no contra quien lee la base: un DBA, una credencial filtrada o
una inyección SQL. Además, el sistema es multiempresa, así que un error no debe mostrar
datos de una empresa a otra.

El esquema se diseñó con dos especificaciones internas, ajustadas a Criba. La segunda (llave
por empresa) es la base. De la primera se conservan el borrado de llaves de memoria y la
idea de filtrar primero por columnas en claro.

Al decidirlo, el backend solo tenía la tabla `user`, sin empresas, sin jobs, sin semillas
y sin datos cifrados.

### Modelo de amenazas

- **Protege contra:**
  - un volcado o una copia de seguridad robada;
  - la lectura directa de la base;
  - la fuga de datos entre empresas;
  - copiar un criptograma a otra columna u otra empresa.
- **No protege contra:**
  - un servidor de aplicación comprometido, que tiene las llaves en memoria;
  - un usuario legítimo que lee a través de la aplicación. Eso lo cubren la autorización y la
    auditoría.
- **No sustituye** el cifrado del disco ni el de las copias: es una capa más.

## Decisión

### Llaves

- **KEK (llave maestra):** una llave simétrica de **AWS KMS**, compartida por todas las
  empresas.
  - La aplicación solo le pide *encrypt* y *decrypt* de llaves: la KEK nunca sale de KMS.
  - El contexto de cifrado de KMS (`app`, `scope`, `purpose`, `version`) ata cada llave
    envuelta a su ámbito y queda registrado en CloudTrail.
  - Cada fila guarda qué KEK la envolvió (`kek_id`). Así se puede rotar la KEK, o pasar más
    adelante a una KEK por empresa, sin tocar los datos.
- **DEKs por ámbito:** cada empresa tiene dos llaves aleatorias e independientes de 256 bits,
  una de cifrado (`enc`) y otra de índice ciego (`idx`).
  - Se guardan envueltas en `encryption_key`, con versión y estado (`active`,
    `decrypt_only`, `retired`, `destroyed`).
  - Un índice único parcial impone una sola `active` por ámbito y propósito.
  - Las llaves de una empresa se crean en la misma transacción que la empresa
    (`EncryptionKeyProvisioner.ProvisionScopeAsync`). Una empresa sin llaves no puede cifrar.
- **Ámbito de plataforma:** los usuarios del equipo pueden pertenecer a varias empresas, así
  que sus datos van con llaves de plataforma. Esas llaves se crean solas la primera vez.
- **Caché en memoria:** los anillos de llaves desenvueltas duran 10 minutos, con un máximo de
  1024 ámbitos y una sola carga a la vez por ámbito. KMS solo se llama cuando falta la llave.
- **Desarrollo y pruebas:** una KEK local de 32 bytes en una variable de entorno, con la
  misma interfaz.
- **Producción:** con la KEK local, o sin KEK, la aplicación **no arranca**. No hay llave
  por defecto ni cifrador nulo.

### Algoritmo y formato

- **Cifrado:** AES-256-GCM de .NET, con nonce aleatorio de 96 bits y tag de 128.
- **Formato:** `enc1.<key_id>.<base64url(nonce ‖ criptograma ‖ tag)>`. Un esquema nuevo lleva
  un prefijo nuevo; nunca se reutiliza uno.
- **Datos asociados:** `enc1|ámbito|tabla|columna`. Un criptograma copiado a otra columna,
  tabla o empresa no descifra.
- **Al descifrar:**
  - la llave se busca solo en el anillo del ámbito esperado;
  - el tag tiene que validar;
  - si algo falla, se lanza `FieldDecryptionException`, se registra un evento de seguridad
    sin el valor (EventId 9001) y suma la métrica `field_encryption.decrypt_failures`.
  - Nunca se devuelve el criptograma ni se acepta texto en claro.
- **Límite de NIST SP 800-38D:** 2^32 cifrados por llave con nonce aleatorio. Con una llave por
  empresa y rotación periódica no se alcanza.
- **Columnas:** las cifradas son `text` y no tienen límite de longitud; el límite se valida
  sobre el texto, en el dominio.
- **`null`:** se guarda como `null`.

### Integración

- **Inventario:** los campos cifrados se declaran en un solo sitio, `EncryptedFields`, y la
  configuración de EF los marca con `.IsEncrypted(...)`.
- **Conversores:** un conversor de valor cifra y descifra, también en las proyecciones. El
  dominio no lo nota.
- **Interceptores:** antes de cada consulta y cada guardado cargan los anillos (los
  conversores de EF son síncronos y no pueden esperar a KMS). Al guardar calculan los índices
  ciegos.
- **Consultas prohibidas:** una consulta que filtra, ordena, agrupa o compara sobre un campo
  cifrado se rechaza al compilarla (`EncryptedFieldQueryGuard`).
- **Empresa de la operación:**
  - en una petición la fija el contexto de empresa;
  - en un job, el job abre el ámbito con la empresa de la fila
    (`TenantCryptoScope.Enter(tenantId)`);
  - sin ámbito, un campo de empresa falla; nunca se cifra con una llave global.
- **Migraciones:** no cifran. La de este ADR se detiene si `user` ya tiene filas, en lugar
  de dejar correos en claro donde se espera un criptograma.
- **Biblioteca única:** toda la criptografía vive en
  `Infrastructure/Security/FieldEncryption`. Una prueba de arquitectura impide usar AES,
  HMAC o KMS fuera de ahí.

### Búsqueda

- **Índice ciego:** HMAC-SHA256 con la llave `idx` del ámbito sobre
  `etiqueta_del_campo ‖ 0x00 ‖ valor_normalizado`. Se guarda en `<columna>_bidx` (32 bytes,
  sin truncar) junto con `<columna>_bidx_v` (la versión de la llave).
  - El índice de base de datos es `(tenant_id, bidx)` en las tablas de empresa y `(bidx)` en
    las de plataforma. Si el campo es único, la restricción va sobre él.
  - Al buscar se calcula un índice por cada llave vigente (`bidx IN (…)`), así que la búsqueda
    funciona también durante una rotación.
- **Normalización:** una por tipo de dato, versionada (`email.v1`, `text.v1`), y forma parte
  de la etiqueta. Cambiarla obliga a reindexar, igual que una rotación.
- **Paginación y orden:** siempre sobre columnas en claro.

### Decisiones tomadas

| # | Decisión | Elegido |
| --- | --- | --- |
| 1 | KMS y alcance de la KEK | AWS KMS, KEK compartida. Las copias de seguridad deben caducar en 35 días o menos (ver «Borrado») |
| 2 | Origen de las llaves por empresa | Aleatorias y envueltas, para poder rotar y borrar empresa por empresa |
| 3 | Id de la fila en los datos asociados | **No.** Los conversores de EF solo ven el valor. Meter la fila obligaría a cifrar en interceptores y a prohibir las proyecciones |
| 4 | Tipo de columna | `text`, para que una consulta SQL directa muestre el prefijo |
| 5 | Índice ciego | Completo (32 bytes). Truncarlo hasta reducir la fuga de verdad obliga a confirmar cada resultado descifrando |
| 6 | Tipo de búsqueda | Exacta para correo, teléfono y DPI. Por palabras y por prefijo (desde 3 letras) para el nombre del candidato, cuando exista |
| 7 | Valor que no descifra | **Error** (falla cerrado). Un marcador «dato no disponible» dentro del conversor se podría volver a guardar y destruir el dato. Si hace falta, se mostrará por campo en los DTO de lectura |
| 8 | Relleno de longitud | No |
| 9 | Datos de los usuarios del equipo | Ámbito de plataforma, con el correo buscable exacto y único |

### Presupuesto de rendimiento

- **Medido:** descifrar un campo cuesta unos 3 µs. Un listado de 50 filas con un campo cifrado
  agrega unos 0.15 ms sobre los 8 ms de la consulta (PostgreSQL local).
- **Presupuesto:** como máximo 1 ms por cada 50 filas y columna cifrada.
- **KMS** solo se nota en el primer acceso a un ámbito cada 10 minutos.

## Alternativas descartadas

- **Solo cifrado de disco o TDE.** No protege de quien lee la base.
- **Una DEK por guardado, envuelta dentro de cada valor.** Era la primera especificación.
  Alarga cada valor y obliga a desenvolver una llave por fila; con una llave por empresa
  sobra.
- **ML-KEM (post-cuántico) para envolver.** Con la KEK en KMS no aporta: la aplicación
  necesita la llave de la empresa para leer y escribir. Envolver con AES-256 ya resiste
  computación cuántica.
- **Llaves derivadas con HKDF de una maestra.** No permiten rotar ni borrar por empresa, y
  dejan un único punto de compromiso.
- **Descifrar en la base (pgcrypto).** Pondría la llave en PostgreSQL.
- **Cifrado que preserva el orden (OPE/ORE).** Revela demasiado.
- **Búsqueda por subcadena (n-gramas).** Si el negocio la necesita, ese campo no debería
  cifrarse.

## Fugas de información que se aceptan

- El índice exacto revela qué filas de una empresa comparten valor, y con qué frecuencia.
- Cuando exista la búsqueda por palabras:
  - los tokens revelarán la frecuencia de cada palabra y cuántas palabras tiene cada valor;
  - los prefijos, además, la estructura de las palabras.
- La longitud del criptograma revela la del dato, y `null` revela que no hay valor.
- Quien pueda escribir en la base puede intercambiar el valor de la misma columna entre dos
  filas de la **misma empresa** (decisión 3). No puede moverlo a otra columna ni a otra
  empresa.
- Las llaves en caché no se borran de memoria al vencer: otro hilo podría estar usándolas, y
  borrarlas lo haría cifrar con ceros. Esto queda fuera del modelo de amenazas (servidor
  comprometido).

## Consecuencias

- **Configuración:** `FIELD_ENCRYPTION_KEK_PROVIDER` es obligatoria. En producción van
  `aws-kms` y `FIELD_ENCRYPTION_KMS_KEY_ID`, y el rol de la tarea necesita `kms:Encrypt` y
  `kms:Decrypt` sobre esa llave.
- **Escrituras:** nada escribe en una columna cifrada sin pasar por la aplicación. Las
  semillas, los arreglos de datos y los scripts usan el código de la aplicación.
- **Pruebas:** las del cifrado corren contra PostgreSQL real (`TEST_POSTGRES_ADMIN_CONNECTION_STRING`)
  y fallan si no está. El CI lo levanta como servicio.
- **Procedimientos:** rotación, llave comprometida, restauración y baja de una empresa están
  en el [runbook](../runbooks/cifrado-de-campos.md).

### Pendiente (llega con las funciones que lo necesitan)

- **Tokens por palabra y prefijo** (`search_token`), con el nombre del candidato.
- **Proceso de recifrado por lotes** tras rotar una DEK (reanudable, con `UPDATE … WHERE
  col = :viejo`) y **reenvoltura de DEKs** tras rotar la KEK. Hoy la rotación crea la
  versión nueva y la anterior sigue descifrando; nada queda ilegible.
- **Contexto de empresa por petición**, con la autenticación y las empresas.
- **Cifrado de archivos** (CV) con la misma biblioteca, en formato binario.
- **Normalización de teléfonos (E.164)**, con WhatsApp.
