# 0006 — CORS desactivado por defecto, lista explícita, sin credenciales

**Estado:** aceptada.

## Contexto

Una API sin cliente de navegador no debe tener política CORS. Cuando lo hay, `*` o una
lista mal escrita son los dos fallos habituales, y ambos silenciosos.

## Decisión

- `CORS_ENABLED=false` por defecto. Activo, `CORS_ALLOWED_ORIGINS` es obligatoria y se
  valida al arrancar: `esquema://host[:puerto]`, comparación literal, `https://*.dominio`
  para subdominios, nunca `*` ni `null`.
- Métodos y cabeceras fijos a los que usa la API; cabeceras expuestas para que el script
  lea correlación, `Retry-After` y `Location`.
- Sin credenciales salvo `CORS_ALLOW_CREDENTIALS=true`: el token Bearer en
  `Authorization` no es una credencial CORS; solo las cookies lo son.
- El middleware va antes de autenticación y limitador: las cabeceras acompañan también a
  un 401 y el preflight no consume cuota.

## Consecuencias

- Un origen mal escrito detiene el arranque con un mensaje que lo nombra.
- Ampliar métodos o cabeceras es tocar `CorsPolicyOptions`, en un sitio.
