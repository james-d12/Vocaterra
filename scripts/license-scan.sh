#!/usr/bin/env bash
# Checks the license of every NuGet package in Vocaterra.Nlp.slnf (direct and transitive). Prints a table and exits 1 when a
# package is GPL/LGPL/AGPL/SSPL, is not on scripts/license-allowlist.txt, or has no license that can be determined.
#
# Usage: scripts/license-scan.sh [--offline] [-h|--help]
#   --offline   read license metadata only from the local NuGet cache (${NUGET_PACKAGES:-~/.nuget/packages}); no network calls
#
# Per package, the cached .nuspec is read first (<license type="expression">, then a licenseUrl that names an SPDX id). Unless
# --offline is given, packages still without a license are looked up in the NuGet registration API (licenseExpression, then a
# recognised licenseUrl). Packages still without a license then take a reviewed entry from scripts/license-overrides.txt when one
# exists for the exact id and version; the override's license is checked against the allowlist like any other. The solution must
# be restored first, because the package list comes from 'dotnet list package'.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
solution="$repo_root/Vocaterra.Nlp.slnf"
allowlist="$script_dir/license-allowlist.txt"
overrides="$script_dir/license-overrides.txt"

usage() {
  cat <<'EOF'
Usage: scripts/license-scan.sh [--offline] [-h|--help]
  --offline   read license metadata only from the local NuGet cache (${NUGET_PACKAGES:-~/.nuget/packages}); no network calls
  Reviewed per-version entries in scripts/license-overrides.txt apply only when the metadata names no license.

Exit status: 0 when every package passes, 1 when any package fails or has an unknown license, 2 on usage or tool errors.
EOF
}

offline=0
for arg in "$@"; do
  case "$arg" in
    --offline) offline=1 ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      printf 'error: unknown argument: %s\n' "$arg" >&2
      usage >&2
      exit 2
      ;;
  esac
done

fail() { printf 'error: %s\n' "$*" >&2; exit 2; }

command -v dotnet >/dev/null 2>&1 || fail "dotnet not found on PATH"
command -v python3 >/dev/null 2>&1 || fail "python3 not found on PATH"
[ -f "$solution" ] || fail "solution not found: $solution"
[ -f "$allowlist" ] || fail "allowlist not found: $allowlist"
[ -f "$overrides" ] || fail "overrides file not found: $overrides"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if ! dotnet list "$solution" package --include-transitive --format json >"$work/packages.json"; then
  fail "'dotnet list package' failed; restore the solution first (dotnet restore)"
fi

python3 -I - "$work/packages.json" "$allowlist" "$offline" "$overrides" <<'PY'
import datetime
import gzip
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

packages_path, allowlist_path, offline, overrides_path = sys.argv[1], sys.argv[2], sys.argv[3] == "1", sys.argv[4]
cache_root = os.environ.get("NUGET_PACKAGES") or os.path.join(os.path.expanduser("~"), ".nuget", "packages")
registration_url = "https://api.nuget.org/v3/registration5-gz-semver2/{id}/{version}.json"
denied_markers = ("GPL", "SSPL")  # checked on every identifier, so LGPL and AGPL are covered too
keywords = ("AND", "OR", "WITH")
token_pattern = re.compile(r"\(|\)|[^\s()]+")
license_url_patterns = (
    re.compile(r"^https?://licenses\.nuget\.org/([A-Za-z0-9.+-]+)/?$", re.IGNORECASE),
    re.compile(r"^https?://(?:www\.)?opensource\.org/licenses/([A-Za-z0-9.+-]+)/?$", re.IGNORECASE),
)
known_license_pages = {
    "apache.org/licenses/license-2.0": "Apache-2.0",
    "unlicense.org": "Unlicense",
    "creativecommons.org/publicdomain/zero/1.0": "CC0-1.0",
    "opensource.org/licenses/mit-license.php": "MIT",
}


class NotFound(Exception):
    """The NuGet registration API has no record of this package version."""


# Allowlist: one SPDX identifier per line (matched whole and case-insensitively) or a whole expression containing spaces.
allowed_ids = set()
allowed_expressions = set()
with open(allowlist_path, encoding="utf-8") as handle:
    for raw in handle:
        entry = " ".join(raw.split("#", 1)[0].split()).upper()
        if not entry:
            continue
        (allowed_expressions if " " in entry or "(" in entry else allowed_ids).add(entry)


def config_error(message):
    """Reports a malformed input file and stops the scan."""
    print("error: " + message, file=sys.stderr)
    sys.exit(2)


