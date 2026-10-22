# Scripts

Helper scripts for the language core. All are bash and can be run from any directory.

## fetch-models.sh

Downloads the models listed in `models/manifest.json` into `${VOCATERRA_MODELS_DIR:-models}/<id>/`, checks the archive SHA-256, extracts it and verifies every listed file against its pinned hash. `--list` reports which models are present, missing, mismatching or unpinned without downloading anything, and model ids on the command line limit the work to those models. Entries without an archive are skipped with a warning because their ONNX export is manual. Needs bash, python3, curl, sha256sum (or shasum) and unzip or tar with bzip2. The optional `models` job in `.github/workflows-pending/ci.yml` runs it for the Vosk model only.

## fetch-lexicon-sources.sh

Downloads the two data sets the lexicon import reads into `${VOCATERRA_LEXICON_SOURCES_DIR:-lexicon-sources}/` (git-ignored): the kaikki.org Spanish extract of English Wiktionary (about 1 GB of JSONL) and `es_50k.txt` of hermitdave/FrequencyWords at a pinned commit. The URLs, byte sizes and SHA-256 hashes are pinned in the script; a file that is already there and matches is kept, a download that does not match is deleted and the script stops (the kaikki.org URL is not versioned, so a mismatch means upstream replaced the file; re-pinning is described in [docs/LEXICON.md](../docs/LEXICON.md)). It then writes `sources.json` next to the files with URL, hash, size, licence and retrieval date, which `Vocaterra.Nlp.ContentBuilder import-lexicon --sidecar` copies into the sidecar of the generated lexicon. `--check` verifies the files that are present and downloads nothing. Needs bash, python3, curl and sha256sum (or shasum). Downloaded files are data: nothing in them is ever executed.

## license-scan.sh

Checks the licence of every NuGet package in `Vocaterra.Nlp.sln`, direct and transitive, as listed by `dotnet list package --include-transitive`. Each package's cached `.nuspec` is read first; packages without an SPDX expression there are looked up in the NuGet registration API unless `--offline` is given, which restricts the scan to the local cache (`NUGET_PACKAGES` or `~/.nuget/packages`). The solution must be restored first. The script prints a table with a status per package and exits 1 when any package is GPL, LGPL, AGPL or SSPL, is not on `license-allowlist.txt`, or has no determinable licence. The allowlist holds one SPDX identifier per line, matched as a whole token and case-insensitively, so `MIT` does not accept `MIT-0`; a line containing spaces is matched as a complete expression. Add an entry only after reading the licence text of the package it would accept. The `license` job in `.github/workflows-pending/ci.yml` runs the script.

Packages whose metadata names no usable licence can be covered by `license-overrides.txt`, one `<package-id>|<version>|<spdx>|<evidence-url>|<reviewed-on>` line per package version. An override applies only to the exact id (case-insensitive) and version on its line, is used only after the nuspec and the API have failed to name a licence, and must cite an https evidence URL. Its SPDX value is still checked against the allowlist, and the table reports its source as `override`. Malformed lines stop the scan with exit status 2. When a package is updated, its override no longer matches and the scan reports it again.


## check-links.sh

Checks the relative Markdown links in every `.md` file under `nlp/` (build output excluded). A link is checked when it has no URL scheme and is not a bare `#anchor`; its target, without any `#fragment`, must exist in the working tree. Links inside code fences and code spans are ignored, and nothing is fetched. It prints each broken link as `file:line: target` and the count of links checked, and exits 1 when any link is broken. Run it as `bash nlp/scripts/check-links.sh` from anywhere in the repository; `--quiet` prints only the broken links and the summary when there are some.

The scripts that build and check the content bundle, the lexicon tools and the LanguageTool server live in the repository root `scripts/` (`content.sh`, `fetch-tools.sh`, `languagetool.sh`), next to the game tools, because the game uses them too. The tool inventory for them is in [TOOLS.md](../TOOLS.md).
