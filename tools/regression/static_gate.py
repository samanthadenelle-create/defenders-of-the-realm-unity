#!/usr/bin/env python3
# =============================================================================
# static_gate.py - UI/Cowork check-in gate (NO Unity required).
# -----------------------------------------------------------------------------
# WO-329. Runs entirely in the Linux sandbox (or any machine with Python 3) and
# performs the static checks that do NOT need a Unity editor:
#
#   1. BRACE BALANCE  - every .cs file (or a passed file list) must have an
#                       equal number of code-level '{' and '}' (CLAUDE.md §1).
#                       Braces inside comments / strings are ignored, so
#                       interpolation like $"{x}/{y}" never false-positives.
#   2. BRIDGE REFLECTION - no NEW `System.Reflection` use in *Bridge*.cs files
#                       (CLAUDE.md §10). A frozen baseline grandfathers the
#                       bridges that already use it; only NEW offenders fail.
#   3. IDamageableStructure USING - any type that *implements*
#                       IDamageableStructure must `using DeNelle.Core.Combat;`
#                       (or fully-qualify it). Comment mentions are ignored.
#   4. ASMDEF BOUNDARY - DeNelle.Village and DeNelle.HUD must never reference
#                       each other (CLAUDE.md §5 cross-assembly rule).
#   5. CANONICAL JSON  - every *.json under the canonical data dirs must parse.
#
# Exit code 0 = clean, non-zero = at least one HARD failure.
#
# Usage:
#   python3 tools/regression/static_gate.py                # scan whole repo
#   python3 tools/regression/static_gate.py FILE.cs ...    # brace-check only the
#                                                          # listed .cs files
#                                                          # (other checks stay
#                                                          #  repo-wide)
#   python3 tools/regression/static_gate.py --root /path/to/repo
# =============================================================================

import json
import os
import re
import sys

# --- repo root ---------------------------------------------------------------
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, os.pardir, os.pardir))

# Directories we never scan for .cs (generated / third-party build output).
SKIP_DIRS = {"Library", "Temp", "obj", "Build", "Builds", "Logs", ".git", ".vs"}

# Bridge files that ALREADY use System.Reflection as of WO-329. These are
# grandfathered: the gate only fails on NEW *Bridge*.cs that introduce it.
# (Path is repo-relative, forward slashes.) Keep this list frozen; if a real
# refactor removes reflection from one of these, deleting its entry is fine.
REFLECTION_BRIDGE_BASELINE = {
    "Assets/_Modules/Pets/MineNodeBridge.cs",
    "Assets/_Modules/Pets/PetAttackVfxBridge.cs",
    "Assets/_Modules/Village/Buildings/BuildMenuHudBridge.cs",
    "Assets/_Modules/Village/BuildMode/BuildButtonBridge.cs",
    "Assets/_Modules/Village/Heart/HeartHudBridge.cs",
    "Assets/_Modules/Village/Hero/HeroAbilitiesHudBridge.cs",
    "Assets/_Modules/Village/NPCs/PartyHudBridge.cs",
    "Assets/_Modules/Village/Walls/WallRepairHudBridge.cs",
}

# Mutually-exclusive assembly pairs (neither may reference the other).
ASMDEF_FORBIDDEN_PAIRS = [
    ("DeNelle.Village", "DeNelle.HUD"),
]

# Canonical JSON dirs (repo-relative). Both are scanned if present.
CANONICAL_DIRS = [
    "Assets/Data/Canonical",
    "Assets/StreamingAssets/Data/Canonical",
]

# A type that *implements* IDamageableStructure: a class/struct whose base list
# contains it. Comment-only mentions never match (no "class/struct Name ... :").
IMPL_RE = re.compile(
    r"\b(?:class|struct)\s+\w[\w<>,\.\s]*?:\s*[^{;]*\bIDamageableStructure\b",
    re.MULTILINE,
)


def rel(root, path):
    return os.path.relpath(path, root).replace("\\", "/")


def iter_cs_files(root):
    base = os.path.join(root, "Assets")
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if fn.endswith(".cs"):
                yield os.path.join(dirpath, fn)


