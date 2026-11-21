#!/usr/bin/env bash
#
# Convierte una copia de la plantilla en el proyecto nuevo:
#
#   1. Renombra namespaces, ensamblados, carpetas, solución y referencias en texto.
#   2. Retira lo que solo sirve a la plantilla: los bloques de Markdown entre
#      <!-- plantilla:inicio --> y <!-- plantilla:fin -->, scripts/verify-template.sh,
#      el workflow template-cleanup.yml y, al final, este mismo script.
#
#   bash setup-template.sh <Company> <Product>
#   bash setup-template.sh Acme Billing      # -> Acme.Billing
#
# Lo ejecuta el workflow template-cleanup.yml en un repositorio creado desde la
# plantilla, o una persona sobre un clon recién hecho. Una sola vez: después ya no
# existe, y lanzado sobre algo que no es la plantilla falla con un mensaje claro.

set -euo pipefail

OLD_NAME="Company.Product.Template"

main() {

  local company="${1:-}"
  local product="${2:-}"

  if [[ -z "$company" || -z "$product" ]]; then
    echo "Uso: bash setup-template.sh <Company> <Product>" >&2
    echo "Ejemplo: bash setup-template.sh Acme Billing" >&2
    exit 1
  fi

  if [[ ! "$company" =~ ^[A-Za-z][A-Za-z0-9_]*$ ]]; then
    echo "Error: Company debe ser un identificador C# válido." >&2
    exit 1
  fi

  if [[ ! "$product" =~ ^[A-Za-z][A-Za-z0-9_]*$ ]]; then
    echo "Error: Product debe ser un identificador C# válido." >&2
    exit 1
  fi

  local new_name="${company}.${product}"

  if [[ "$new_name" == "$OLD_NAME" ]]; then
    echo "Nada que hacer: el nombre nuevo es igual al de la plantilla." >&2
    exit 0
  fi

  if [[ ! -f "${OLD_NAME}.sln" ]]; then
    echo "Error: no encuentro ${OLD_NAME}.sln." >&2
    echo "Ejecuta este script en la raíz de una copia recién hecha de la plantilla, una sola vez." >&2
    exit 1
  fi

  # -I: sin el directorio actual en sys.path, para que un pathlib.py o re.py suelto en
  # la copia no sustituya a la biblioteca estándar.
  python3 -I - "$OLD_NAME" "$new_name" "$(basename "${BASH_SOURCE[0]}")" <<'PY'
from __future__ import annotations

import sys
from pathlib import Path

old, new, self_name = sys.argv[1], sys.argv[2], sys.argv[3]
root = Path.cwd()

skip_dirs = {".git", ".vs", ".idea", "bin", "obj", "TestResults", "coverage", "artifacts"}

text_extensions = {
    ".cs", ".csproj", ".sln", ".props", ".targets", ".json", ".yml", ".yaml", ".xml",
    ".md", ".txt", ".editorconfig", ".gitignore", ".dockerignore", ".sh", ".ps1",
    ".cmd", ".bat", ".config", ".http", ".env", ".sql", ".Dockerfile",
}

exact_names = {"Dockerfile", ".editorconfig", ".gitignore", ".dockerignore", ".env.example"}


def should_skip(path: Path) -> bool:
    # Relativo a la raíz: el directorio donde vive la copia (en el CI, el nombre del
    # repositorio) no debe decidir qué se salta.
    return any(part in skip_dirs for part in path.relative_to(root).parts)


def is_text_file(path: Path) -> bool:
    return path.name in exact_names or path.suffix.lower() in text_extensions


def replace_in_file(path: Path) -> None:
    # El propio script queda fuera: reescribirlo mientras bash lo ejecuta desplaza
    # el offset de lectura y bash acaba interpretando basura a partir de ahí.
    # Los enlaces simbólicos también: escribir a través de uno modificaría su destino,
    # que puede estar fuera de la copia.
    if path.name == self_name or should_skip(path) or path.is_symlink() or not path.is_file() or not is_text_file(path):
        return
    try:
        content = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return
    if old not in content:
        return
    with path.open("w", encoding="utf-8", newline="") as fh:
        fh.write(content.replace(old, new))


for file_path in [p for p in root.rglob("*") if p.is_file()]:
    replace_in_file(file_path)

paths_to_rename = [p for p in root.rglob("*") if old in p.name and not should_skip(p)]

for path in sorted(paths_to_rename, key=lambda p: len(p.parts), reverse=True):
    target = path.with_name(path.name.replace(old, new))
    if target.exists():
        raise RuntimeError(f"No se puede renombrar '{path}' porque '{target}' ya existe.")
    path.rename(target)

print(f"Renombrado completado: {old} -> {new}")
PY

  python3 -I - <<'PY'
from __future__ import annotations

import re
import shutil
from pathlib import Path

root = Path.cwd()

# Bloques de documentación que solo tienen sentido en la plantilla.
block = re.compile(r"<!-- plantilla:inicio -->.*?<!-- plantilla:fin -->\n?", re.DOTALL)

for path in root.rglob("*.md"):
    if path.is_symlink() or any(part in {".git", "bin", "obj"} for part in path.relative_to(root).parts):
        continue
    content = path.read_text(encoding="utf-8")
    cleaned = re.sub(r"\n{3,}", "\n\n", block.sub("", content))
    if cleaned != content:
        with path.open("w", encoding="utf-8", newline="") as fh:
            fh.write(cleaned)

# Ficheros que solo sirven a la plantilla. Las carpetas que quedan vacías se van con ellos.
for relative in ("scripts/verify-template.sh", ".github/workflows/template-cleanup.yml"):
    target = root / relative
    if target.exists():
        target.unlink()
    for parent in target.parents:
        if parent == root or not parent.exists() or any(parent.iterdir()):
            break
        shutil.rmtree(parent)

print("Retirado lo propio de la plantilla.")
PY

  # Hooks de pre-commit del repositorio (secretos y formato). Solo en un clon local: en
  # el CI no hay gitleaks y la configuración del runner se pierde al terminar.
  if [[ -z "${CI:-}" ]] && git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    git config core.hooksPath .githooks
    echo "Hooks de pre-commit activados (core.hooksPath = .githooks)."
  fi

  # Último paso: el script tampoco pertenece al proyecto. bash ya ha leído el fichero
  # entero (ver la última línea), así que borrarlo no afecta a lo que queda por ejecutar.
  rm -f -- "${BASH_SOURCE[0]}"

  echo
  echo "Siguientes pasos (README, «Puesta en marcha»):"
  echo "  1) cp .env.example .env    y revisa sus valores"
  echo "  2) dotnet tool restore     (dotnet-ef fijado en .config/dotnet-tools.json)"
  echo "  3) dotnet build"
  echo "  4) dotnet test"

}

# Definir main y llamarla en la última línea hace que bash haya leído el fichero
# entero antes de ejecutar nada; el exit evita que siga leyendo después.
main "$@"; exit
