#!/usr/bin/env bash
# Packs the Vocaterra.Nlp.* libraries and the vocaterra-content dotnet tool (Vocaterra.Nlp.ContentBuilder) into artifacts/packages (a .nupkg
# and a .snupkg each) and copies the .nupkg files into artifacts/feed, a folder that NuGet reads as a local feed (samples/HostConsole/nuget.config
# points there, and `dotnet tool install --add-source artifacts/feed` reads it for the tool).
#
# Usage: scripts/pack.sh
#   Only the projects under src/ and the ContentBuilder tool are packable (Directory.Build.props and its project file); tests and samples are not.
#   Packages are built in Release from the current tree. Nothing is published to a hosted feed: a consuming repository takes the core as a
#   submodule and uses either its projects or this folder (docs/PACKAGING.md, D5). The packages are Apache-2.0 (LICENSE, D3). The tool's package depends on the sherpa-onnx native build, so it is for local installation only until D2 is settled.
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
