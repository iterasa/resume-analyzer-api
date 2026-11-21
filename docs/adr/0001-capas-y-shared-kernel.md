# 0001 — Cinco proyectos, dominio `internal`, `Shared` como núcleo compartido

**Estado:** aceptada.

## Contexto

Una API de plantilla debe hacer difícil el atajo que rompe la arquitectura: un controller
que toca un repositorio, un caso de uso que conoce EF Core, un agregado que llama a un
servicio. Las reglas de dependencia que solo viven en un documento se incumplen en el
primer refactor con prisa.

## Decisión

- `Domain`, `Application`, `Infrastructure`, `Api` y `Shared`. Los tipos del dominio son
  `internal` con `InternalsVisibleTo` para `Application`, `Infrastructure` y los tests: la
  capa de API **no puede** referenciar un agregado ni por accidente.
- `Shared` contiene lo que dominio y API necesitan a la vez sin que uno vea al otro:
  `DomainException`, `Permissions`, `Result`/`Error`, paginación y lectura de variables.
- Las reglas las vigilan tests de arquitectura (NetArchTest y una regla propia sobre el IL),
  no la revisión de código.

## Alternativas descartadas

- Dominio público: más cómodo, y la puerta abierta a `return user` desde un controller.
- Un proyecto por *feature* (vertical slices): válido para productos con muchos módulos;
  para una plantilla añade decisiones que cada proyecto tendría que rehacer.

## Consecuencias

- Los DTO de respuesta viven en `Application`; los contratos HTTP en `Api`.
- `Shared` mezcla núcleo de dominio y transporte. Es la costura que antes se erosiona;
  si crece, se divide en `Domain.Common` y `Shared`.
