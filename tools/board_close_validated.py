#!/usr/bin/env python3
"""Bounce owner Fail / Needs Work marks, and (legacy) salvage marks from Chrome.

⛔ THE CLOSE HALF OF THIS SCRIPT NO LONGER LIVES HERE (WO-1355, owner ruling 2026-09-03).
    "when you do board next you flip all passed and validated to closed" - so the
    Pass -> CLOSED pass is now part of `python tools/board_build.py` itself, in
    tools/board_close_pass.py, and this script CALLS that one module rather than keeping a
    second copy of the rules. There is exactly ONE implementation of a status rewrite; a
    second copy would drift, and a drifted copy rewrites the owner's tickets.

⛔ AND THE BOUNCE HALF LEFT TOO (WO-1356, owner ruling 2026-09-03).
    "move the needs work and failed back to ready with a note" - so the bounce is ALSO
    part of `python tools/board_build.py` now, implemented once in
    tools/board_close_pass.py (bounce_pass / run_bounce). The header above said the
    bounce "stays an explicit command"; that stopped being true the moment the same
    reasoning that moved the close applied to it - a routing step that only happens when
    a seat remembers a second script is a routing step that does not happen.

WHAT IS STILL THIS SCRIPT'S OWN JOB
    Nothing but the LEGACY Chrome-LevelDB salvage below, plus a thin adapter (apply())
    from the salvage's (verdict, note) tuples onto that one bounce implementation. If the
    durable record has the marks - which it does, once she taps Submit - you never need
    this script at all: just run `python tools/board_build.py --submit`.

PRIMARY SOURCE: proof/owner-validations.json - the durable, committed record (see
tools/owner_validations.py).

FALLBACK, kept only for marks that never made it into the record: copy Chrome's
LevelDB out of the user profile and regex-salvage JSON fragments from the raw bytes.
That hack existed because the board's sign-offs lived in browser localStorage under a
per-commit key and the CLI had no other way to see them. It works on exactly one
desktop browser and never on the phone the owner actually validates from, which is
why the record now exists. Do not extend it; extend the record.
"""
from __future__ import annotations

import glob
import os
import re
import shutil
import subprocess
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import owner_validations
import board_close_pass

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WO_DIR = board_close_pass.WO_DIR   # honours EOA_WO_DIR, same as the board build
LS_DST = os.path.join(ROOT, "tmp", "chrome-ls")
CHROME_LS = os.path.join(
    os.path.expanduser("~"),
    r"AppData\Local\Google\Chrome\User Data\Default\Local Storage\leveldb",
)


# NOTE: first_status/set_status used to live here. They are now board_close_pass
# .read_status/.write_status - one implementation of "rewrite a **Status:** line", shared
# with the board build. Two copies would drift, and a drifted copy rewrites live tickets.


def live_key() -> str:
    html = open(os.path.join(ROOT, "BOARD.html"), encoding="utf-8", errors="replace").read()
    m = re.search(r"const validationKey='(eoa-owner-validation:[^']+)'", html)
    return m.group(1) if m else ""


def copy_ls() -> int:
    os.makedirs(LS_DST, exist_ok=True)
    for p in glob.glob(os.path.join(LS_DST, "*")):
        try:
            os.remove(p)
        except OSError:
            pass
    n = 0
    if not os.path.isdir(CHROME_LS):
        print("NO_CHROME_LS")
        return 0
    for p in glob.glob(os.path.join(CHROME_LS, "*")):
        if not os.path.isfile(p):
            continue
        try:
            shutil.copy2(p, os.path.join(LS_DST, os.path.basename(p)))
            n += 1
        except OSError as e:
            print("skip", os.path.basename(p), type(e).__name__)
    print("copied", n)
    return n


def salvage(key: str) -> dict[str, tuple[str, str]]:
    parts = []
    for p in glob.glob(os.path.join(LS_DST, "*")):
        if os.path.isfile(p):
            parts.append(open(p, "rb").read())
    blob = b"".join(parts)
    needle = key.encode("ascii", "replace")
    idxs = []
    i = 0
    while True:
        j = blob.find(needle, i)
        if j < 0:
            break
        idxs.append(j)
        i = j + 1
    print("key copies", len(idxs), "needle", key)
    if not idxs:
        return {}
    win = b"".join(blob[k : k + 90000] for k in idxs)
    text = re.sub(rb"[^\x20-\x7e]", b" ", win).decode("ascii")
    found: dict[str, tuple[str, str]] = {}
    # Prefer explicit JSON-ish fragments: "FILE":{"verdict":"Pass"
    for m in re.finditer(
        r'"(WORK_ORDER_[A-Za-z0-9_.\-]+\.md)"\s*:\s*\{\s*"verdict"\s*:\s*"(Pass|Fail|Needs Work)"'
        r'(?:.*?"note"\s*:\s*"([^"]{0,240})")?',
        text,
    ):
        name, v, note = m.group(1), m.group(2), m.group(3) or ""
        found[name] = (v, note)
    print("parsed", len(found), dict(Counter(v for v, _ in found.values())))
    return found


def durable() -> dict[str, tuple[str, str]]:
    """Verdicts from the committed record. This is the source that survives a commit."""
    out: dict[str, tuple[str, str]] = {}
    try:
        entries = owner_validations.entries()
    except owner_validations.ValidationsUnreadable as e:
        print("VALIDATIONS_PARSE_FAIL", e)
        return out
    for name, state in sorted(entries.items()):
        verdict = (state or {}).get("verdict")
        if verdict in ("Pass", "Fail", "Needs Work"):
            out[name] = (verdict, (state or {}).get("note") or "")
    print("record verdicts", len(out), dict(Counter(v for v, _ in out.values())))
    return out


def apply(found: dict[str, tuple[str, str]]) -> list[str]:
    """Bounce Fail / Needs Work back to READY, carrying her note into the ticket.

    THIS IS NOT A SECOND BOUNCER (WO-1356). The rules, the note sanitising, the
    idempotency and the PRIOR STATUS: preservation all live in ONE place -
    board_close_pass.bounce_pass - beside the close, because both are the same
    dangerous act: rewriting a **Status:** line from a data file. This function only
    ADAPTS the legacy (verdict, note) tuple shape that durable()/salvage() produce
    into the record's entry shape and hands it over. The `board_build.py` run at the
    end of main() performs the same bounce itself for the normal path, so a seat that
    never runs this script still gets the routing.
    """
    entries = {name: {"verdict": verdict, "note": note}
               for name, (verdict, note) in found.items()}
    _ok, res = board_close_pass.run_bounce(entries=entries)
    return [name for name, _verdict, _note in (res or {}).get("bounced", [])]


def main() -> int:
    os.chdir(ROOT)
    # The durable record first. The LevelDB scrape runs ONLY when the record is empty,
    # so a normal run never depends on a browser profile being present.
    found = durable()
    if not found:
        print("RECORD_EMPTY - falling back to the Chrome LevelDB salvage")
        copy_ls()
        key = live_key()
        if not key:
            print("NO_VALIDATION_KEY")
            return 2
        found = salvage(key)
    bounced = apply(found)
    # The close is deliberately NOT re-implemented here: board_build.py runs
    # board_close_pass itself, so this subprocess both closes and redraws.
    subprocess.check_call([sys.executable, os.path.join(ROOT, "tools", "board_build.py")])
    print("BOARD_PASS_OK bounced", len(bounced), "- closes are reported by BOARD_CLOSE_OK above")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
