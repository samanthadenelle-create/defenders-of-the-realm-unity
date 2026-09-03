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
import shutil
import subprocess
import sys
import tempfile
import types

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
    # EOA_BOARD_CLOSE=0: this run points at the LIVE WorkOrders/ while the temp record now
    # carries a Pass+validated mark on a REAL Fixed ticket, so without the opt-out the
    # WO-1355 close pass would rewrite that ticket's status line for real. Stage 5 proves
    # the close through this same entry point against a THROWAWAY WorkOrders/ instead.
    env = dict(os.environ, EOA_BOARD_CLOSE="0")
    proc = subprocess.run([sys.executable, os.path.join(TOOLS, "board_build.py")],
                          cwd=ROOT, capture_output=True, text=True,
                          encoding="utf-8", errors="replace", env=env)
    log = (proc.stdout or "") + (proc.stderr or "")
    check("VALIDATIONS_OK 1 recorded, 1 validated" in log,
          "VALIDATIONS_OK reports 1 recorded / 1 validated")
    check("BOARD_CHECK_OK" in log, "BOARD_CHECK_OK still printed")
    check("BOARD_CLOSE_SKIPPED" in log,
          "the close pass honoured EOA_BOARD_CLOSE=0 against the live WorkOrders/")
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


    # == WO-1355: THE CLOSE PASS =================================================
    # Owner ruling 2026-09-03: a Pass+validated sign-off on a FIXED ticket is flipped to
    # CLOSED by the NEXT board build. That pass REWRITES **Status:** lines from a data
    # file, so everything below runs against a THROWAWAY WorkOrders/ (EOA_WO_DIR) and a
    # THROWAWAY record - the live tickets are never in reach of this test.
    import board_close_pass as bcp        # noqa: E402

    wodir = os.path.join(tmp, "WorkOrders")
    rec2 = os.path.join(tmp, "close-record.json")
    out2 = os.path.join(tmp, "BOARD-close.html")

    # ONE ticket per rule, so a failure names the rule it broke.
    FIXTURES = {
        # eligible: FIXED + Pass + validated. Its body carries real engineering detail,
        # which rule 4 says must survive the close verbatim.
        "WORK_ORDER_9001_fixed_pass.md":
            "FIXED 2026-09-01 - wolf routing shipped; HELD awaiting her retag; "
            "finding 3 deliberately not fixed.",
        # rule 2: never closed - a sign-off cannot close what never reached the device.
        "WORK_ORDER_9002_ready_pass.md":
            "READY TO IMPLEMENT - not built yet.",
        # rule 3: already CLOSED - never re-stamped, never re-dated.
        "WORK_ORDER_9003_closed_pass.md":
            "CLOSED 2026-08-01 - owner felt-test PASS. PRIOR STATUS: FIXED 2026-07-30.",
        # rule 1: FIXED but the verdict is Fail - closes nothing (it bounces, elsewhere).
        "WORK_ORDER_9004_fixed_fail.md":
            "FIXED 2026-09-01 - shipped, awaiting her felt test.",
        # rule 1: FIXED + Pass but NEVER validated - one signal is not two.
        "WORK_ORDER_9005_fixed_unvalidated.md":
            "FIXED 2026-09-01 - shipped, awaiting her felt test.",
        # rule 1: FIXED + validated but the verdict is blank. This is also where an
        # UNRECOGNISED verdict lands: owner_validations.normalize() coerces anything it
        # does not know to "", so a garbage verdict can never be read as a Pass.
        "WORK_ORDER_9006_fixed_novrd.md":
            "FIXED 2026-09-01 - shipped, awaiting her felt test.",
        # rule 2: DONE + Pass + validated - a finished-but-not-Fixed row stays untouched.
        "WORK_ORDER_9007_done_pass.md":
            "DONE 2026-08-20 - landed and verified headless.",
    }
    MARKS = {
        "WORK_ORDER_9001_fixed_pass.md":        ("Pass", True),
        "WORK_ORDER_9002_ready_pass.md":        ("Pass", True),
        "WORK_ORDER_9003_closed_pass.md":       ("Pass", True),
        "WORK_ORDER_9004_fixed_fail.md":        ("Fail", True),
        "WORK_ORDER_9005_fixed_unvalidated.md": ("Pass", False),
        "WORK_ORDER_9006_fixed_novrd.md":       ("", True),
        "WORK_ORDER_9007_done_pass.md":         ("Pass", True),
    }
    UNTOUCHED = [k for k in FIXTURES if k != "WORK_ORDER_9001_fixed_pass.md"]

    def seed_wos(target):
        if os.path.isdir(target):
            shutil.rmtree(target)
        os.makedirs(target)
        for name, status in FIXTURES.items():
            body = "# " + name + "\n\n**Status:** " + status + "\n\nBody text.\n"
            with open(os.path.join(target, name), "w", encoding="utf-8", newline="\n") as f:
                f.write(body)
        return {n: open(os.path.join(target, n), encoding="utf-8").read() for n in FIXTURES}

    def marks(extra=None):
        out = {}
        for name, (verdict, validated) in MARKS.items():
            out[name] = {"validated": validated, "verdict": verdict, "note": "",
                         "at": "2026-09-03T08:00:00", "build": "2026.09.03.353742"}
        if extra:
            out.update(extra)
        return out

    def close_assertions(target, baseline):
        """The close pass's whole contract, reused verbatim by the RED proof below."""
        bad = []
        got = {n: open(os.path.join(target, n), encoding="utf-8").read() for n in FIXTURES}
        eligible = "WORK_ORDER_9001_fixed_pass.md"
        st = bcp.read_status(os.path.join(target, eligible))
        if not st.upper().startswith("CLOSED"):
            bad.append("the eligible FIXED+Pass+validated ticket was not closed: " + repr(st[:40]))
        if FIXTURES[eligible] not in st:
            bad.append("rule 4: the original FIXED status body did not survive the close")
        if "PRIOR STATUS:" not in st:
            bad.append("rule 4: no PRIOR STATUS: marker carrying the old line")
        if "2026-09-03T08:00:00" not in st or "2026.09.03.353742" not in st:
            bad.append("rule 5: the close stamp records neither the 'at' nor the 'build'")
        if "Body text." not in got[eligible]:
            bad.append("the rest of the work-order file did not survive")
        for name in UNTOUCHED:
            if got[name] != baseline[name]:
                bad.append("rule 1/2/3: " + name + " was rewritten and must not have been: "
                           + repr(bcp.read_status(os.path.join(target, name))[:50]))
        return bad

    # -- stage 5: the close runs INSIDE the real board_build entry point ----------
    print("stage 5 - python tools/board_build.py performs the close itself (no 2nd command)")
    base5 = seed_wos(wodir)
    ov.ingest(marks(), path=rec2)
    env2 = dict(os.environ, EOA_WO_DIR=wodir, EOA_VALIDATIONS_PATH=rec2, EOA_BOARD_OUT=out2)
    env2.pop("EOA_BOARD_CLOSE", None)
    p5 = subprocess.run([sys.executable, os.path.join(TOOLS, "board_build.py")],
                        cwd=ROOT, capture_output=True, text=True,
                        encoding="utf-8", errors="replace", env=env2)
    log5 = (p5.stdout or "") + (p5.stderr or "")
    check("BOARD_CLOSE_OK closed 1, held 5, already-closed 1, missing 0" in log5,
          "board_build emits BOARD_CLOSE_OK closed 1, held 5, already-closed 1, missing 0")
    for f in close_assertions(wodir, base5):
        check(False, f)
    check(not close_assertions(wodir, base5),
          "exactly the FIXED+Pass+validated ticket closed; body preserved; the other 6 untouched")

    # -- stage 6: IDEMPOTENCY - three runs, one run's bytes ----------------------
    print("stage 6 - idempotency: rebuild twice more, the bytes must not move")
    after1 = {n: open(os.path.join(wodir, n), encoding="utf-8").read() for n in FIXTURES}
    for _ in (2, 3):
        subprocess.run([sys.executable, os.path.join(TOOLS, "board_build.py")],
                       cwd=ROOT, capture_output=True, text=True,
                       encoding="utf-8", errors="replace", env=env2)
    after3 = {n: open(os.path.join(wodir, n), encoding="utf-8").read() for n in FIXTURES}
    check(after1 == after3, "runs 2 and 3 produce byte-identical work-order files "
                            "(a closed ticket is never re-stamped or re-dated)")

    # -- stage 7: a mark naming a MISSING WO file is reported, never dropped ------
    print("stage 7 - a validation naming a file that does not exist is REPORTED")
    base7 = seed_wos(wodir)
    lines = []
    ok7, res7 = bcp.run(entries=marks({"WORK_ORDER_9999_ghost.md": {
        "validated": True, "verdict": "Pass", "at": "2026-09-03T08:00:00"}}),
        wo_dir=wodir, echo=lines.append)
    log7 = "\n".join(lines)
    check("WORK_ORDER_9999_ghost.md" in log7 and "MISSING" in log7,
          "the missing WO is named on the log, not silently dropped")
    check("BOARD_CLOSE_FAIL" in log7 and "missing 1" in log7,
          "and the pass reports FAIL rather than a clean OK")
    check(not ok7, "run() returns not-ok so a caller can act on it")
    check(not close_assertions(wodir, base7),
          "the real tickets still closed/held correctly around the ghost entry")

    # -- stage 8: a CORRUPT record ABORTS the close, closing no partial set -------
    print("stage 8 - a corrupt validation record aborts the close pass")
    base8 = seed_wos(wodir)
    bad_rec = os.path.join(tmp, "corrupt.json")
    with open(bad_rec, "w", encoding="utf-8", newline="\n") as f:
        f.write('{"_schema": 1, "validations": [ this is not json')
    saved = ov.PATH
    try:
        ov.PATH = bad_rec
        lines = []
        ok8, res8 = bcp.run(entries=None, wo_dir=wodir, echo=lines.append)
    finally:
        ov.PATH = saved
    log8 = "\n".join(lines)
    check("VALIDATIONS_PARSE_FAIL" in log8 and "BOARD_CLOSE_FAIL" in log8,
          "a corrupt record prints VALIDATIONS_PARSE_FAIL + BOARD_CLOSE_FAIL")
    check(res8 is None and not ok8, "the pass aborts instead of closing a partial set")
    now8 = {n: open(os.path.join(wodir, n), encoding="utf-8").read() for n in FIXTURES}
    check(now8 == base8, "not one **Status:** line was touched during the abort")

    # -- stage 9: RED PROOF - mutate the close pass, demand the oracle catch it ---
    # Assertions that cannot fail prove nothing. Each mutation below deletes exactly one
    # safety rule from a COPY of the module source (the file on disk is never touched)
    # and the stage-5 contract must go red for it.
    print("stage 9 - RED proof: four mutations of the close pass, each must be caught")
    src = open(os.path.join(TOOLS, "board_close_pass.py"), encoding="utf-8").read()
    MUTANTS = [
        ([('if verdict != "Pass" or not validated:', 'if verdict != "Pass":')],
         "rule 1 - drop the 'validated' signal"),
        ([('if bucket != "Fixed":', 'if bucket not in ("Fixed", "Ready", "Done"):')],
         "rule 2 - let a non-FIXED ticket close"),
        # Rule 3 needs BOTH edits: the already-Closed early return is what protects a
        # closed ticket, and the Fixed check would catch it afterwards. Removing one and
        # not the other proves nothing, so this mutant removes the pair.
        ([('if bucket == "Closed":', 'if bucket == "NeverMatches":'),
          ('if bucket != "Fixed":', 'if bucket not in ("Fixed", "Closed"):')],
         "rule 3 - re-stamp a ticket that is already CLOSED"),
        ([('return f"{head}. PRIOR STATUS: {prior}"', 'return head')],
         "rule 4 - throw away the existing status body"),
    ]
    for edits, what in MUTANTS:
        mutated = src
        anchors_ok = True
        for find, repl in edits:
            if mutated.count(find) != 1:
                anchors_ok = False
                break
            mutated = mutated.replace(find, repl)
        if not anchors_ok:
            check(False, "RED proof could not apply mutation (" + what + "): anchor not unique")
            continue
        mod = types.ModuleType("board_close_pass_mutant")
        mod.__file__ = os.path.join(TOOLS, "board_close_pass.py")
        exec(compile(mutated, mod.__file__, "exec"), mod.__dict__)
        base9 = seed_wos(wodir)
        mod.run(entries=marks(), wo_dir=wodir, echo=lambda *a: None)
        broke = close_assertions(wodir, base9)
        check(bool(broke), "mutation caught (" + what + ") -> "
              + (broke[0] if broke else "NOTHING"))
    base9 = seed_wos(wodir)
    bcp.run(entries=marks(), wo_dir=wodir, echo=lambda *a: None)
    check(not close_assertions(wodir, base9),
          "and the UNmutated close pass is green again (the success path, proven)")

    print(f"record: {rec}")
    if failures:
        print("VALIDATION_ROUNDTRIP_FAIL " + "; ".join(failures))
        return 1
    print("VALIDATION_ROUNDTRIP_OK rebuild preserves owner validations; the board build "
          "closes only Pass+validated FIXED tickets, idempotently, with the status body "
          "preserved; guards proven red (read path + 4 close-pass mutations)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