def read(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        return fh.read()


# --- 1) brace balance --------------------------------------------------------
def code_brace_balance(text):
    """Count '{' / '}' that live in actual CODE - ignoring braces inside
    line/block comments, char literals, and string literals (regular, verbatim,
    interpolated, verbatim-interpolated). A naive text.count() trips on
    `$"{x}/{y}"` and `"{0:N0}"`; this scanner does not. Returns (opens, closes).
    """
    n = len(text)
    counts = [0, 0]  # opens, closes

    def skip_char(i):
        i += 1  # past opening '
        while i < n:
            if text[i] == "\\":
                i += 2
                continue
            if text[i] == "'":
                return i + 1
            i += 1
        return i

    def string_prefix(i):
        # Returns (prefix_len_incl_quote, interp, verbatim) or None.
        c = text[i]
        if c == '"':
            return (1, False, False)
        if c in "$@":
            j = i
            interp = verbatim = False
            while j < n and text[j] in "$@":
                if text[j] == "$":
                    interp = True
                else:
                    verbatim = True
                j += 1
            if j < n and text[j] == '"':
                return (j - i + 1, interp, verbatim)
        return None

    def scan_code(i, in_hole):
        # When in_hole, returns just after the '}' closing the current
        # interpolation hole (depth-aware so nested braces are honored).
        depth = 0
        while i < n:
            c = text[i]
            nxt = text[i + 1] if i + 1 < n else ""
            if c == "/" and nxt == "/":
                j = text.find("\n", i)
                i = n if j < 0 else j
                continue
            if c == "/" and nxt == "*":
                j = text.find("*/", i + 2)
                i = n if j < 0 else j + 2
                continue
            if c == "'":
                i = skip_char(i)
                continue
            pre = string_prefix(i)
            if pre:
                plen, interp, verbatim = pre
                i = scan_string(i + plen, interp, verbatim)
                continue
            if c == "{":
                counts[0] += 1
                depth += 1
                i += 1
                continue
            if c == "}":
                counts[1] += 1
                i += 1
                if in_hole and depth == 0:
                    return i
                depth -= 1
                continue
            i += 1
        return i

    def scan_string(i, interp, verbatim):
        # i points just past the opening quote.
        while i < n:
            c = text[i]
            nxt = text[i + 1] if i + 1 < n else ""
            if verbatim:
                if c == '"' and nxt == '"':
                    i += 2
                    continue
                if c == '"':
                    return i + 1
            else:
                if c == "\\":
                    i += 2
                    continue
                if c == '"':
                    return i + 1
            if interp:
                if c == "{" and nxt == "{":
                    i += 2
                    continue
                if c == "}" and nxt == "}":
                    i += 2
                    continue
                if c == "{":
                    counts[0] += 1               # hole-open brace
                    i = scan_code(i + 1, True)   # consumes closing '}' (counted there)
                    continue
            i += 1
        return i

    scan_code(0, False)
    return counts[0], counts[1]


def check_braces(files, root):
    failures = []
    for path in files:
        if not os.path.isfile(path):
            failures.append((rel(root, path), "file not found"))
            continue
        opens, closes = code_brace_balance(read(path))
        if opens != closes:
            failures.append((rel(root, path), f"{opens} open vs {closes} close (code braces)"))
    return failures


# --- 2) bridge reflection ----------------------------------------------------
def check_bridge_reflection(cs_files, root):
    failures = []
    for path in cs_files:
        if "Bridge" not in os.path.basename(path):
            continue
        relpath = rel(root, path)
        if relpath in REFLECTION_BRIDGE_BASELINE:
            continue
        if "System.Reflection" in read(path):
            failures.append((relpath, "new System.Reflection in a bridge script"))
    return failures


# --- 3) IDamageableStructure using -------------------------------------------
def check_idamageable_using(cs_files, root):
    failures = []
    for path in cs_files:
        if os.path.basename(path) == "IDamageableStructure.cs":
            continue
        text = read(path)
        if "IDamageableStructure" not in text:
            continue
        if not IMPL_RE.search(text):
            continue  # only references it (e.g. in a comment) - not an implementer
        has_using = "using DeNelle.Core.Combat;" in text
        fully_qualified = "DeNelle.Core.Combat.IDamageableStructure" in text
        if not has_using and not fully_qualified:
            failures.append(
                (rel(root, path), "implements IDamageableStructure without "
                                  "`using DeNelle.Core.Combat;`")
            )
    return failures


# --- 4) asmdef boundary ------------------------------------------------------
def check_asmdef_boundaries(root):
    failures = []
    asmdefs = {}  # name -> (relpath, references list)
    base = os.path.join(root, "Assets")
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if not fn.endswith(".asmdef"):
                continue
            path = os.path.join(dirpath, fn)
            try:
                data = json.loads(read(path))
            except Exception as exc:  # noqa: BLE001
                failures.append((rel(root, path), f"invalid asmdef JSON: {exc}"))
                continue
            name = data.get("name", "")
            refs = data.get("references", []) or []
            asmdefs[name] = (rel(root, path), list(refs))

    for a, b in ASMDEF_FORBIDDEN_PAIRS:
        for x, y in ((a, b), (b, a)):
            if x in asmdefs and y in asmdefs[x][1]:
                failures.append((asmdefs[x][0], f"{x} must not reference {y}"))
    return failures


# --- 5) canonical JSON -------------------------------------------------------
def check_canonical_json(root):
    failures = []
    checked = 0
    for d in CANONICAL_DIRS:
        full = os.path.join(root, d)
        if not os.path.isdir(full):
            continue
        for dirpath, _dirnames, filenames in os.walk(full):
            for fn in filenames:
                if not fn.endswith(".json"):
                    continue
                path = os.path.join(dirpath, fn)
                try:
                    json.loads(read(path))
                    checked += 1
                except Exception as exc:  # noqa: BLE001
                    failures.append((rel(root, path), f"invalid JSON: {exc}"))
    return failures, checked


def report(failures):
    if failures:
        print(f"  FAIL ({len(failures)}):")
        for relpath, why in failures:
            print(f"    x {relpath}: {why}")
    else:
        print("  ok")
    return len(failures)


def main(argv):
    root = DEFAULT_ROOT
    explicit_files = []
    args = list(argv)
    i = 0
    while i < len(args):
        if args[i] == "--root":
            root = os.path.abspath(args[i + 1])
            i += 2
            continue
        explicit_files.append(args[i])
        i += 1

    root = os.path.abspath(root)
    if not os.path.isdir(os.path.join(root, "Assets")):
        print(f"ERROR: '{root}' does not look like the repo root (no Assets/).")
        return 2

    print("=" * 70)
    print("DEFENDERS OF THE REALM - STATIC CHECK-IN GATE (no Unity)")
    print(f"repo root: {root}")
    print("=" * 70)

    all_cs = list(iter_cs_files(root))
    if explicit_files:
        brace_targets = [os.path.abspath(f) for f in explicit_files]
        print(f"\n(brace check restricted to {len(brace_targets)} passed file(s))")
    else:
        brace_targets = all_cs

    total_fail = 0
    n_bridge = sum("Bridge" in os.path.basename(f) for f in all_cs)

    print(f"\n--- 1. Brace balance ({len(brace_targets)} .cs files) ---")
    total_fail += report(check_braces(brace_targets, root))

    print(f"\n--- 2. Bridge reflection ({n_bridge} bridge files) ---")
    total_fail += report(check_bridge_reflection(all_cs, root))

    print("\n--- 3. IDamageableStructure using-directive ---")
    total_fail += report(check_idamageable_using(all_cs, root))

    print("\n--- 4. Asmdef boundaries (Village <-> HUD) ---")
    total_fail += report(check_asmdef_boundaries(root))

    json_failures, json_count = check_canonical_json(root)
    print(f"\n--- 5. Canonical JSON validity ({json_count} files parsed) ---")
    total_fail += report(json_failures)

    print("\n" + "=" * 70)
    if total_fail == 0:
        print("STATIC GATE: PASS - all checks clean.")
        print("=" * 70)
        return 0
    print(f"STATIC GATE: FAIL - {total_fail} problem(s) above must be fixed.")
    print("=" * 70)
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
