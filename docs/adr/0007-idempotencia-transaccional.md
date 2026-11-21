# 0007 — Idempotencia por clave con transacción única

**Estado:** aceptada (21 nov 2026). Viene de la rama `full` de la plantilla, ajustada a Criba.

## Contexto

Quien reintenta un `POST` tras un corte de red no sabe si la primera petición llegó. Sin
idempotencia obtiene un duplicado (dos vacantes iguales) o un 409 que no sabe interpretar.

## Decisión

- Cabecera `Idempotency-Key` (borrador IETF `httpapi-idempotency-key-header`), opcional, en
  las acciones marcadas con `[Idempotent]`: hoy, crear, cerrar, pausar, reanudar y reabrir
  vacantes. Los `PUT` ya son idempotentes por naturaleza.
- La clave se acota a **la empresa y el usuario** de la sesión (`t:<empresa>:u:<usuario>`). Sin
  sesión no hay idempotencia: un ámbito «anónimo» compartido dejaría a un tercero que adivine
  la clave reproducir la respuesta de otro.
- La operación y el registro de su respuesta comparten **una transacción**. Dos peticiones
  simultáneas con la misma clave se resuelven en la base: la clave primaria `(scope, key)` deja
  confirmar solo a una; la otra se deshace entera y reproduce la respuesta ganadora.
- Se registran también los errores 4xx: el reintento ve lo mismo que la primera vez. Misma
  clave con otro contenido: 422 `idempotency_key_reused`. Una excepción no se registra.
- La respuesta se serializa con las opciones del formateador de MVC, no con `JsonOptions`: MVC
  les cambia el codificador y, sin eso, «Inglés» se reproducía como «Inglés».

## Alternativas descartadas

- Registro en caché: no sobrevive al proceso ni se comparte entre instancias.
- Fila «en curso» + fila «completada»: dos escrituras y un estado huérfano si el proceso cae.

## Consecuencias

- Las claves caducan (`IDEMPOTENCY_KEY_TTL_HOURS`, 24 h) y las purga `ExpiredRecordPurgeService`.
- CORS admite la cabecera y expone `Idempotency-Replayed`.
- La respuesta guardada va cifrada con las llaves de la empresa (puede llevar datos personales).
- Quien pierde la carrera deja en el log un evento `idempotency.race` que compensa la auditoría deshecha.
- La web todavía no envía la cabecera: funciona igual sin ella.
