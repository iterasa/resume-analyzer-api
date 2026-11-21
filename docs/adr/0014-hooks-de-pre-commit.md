# 0014 — Hooks de pre-commit sin dependencias: gitleaks y formato

**Estado:** aceptada.

## Contexto

Un secreto confirmado por error queda en el historial aunque se borre después, y un
fichero sin formatear convierte cada revisión en una discusión de espacios. Ambas cosas
se detectan antes de confirmar si hay un hook; pero un hook que exige instalar un
gestor (`pre-commit`, Husky) es el primero que nadie instala.

## Decisión

- `.githooks/pre-commit` en el repositorio, en bash: `gitleaks` sobre lo que entra en el
  commit y `dotnet format --verify-no-changes` sobre los `.cs` modificados. Nada más.
- Se activa con `git config core.hooksPath .githooks`; `setup-template.sh` lo hace.
- Sin `gitleaks` instalado el hook **falla** con instrucciones: una comprobación de
  secretos que se salta en silencio no protege de nada. `git commit --no-verify` sigue
  siendo la salida consciente.
- El hook es una red local. Las mismas comprobaciones deben correr en el CI, que es lo
  que bloquea de verdad.

## Alternativas descartadas

- `pre-commit` (Python) con el hook oficial de gitleaks: exige Go o Python en cada máquina.
- Husky.Net: idiomático, pero una dependencia más para dos comandos.
- Compilar y probar en el hook: demasiado lento para cada commit; es trabajo del CI.

## Consecuencias

- `.github/workflows/ci.yml` repite en cada PR y en cada push a `main` las dos
  comprobaciones del hook, sobre todo el historial en el caso de gitleaks, y añade la
  compilación y las pruebas.

- `.config/dotnet-tools.json` fija `dotnet-ef` para que las migraciones no dependan de
  una herramienta global desactualizada.
