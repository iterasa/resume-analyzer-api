#!/usr/bin/env bash
# Verifica que la plantilla sigue siendo utilizable: copia el repositorio a un directorio
# temporal, lo convierte en proyecto con setup-template.sh, comprueba que no sobrevive
# nada de la plantilla, compila y ejecuta la suite completa.
#
# Existe porque el renombrado se rompe en silencio: basta con que un literal escape a la
# sustitución para que el proyecto derivado compile y arrastre el nombre de otro cliente.
#
#   bash scripts/verify-template.sh [Company] [Product]

set -euo pipefail

COMPANY="${1:-Acme}"
PRODUCT="${2:-Billing}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"

cleanup() { rm -rf "$WORK"; }
trap cleanup EXIT

echo "==> Comprobando que ningún workflow contiene el nombre de la plantilla"
# El renombrado lo modificaría, y GitHub rechaza que el GITHUB_TOKEN cree o modifique
# workflows: el push del workflow template-cleanup.yml fallaría.
if [ -d "$ROOT/.github/workflows" ] && grep -rIq -e 'Company\.Product\.Template' "$ROOT/.github/workflows"; then
  echo "FALLO: hay workflows que contienen el nombre de la plantilla:" >&2
  grep -rIl -e 'Company\.Product\.Template' "$ROOT/.github/workflows" >&2
  exit 1
fi

echo "==> Copiando el repositorio a $WORK"
rsync -a --exclude '.git' --exclude 'bin' --exclude 'obj' --exclude 'TestResults' "$ROOT/" "$WORK/"

echo "==> Renombrando a ${COMPANY}.${PRODUCT}"
( cd "$WORK" && bash setup-template.sh "$COMPANY" "$PRODUCT" >/dev/null )

echo "==> Comprobando que no sobrevive el nombre anterior"
# grep -q y no `grep -l | grep -q .`: con pipefail, el SIGPIPE del primero daría un falso negativo.
if grep -rIq --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git \
     -e 'Company\.Product\.Template' -e 'Company Product Template' "$WORK/src" "$WORK/tests" 2>/dev/null; then
  echo "FALLO: quedan referencias al proyecto de origen tras el renombrado:" >&2
  grep -rIl --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git \
    -e 'Company\.Product\.Template' -e 'Company Product Template' "$WORK/src" "$WORK/tests" >&2
  exit 1
fi

echo "==> Comprobando que no queda nada propio de la plantilla"
for leftover in setup-template.sh scripts/verify-template.sh .github/workflows/template-cleanup.yml; do
  if [ -e "$WORK/$leftover" ]; then
    echo "FALLO: el proyecto conserva $leftover" >&2
    exit 1
  fi
done
if grep -rIq --include='*.md' --exclude-dir=bin --exclude-dir=obj -e 'plantilla:inicio' -e 'plantilla:fin' "$WORK"; then
  echo "FALLO: quedan bloques de documentación de la plantilla:" >&2
  grep -rIl --include='*.md' --exclude-dir=bin --exclude-dir=obj -e 'plantilla:inicio' -e 'plantilla:fin' "$WORK" >&2
  exit 1
fi

echo "==> Compilando y ejecutando la suite"
( cd "$WORK" && dotnet test "${COMPANY}.${PRODUCT}.sln" --nologo -v q )

echo "==> La plantilla se renombra, compila y pasa sus pruebas"