# Reviewed overrides: <package-id>|<version>|<spdx>|<evidence-url>|<reviewed-on YYYY-MM-DD>, keyed by lowercase id and exact version.
overrides = {}
with open(overrides_path, encoding="utf-8") as handle:
    for number, raw in enumerate(handle, 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        fields = [field.strip() for field in line.split("|")]
        where = "license-overrides.txt:%d" % number
        if len(fields) != 5 or not all(fields):
            config_error(where + ": expected 5 non-empty '|' separated fields")
        if not fields[3].startswith("https://"):
            config_error(where + ": evidence must be an https URL")
        try:
            datetime.date.fromisoformat(fields[4])
        except ValueError:
            config_error(where + ": reviewed-on must be a YYYY-MM-DD date")
        key = (fields[0].lower(), fields[1])
        if key in overrides:
            config_error(where + ": duplicate entry for %s %s" % (fields[0], fields[1]))
        overrides[key] = {"spdx": fields[2], "url": fields[3], "reviewed": fields[4]}


def is_allowed(identifier):
    """True when one license identifier is on the allowlist and is not a copyleft family member."""
    name = identifier.upper().rstrip("+")
    if any(marker in name for marker in denied_markers):
        return False
    return name in allowed_ids


def identifiers_of(tokens):
    """License identifiers in an expression, skipping operators and WITH exception ids."""
    names = []
    skip_next = False
    for token in tokens:
        if skip_next:
            skip_next = False
        elif token.upper() == "WITH":
            skip_next = True
        elif token not in ("(", ")") and token.upper() not in ("AND", "OR"):
            names.append(token)
    return names


def evaluate_tokens(tokens):
    """Evaluates an SPDX expression with AND binding tighter than OR; a WITH exception does not change the base license."""
    cursor = [0]

    def next_token():
        if cursor[0] >= len(tokens):
            raise ValueError("unexpected end of expression")
        cursor[0] += 1
        return tokens[cursor[0] - 1]

    def peek():
        return tokens[cursor[0]].upper() if cursor[0] < len(tokens) else None

    def atom():
        token = next_token()
        if token == "(":
            value = disjunction()
            if next_token() != ")":
                raise ValueError("missing closing parenthesis")
            return value
        if token == ")" or token.upper() in keywords:
            raise ValueError("unexpected " + token)
        return is_allowed(token)

    def atom_with_exception():
        value = atom()
        if peek() == "WITH":
            next_token()
            next_token()
        return value

    def conjunction():
        value = atom_with_exception()
        while peek() == "AND":
            next_token()
            right = atom_with_exception()
            value = value and right
        return value

    def disjunction():
        value = conjunction()
        while peek() == "OR":
            next_token()
            right = conjunction()
            value = value or right
        return value

    result = disjunction()
    if cursor[0] != len(tokens):
        raise ValueError("unexpected token " + tokens[cursor[0]])
    return result


def evaluate(expression):
    """Returns (verdict, note) for a license expression found in the NuGet metadata."""
    normalized = " ".join(expression.split()).upper()
    if normalized in allowed_expressions:
        return "PASS", "-"
    tokens = token_pattern.findall(expression)
    try:
        if evaluate_tokens(tokens):
            return "PASS", "-"
    except ValueError as error:
        return "FAIL", "cannot parse expression: " + str(error)
    names = identifiers_of(tokens)
    denied = sorted({name for name in names if any(marker in name.upper() for marker in denied_markers)})
    if denied:
        return "FAIL", "copyleft: " + ", ".join(denied)
    unlisted = sorted({name for name in names if not is_allowed(name)})
    return "FAIL", "not on allowlist: " + ", ".join(unlisted)


def local_name(tag):
    """Element name without its XML namespace."""
    return tag.rsplit("}", 1)[-1]


def nuspec_license(package_id, version):
    """License fields of the cached .nuspec (expression, file, url), or None when the package is not in the cache."""
    path = os.path.join(cache_root, package_id.lower(), version.lower(), package_id.lower() + ".nuspec")
    if not os.path.isfile(path):
        return None
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError:
        return {}
    metadata = next((element for element in root if local_name(element.tag) == "metadata"), None)
    fields = {}
    for element in (metadata if metadata is not None else []):
        name = local_name(element.tag)
        text = (element.text or "").strip()
        if name == "license" and element.get("type") == "expression":
            fields["expression"] = text
        elif name == "license" and element.get("type") == "file":
            fields["file"] = text
        elif name == "licenseUrl":
            fields["url"] = text
    return fields


def identifier_from_url(url):
    """The SPDX identifier named by a well-known license URL, or None."""
    text = url.strip()
    # Known pages first: opensource.org also serves its MIT text at mit-license.php, which the id pattern below would read as an id.
    page = re.sub(r"^https?://(www\.)?", "", text.lower()).rstrip("/")
    if page in known_license_pages:
        return known_license_pages[page]
    for pattern in license_url_patterns:
        match = pattern.match(text)
        if match:
            return match.group(1)
    return None


def fetch_json(url):
    """GET a JSON document, decoding it when the body is gzip-compressed; retries transient failures."""
    last_error = None
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "Vocaterra.Nlp-license-scan"})
            with urllib.request.urlopen(request, timeout=30) as response:
                body = response.read()
            if body[:2] == b"\x1f\x8b":
                body = gzip.decompress(body)
            return json.loads(body.decode("utf-8"))
        except urllib.error.HTTPError as error:
            if error.code == 404:
                raise NotFound(url) from error
            last_error = error
        except (OSError, ValueError) as error:
            last_error = error
        if attempt < 2:
            time.sleep(2 * (attempt + 1))
    raise last_error


