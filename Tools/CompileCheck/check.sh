#!/usr/bin/env bash
# Compile-check the Unity C# sources without the Unity Editor.
#
#   Tools/CompileCheck/check.sh            # runtime scripts (no Editor folders)
#   Tools/CompileCheck/check.sh --editor   # everything, including Editor scripts
#   Tools/CompileCheck/check.sh --filter Scripts/Map   # only print diagnostics for paths containing this text
#
# Needs the .NET SDK (8+) and Unity reference assemblies in $UNITY_REF_DIR
# (default /opt/unity-ref). If they are missing they are fetched from nuget.org:
#   UnityEngine.Modules 2021.3.33  -> engine modules
#   Unity3D.SDK 2021.1.14.1        -> UnityEditor.dll
# Each run builds into its own temp directory so several checks can run at once.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REF="${UNITY_REF_DIR:-/opt/unity-ref}"
PROJ="$HERE/Runtime.csproj"
FILTER=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --editor) PROJ="$HERE/Editor.csproj"; shift ;;
    --filter) FILTER="$2"; shift 2 ;;
    *) echo "unknown arg $1"; exit 2 ;;
  esac
done

if [[ ! -f "$REF/UnityEngine.CoreModule.dll" || ! -f "$REF/editor/UnityEditor.dll" ]]; then
  echo "Fetching Unity reference assemblies into $REF ..."
  TMP="$(mktemp -d)"
  mkdir -p "$REF/editor"
  curl -sSL -o "$TMP/ue.nupkg" https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  (cd "$TMP" && unzip -q -o ue.nupkg 'lib/netstandard2.0/*' && cp lib/netstandard2.0/*.dll "$REF/")
  curl -sSL -o "$TMP/sdk.nupkg" https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg
  (cd "$TMP" && unzip -q -o sdk.nupkg 'lib/UnityEditor.dll' && cp lib/UnityEditor.dll "$REF/editor/")
  rm -rf "$TMP"
fi

OUT="$(mktemp -d /tmp/poc-compile.XXXXXX)"
trap 'rm -rf "$OUT"' EXIT
set +e
dotnet build "$PROJ" -nologo -v:q -clp:NoSummary \
  -p:UnityRefDir="$REF" \
  -p:BaseIntermediateOutputPath="$OUT/obj/" \
  -p:OutputPath="$OUT/bin/" \
  -p:MSBuildProjectExtensionsPath="$OUT/obj/" > "$OUT/log.txt" 2>&1
STATUS=$?
set -e
# de-duplicate diagnostics, strip project suffix
grep -E "(error|warning) [A-Z]+[0-9]+" "$OUT/log.txt" | sed -E 's/ \[[^]]*\]$//' | sort -u > "$OUT/diag.txt" || true
if [[ -n "$FILTER" ]]; then
  grep -F "$FILTER" "$OUT/diag.txt" || true
else
  cat "$OUT/diag.txt"
fi
ERRS=$(grep -c " error " "$OUT/diag.txt" || true)
WARNS=$(grep -c " warning " "$OUT/diag.txt" || true)
if [[ $STATUS -ne 0 && $ERRS -eq 0 ]]; then
  cat "$OUT/log.txt"
fi
echo "---- compile check: $ERRS error(s), $WARNS warning(s) [$(basename "$PROJ")]"
exit $STATUS
