# 0004 — El dominio no lee el reloj

**Estado:** aceptada.

## Contexto

Con `DateTimeOffset.UtcNow` repartido por dominio y aplicación, bloqueo por intentos,
expiración de tokens y purgas no se podían probar sin esperar o sin trucos.

## Decisión

- Cada verbo del dominio que deja marca temporal recibe el instante (`now`), como ya
  hacía `RegisterFailedSignIn`.
- Los casos de uso toman el instante una vez de `TimeProvider` (BCL, registrado en
  `AddApplication`) y lo pasan.
- Un test de arquitectura inspecciona el IL de `Domain` y `Application`, incluidos los
  tipos anidados de lambdas y máquinas de estado `async`, y falla ante cualquier lectura de
  `DateTime*.UtcNow/Now/Today`.

## Alternativas descartadas

- `IClock` estático intercambiable: estado global; las pruebas en paralelo se pisan.
- Solo `TimeProvider` en aplicación, dominio con `UtcNow`: la mitad del problema.

## Consecuencias

- Firmas algo más largas en el dominio. A cambio, toda prueba fija la hora sin esperar.
- Las pruebas de integración pueden sustituir `TimeProvider` por uno falso.
