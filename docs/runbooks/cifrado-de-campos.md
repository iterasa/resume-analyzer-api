# Runbook: cifrado de campos

Procedimientos de operación del cifrado de campos ([ADR 0015](../adr/0015-cifrado-de-campos.md)).
Todo cambio sobre llaves o datos de producción se ensaya antes en una copia restaurada, y
la ventana se avisa.

Señales que vigilar en todos los casos:

- la métrica `field_encryption.decrypt_failures` (por campo y motivo): debe ser cero;
- el evento de log **9001** ("Security: could not decrypt …"), que dice campo, ámbito, llave
  y motivo, nunca el valor;
- los errores `AccessDenied` o `InvalidCiphertext` de KMS en CloudTrail.

## 1. Rotación ordinaria

### DEK de una empresa o de la plataforma

1. Ejecutar `EncryptionKeyRotation.RotateAsync(scopeId, purpose)` con `enc`, con `idx` o con
   ambos. La versión nueva queda `active` y la anterior `decrypt_only`; nada deja de leerse.
2. Esperar al menos 10 minutos, lo que dura la caché de llaves: hasta entonces otras
   instancias pueden seguir escribiendo con la versión anterior.
3. **Pendiente de automatizar.** Recifrar (`enc`) o reindexar (`idx`) las filas de ese
   ámbito con la versión anterior. Se hace por lotes, con
   `UPDATE … SET col = :nuevo WHERE id = :id AND col = :viejo`, en horario de poca carga.
   Mientras no exista el proceso, la versión anterior se queda en `decrypt_only`, lo que es
   seguro.
4. Cuando una consulta confirme que ninguna fila usa ya la versión anterior (el `key_id` del
   criptograma o `<col>_bidx_v`), marcarla como `retired`.

### KEK en KMS

1. Con la rotación automática de KMS (anual), el ARN no cambia y KMS conserva el material
   anterior: no hay que hacer nada más.
2. Si se cambia a otra llave de KMS: dar `kms:Decrypt` sobre la llave anterior y
   `kms:Encrypt` y `kms:Decrypt` sobre la nueva, y cambiar `FIELD_ENCRYPTION_KMS_KEY_ID`.
   Las DEK nuevas se envuelven con la nueva; las existentes se siguen leyendo con su
   `kek_id`.
3. **Pendiente de automatizar.** Volver a envolver las DEK existentes con la llave nueva. Los
   datos no se tocan. Después, quitar el permiso sobre la llave anterior.

## 2. Llave comprometida

### Una DEK (se filtró la llave de una empresa)

1. Rotar la DEK afectada (paso 1 del procedimiento anterior).
2. Recifrar o reindexar **de inmediato** todo el ámbito, sin esperar a horario de poca
   carga.
3. Marcar la versión comprometida como `retired` en cuanto no la use ninguna fila.
4. Revisar en la auditoría qué datos de esa empresa pudieron leerse. Evaluar el aviso a la
   empresa y a la autoridad según el país.

### La KEK (credenciales con `kms:Decrypt`)

1. Revocar las credenciales y revisar en CloudTrail cada `Decrypt` con el contexto
   `app=criba`.
2. Crear una llave de KMS nueva, cambiar a ella (paso 2 de la rotación de la KEK) y volver a
   envolver todas las DEK.
3. Si hubo descifrados ilegítimos de DEK, tratar esas empresas como en el caso de una DEK
   comprometida.

## 3. Restauración de una copia

1. Restaurar la base completa, incluida `encryption_key`. Sin ella, nada descifra.
2. La KEK que envolvió esas DEK (`kek_id`) tiene que seguir existiendo en KMS. Una llave de
   KMS programada para borrarse se puede cancelar dentro de su período de espera.
3. Arrancar la aplicación con la misma `FIELD_ENCRYPTION_KMS_KEY_ID`, o con permiso de
   `kms:Decrypt` sobre la KEK de la copia.
4. Comprobar en un listado de cada empresa que la métrica `decrypt_failures` sigue en cero.
5. Si la copia es anterior a una baja de empresa, volver a destruir las llaves de esa
   empresa (procedimiento 4) **antes** de abrir el tráfico.

## 4. Baja de una empresa (borrado criptográfico)

1. Exportar lo que la empresa tenga derecho a llevarse **antes**: después ya no se puede.
2. Marcar sus llaves como `destroyed` y borrar su `wrapped_key`. Sin la DEK, sus datos son
   ilegibles aunque las filas sigan ahí.
3. Borrar sus filas según la política de retención.
4. **Salvedad:** con la KEK compartida, una copia de seguridad anterior todavía contiene la
   DEK envuelta. El borrado solo es efectivo cuando caducan esas copias, y por eso la
   retención de copias debe ser de 35 días o menos. Si una empresa exige borrado inmediato,
   la alternativa es una KEK propia en KMS para ella (ADR 0015, decisión 1).
5. Dejar constancia en la auditoría: quién, cuándo, y qué empresa (por id, sin datos
   personales).

## Usuarios existentes antes del cifrado

La migración `FieldEncryption` se detiene si `user` tiene filas, porque una migración no
puede cifrar. Si una base de desarrollo tiene usuarios de prueba, se borran antes de
migrar. En un entorno con datos reales hay que hacer una migración dual:

1. desplegar código que lea en claro y en el formato nuevo, y que escriba siempre en el
   nuevo;
2. recifrar con la aplicación;
3. comprobar con una consulta que no queda nada en claro;
4. retirar la lectura en claro.

Este proyecto no la necesitó: no había datos.

## Llaves de Data Protection (antiforgery)

Las llaves que firman los tokens antiforgery viven en `data_protection_key`, con el XML cifrado
con las llaves de plataforma (ADR 0017). ASP.NET crea una nueva cada 90 días y conserva las
anteriores para validar los tokens ya emitidos.

- Al crear una, Data Protection registra *«No XML encryptor configured»*: es esperado, el
  cifrado lo hace la columna, no Data Protection.
- **Llave comprometida**: borrar las filas de `data_protection_key` y reiniciar las instancias.
  Se crea otra al primer uso; los tokens emitidos dejan de valer y la web pide uno nuevo sola
  (`antiforgery_invalid` → reintento). Las sesiones no se ven afectadas: no dependen de estas llaves.
- **Restauración**: si la KEK o la llave de plataforma no corresponden, el XML no descifra y la
  API no puede emitir tokens. Mismo remedio: borrar las filas.

## Usuarios existentes antes de `TenantsSessionsVacancies`

Igual que la migración del cifrado, `20261121214257_TenantsSessionsVacancies` se detiene si
`user` tiene filas: el nombre nuevo va cifrado y solo lo cifra la aplicación. Si llegara a haber
usuarios, se recrean con el registro (no había datos reales al escribirla).
