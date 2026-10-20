#!/usr/bin/env bash
# Packs the Vocaterra.Nlp.* libraries into artifacts/packages (a .nupkg and a .snupkg each) and copies the .nupkg files into
# artifacts/feed, a folder that NuGet reads as a local feed (samples/HostConsole/nuget.config points there).
#
# Usage: scripts/pack.sh
#   Only the projects under src/ are packable (Directory.Build.props); tests, tools and samples are not. Packages are built in Release
#   from the current tree. Nothing is published: the package feed is undecided (docs/DECISIONS.md D5), and no licence is named (D3).
set -euo pipefail
# A user-local SDK (~/.dotnet, as on the development machine) is preferred when present; CI uses the SDK that setup-dotnet installed.
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1; export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

rm -rf artifacts/packages artifacts/feed
dotnet pack Vocaterra.Nlp.sln -c Release -o artifacts/packages
mkdir -p artifacts/feed
cp artifacts/packages/*.nupkg artifacts/feed/
echo "packed $(ls artifacts/packages/*.nupkg | wc -l) packages into artifacts/packages; local feed: artifacts/feed"
