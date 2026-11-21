# 0013 — Argon2id por defecto, PBKDF2 seleccionable para FIPS, rehash al iniciar sesión

**Estado:** aceptada (21 nov 2026). Viene de la rama `full` de la plantilla.

## Contexto

Las contraseñas del equipo de cada empresa abren datos personales de candidatos. OWASP
recomienda Argon2id como primera opción; PBKDF2 solo cuando se exige FIPS-140. Un cambio de
algoritmo no puede invalidar los hashes que ya existen.

## Decisión

- Argon2id (RFC 9106) con la configuración de referencia de OWASP (m=19 MiB, t=2, p=1), en
  formato PHC `$argon2id$v=19$m=…,t=…,p=…$sal$hash`.
- `PASSWORD_HASH_ALGORITHM=pbkdf2-sha256` (600 000 iteraciones) para entornos FIPS.
- La verificación acepta ambos formatos: el hash lleva su algoritmo y parámetros. El inicio de
  sesión, único momento con la contraseña en claro, rehace los que no coinciden con lo
  configurado (`User.UpgradePasswordHash`, sin marca administrativa).
- Se verifica contra un hash de relleno cuando la cuenta no existe: el tiempo de respuesta no
  la delata.
- Política de contraseñas NIST 800-63B: 12 a 256 caracteres, sin reglas de composición.

## Alternativas descartadas

- Solo PBKDF2 (está en .NET): más débil frente a GPU y ASIC.
- libsodium nativo: más rápido, pero un binario por plataforma en la imagen. La implementación
  gestionada (`Konscious.Security.Cryptography.Argon2`, única dependencia nueva de seguridad)
  basta para el ritmo de inicios de sesión.

## Consecuencias

- Cada inicio de sesión cuesta decenas de milisegundos y ~19 MiB; la política `auth` del
  limitador acota cuántos puede provocar una IP.
- Endurecer parámetros es cambiar `ARGON2_*`: los hashes antiguos siguen valiendo y se renuevan solos.
