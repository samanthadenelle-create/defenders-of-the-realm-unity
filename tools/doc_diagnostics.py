#!/usr/bin/env python3
# doc_diagnostics.py -- Canon freshness health check (owner-runnable, stdlib only).
#
# WHY: CLAUDE.md sec.15 pain -- load-bearing docs drift behind reality and nobody
# notices until a fleet-scale audit. This is the 5-minute catch: run it, read the
# ranked ASCII report, fix or flag whatever is STALE. It is a DIAGNOSTIC, not a gate
# -- it never fails a build (always exits 0). Colorblind-safe: severity is TEXT
# (STALE / WARN / OK), never color.
#
# HOW TO RUN (from the repo root -- machine-dependent path, do not hardcode a drive):
#     python3 tools/doc_diagnostics.py
#     py -3 tools\doc_diagnostics.py        (Windows launcher)
#
# It reads the newest CANON_GROUND_TRUTH_*.md as the anchor of current reality and
# checks the load-bearing doc set against it.

import os
import re
import sys
import glob

# ----------------------------------------------------------------------------
# Config -- the load-bearing set (CLAUDE.md sec.15 "keep these green").
# ----------------------------------------------------------------------------
LOAD_BEARING = [
    "START_HERE.md",
    "KEY_FACTS.md",
    "SESSION_CANON_LOADER.md",
    "docs/HANDOVER.md",
    "PIPELINE_STATE.md",
    "docs/MASTER_CATALOG.md",
    "docs/ARCHITECTURE.md",
    "PROJECT_INDEX.md",
    "CLAUDE.md",
]
LANES_AUTHORITY = "CLI_LANES_WO_NUMBERS.md"
WORKORDER_GLOB = "WorkOrders/WORK_ORDER_*.md"

DATE_RE = re.compile(r"20\d\d-[01]\d-[0-3]\d")
ANCHOR_NAME_RE = re.compile(r"CANON_GROUND_TRUTH_(20\d\d-[01]\d-[0-3]\d)\.md")
# any anchor filename cited inside a doc body
ANCHOR_CITE_RE = re.compile(r"CANON_GROUND_TRUTH_(20\d\d-[01]\d-[0-3]\d)")
# save-schema version tokens
SCHEMA_TOKEN_RE = re.compile(r"\b(?:save\s+schema|schema)\s*=?\s*v?(\d{1,3})\b", re.I)
SCHEMA_CURVER_RE = re.compile(r"CurrentVersion\s*=?\s*(\d{1,3})", re.I)
SCHEMA_VTOKEN_RE = re.compile(r"\bv(2\d|3\d)\b")  # v20..v39, the plausible schema band
STALE_MARK_RE = re.compile(r"\bSTALE\s*:", re.I)
# "next free" WO number in the lanes authority (prefer the refreshed banner)
NEXTFREE_RE = re.compile(r"next\s+free\s+WO\s*=\s*\*{0,2}(\d{2,4})", re.I)

SEVERITY_ORDER = {"STALE": 0, "WARN": 1, "OK": 2}


def asciify(s):
    # Guarantee ASCII-only output (owner console is cp1252; doc bodies carry
    # em-dashes / arrows). Non-ASCII -> '?'. Keeps the report portable.
    return (s or "").encode("ascii", "replace").decode("ascii")


def repo_root():
    # tools/doc_diagnostics.py -> repo root is one dir up.
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.abspath(os.path.join(here, ".."))
    # allow running from anywhere that has the anchor
    if glob.glob(os.path.join(root, "CANON_GROUND_TRUTH_*.md")):
        return root
    return os.getcwd()


