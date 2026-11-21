# 0009 — FluentAssertions fijado en 7.x

**Estado:** aceptada.

## Contexto

FluentAssertions 8.0 (enero de 2025) cambió a la licencia Xceed: gratuita para uso no
comercial, de pago para el resto. 7.x es la última línea Apache-2.0 y sigue recibiendo
correcciones.

## Decisión

Fijar `FluentAssertions` en 7.x. La razón está anotada junto a la versión en
`Directory.Packages.props`, que es donde la vería quien fuese a subirla.

## Alternativas descartadas

- Pagar la licencia: decisión de cada organización, no de la plantilla.
- Migrar a `AwesomeAssertions` (bifurcación Apache-2.0 compatible en API): opción válida
  si 7.x dejara de mantenerse.

## Consecuencias

- Dependabot/Renovate deben ignorar el major de este paquete.
