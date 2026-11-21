# 0010 — Contenedor sin root con `HEALTHCHECK`; SDK fijado con `global.json`

**Estado:** aceptada.

## Decisión

- La etapa de ejecución declara `USER $APP_UID` (uid 1654, el usuario `app` de la imagen
  base). Numérico, no por nombre: es lo que un orquestador comprueba con `runAsNonRoot`.
- `HEALTHCHECK` contra `/health/live` con `bash` y `/dev/tcp`: la imagen no trae `curl` ni
  `wget` y no conviene instalarlos solo para una sonda. Kubernetes lo ignora y usa sus
  probes; Docker, Compose y ECS lo usan.
- `global.json` fija el SDK `10.0.x` con `rollForward: latestFeature`: la misma versión
  mayor.menor en cada máquina y en el CI, sin fallar cuando la imagen `sdk:10.0` avance de
  banda.

## Consecuencias

- Cambiar el puerto de `ASPNETCORE_URLS` exige cambiarlo también en el `HEALTHCHECK`.
- La misma receta produce `amd64` y `arm64` con `docker buildx`.
