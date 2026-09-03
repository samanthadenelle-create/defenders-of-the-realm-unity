#!/usr/bin/env python3
"""board_close_pass.py - flip owner Pass-validated FIXED tickets to CLOSED.

THE OWNER'S RULING (2026-09-03)
    "i test and sign off in the owner validation section when you do board next you
     flip all passed and validated to closed"

    and, defining the state she signs off FROM:

    "once you move to device for testing gets moved to fixed"

    So the lifecycle is:

        new issue -> ticket -> assign an SME -> check in when complete
                  -> ON HER DEVICE          = FIXED   (not "code complete", not "committed")
                  -> she signs off in Owner Validation (Passed + Validated)
                  -> the NEXT board build flips those to CLOSED

WHY THIS LIVES INSIDE THE BOARD BUILD
    It used to be a second command (tools/board_close_validated.py) that a seat had to
    remember to run, and the seat kept forgetting - the owner had to ask twice. CLAUDE.md
    16 states the rule that settles it: *a gate whose remedy is "a human remembers a second
    command" is not a gate*. So tools/board_build.py now runs this pass itself, before it
    parses the work orders, and the page it writes already shows the closes. There is
    exactly ONE implementation of the close and both entry points call it.

⛔ THIS MODULE REWRITES **Status:** LINES FROM A DATA FILE. The safety rules are the design:

  1. BOTH SIGNALS, or nothing.  verdict == "Pass" AND validated == True. A Fail, a
     "Needs Work", a blank/unrecognised verdict, a Pass that was never validated, or a
     validated entry with no verdict all CLOSE NOTHING. (owner_validations.normalize()
     has already coerced any unknown verdict string to "", so an unrecognised verdict
     arrives here as blank and is held, never guessed at.)
  2. ONLY A FIXED TICKET IS ELIGIBLE. Fixed now means "it reached her device", and a
     felt-test sign-off can only validly follow that state. A READY / SPEC / BLOCKED /
     DONE ticket is NEVER closed by a stale mark - the mark is reported and held.
     Eligibility is read through board_build.classify_status, the board's own status
     vocabulary, so the closer and the Fixed bucket can never disagree about what
     "Fixed" means.
  3. NEVER RESURRECT, NEVER DOWNGRADE. An already-CLOSED ticket is not rewritten - not
     re-stamped, not re-dated. Ten runs produce the same bytes as one.
  4. THE EXISTING STATUS TEXT SURVIVES VERBATIM. Those FIXED lines carry the real
     engineering record - what shipped, what is HELD awaiting a retag, findings
     deliberately not fixed. The stamp is PREPENDED and the old line is carried after
     "PRIOR STATUS:", which is already this repo's convention for historical status prose
     (board_build.status_contradiction splits on exactly that marker, so preserving the
     body cannot manufacture a false "contradiction" defect).
  5. AUDITABLE. The stamp records the "at" and "build" from the validation entry, so
     which sign-off on which build closed the ticket is readable off the status line.
  6. A MALFORMED / UNREADABLE RECORD ABORTS THE PASS. It never closes a partial set and
     never reports success while silently closing nothing - same discipline as the board
     build's own VALIDATIONS_PARSE_FAIL abort.
  7. A VALIDATION NAMING A MISSING WO FILE IS REPORTED, never dropped. A renamed or moved
     work order must not silently swallow a sign-off.

MARKER
    BOARD_CLOSE_OK   closed <n>, held <n>, already-closed <n>, missing <n>
    BOARD_CLOSE_FAIL <why>
    Judge it by the marker on a fresh log, never by the exit code (CLAUDE.md 8/16).
"""
from __future__ import annotations

import datetime
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import owner_validations

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# Overridable so the round-trip self-check can exercise the REAL close path against a
# throwaway WorkOrders/ instead of the live tickets. A test must never be able to rewrite
# the status lines it exists to protect.
WO_DIR = os.environ.get("EOA_WO_DIR") or os.path.join(ROOT, "WorkOrders")

_STATUS_LINE = re.compile(r"(?m)^(\*\*Status:\*\*[ \t]*)(.+)$")

# The escape hatch is deliberately INVERTED: the close runs by default and a caller must
# opt OUT. An opt-IN flag would restore the exact hole this module closes.
def close_disabled() -> bool:
    return (os.environ.get("EOA_BOARD_CLOSE") or "").strip() in ("0", "false", "no", "off")


def read_status(path: str) -> str:
    try:
        text = open(path, encoding="utf-8", errors="replace").read()
    except OSError:
        return ""
    m = _STATUS_LINE.search(text)
    return m.group(2).strip() if m else ""


def _bucket(status: str) -> str:
    """The board's own vocabulary, imported lazily.

    Lazy because tools/board_build.py imports THIS module at its top; importing it back
    at module scope would be a cycle. By the time a close actually runs, board_build is
    fully loaded. Sharing the function is the point: if the board calls a row Fixed, the
    closer must consider that same row eligible, and vice versa - two copies of the
    keyword rules would drift, and a drifted copy here rewrites files.
    """
    import board_build
    return board_build.classify_status(status, has_result=False, is_wo=True)[0]


