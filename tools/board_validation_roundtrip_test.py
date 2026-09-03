#!/usr/bin/env python3
"""board_validation_roundtrip_test.py - prove a board REBUILD cannot lose a sign-off.

    python tools/board_validation_roundtrip_test.py

Prints VALIDATION_ROUNDTRIP_OK (all assertions passed, and they have teeth) or
VALIDATION_ROUNDTRIP_FAIL <what>. Judge it by the marker, not the exit code.

WHY THIS EXISTS
    The board's owner-validation state used to live in browser localStorage under a
    key carrying the APK build id AND the source commit sha, so every commit orphaned
    every sign-off. The fix moved the record to proof/owner-validations.json and made
    tools/board_build.py READ it. The property that must never regress is therefore:
    "regenerate the board and the sign-offs are still there, on the page and on disk."

    Nothing here touches the live record or the live BOARD.html - both are redirected
    to a temp directory via EOA_VALIDATIONS_PATH / EOA_BOARD_OUT. A test must not be
    able to damage the evidence it exists to protect.

THE RED PROOF (stage 4)
    Assertions that cannot fail prove nothing, so this test breaks the read path on
    purpose - stubs the record loader back to "always empty", the exact behaviour of
    the old code - and requires that stage 1's assertions then FAIL. If the break
    goes undetected, the test reports FAIL: the guard is asleep.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(TOOLS)

failures: list[str] = []


def check(ok, what):
    if not ok:
        failures.append(what)
    print(f"  {'ok  ' if ok else 'FAIL'} {what}")
    return ok


def main() -> int:
    tmp = tempfile.mkdtemp(prefix="eoa-validation-roundtrip-")
    rec = os.path.join(tmp, "owner-validations.json")
    out = os.path.join(tmp, "BOARD.html")
    os.environ["EOA_VALIDATIONS_PATH"] = rec
    os.environ["EOA_BOARD_OUT"] = out

    sys.path.insert(0, TOOLS)
    import board_build as bb          # noqa: E402  (env must be set before import)
    import owner_validations as ov    # noqa: E402

    rows = bb.parse_wos()
    fixed = [r for r in rows if r["bucket"] == "Fixed" and r["is_wo"]]
    if not check(bool(fixed), "the repo has at least one Fixed work order to validate"):
        print("VALIDATION_ROUNDTRIP_FAIL no Fixed rows to exercise")
        return 1
    ticket = fixed[0]["file"]

    # ── stage 1: a validation on disk RENDERS ────────────────────────────────────
    print(f"stage 1 - a recorded sign-off renders ({ticket})")
    ov.ingest({ticket: {"validated": True, "verdict": "Pass", "note": "roundtrip probe",
                        "at": "2026-09-03T00:00:00", "build": "test-build"}})
    before = open(rec, encoding="utf-8").read()

    def assertions(page):
        """Stage 1's contract, reused verbatim by the RED proof. Returns failures."""
        bad = []
        i = page.find(f'data-ticket="{ticket}"')
        if i < 0:
            return [f"the validated ticket {ticket} is not on the page at all"]
        item = page[i - 200:i + 1400]
        if "isvalidated" not in item: bad.append("row is not marked isvalidated")
        if "[X] VALIDATED" not in item: bad.append("row carries no VALIDATED word badge")
        if ">Validated<" not in item: bad.append("button still reads Validate, not Validated")
        if "<option selected>Pass</option>" not in item: bad.append("verdict Pass is not pre-selected")
        if "roundtrip probe" not in item: bad.append("the note did not render")
        return bad

    page = bb.build_html(rows)
    bad = assertions(page)
    for f in bad:
        check(False, f)
    check(not bad, "row renders VALIDATED + Pass + note, server-side, from the record")
    check('<span class="gcount">1 /' in page,
          "a group count renders 1 done straight from disk (no JS needed)")

    # ── stage 2: a REBUILD preserves it (the whole point) ────────────────────────
    print("stage 2 - rebuild twice; the record and the render both survive")
    bb.build_html(rows)
    page2 = bb.build_html(rows)
    check(not assertions(page2), "the sign-off still renders after two more rebuilds")
    check(open(rec, encoding="utf-8").read() == before,
          "the record file is byte-identical after rebuilds (board never writes it)")

    # ── stage 3: the REAL entry point emits the marker ───────────────────────────
    print("stage 3 - the real entry point emits VALIDATIONS_OK")
    proc = subprocess.run([sys.executable, os.path.join(TOOLS, "board_build.py")],
                          cwd=ROOT, capture_output=True, text=True,
                          encoding="utf-8", errors="replace")
    log = (proc.stdout or "") + (proc.stderr or "")
    check("VALIDATIONS_OK 1 recorded, 1 validated" in log,
          "VALIDATIONS_OK reports 1 recorded / 1 validated")
    check("BOARD_CHECK_OK" in log, "BOARD_CHECK_OK still printed")
    check(os.path.exists(out) and not assertions(open(out, encoding="utf-8").read()),
          "the written page carries the sign-off")
    check(open(rec, encoding="utf-8").read() == before,
          "a full board_build run left the record untouched")

    # ── stage 4: RED PROOF - break the read path, demand a FAIL ──────────────────
    print("stage 4 - RED proof: stub the record loader empty (the OLD behaviour)")
    real = bb.owner_validations.entries
    try:
        bb.owner_validations.entries = lambda *a, **k: {}
        broken = assertions(bb.build_html(rows))
    finally:
        bb.owner_validations.entries = real
    check(bool(broken), f"a broken read path FAILS the stage-1 assertions "
                        f"({len(broken)} caught: {broken[0] if broken else 'none'})")
    check(not assertions(bb.build_html(rows)),
          "and the restored read path renders it again")

    print(f"record: {rec}")
    if failures:
        print("VALIDATION_ROUNDTRIP_FAIL " + "; ".join(failures))
        return 1
    print("VALIDATION_ROUNDTRIP_OK rebuild preserves owner validations; "
          "guard proven red when the read path breaks")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