def registration_license(package_id, version):
    """licenseExpression and licenseUrl from the NuGet registration API (the catalog link is followed when needed)."""
    leaf = fetch_json(registration_url.format(id=package_id.lower(), version=version.lower()))
    entry = leaf.get("catalogEntry") or {}
    if isinstance(entry, str):
        entry = fetch_json(entry)
    return {"expression": entry.get("licenseExpression") or None, "url": entry.get("licenseUrl") or None}


def automatic_license(package_id, version):
    """Returns (license, source, note) from the nuspec or the NuGet API; the license is None when neither names one."""
    local = nuspec_license(package_id, version)
    if local and local.get("expression"):
        return local["expression"], "nuspec", ""
    if local and local.get("url") and identifier_from_url(local["url"]):
        return identifier_from_url(local["url"]), "nuspec-url", ""

    if local is None:
        local_note = "not in local NuGet cache"
    elif local.get("file"):
        local_note = "nuspec ships license file " + local["file"]
    elif local.get("url"):
        local_note = "nuspec licenseUrl not recognised: " + local["url"]
    else:
        local_note = "no license in nuspec"
    if offline:
        return None, "nuspec", local_note + " (offline)"

    try:
        remote = registration_license(package_id, version)
    except NotFound:
        return None, "api", local_note + "; not in NuGet registration API"
    except (OSError, ValueError) as error:
        return None, "api", local_note + "; NuGet API error: " + str(error)
    if remote["expression"]:
        return remote["expression"], "api", ""
    if remote["url"] and identifier_from_url(remote["url"]):
        return identifier_from_url(remote["url"]), "api-url", ""
    remote_note = "API licenseUrl not recognised: " + remote["url"] if remote["url"] else "API has no license"
    return None, "api", local_note + "; " + remote_note


def resolve_license(package_id, version):
    """Returns (license, source, note); a reviewed override is used only when the automatic lookup names no license."""
    license_text, source, note = automatic_license(package_id, version)
    override = overrides.get((package_id.lower(), version))
    if license_text is None and override is not None:
        return override["spdx"], "override", "reviewed " + override["reviewed"] + " " + override["url"]
    return license_text, source, note


with open(packages_path, encoding="utf-8") as handle:
    listing = json.load(handle)
packages = {}
for project in listing.get("projects", []):
    for framework in project.get("frameworks", []):
        for group in ("topLevelPackages", "transitivePackages"):
            for package in framework.get(group, []):
                version = package.get("resolvedVersion") or package.get("requestedVersion")
                packages.setdefault((package["id"].lower(), version), package["id"])

rows = []
for (_, version), package_id in sorted(packages.items()):
    license_text, source, note = resolve_license(package_id, version)
    if license_text is None:
        verdict = "UNKNOWN"
    else:
        verdict, check_note = evaluate(license_text)
        if verdict != "PASS":
            note = check_note
    rows.append((package_id, version, license_text or "-", source, verdict, note or "-"))

headers = ("PACKAGE", "VERSION", "LICENSE", "SOURCE", "STATUS", "NOTE")
widths = [max([len(headers[i])] + [len(row[i]) for row in rows]) for i in range(len(headers))]


def format_row(cells):
    """One table line with padded columns."""
    return "  ".join(cell.ljust(width) for cell, width in zip(cells, widths)).rstrip()


print(format_row(headers))
print(format_row(["-" * width for width in widths]))
for row in rows:
    print(format_row(row))

counts = {verdict: sum(1 for row in rows if row[4] == verdict) for verdict in ("PASS", "FAIL", "UNKNOWN")}
mode = "offline" if offline else "online"
print()
print(f"packages: {len(rows)}  pass: {counts['PASS']}  fail: {counts['FAIL']}  unknown: {counts['UNKNOWN']}  mode: {mode}")
failing = counts["FAIL"] + counts["UNKNOWN"]
print("Result: FAILED" if failing else "Result: PASSED")
sys.exit(1 if failing else 0)
PY