def stamp_for(state: dict, today: str, prior: str) -> str:
    """The replacement status line. PREPENDS a close stamp; never replaces the body."""
    prov = []
    if state.get("at"):
        prov.append(f"validated {state['at']}")
    if state.get("build"):
        prov.append(f"build {state['build']}")
    note = (state.get("note") or "").replace("\n", " ").strip()
    if len(note) > 160:
        note = note[:157] + "..."
    head = f"CLOSED {today} - owner felt-test PASS"
    if prov:
        head += " (" + ", ".join(prov) + ")"
    if note:
        head += f' - "{note}"'
    return f"{head}. PRIOR STATUS: {prior}"


def write_status(path: str, new_status: str) -> bool:
    text = open(path, encoding="utf-8", errors="replace").read()
    # A FUNCTION replacement, not a template: a status body can legitimately contain a
    # backslash or a `\1`, and a string replacement would try to expand it.
    new_text, n = _STATUS_LINE.subn(lambda m: m.group(1) + new_status, text, count=1)
    if n != 1:
        return False
    # No BOM, LF endings - other tools read these files.
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_text)
    return True


def close_pass(entries=None, wo_dir=None, today=None):
    """Apply the close pass. Returns a result dict; raises nothing on ordinary data.

    entries: {filename: state}. None means "read the durable record" - and an unreadable
             record propagates ValidationsUnreadable to the caller, which must ABORT.
    """
    wo_dir = wo_dir or WO_DIR
    today = today or datetime.date.today().isoformat()
    if entries is None:
        entries = owner_validations.entries()   # may raise ValidationsUnreadable - by design

    res = {"closed": [], "held": [], "already": [], "missing": [], "unwritable": []}
    for name in sorted(entries):
        state = entries.get(name) or {}
        if not isinstance(state, dict):
            res["held"].append((name, "entry is not an object"))
            continue
        verdict = state.get("verdict") or ""
        validated = bool(state.get("validated"))
        # ── RULE 1: both signals, or nothing ────────────────────────────────────
        if verdict != "Pass" or not validated:
            res["held"].append(
                (name, f"verdict={verdict or 'blank'} validated={'yes' if validated else 'no'}"))
            continue
        path = os.path.join(wo_dir, name)
        # ── RULE 7: a mark naming a missing file is REPORTED ─────────────────────
        if not os.path.isfile(path):
            res["missing"].append(name)
            continue
        prior = read_status(path)
        bucket = _bucket(prior)
        # ── RULE 3: never resurrect, never re-stamp ──────────────────────────────
        if bucket == "Closed":
            res["already"].append(name)
            continue
        # ── RULE 2: only a FIXED ticket is eligible ──────────────────────────────
        if bucket != "Fixed":
            res["held"].append((name, f"not Fixed (bucket={bucket}) - a sign-off cannot "
                                      f"close a ticket that never reached the device"))
            continue
        if write_status(path, stamp_for(state, today, prior)):
            res["closed"].append(name)
        else:
            res["unwritable"].append(name)
    return res


def run(entries=None, wo_dir=None, today=None, echo=print):
    """Print the pass in the house style and return (ok, result). Never raises."""
    if close_disabled():
        echo("BOARD_CLOSE_SKIPPED EOA_BOARD_CLOSE=0 - no **Status:** line was touched")
        return True, {"closed": [], "held": [], "already": [], "missing": [], "unwritable": []}
    try:
        res = close_pass(entries=entries, wo_dir=wo_dir, today=today)
    except owner_validations.ValidationsUnreadable as e:
        echo(f"VALIDATIONS_PARSE_FAIL {e}")
        echo("    The close pass ABORTED. No **Status:** line was touched - a partial "
             "close off a damaged record is worse than none. Repair the record "
             "(git checkout proof/owner-validations.json) and re-run.")
        echo("BOARD_CLOSE_FAIL owner-validation record unreadable")
        return False, None
    for name in res["closed"]:
        echo(f"    CLOSED  {name}")
    for name in res["missing"]:
        echo(f"    MISSING {name}   <- validated, but no such file in {os.path.basename(wo_dir or WO_DIR)}/")
    for name in res["unwritable"]:
        echo(f"    NO-STATUS-LINE {name}   <- validated Pass but the file has no **Status:** line")
    ok = not res["missing"] and not res["unwritable"]
    line = (f"BOARD_CLOSE_OK closed {len(res['closed'])}, held {len(res['held'])}, "
            f"already-closed {len(res['already'])}, missing {len(res['missing'])}")
    if not ok:
        line = (f"BOARD_CLOSE_FAIL closed {len(res['closed'])}, held {len(res['held'])}, "
                f"already-closed {len(res['already'])}, missing {len(res['missing'])}, "
                f"no-status-line {len(res['unwritable'])}")
    echo(line)
    return ok, res


def main() -> int:
    ok, _ = run()
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
