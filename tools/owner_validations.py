#!/usr/bin/env python3
"""owner_validations.py - the DURABLE owner-validation record (shared by both seats).

THE DEFECT THIS REPLACES
    BOARD.html used to persist the owner's felt-test sign-offs in browser localStorage
    under a key scoped to the APK build id AND the source commit sha:

        eoa-owner-validation:<build>:<sha>

    Every commit therefore minted a NEW key, so every validation she had made was
    orphaned - not deleted, just stored where nothing would ever read it again. The
    CLI commits hourly. The person whose sign-off is the ONLY thing that closes a
    ticket (CLAUDE.md 13 - PO closes, never the CLI) had her sign-off pinned to a
    hash that changes hourly. Worse, the record lived in her browser, so the CLI
    could not SEE it at all: tools/board_close_validated.py exists solely to copy
    Chrome's LevelDB out of her user profile and regex-salvage JSON fragments from
    the raw bytes - which works on exactly one desktop browser and never on the
    phone she actually validates from.

THE RECORD
    proof/owner-validations.json - committed, human-readable, diffable.

    proof/ is chosen because it is already the repo's tracked evidence directory,
    and a felt-test sign-off IS evidence. It is data, never a derived view: the
    board READS it and no code path here rewrites it during a board rebuild.

    Shape:
        {
          "_schema": 1,
          "_readme": [ ...why this file exists, for whoever opens it cold... ],
          "validations": {
            "WORK_ORDER_1234_slug.md": {"validated": true, "verdict": "Pass",
                                        "note": "", "at": "2026-09-03T09:12:44",
                                        "build": "2026.09.03.353742"}
          }
        }

    Keyed by WO FILENAME, matching the key BOARD.html already used, because this
    repo has historical duplicate WO numbers - the friendly label "WO-812" can name
    two unrelated files, and keying on it would make them share one sign-off.

    Written one ticket per LINE inside "validations", keys sorted. That is what
    makes it safe to merge: two seats validating different tickets touch different
    lines, so git resolves it without a human, and a conflict (same ticket, two
    verdicts) is a real disagreement that SHOULD stop and be read.

BUILD SCOPING - the decision, and why (this is the load-bearing call)
    A validation is NOT build-scoped. It is keyed by ticket alone.

    The old code's comment argued the other side: a felt-test RESULT belongs to one
    APK. That is true of a *measurement* and false of a *sign-off*. What the owner
    records is "the wolf routes correctly now" - a judgement about a FIX, not about a
    binary. The fix does not stop being correct because the CLI committed a doc
    change, and it does not stop being correct because a new APK was built for an
    unrelated lane. Under the old rule she had to re-test every ticket after every
    commit, which is precisely the cost that makes a person stop marking anything -
    and a sign-off mechanism nobody uses closes zero tickets.

    Provenance is kept WITHOUT scoping the key: each entry records the "build" and
    "at" it was made on. So nothing is lost - a seat that wants to know whether a
    sign-off predates the current APK can read it off the entry and ask her to
    re-check that one ticket. That is the strictly better trade: durable by default,
    with the staleness question answerable per-ticket instead of being force-answered
    "everything is stale" once an hour.
"""
from __future__ import annotations

import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Overridable so the round-trip self-check can exercise the real read path against a
# temp file instead of the owner's live record. Tests must never be able to corrupt
# evidence in order to prove that evidence survives.
PATH = os.environ.get("EOA_VALIDATIONS_PATH") or os.path.join(
    ROOT, "proof", "owner-validations.json")

SCHEMA = 1

README = [
    "OWNER FELT-TEST SIGN-OFFS. This file is DATA, not a derived view.",
    "BOARD.html reads it; tools/board_build.py never rewrites it on a rebuild.",
    "Keyed by work-order FILENAME (this repo has duplicate WO numbers).",
    "NOT build-scoped: a sign-off is a judgement about a fix, not about one APK.",
    "Each entry keeps 'build' and 'at' as provenance so staleness stays answerable.",
    "One ticket per line, keys sorted - so two seats merge without a human.",
    "How a sign-off gets here from her phone: BOARD.html > Owner Validation >",
    "  'Export for the CLI' > Copy, hand the text to the CLI, which runs",
    "  python tools/board_build.py --ingest -   (paste)  or --ingest <file>.",
    "Closing a ticket is still the owner's act: tools/board_close_validated.py.",
]

