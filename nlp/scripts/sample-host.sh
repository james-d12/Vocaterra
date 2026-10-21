#!/usr/bin/env bash
# Proves that the packages expose everything a host needs. Packs the libraries (scripts/pack.sh), restores and builds
# samples/HostConsole against the local feed, and runs it on a content bundle. The sample is not in Vocaterra.Nlp.sln and references
# the packages by version, never by project; its nuget.config lists artifacts/feed and nuget.org (for transitive packages only).
#
# Usage: scripts/sample-host.sh [bundleDir]     (bundleDir defaults to content/v1)
#   Exits non-zero when the sample does: it must print the verdicts of the two demo utterances and both must match.
set -euo pipefail
# A user-local SDK (~/.dotnet, as on the development machine) is preferred when present; CI uses the SDK that setup-dotnet installed.
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1; export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

bash scripts/pack.sh

# The global packages folder keeps every version it has restored, and the local feed is rebuilt under the same version number, so the
# Vocaterra.Nlp.* folders are dropped first; otherwise the sample could run against a previous build of the same version.
rm -rf "${NUGET_PACKAGES:-$HOME/.nuget/packages}"/vocaterra.nlp.*

SAMPLE=samples/HostConsole/HostConsole.csproj
dotnet restore "$SAMPLE"
dotnet build "$SAMPLE" --no-restore -c Release
dotnet run --project "$SAMPLE" --no-build -c Release -- "${1:-content/v1}"
