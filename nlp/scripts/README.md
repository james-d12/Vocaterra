# Scripts

Helper scripts for the repository. Both are bash and can be run from any directory.

## fetch-models.sh

Downloads the models listed in `models/manifest.json` into `${SLG_MODELS_DIR:-models}/<id>/`, checks the archive SHA-256, extracts it and verifies every listed file against its pinned hash. `--list` reports which models are present, missing, mismatching or unpinned without downloading anything, and model ids on the command line limit the work to those models. Entries without an archive are skipped with a warning because their ONNX export is manual. Needs bash, python3, curl, sha256sum (or shasum) and unzip or tar with bzip2. The optional `models` job in `.github/workflows-pending/ci.yml` runs it for the Vosk model only.

## license-scan.sh

Checks the licence of every NuGet package in `SpanishLearningGame.sln`, direct and transitive, as listed by `dotnet list package --include-transitive`. Each package's cached `.nuspec` is read first; packages without an SPDX expression there are looked up in the NuGet registration API unless `--offline` is given, which restricts the scan to the local cache (`NUGET_PACKAGES` or `~/.nuget/packages`). The solution must be restored first. The script prints a table with a status per package and exits 1 when any package is GPL, LGPL, AGPL or SSPL, is not on `license-allowlist.txt`, or has no determinable licence. The allowlist holds one SPDX identifier per line, matched as a whole token and case-insensitively, so `MIT` does not accept `MIT-0`; a line containing spaces is matched as a complete expression. Add an entry only after reading the licence text of the package it would accept. The `license` job in `.github/workflows-pending/ci.yml` runs the script.

Packages whose metadata names no usable licence can be covered by `license-overrides.txt`, one `<package-id>|<version>|<spdx>|<evidence-url>|<reviewed-on>` line per package version. An override applies only to the exact id (case-insensitive) and version on its line, is used only after the nuspec and the API have failed to name a licence, and must cite an https evidence URL. Its SPDX value is still checked against the allowlist, and the table reports its source as `override`. Malformed lines stop the scan with exit status 2. When a package is updated, its override no longer matches and the scan reports it again.