VERDICTS = ("", "Pass", "Fail", "Needs Work")


class ValidationsUnreadable(Exception):
    """The record exists but could not be parsed. NEVER silently treated as empty."""


def _blank():
    return {"_schema": SCHEMA, "_readme": list(README), "validations": {}}


def load(path=None):
    """Return the record. Missing file -> a blank record. Corrupt file -> raises.

    A corrupt record must NEVER read as "no validations": that would render an
    empty board, look normal, and invite a rebuild that buries the damage. The
    caller is expected to report it loudly and leave the bytes untouched.
    """
    p = path or PATH
    if not os.path.exists(p):
        return _blank()
    try:
        with open(p, encoding="utf-8") as f:
            data = json.load(f)
    except Exception as e:
        raise ValidationsUnreadable(f"{p}: {type(e).__name__}: {e}") from e
    if not isinstance(data, dict) or not isinstance(data.get("validations"), dict):
        raise ValidationsUnreadable(f"{p}: expected an object with a 'validations' object")
    return data


def entries(path=None):
    """Just the {filename: state} map."""
    return load(path).get("validations", {})


def normalize(state):
    """Coerce one incoming entry to the stored shape, dropping anything unknown."""
    if not isinstance(state, dict):
        return None
    verdict = state.get("verdict") or ""
    if verdict not in VERDICTS:
        verdict = ""
    out = {
        "validated": bool(state.get("validated")),
        "verdict": verdict,
        "note": str(state.get("note") or "")[:400],
    }
    for k in ("at", "build"):
        v = state.get(k)
        if v:
            out[k] = str(v)[:64]
    return out


def merge(existing, incoming):
    """Merge incoming entries into existing. Returns (merged, changed_keys).

    Newest-'at' wins, and an entry with no 'at' loses to one that has it - so two
    devices reconcile deterministically instead of by whoever pasted last.
    """
    merged = dict(existing)
    changed = []
    for key, raw in sorted((incoming or {}).items()):
        if not isinstance(key, str) or not key.endswith(".md"):
            continue
        new = normalize(raw)
        if new is None:
            continue
        old = merged.get(key)
        if old == new:
            continue
        if old is not None and old.get("at") and new.get("at", "") < old["at"]:
            continue  # stale paste; the record already holds a newer sign-off
        merged[key] = new
        changed.append(key)
    return merged, changed


def dumps(data):
    """Serialize with ONE LINE PER TICKET, keys sorted - the diff/merge contract."""
    vals = data.get("validations", {})
    lines = ['{', f'  "_schema": {json.dumps(data.get("_schema", SCHEMA))},',
             '  "_readme": [']
    readme = data.get("_readme") or README
    for i, line in enumerate(readme):
        lines.append(f'    {json.dumps(line)}{"" if i == len(readme) - 1 else ","}')
    lines.append('  ],')
    lines.append('  "validations": {')
    keys = sorted(vals)
    for i, key in enumerate(keys):
        body = json.dumps(vals[key], sort_keys=True, separators=(", ", ": "))
        lines.append(f'    {json.dumps(key)}: {body}{"" if i == len(keys) - 1 else ","}')
    lines.append('  }')
    lines.append('}')
    return "\n".join(lines) + "\n"


def save(data, path=None):
    """Write the record atomically, LF endings, no BOM."""
    p = path or PATH
    os.makedirs(os.path.dirname(p), exist_ok=True)
    tmp = p + ".tmp"
    with open(tmp, "w", encoding="utf-8", newline="\n") as f:
        f.write(dumps(data))
    os.replace(tmp, p)
    return p


def ingest(incoming, path=None):
    """Merge a pasted/exported payload into the record. Returns (changed, total).

    Accepts either the full file shape ({"validations": {...}}) or a bare
    {filename: state} map, because a human pasting from a phone should not have to
    get the wrapper right.
    """
    if isinstance(incoming, dict) and isinstance(incoming.get("validations"), dict):
        incoming = incoming["validations"]
    data = load(path)
    merged, changed = merge(data.get("validations", {}), incoming)
    data["_schema"] = SCHEMA
    data["_readme"] = list(README)
    data["validations"] = merged
    save(data, path)
    return changed, merged