def read(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except OSError:
        return None


def newest_anchor(root):
    best_path, best_date = None, None
    for p in glob.glob(os.path.join(root, "CANON_GROUND_TRUTH_*.md")):
        m = ANCHOR_NAME_RE.search(os.path.basename(p))
        if not m:
            continue
        d = m.group(1)
        if best_date is None or d > best_date:
            best_date, best_path = d, p
    return best_path, best_date


def newest_date_in(text):
    dates = DATE_RE.findall(text or "")
    return max(dates) if dates else None


def anchor_schema_version(text):
    # Prefer an explicit "Save schema = vNN"; fall back to the max v2x/v3x token.
    m = SCHEMA_TOKEN_RE.search(text or "")
    if m:
        return int(m.group(1))
    cands = [int(x) for x in SCHEMA_VTOKEN_RE.findall(text or "")]
    return max(cands) if cands else None


def line_context(text, needle_regex, limit=8):
    out = []
    for i, line in enumerate((text or "").splitlines(), 1):
        if needle_regex.search(line):
            out.append((i, line.strip()))
            if len(out) >= limit:
                break
    return out


def main():
    root = repo_root()
    findings = []  # (severity, category, message)

    def add(sev, cat, msg):
        findings.append((sev, cat, msg))

    anchor_path, anchor_date = newest_anchor(root)
    if not anchor_path:
        print("STALE  no CANON_GROUND_TRUTH_*.md anchor found under", root)
        print("       cannot run freshness check -- is this the repo root?")
        return 0

    anchor_text = read(anchor_path) or ""
    anchor_ver = anchor_schema_version(anchor_text)
    anchor_base = os.path.basename(anchor_path)

    print("=" * 74)
    print("  CANON DOC-FRESHNESS DIAGNOSTIC")
    print("  repo root : %s" % root)
    print("  anchor    : %s  (date %s)" % (anchor_base, anchor_date))
    print("  schema    : %s" % ("v%d" % anchor_ver if anchor_ver else "UNKNOWN"))
    print("=" * 74)

    # --- per-file checks -----------------------------------------------------
    for rel in LOAD_BEARING:
        path = os.path.join(root, rel.replace("/", os.sep))
        text = read(path)
        if text is None:
            add("WARN", "MISSING", "%s -- listed load-bearing doc not found" % rel)
            continue

        # 1) newest date-stamp older than the anchor
        d = newest_date_in(text)
        if d and anchor_date and d < anchor_date and rel != "CLAUDE.md":
            # CLAUDE.md is rules (not a dated state snapshot); note it softly only.
            add("STALE", "OLD-STAMP",
                "%s newest date-stamp %s < anchor %s (state may predate reality)"
                % (rel, d, anchor_date))

        # 2) STALE: markers already present (list them -- these are self-flags)
        for ln, txt in line_context(text, STALE_MARK_RE, limit=6):
            snippet = txt[:110] + ("..." if len(txt) > 110 else "")
            add("WARN", "SELF-FLAG", "%s:%d %s" % (rel, ln, snippet))

        # 3) superseded anchor cited (older CANON_GROUND_TRUTH still referenced)
        cited = set(ANCHOR_CITE_RE.findall(text))
        for cd in sorted(cited):
            if anchor_date and cd < anchor_date:
                # WARN unless the citing lines all mark it superseded/stale/history
                lines = [t for _, t in line_context(
                    text, re.compile(r"CANON_GROUND_TRUTH_" + re.escape(cd)), limit=12)]
                guarded = all(
                    re.search(r"supersed|stale|history|earlier|old", t, re.I)
                    for t in lines) if lines else False
                if not guarded:
                    add("STALE", "OLD-ANCHOR",
                        "%s cites CANON_GROUND_TRUTH_%s as live (anchor is %s)"
                        % (rel, cd, anchor_date))
                else:
                    add("OK", "OLD-ANCHOR",
                        "%s cites %s but marks it superseded (fine)" % (rel, cd))

        # 4) save-schema version -- compare the doc's HIGHEST version claim (its
        #    idea of "current") against the anchor. Lower migration-path numbers
        #    (e.g. "v1 -> v29") are legitimate and not flagged individually.
        if anchor_ver:
            seen = set(int(x) for x in SCHEMA_VTOKEN_RE.findall(text))
            seen |= set(int(x) for x in SCHEMA_CURVER_RE.findall(text))
            seen = set(v for v in seen if 20 <= v <= 39)
            if seen:
                file_max = max(seen)
                if file_max < anchor_ver:
                    add("STALE", "SCHEMA",
                        "%s newest schema claim v%d < anchor v%d (behind)"
                        % (rel, file_max, anchor_ver))
                elif file_max > anchor_ver:
                    add("WARN", "SCHEMA",
                        "%s claims schema v%d > anchor v%d (ahead -- reconcile)"
                        % (rel, file_max, anchor_ver))

    # --- WO numbering: disk count vs lanes-authority next-free ---------------
    wo_files = glob.glob(os.path.join(root, WORKORDER_GLOB.replace("/", os.sep)))
    # ignore *.RESULT.md and *_SUPERSEDED.md for the max-number scan
    disk_nums = []
    for p in wo_files:
        m = re.search(r"WORK_ORDER_(\d+)", os.path.basename(p))
        if m:
            disk_nums.append(int(m.group(1)))
    disk_max = max(disk_nums) if disk_nums else 0
    disk_count = len(set(disk_nums))

    lanes_text = read(os.path.join(root, LANES_AUTHORITY))
    if lanes_text is None:
        add("WARN", "WO-NUM", "%s not found -- cannot check next-free claim"
            % LANES_AUTHORITY)
    else:
        claims = [int(x) for x in NEXTFREE_RE.findall(lanes_text)]
        # The refreshed banner is usually the highest; the stale in-body line lower.
        next_free = max(claims) if claims else None
        if next_free is None:
            add("WARN", "WO-NUM",
                "%s has no parseable 'next free WO =' -- add the banner"
                % LANES_AUTHORITY)
        elif next_free <= disk_max:
            add("STALE", "WO-NUM",
                "%s next-free=%d but disk WO max=%d (authority behind disk by %d)"
                % (LANES_AUTHORITY, next_free, disk_max, disk_max - next_free + 1))
        else:
            add("OK", "WO-NUM",
                "%s next-free=%d > disk max=%d (%d WO files on disk)"
                % (LANES_AUTHORITY, next_free, disk_max, disk_count))
        # also flag any low stale in-body claims well behind disk
        stale_claims = sorted(c for c in claims if c < disk_max - 50)
        if stale_claims:
            add("WARN", "WO-NUM",
                "%s also carries stale in-body next-free claim(s): %s (disk max %d)"
                % (LANES_AUTHORITY,
                   ", ".join(str(c) for c in stale_claims), disk_max))

    # --- ranked report -------------------------------------------------------
    findings.sort(key=lambda f: (SEVERITY_ORDER.get(f[0], 3), f[1]))
    counts = {"STALE": 0, "WARN": 0, "OK": 0}
    for sev, _, _ in findings:
        counts[sev] = counts.get(sev, 0) + 1

    print()
    print("RANKED FINDINGS  (STALE = fix now, WARN = review, OK = confirmed clean)")
    print("-" * 74)
    if not findings:
        print("OK     no findings -- load-bearing canon is aligned with the anchor.")
    for sev, cat, msg in findings:
        print(asciify("%-6s [%-10s] %s" % (sev, cat, msg)))

    print("-" * 74)
    print("SUMMARY: %d STALE, %d WARN, %d OK  |  %d WO files on disk (max #%d)"
          % (counts.get("STALE", 0), counts.get("WARN", 0),
             counts.get("OK", 0), disk_count, disk_max))
    print("Diagnostic only -- exit 0. Fix STALE items or add a top-of-file STALE: flag.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
