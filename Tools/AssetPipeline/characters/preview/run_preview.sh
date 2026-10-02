#!/usr/bin/env bash
# Builds the preview harness (into a temp dir) and runs a scenario, writing JSON dumps to OUT_DIR,
# then renders them with render.py.
#   Tools/AssetPipeline/characters/preview/run_preview.sh OUT_DIR scenario [args...]
# Scenarios: see Harness/Program.cs (lineup, idle, walk, actions, poses, figures, items, fparms ...).
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$1"; shift
mkdir -p "$OUT"
BUILD="${PREVIEW_BUILD_DIR:-/tmp/poc-char-preview-build}"
dotnet build "$HERE/Preview.csproj" -nologo -v:q -clp:NoSummary \
  -p:BaseIntermediateOutputPath="$BUILD/obj/" -p:OutputPath="$BUILD/bin/" \
  -p:MSBuildProjectExtensionsPath="$BUILD/obj/" > "$BUILD.log" 2>&1 || { grep -E "error|Error" "$BUILD.log" | sort -u | head -40; exit 1; }
dotnet "$BUILD/bin/Preview.dll" "$OUT" "$@"
