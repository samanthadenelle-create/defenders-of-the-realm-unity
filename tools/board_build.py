#!/usr/bin/env python3
"""
board_build.py - generate BOARD.html (the live work-order board) FROM the repo.

The repo's markdown is the single source of truth; this board is a derived view,
so it can never drift the way a hand-mirrored Notion board does. Re-run any time:

    python tools/board_build.py

Reads:  WorkOrders/*.md  (status line, title, RESULT markers)
        CLI_LANES_WO_NUMBERS.md  (next-free mint numbers per seat)
Writes: BOARD.html (repo root) - open in any browser; links open the md files.
"""
import os, re, glob, html, time, datetime, sys, subprocess

# Windows consoles default to cp1252, which cannot encode the characters this repo's
# work orders actually use (the U+26D4 no-entry sign, box drawing, arrows). On
# 2026-08-22 that raised UnicodeEncodeError from a print() AFTER BOARD.html had been
# written successfully -- so the board was fine and the run still looked like a hard
# failure, which is the worst of both: a false alarm that also hides real check output
# (BOARD_CHECK_OK / DUPLICATE_WO_NUMBERS / BANNER_OK all print after that point).
# Judge the board by its own check markers, not by this script's exit code.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass  # older Python, or a stream that is not reconfigurable; prints degrade, not die

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WO_DIR = os.path.join(ROOT, "WorkOrders")
OUT = os.path.join(ROOT, "BOARD.html")

# ── parser SCOPE: what in WorkOrders/ is actually a work order? (WO-937 A) ────
# WorkOrders/ holds two kinds of file, and only one of them owes the board a status:
#
#   1. WORK ORDERS      WORK_ORDER_*.md          - a unit of work; MUST carry **Status:**
#   2. COMPANION DOCS   AUDIT_/BRIEF_/HANDOFF_/NOTES_/QA_CHECKLIST_/DESIGN_/README.md/...
#                                                - references, not work; a status line would be absurd
#
# Before this, EVERY .md in the folder was treated as a work order, so 18 companion docs parsed
# with no **Status:** line and landed in "Unlabeled" - which docs/BOARD.md defines as a DEFECT.
# That made the Unlabeled number a mix of real defects and category errors, so --check could not
# be gated on honestly.
#
# We BUCKET them as "Doc" rather than EXCLUDING them. Excluding is the smaller diff and the wrong
# call: several of these are live references people need to find (WO541_MODEL_API.md,
# DESIGN_CONNECTOR_IS_THE_ONLY_CONTRACT.md, the raid audits, DUNGEON_WO_INDEX.md), and this board is
# the only index of WorkOrders/ anyone actually opens. Dropping them would trade a cosmetic
# miscount for a discoverability hole - a strictly worse bug, and a silent one.
#
# SCOPE IS BY DOCUMENT KIND (filename prefix), NOT BY "does it have a number". That distinction is
# load-bearing: 18 files are named WORK_ORDER_<slug>.md with no number (WORK_ORDER_ad_generator.md,
# WORK_ORDER_second_grom_companion.md, ...). Those ARE work orders - legacy, unnumbered, but real
# work - so a missing status on one is a GENUINE defect and must keep counting. Scoping on the
# number instead would have laundered 5 real defects into the non-defect bucket.
_WO_FILENAME = re.compile(r"^WORK_ORDER_", re.IGNORECASE)

# A SECOND companion shape the prefix rule above cannot see (owner-reported 2026-08-23):
# WORK_ORDER_<num>_<KIND>.md, where KIND is an ALL-CAPS document kind rather than a slug -
# WORK_ORDER_1114_IMPLEMENTATION_PLAN.md, WORK_ORDER_1038_VERIFICATION.md. These carry the
# WORK_ORDER_ prefix, so they parsed as work orders, owed a **Status:** they will never have,
# and sat in Unlabeled - which docs/BOARD.md defines as a DEFECT. They are companions to a
# numbered WO that already carries the status, not units of work.
#
# ⛔ SCOPED TO A NUMBER + AN EXPLICIT KIND WHITELIST, deliberately. A loose "is it ALL-CAPS"
# test would swallow the 18 legacy UNNUMBERED work orders (WORK_ORDER_ad_generator.md and
# friends) whose missing status IS a genuine defect - laundering real defects into the
# non-defect bucket is the exact failure the comment above warns about. Add a kind here only
# when it is genuinely a companion to a numbered WO.
_WO_COMPANION_KIND = re.compile(
    r"^WORK_ORDER_\d+_(IMPLEMENTATION_PLAN|VERIFICATION|AUDIT|INDEX|NOTES|ADDENDUM)",
    re.IGNORECASE)

def is_work_order(basename):
    """True for a real work order (numbered or legacy-unnumbered); False for a companion doc."""
    if _WO_COMPANION_KIND.match(basename): return False
    return bool(_WO_FILENAME.match(basename))

# ── status bucketing (keyword priority order) ─────────────────────────────────
def classify_status(status_text, has_result, is_wo=True):
    """Return (bucket, used_substring_fallback) for one status line."""
    # Companion docs are out of the status workflow entirely - never Unlabeled, never a defect.
    if not is_wo: return "Doc", False
    s = (status_text or "").upper()
    # ⛔ THE VERDICT IS THE FIRST WORD. Everything after it is commentary (2026-08-23).
    #
    # Every keyword below except FIXED used to be tested as a bare SUBSTRING, so a status whose
    # verdict was READY/BLOCKED/SPEC was yanked into Done or Closed because the word appeared
    # LATER in the sentence: "the PRE-ACK hole closed" read as Closed; "design complete, can be
    # implemented" read as Done; "UNBLOCKED" read as Blocked. A board sweep found FOURTEEN
    # tickets mis-bucketed this way.
    #
    # ⚠ AND THE ERROR ONLY EVER RAN ONE WAY: toward "finished". Live work rendered as done, so
    # nobody looked at it again. A board that hides open work is worse than no board.
    #
    # Rewording the fourteen statuses cured the instances and not the mechanism - the next author
    # who writes "hole closed" in a Fixed line reopens it. So the leading-word test that FIXED
    # already had (see the block below, added the same day after it bit) is now promoted to ALL
    # of them.
    #
    # THE SUBSTRING PASS IS KEPT AS A FALLBACK, deliberately: many legacy statuses lead with a
    # non-canonical word ("PARTIAL 2026-08-22 - ..."), and a leading-word-only rule would dump
    # every one of them into Unlabeled, trading a silent mis-bucket for a loud false defect.
    # First word wins when it is canonical; otherwise we fall back to the old behaviour.
    lead = s.lstrip().split(None, 1)
    lead = lead[0].strip("*:-—,.") if lead else ""
    if lead in ("SUPERSEDED", "CLOSED", "CANCELLED"): return "Closed", False
    if lead == "FIXED": return "Fixed", False
    if lead in ("DONE", "IMPLEMENTED", "COMPLETE"): return "Done", False
    if lead == "BLOCKED": return "Blocked", False
    if lead in ("READY", "SPEC", "DRAFT", "PROPOSAL", "PARKED", "FUTURE", "LATENT"):
        return ("Ready" if lead == "READY" else "Spec"), False

    if "SUPERSEDED" in s or "CLOSED" in s or "CANCELLED" in s: return "Closed", True
    # FIXED = built, gated and on disk, but NOT closed: it is waiting on the owner's felt test
    # (CLAUDE.md 13 - "PO felt-verifies + CLOSES"; headless cannot judge feel). Owner ruling
    # 2026-08-23.
    #
    # THIS TEST MUST STAY ABOVE THE "Done" LINE, and the reason is the `has_result` term in it.
    # A Fixed ticket normally DOES have a RESULT.md - that is what writing up the work produces.
    # If Fixed were tested after, `has_result` would swallow it into Done and the ticket would
    # read as finished the moment the write-up landed, silently skipping the felt test that is
    # the entire point of the bucket. Ordering IS the guarantee here, not a style preference.
    #
    # ⛔ LEADING KEYWORD, NOT A SUBSTRING - and this one genuinely bit on the first build.
    # A plain `"FIXED" in s` matched ELEVEN already-finished tickets whose prose merely contains
    # the word: "DONE - owner-confirmed fixed", "CLOSED - SUPERSEDED ... fixed-layout castle",
    # "PARTIAL - 2 fixed, 1 deferred". Because this test sits above the Done/Closed lines, every
    # one of them was yanked back out of Done into the owner's to-test queue - handing her work
    # she had already closed, which is the exact opposite of what the bucket is for. The status
    # VERDICT is the first word of the line; anything later is commentary.
    if s.lstrip().startswith("FIXED"): return "Fixed", True
    if has_result or "DONE" in s or "IMPLEMENTED" in s or "COMPLETE" in s: return "Done", True
    if "BLOCKED" in s: return "Blocked", True
    if "READY" in s or "IN PROGRESS" in s: return "Ready", True
    # PARKED / FUTURE / LATENT are all "real, understood, deliberately not scheduled" - the same
    # shape as SPEC, and all three were in live use on WO status lines while landing in Unlabeled
    # (owner-reported 2026-08-23: WO-1148 "FUTURE - not scheduled", WO-1140 "LATENT GUARD - NOT AN
    # ACTIVE DEFECT"). Bucketing them is right BECAUSE they are considered decisions, not gaps.
    if ("DRAFT" in s or "SPEC" in s or "NOT STARTED" in s or "PROPOSAL" in s
            or "PARKED" in s or "FUTURE" in s or "LATENT" in s): return "Spec", True
    return "Unlabeled", False

def bucket_of(status_text, has_result, is_wo=True):
    """Compatibility wrapper for callers that need only the bucket."""
    return classify_status(status_text, has_result, is_wo)[0]

# Exact markdown is part of the board contract. A near miss remains visible, but is
# named as a defect instead of being silently accepted (WO-1180).
_STATUS_EXACT = re.compile(r"^\*\*Status:\*\*\s*(.+)$", re.MULTILINE)
_STATUS_MALFORMED = re.compile(r"^\*\*Status\b[^\r\n]*$", re.MULTILINE)

def parse_status(text):
    """Return (status text, malformed_marker) without dropping malformed rows.

    Returns (status, malformed, near_miss).

    A malformed marker is REPORTED, never dropped - a row that vanishes is the failure
    WO-1180 exists to prevent.

    TWO TIERS, deliberately, because they are not the same risk:
      * malformed = the row's verdict CAME FROM a near-miss line, because the file has no
        exact `**Status:**` at all. These are the rows one edit from vanishing (the WO-932
        shape). This is the drainable worklist.
      * near_miss  = the file has a good `**Status:**` AND also carries a near-miss line
        somewhere. Cosmetic under the exact pattern - but it is what shadowed WO-414 under
        the OLD loose pattern, which took whichever line came first. Counted, not listed in
        full: 264 of these exist (mostly the legacy `**Status: READY TO IMPLEMENT**`), and a
        264-line wave is a report nobody drains.
    """
    exact = _STATUS_EXACT.search(text)
    bad = [m.group(0).strip() for m in _STATUS_MALFORMED.finditer(text)
           if not _STATUS_EXACT.match(m.group(0))]
    if exact:
        return exact.group(1).strip(), False, bool(bad)
    if not bad:
        return "", False, False
    value = re.sub(r"^\*\*Status\s*:?[\s*]*", "", bad[0], count=1,
                   flags=re.IGNORECASE).strip()
    value = re.sub(r"\*\*\s*$", "", value).strip()
    return value, True, False

# WORK REMAINING != VERIFICATION REMAINING (WO-1181, and this is the whole ship/no-ship line).
# CLAUDE.md 13 reserves CLOSING for the PO, so EVERY correctly handled Fixed row says it is
# awaiting the owner. A lint that flags "awaiting owner felt-verify" flags the entire healthy
# bucket and gets switched off inside a day. Ban the PHRASE, never the WORD.
#
# Each entry is (pattern, close_context_exempt). The close_context_exempt ones are the OPEN
# family: "4 still open" is work remaining, but "owner felt-close still open" is the NORMAL
# state of a healthy row - that false positive is what WO-999 cost on the first HEAD run.
_STATUS_CONTRADICTIONS = (
    (re.compile(r"\bPARTIAL\b", re.IGNORECASE), False),
    (re.compile(r"\bNOT(?:\s+YET)?\s+(?:DONE|BUILT|ADDED)\b", re.IGNORECASE), False),
    (re.compile(r"\bSTILL\s+OPEN\b", re.IGNORECASE), True),
    (re.compile(r"\b(?:IS|REMAINS)\s+OPEN\b", re.IGNORECASE), True),
    (re.compile(r"\bOWNER\s+RULING\s+(?:IS\s+)?OPEN\b", re.IGNORECASE), False),
    (re.compile(r"\bAWAITING\s+OWNER\s+RULING\b", re.IGNORECASE), False),
    (re.compile(r"\bNUMBERS\s+OPEN\b", re.IGNORECASE), False),
    (re.compile(r"\bBULK\s+OF\s+THE\s+TICKET\b", re.IGNORECASE), False),
    # Bare "owed" is mostly healthy verification debt. Name implementation artefacts only.
    (re.compile(r"\b(?:R2\s+)?PUSH\s+OWED\b", re.IGNORECASE), False),
    (re.compile(r"\b(?:CODE|IMPLEMENTATION|MIGRATION|GATE|RESULT\s+FILE)\s+OWED\b",
                re.IGNORECASE), False),
)

# What turns an OPEN-family match into VERIFICATION debt rather than work debt: the words
# immediately in front of it are the owner's close / felt test / sign-off.
_CLOSE_CONTEXT = re.compile(
    r"(?:OWNER|PO)\b[^.;:|]{0,40}?\b(?:FELT[-\s]?(?:TEST|VERIFY|CHECK)?|CLOSE|CLOSES|CLOSURE|"
    r"SIGN[-\s]?OFF|VERIFY|VERIFICATION)\b[^.;:|]{0,25}$", re.IGNORECASE)

# A phrase inside quotation marks is being REPORTED, not asserted - the same reason
# "PRIOR STATUS:" and "(THIS LINE SAID ...)" are already stripped below. WO-1157 reads
#: the slice this line called "not done" IS IN THE TREE - a refutation, not a confession.
_QUOTED_SPAN = re.compile(r"\"[^\"]{0,200}\"|\u201c[^\u201d]{0,200}\u201d")

# ...and an OPEN-family match that is being DENIED is not a claim of work remaining either.
# PROD-007 reads: preservePrefabRotation ... is NOT evidence 6 is open.
_REFUTATION_CONTEXT = re.compile(r"\b(?:NOT|NO)\s+(?:EVIDENCE|PROOF)\b[^.;:|]{0,40}$", re.IGNORECASE)

_FINISHED_LEADS = ("FIXED", "DONE", "IMPLEMENTED", "COMPLETE",
                   "CLOSED", "SUPERSEDED", "CANCELLED")

def status_contradiction(status, bucket, filename=""):
    """Return the first contradictory phrase on a finished verdict, or an empty string."""
    # *.RESULT.md is EXEMPT. CLAUDE.md 15 freezes RESULT files ("never rewrite"), so a finding
    # on one would demand an edit canon forbids and this lint's acceptance could never go
    # green. (They are already skipped upstream in parse_wos; this states the contract.)
    if filename.endswith(".RESULT.md"):
        return ""
    # Lint only an explicit finished verdict, never a legacy row merely rescued into a
    # finished bucket by substring fallback. WO-1180 reports that separate fragility.
    upper = (status or "").lstrip().upper()
    lead = upper.split(None, 1)
    lead = lead[0].strip("*:-,.\u2014") if lead else ""
    if lead not in _FINISHED_LEADS:
        return ""
    # Historical status prose is evidence, not a claim about current work.
    active = re.split(r"\bPRIOR\s+STATUS\s*:", status or "", maxsplit=1,
                      flags=re.IGNORECASE)[0]
    active = re.sub(r"\*?\(THIS LINE SAID .*?\)\*?", "", active, flags=re.IGNORECASE)
    quoted = [m.span() for m in _QUOTED_SPAN.finditer(active)]
    for pattern, close_exempt in _STATUS_CONTRADICTIONS:
        for match in pattern.finditer(active):
            if any(a <= match.start() and match.end() <= b for a, b in quoted):
                continue  # reported, not asserted
            before = active[:match.start()]
            if close_exempt and _CLOSE_CONTEXT.search(before):
                continue  # verification remaining, not work remaining
            if close_exempt and _REFUTATION_CONTEXT.search(before):
                continue  # the line is denying it, not admitting it
            return match.group(0)
    return ""

# Fixed sits directly after Ready because that is the owner's queue: what is built and waiting
# on her, before anything that is merely proposed. It is deliberately NOT next to Done - Done is
# finished, Fixed is owed a test.
BUCKET_ORDER = ["Ready", "Fixed", "Blocked", "Spec", "Unlabeled", "Done", "Closed", "Doc"]
# Owner is RED/GREEN COLOURBLIND: every bucket is labelled in TEXT and the colour is decoration
# only. Fixed is a cyan that also separates from Ready's amber and Done's green in GREYSCALE
# (luminance ~0.62 vs 0.72 and 0.63) - but never let the hue carry the meaning.
BUCKET_COLOR = {"Ready": "#e0b341", "Fixed": "#4fb3c4", "Blocked": "#d06060", "Spec": "#7fa8d9",
                "Unlabeled": "#999999", "Done": "#6fae6f", "Closed": "#777777",
                "Doc": "#a98fd0"}

# ── the WO-numbering authority (WO-1112) ──────────────────────────────────────
# THE BANNER IS PROSE, NOT A TABLE. This parser expected markdown table rows
# (`| **main line** | ... | **1112** |`) and CLI_LANES_WO_NUMBERS.md contains ZERO of
# them - the live banner is blockquote prose at line 3:
#     > ## RECONCILED 2026-08-16 (CLI): main line next free = **1112**.
# so both regexes returned None and the board drew "Next mint - CLI: ?, UI seat: ?".
#
# That silence is the actual bug, worse than the wrong regex: a "?" reads as "nobody
# filled this in yet", not as "the numbering authority is UNREADABLE" - and an unreadable
# authority is the exact precondition for the five-collision day the banner itself
# documents (a seat that cannot read the next free number mints on top of another seat).
# So a parse failure is now LOUD in all three places a person could be looking: the board
# renders a visible error strip instead of a question mark, the console prints the miss,
# and --check refuses.
#
# Forms are tried NEWEST-FIRST BY FILE ORDER (the file is prepend-ordered, newest banner on
# top) and superseded headers strike their number through (`~~1000~~`), which no pattern
# matches - so a retired banner cannot win over the live one.
_BANNER_MAIN = [
    r"main line next free\s*=\s*\*\*(\d+)\*\*",                       # live prose form
    r"\|\s*\*\*main line\*\*\s*\|[^|]*\|\s*\*\*(\d+)\*\*",            # legacy table row
]
_BANNER_UI = [
    r"UI[- ]seat bumped\s*\d+\s*->\s*\*?\*?(\d+)",                    # live prose form
    r"UI seat next free\s*=\s*\*\*(\d+)\*\*",
    r"\|\s*\*\*1000[^|]*\*\*\s*\|[^|]*\|\s*\*\*(\d+)\*\*",            # legacy table row
]

def _first_match(text, patterns):
    """Earliest match in FILE ORDER across all patterns (newest banner is at the top)."""
    best = None
    for pat in patterns:
        m = re.search(pat, text)
        if m and (best is None or m.start() < best.start()):
            best = m
    return best.group(1) if best else None

def parse_banner():
    """(main_next, ui_next, errors) from the numbering authority. Never silently None."""
    path = os.path.join(ROOT, "CLI_LANES_WO_NUMBERS.md")
    errors = []
    try:
        text = open(path, encoding="utf-8", errors="replace").read()
    except OSError as e:
        return None, None, ["CLI_LANES_WO_NUMBERS.md unreadable ({}) - the WO-numbering "
                            "authority cannot be parsed at all.".format(e)]
    main = _first_match(text, _BANNER_MAIN)
    ui = _first_match(text, _BANNER_UI)
    if main is None:
        errors.append("could not parse the MAIN-LINE next-free number from CLI_LANES_WO_NUMBERS.md "
                      "(expected e.g. 'main line next free = **1112**')")
    if ui is None:
        errors.append("could not parse the UI-SEAT next-free number from CLI_LANES_WO_NUMBERS.md "
                      "(expected e.g. 'UI-seat bumped 1030 -> 1031')")
    return main, ui, errors

# ── CREATED date, never modified date (WO-940) ────────────────────────────────
# The board's date column used to be os.path.getmtime - LAST MODIFIED. Any edit (a status
# fix, a banner sweep) reset a ticket's apparent age, inverting the exact signal the owner
# wants ("opened within"). The date shown is the CREATED date, resolved in priority order:
#   1. **Minted:** YYYY-MM-DD parsed from the WO body  - authored by a human, most trustworthy
#   2. git first-add date of the file                  - ONE git call for all ~950 files
#   3. mtime                                           - last resort, visibly marked '~' (estimate)
# Age is DERIVED at generation time, never typed into the WO files (a stored age is stale the
# next morning - the disease this exists to cure).
_MINTED = re.compile(r"^\*\*Minted:?\*\*:?[^\n]*?(\d{4}-\d{2}-\d{2})", re.MULTILINE)

def git_added_dates():
    """Map basename -> YYYY-MM-DD of the commit that first ADDED the file.
    ONE git call over WorkOrders/ (a per-file loop over ~950 files would kill the
    2-second build). --no-renames so a file moved into WorkOrders/ still registers
    an Add there (rename detection would filter it to R and lose the date)."""
    dates = {}
    try:
        out = subprocess.run(
            ["git", "log", "--reverse", "--no-renames", "--diff-filter=A",
             "--date=short", "--format=%x01%ad", "--name-only", "--", "WorkOrders/"],
            cwd=ROOT, capture_output=True, text=True, timeout=120)
        cur = None
        for line in out.stdout.splitlines():
            if line.startswith("\x01"):
                cur = line[1:].strip()
            elif line.strip() and cur:
                # --reverse walks oldest-first; setdefault keeps the FIRST add.
                dates.setdefault(os.path.basename(line.strip()), cur)
    except Exception:
        pass  # no git / not a repo: every row falls back to the marked mtime estimate
    return dates

# ── WO-1080: CAPTURE PROVENANCE, and why a DATE cannot do this job ────────────
# Four layout tickets (WO-1075/1076/1077/1078) were minted from ONE aged capture log,
# `Builds/wo1060-capture.log`, and described a tree that had already moved on. WO-1076 was
# reopened against a panel fixed three days earlier (a2162f17d) and cost a seat a morning.
#
# ⛔ A CAPTURE LOG'S FILE DATE IS NOT EVIDENCE OF THE TREE IT MEASURED. That log's mtime is
# 2026-08-23 and its in-log licensing stamp is 2026-08-23T17:39:59Z; the fix it fails to
# contain landed 2026-08-21. The log is NEWER than the commit it does not have — so any
# mtime comparison is defeated by the exact case that motivated it. Only the COMMIT identifies
# the tree, which is why `UICaptureLaunch.RunCaptureHeadless` now stamps
# `UI_CAPTURE_HEAD <sha> <branch> dirty=<bool>` into every log it writes.
#
# A layout/touch ticket therefore carries, in its header block:
#
#     **Capture:** `Builds/<log>.log` @ `<sha>` — targets `Assets/.../<File>.cs`
#
# and this parser + the check below turn "is that citation still true?" into arithmetic:
# if the newest commit touching the target is NOT reachable from the cited sha, the capture
# predates the target's current state and the ticket is STALE-CAPTURE.
#
# REPORT-ONLY, exactly like DUPLICATE_WO_NUMBERS: a collision is its own finding and the board
# flags it rather than silently repairing it. A WO with no `**Capture:**` line is untouched —
# this binds layout/touch tickets, not the whole board.
_CAPTURE = re.compile(
    r"^\*\*Capture:?\*\*:?\s*`([^`]+)`\s*@\s*`([0-9a-fA-F]{7,40})`(.*)$",
    re.MULTILINE)

def parse_capture(text):
    """(log, sha, [target paths]) or (None, None, []) when the WO cites no capture."""
    m = _CAPTURE.search(text)
    if not m:
        return None, None, []
    log, sha, tail = m.group(1).strip(), m.group(2).strip().lower(), m.group(3)
    # Every backticked path on the rest of the line is a target. Multiple targets are
    # allowed on purpose: one ticket may legitimately name a panel and its VM.
    targets = [t.strip() for t in re.findall(r"`([^`]+)`", tail) if t.strip()]
    return log, sha, targets

def _git(args, timeout=30):
    """(returncode, stdout) — never raises. Returns (None, '') when git is unavailable."""
    try:
        out = subprocess.run(["git"] + args, cwd=ROOT, capture_output=True,
                             text=True, timeout=timeout)
        return out.returncode, (out.stdout or "").strip()
    except Exception:
        return None, ""

def check_capture_staleness(rows):
    """Annotate rows carrying a **Capture:** line with capture_verdict / capture_detail.

    Only rows that cite a capture cost a git call, so the ~2 s build is unaffected while
    the count of such tickets is small. Verdicts:
      FRESH   — the newest commit touching every target is reachable from the cited sha
      STALE   — it is NOT: the capture measured a tree that predates the target's state
      UNKNOWN — the sha, the target, or git itself could not be resolved (never silently FRESH)
    """
    cited = [r for r in rows if r.get("capture_sha")]
    if not cited:
        return []
    rc, _ = _git(["rev-parse", "--git-dir"])
    if rc != 0:
        for r in cited:
            r["capture_verdict"] = "UNKNOWN"
            r["capture_detail"] = "git unavailable, so the citation could not be checked"
        return cited

    newest_cache = {}
    for r in cited:
        sha = r["capture_sha"]
        rc, resolved = _git(["rev-parse", "--verify", "--quiet", sha + "^{commit}"])
        if rc != 0 or not resolved:
            r["capture_verdict"] = "UNKNOWN"
            r["capture_detail"] = "cited sha %s is not a commit in this clone" % sha
            continue
        if not r.get("capture_targets"):
            r["capture_verdict"] = "UNKNOWN"
            r["capture_detail"] = ("the **Capture:** line names no target file, so there is "
                                   "nothing to date the citation against")
            continue

        stale_for = []
        unknown_for = []
        for target in r["capture_targets"]:
            if target not in newest_cache:
                newest_cache[target] = _git(
                    ["log", "-1", "--format=%H", "--", target])[1]
            newest = newest_cache[target]
            if not newest:
                unknown_for.append(target + " (no commit touches it)")
                continue
            # Reachability, not dates: a commit made on another branch at an earlier clock
            # time can still be absent from the cited tree, and a later clock time can still
            # be an ancestor. `--is-ancestor` answers the only question that matters.
            anc, _ = _git(["merge-base", "--is-ancestor", newest, resolved])
            if anc != 0:
                stale_for.append("%s newest=%s" % (target, newest[:12]))

        if stale_for:
            r["capture_verdict"] = "STALE"
            r["capture_detail"] = ("cited capture %s predates: " % sha[:12]) + "; ".join(stale_for)
        elif unknown_for:
            r["capture_verdict"] = "UNKNOWN"
            r["capture_detail"] = "; ".join(unknown_for)
        else:
            r["capture_verdict"] = "FRESH"
            r["capture_detail"] = "cited capture %s contains every target's newest commit" % sha[:12]
    return cited

def resolve_created(text, base, mtime, added_dates):
    """(YYYY-MM-DD, is_estimate) per the priority order above."""
    m = _MINTED.search(text)
    if m:
        return m.group(1), False
    if base in added_dates:
        return added_dates[base], False
    return datetime.datetime.fromtimestamp(mtime).strftime("%Y-%m-%d"), True

def parse_wos():
    rows = []
    added_dates = git_added_dates()
    today = datetime.date.today()
    results = {os.path.basename(p).replace(".RESULT.md", ".md")
               for p in glob.glob(os.path.join(WO_DIR, "*.RESULT.md"))}
    for path in sorted(glob.glob(os.path.join(WO_DIR, "*.md"))):
        base = os.path.basename(path)
        if base.endswith(".RESULT.md"):
            continue
        try:
            text = open(path, encoding="utf-8", errors="replace").read(20000)
        except OSError:
            continue
        # ── TWO NUMBER NAMESPACES (owner ruling 2026-08-17, "PROD WO 001") ──────────
        # Echoes of Elarion is LIVE on the Solana dApp Store. Numbering restarts at
        # PROD-001 to draw a hard line at that date: everything before it was
        # pre-launch, everything after it ships to real players.
        #
        # LEGACY WO-#### IS FROZEN AND NEVER RENAMED. 1,154 files carry those numbers,
        # and so do code comments, commit messages, regression-suite headers and every
        # canon doc. Renaming would break far more than it tidies — the point is a
        # clean START, not a rewritten history.
        #
        # ⚠ WHY THIS PARSE EXISTS AT ALL: `is_work_order` matches any "WORK_ORDER_"
        # prefix, so a PROD file was already RECOGNISED — but the old
        # `WORK_ORDER_(\d+)` could not read "PROD-001", so every PROD ticket would have
        # silently joined the 18 legacy UNNUMBERED files: no sort order and, worse, no
        # duplicate detection. The collision guard would have been off for exactly the
        # tickets that matter most, and nothing would have said so.
        #
        # `prod` is carried alongside `num` (never merged into it) so PROD-001 can
        # never collide with legacy WO-1 in any downstream count or dedup.
        # MON is a LANE TAG, not a number series (owner ruling 2026-08-22: "add as a
        # tag WO XX - MON"). It rides on an ORDINARY banner-minted WO number, so there
        # stays exactly ONE numbering authority and the duplicate guard keeps working on
        # it - the thing that would have been silently off for a private series. The tag
        # only sets the DISPLAY label and the priority sort.
        # Matches WORK_ORDER_<num>_MON_<slug> and WORK_ORDER_<num>_MON-<slug>.
        #
        # UI-### is a THIRD sanctioned series, on exactly the PROD- footing (lead ruling
        # 2026-08-24). Same failure mode as PROD before it was taught: `is_work_order`
        # already RECOGNISED WORK_ORDER_UI-001_*.md, but no pattern could read its number,
        # so two live tickets rendered as the unassignable "WO-?" - including UI-001, which
        # is on the owner's active felt-test route. `ui` is carried alongside `num` and
        # `prod`, never merged, so UI-001 can never collide with legacy WO-1 or PROD-001.
        mon_tag = bool(re.match(r"WORK_ORDER_\d+[_-]MON[_-]", base, re.IGNORECASE))
        prod_m  = re.match(r"WORK_ORDER_PROD-(\d+)", base, re.IGNORECASE)
        ui_m    = re.match(r"WORK_ORDER_UI-(\d+)", base, re.IGNORECASE)
        num_m   = re.match(r"WORK_ORDER_(\d+)", base)
        mon     = None
        prod    = int(prod_m.group(1)) if prod_m else None
        ui      = int(ui_m.group(1)) if ui_m else None
        num     = int(num_m.group(1)) if (num_m and not prod_m and not ui_m) else None
        title_m = re.search(r"^#\s+(.+)$", text, re.MULTILINE)
        title = title_m.group(1).strip() if title_m else base
        title = re.sub(r"[*`]", "", title)
        status, malformed_status, near_miss_status = parse_status(text)
        status = re.sub(r"[*`]", "", status).strip()
        has_result = base in results
        is_wo = is_work_order(base)
        mtime = os.path.getmtime(path)
        created, created_est = resolve_created(text, base, mtime, added_dates)
        try:
            age_days = (today - datetime.date(*map(int, created.split("-")))).days
        except ValueError:
            age_days = 0
        bucket, fallback_bucketed = classify_status(status, has_result, is_wo)
        cap_log, cap_sha, cap_targets = parse_capture(text)   # WO-1080; (None, None, []) if absent
        rows.append({
            "capture_log": cap_log, "capture_sha": cap_sha, "capture_targets": cap_targets,
            "num": num, "prod": prod, "ui": ui, "mon_tag": mon_tag, "file": base, "title": title, "status": status,
            "bucket": bucket, "result": has_result, "malformed_status": malformed_status,
            "near_miss_status": near_miss_status,
            "fallback_bucketed": fallback_bucketed,
            "is_wo": is_wo, "mtime": mtime,
            "created": created, "created_est": created_est, "age_days": max(0, age_days),
        })
    return rows

def build_html(rows):
    main_next, ui_next, banner_errors = parse_banner()
    counts = {b: 0 for b in BUCKET_ORDER}
    for r in rows: counts[r["bucket"]] += 1
    stamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

    def row_html(r):
        # PROD tickets render as PROD-001 (post-launch, zero-padded so they sort as text
        # too); legacy pre-launch tickets keep WO-####. The two are deliberately visually
        # distinct on the board — at a glance you can see whether a row predates going live.
        if r.get("prod") is not None:
            num = f"PROD-{r['prod']:03d}"
        elif r.get("ui") is not None:
            num = f"UI-{r['ui']:03d}"
        elif r["num"] is not None:
            num = f"WO-{r['num']}"
        else:
            num = "DOC" if not r["is_wo"] else "WO-?"
        # LANE TAG appended to the number, so the board reads "WO-1146 - MON" exactly as
        # the owner asked. It is a SUFFIX on the real number, never a replacement - the
        # banner stays the one numbering authority and the duplicate guard keeps working.
        if r.get("mon_tag"):
            num += " - MON"
        # CREATED date + age in days (WO-940) - never mtime. '~' marks an mtime-estimated
        # date so nobody mistakes a guess for a creation date.
        est = "~" if r["created_est"] else ""
        days = r["age_days"]
        # Older than 7 days (SUNDAY_HOUSEKEEPING threshold): word/symbol PLUS colour -
        # the owner is red/green colourblind, a hue alone is invisible to her.
        old = ' <span class="oldm">7d+</span>' if days > 7 else ""
        color = BUCKET_COLOR[r["bucket"]]
        res = ' <span class="res">RESULT</span>' if r["result"] else ""
        # WO-1080. Rendered ONLY on a row that cites a capture, so every other row's HTML is
        # byte-identical to before. Reuses the existing word-plus-colour badge class: the
        # owner is red/green colourblind, so the WORD carries the finding, never the hue.
        cap = r.get("capture_verdict")
        if cap == "STALE":
            res += ' <span class="oldm">STALE-CAPTURE</span>'
        elif cap == "UNKNOWN":
            res += ' <span class="oldm">CAPTURE-UNVERIFIED</span>'
        # filename is in the search text so companion docs stay FINDABLE by name - that
        # discoverability is the whole reason they are bucketed rather than dropped (WO-937 A).
        search = " ".join((num, r["file"], r["title"], r["status"])).lower()
        return (f'<tr class="row" data-bucket="{r["bucket"]}" data-age-days="{days}" '
                f'data-text="{html.escape(search)}">'
                f'<td class="num"><a href="WorkOrders/{html.escape(r["file"])}">{num}</a></td>'
                f'<td class="title">{html.escape(r["title"][:110])}{res}</td>'
                f'<td><span class="badge" style="border-color:{color};color:{color}">'
                f'{r["bucket"]}</span></td>'
                f'<td class="status">{html.escape(r["status"][:80])}</td>'
                f'<td class="age">{est}{r["created"]} &middot; {days}d{old}</td></tr>')

    # Within a bucket: PROD tickets FIRST (newest first), then legacy WO (newest first).
    # Post-launch work outranks pre-launch work on sight — that is the whole point of the
    # namespace split, and a board that interleaved them would bury PROD-001 among 1,154
    # legacy rows.
    rows_sorted = sorted(rows, key=lambda r: (
        BUCKET_ORDER.index(r["bucket"]),
        0 if r.get("mon_tag") else 1,              # MON is the priority lane (owner 2026-08-22)
        0 if r.get("prod") is not None else 1,
        -(r.get("prod") or 0),
        0 if r.get("ui") is not None else 1,      # UI-### ranks after PROD, ahead of legacy
        -(r.get("ui") or 0),
        -(r["num"] or 0)))
    body_rows = "\n".join(row_html(r) for r in rows_sorted)
    filters = "".join(
        f'<button class="fbtn" data-f="{b}" style="border-color:{BUCKET_COLOR[b]}">'
        f'{b} <span class="cnt">{counts[b]}</span></button>' for b in BUCKET_ORDER)
    # LANE CHIP (owner 2026-08-22): MON is a dedicated, prioritised lane, so it gets its
    # own filter beside the buckets. It filters on the row TEXT containing "- mon", which
    # is the same suffix row_html writes into the number cell - one source, so the chip
    # and the label can never disagree. It composes with the bucket chips and the search
    # box via the existing AND, and is deliberately NOT a bucket: a MON ticket is still
    # Ready or Done like any other.
    mon_count = sum(1 for r in rows if r.get("mon_tag"))
    lane_filters = (f'<button class="lbtn" data-l="- mon" style="border-color:#d98f2b">'
                    f'MON <span class="cnt">{mon_count}</span></button>') if mon_count else ""

    # "Opened within" filter (WO-940): by CREATED age in days, composing with the
    # bucket chips and the search box (AND), never replacing them.
    def _within(d): return sum(1 for r in rows if r["age_days"] <= d)
    age_filters = (
        '<span class="agef">opened within: '
        + "".join(f'<button class="abtn" data-a="{d}">{d}d '
                  f'<span class="cnt">{_within(d)}</span></button>' for d in (7, 30, 90))
        + f'<button class="abtn on" data-a="all">all <span class="cnt">{len(rows)}</span></button>'
        '</span>')

    # A parse miss is NEVER a quiet "?" (WO-1112). The mint numbers are the collision guard;
    # if the board cannot read them it must SAY so, in the place the reader is already looking.
    if banner_errors:
        mint_html = ('<span class="bannererr">MINT NUMBERS UNREADABLE &mdash; '
                     + html.escape("; ".join(banner_errors))
                     + ' &mdash; do NOT mint from this board; read CLI_LANES_WO_NUMBERS.md directly.</span>')
    else:
        mint_html = f'Next mint &mdash; CLI: <b>{main_next}</b>, UI seat: <b>{ui_next}</b>'

    canon_links = "".join(
        f'<a href="{p}">{n}</a>' for n, p in [
            ("Canon loader", "SESSION_CANON_LOADER.md"), ("Handover", "docs/HANDOVER.md"),
            ("Master catalog", "docs/MASTER_CATALOG.md"), ("Pipeline state", "PIPELINE_STATE.md"),
            ("WO numbers (authority)", "CLI_LANES_WO_NUMBERS.md"), ("Key facts", "KEY_FACTS.md"),
            ("Docs index", "docs/README.md"), ("Project index", "PROJECT_INDEX.md"),
        ])

    return f"""<!DOCTYPE html><html><head><meta charset="utf-8">
<title>EoA Board - {stamp}</title>
<style>
 body{{background:#14151a;color:#ddd;font:14px/1.5 'Segoe UI',sans-serif;margin:0;padding:24px}}
 h1{{color:#e0b341;font-size:22px;margin:0 0 4px}}
 .sub{{color:#888;margin-bottom:14px}} .sub b{{color:#bbb}}
 .canon a{{color:#7fa8d9;margin-right:14px;text-decoration:none}} .canon{{margin-bottom:16px}}
 #q{{background:#1e2027;border:1px solid #333;color:#ddd;padding:8px 12px;width:340px;border-radius:6px}}
 .lbtn{{background:#1e2027;border:1px solid #d98f2b;color:#e8c07a;padding:6px 12px;margin-left:10px;border-radius:14px;cursor:pointer;font-weight:600}} .lbtn.off{{opacity:.35}}
 .fbtn{{background:#1e2027;border:1px solid #555;color:#ccc;padding:6px 12px;margin-left:6px;
        border-radius:14px;cursor:pointer}} .fbtn.off{{opacity:.35}}
 .cnt{{color:#888}}
 table{{border-collapse:collapse;width:100%;margin-top:14px}}
 td{{padding:6px 10px;border-bottom:1px solid #24262e;vertical-align:top}}
 .num a{{color:#e0b341;text-decoration:none;white-space:nowrap}}
 .badge{{border:1px solid;border-radius:10px;padding:1px 9px;font-size:12px;white-space:nowrap}}
 .status{{color:#999;font-size:12px}} .age{{color:#666;font-size:12px;white-space:nowrap}}
 .res{{background:#2c4a2c;color:#9c9;font-size:10px;padding:1px 6px;border-radius:8px;margin-left:6px}}
 .oldm{{border:1px solid #b08030;color:#d0a050;font-size:10px;padding:0 5px;border-radius:8px;margin-left:5px}}
 .bannererr{{background:#5a1f1f;border:1px solid #d06060;color:#ffd7d7;padding:2px 8px;
             border-radius:6px;font-weight:bold}}
 .agef{{margin-left:18px;color:#888;font-size:13px}}
 .abtn{{background:#1e2027;border:1px solid #555;color:#ccc;padding:6px 12px;margin-left:6px;
        border-radius:14px;cursor:pointer;opacity:.35}} .abtn.on{{opacity:1;border-color:#e0b341}}
</style></head><body>
<h1>Echoes of Elarion — Work Order Board</h1>
<div class="sub">Generated <b>{stamp}</b> from the repo (WorkOrders/*.md) — the repo is the source of
 truth, this page is a view. Regenerate: <b>python tools/board_build.py</b>
 &nbsp;|&nbsp; {mint_html}</div>
<div class="canon">{canon_links}</div>
<input id="q" placeholder="Search number / title / status...">{filters}{lane_filters}{age_filters}
<table><tbody id="tb">
{body_rows}
</tbody></table>
<script>
const q=document.getElementById('q'), rows=[...document.querySelectorAll('.row')];
const active=new Set({BUCKET_ORDER!r});
let ageMax=Infinity; // "opened within" (WO-940): ANDs with buckets + search
let lane=''; // LANE chip (MON): ANDs with buckets + search + age. '' = every lane.
function apply(){{const t=q.value.toLowerCase();
 rows.forEach(r=>{{r.style.display=(active.has(r.dataset.bucket)&&r.dataset.text.includes(t)
  &&(+r.dataset.ageDays<=ageMax)&&(lane===''||r.dataset.text.includes(lane)))?'':'none'}})}}
q.addEventListener('input',apply);
document.querySelectorAll('.fbtn').forEach(b=>b.addEventListener('click',()=>{{
 const f=b.dataset.f; if(active.has(f)){{active.delete(f);b.classList.add('off')}}
 else{{active.add(f);b.classList.remove('off')}} apply()}}));
document.querySelectorAll('.lbtn').forEach(b=>b.addEventListener('click',()=>{{
 const l=b.dataset.l; if(lane===l){{lane='';b.classList.add('off')}}
 else{{lane=l;b.classList.remove('off')}} apply()}}));
document.querySelectorAll('.abtn').forEach(b=>b.addEventListener('click',()=>{{
 ageMax=(b.dataset.a==='all')?Infinity:+b.dataset.a;
 document.querySelectorAll('.abtn').forEach(x=>x.classList.remove('on'));
 b.classList.add('on'); apply()}}));
</script></body></html>"""

def main():
    # WO-1011: --check makes the vocabulary ENFORCEABLE. An Unlabeled row is a defect in the
    # WO file (its **Status:** line contains no canonical keyword), and the board renders it
    # faithfully as "Unlabeled" — which reads like a category rather than a mistake. With this
    # flag the check-in gate can reject the drift instead of drawing it. Report-only by default:
    # a plain run must never start failing builds because a WO file is sloppy.
    check = "--check" in sys.argv

    rows = parse_wos()
    # Parsed here as well as inside build_html so the MISS reaches the console (and the
    # --check exit code), not just the page. A seat reading the terminal must not have to
    # open the HTML to discover the numbering authority went unreadable (WO-1112).
    _main_next, _ui_next, banner_errors = parse_banner()
    # WO-1080: annotate BEFORE the HTML is built, so a stale citation is visible on the page
    # as well as on the console. Costs nothing while no ticket cites a capture.
    cited_captures = check_capture_staleness(rows)
    html_text = build_html(rows)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html_text)
    from collections import Counter
    c = Counter(r["bucket"] for r in rows)
    n_wo = sum(1 for r in rows if r["is_wo"])
    print(f"BOARD.html written: {len(rows)} rows = {n_wo} work orders + {len(rows) - n_wo} docs "
          f"({', '.join(f'{b}:{c.get(b,0)}' for b in BUCKET_ORDER)})")

    # Doc rows can never be Unlabeled (bucket_of short-circuits), so this is a pure defect list:
    # real work orders whose **Status:** line carries no canonical keyword. Nothing else.
    unlabeled = [r for r in rows if r["bucket"] == "Unlabeled"]
    if unlabeled:
        # Named, not just counted — "91 Unlabeled" is unactionable; a list is a to-do.
        print(f"UNLABELED {len(unlabeled)} work order(s) — the **Status:** line carries no "
              f"canonical keyword (READY / FIXED / DONE / BLOCKED / SPEC / CLOSED). Fix the WO file:")
        for r in unlabeled[:40]:
            print(f"    WO-{r.get('num','?')}  {r.get('file','?')}  status={r.get('status','') !r}")
        if len(unlabeled) > 40:
            print(f"    ... and {len(unlabeled) - 40} more")

    malformed = [r for r in rows if r.get("malformed_status")]
    if malformed:
        print(f"MALFORMED_STATUS_MARKER {len(malformed)} work order(s) whose verdict comes from a "
              f"near-miss marker - no exact **Status:** in the file (row still rendered):")
        for r in malformed:
            print(f"    {r['file']}  status={r.get('status', '')!r}")
    near_miss = [r for r in rows if r.get("near_miss_status")]
    if near_miss:
        print(f"NEAR_MISS_STATUS_MARKER {len(near_miss)} work order(s) carry a `**Status: ...**` "
              f"line alongside a good marker (cosmetic now; it SHADOWED the real line under the "
              f"pre-WO-1180 pattern - e.g. WO-414). First 5:")
        for r in near_miss[:5]:
            print(f"    {r['file']}")

    fallback = [r for r in rows if r.get("fallback_bucketed")]
    print(f"FALLBACK_BUCKETED {len(fallback)} work order(s):")
    for r in fallback:
        print(f"    {r['file']}  -> {r['bucket']}  status={r.get('status', '')!r}")

    contradictions = []
    for r in rows:
        phrase = status_contradiction(r.get("status", ""), r["bucket"], r["file"])
        if phrase:
            contradictions.append((r, phrase))
    if contradictions:
        print(f"STATUS_CONTRADICTION {len(contradictions)} finished-verdict status line(s):")
        for r, phrase in contradictions:
            print(f"    {r['file']}  phrase={phrase!r}  status={r.get('status', '')!r}")

    # WO-1180: an UNNUMBERED work order renders as "WO-?", and "WO-?" is NOT an assignable
    # key - every unnumbered file shares it, so two unrelated tickets (the Ad Generator and
    # the Economy Store Packs) answer to the same handle and the duplicate guard above cannot
    # see them (it keys on `num`, which is None here). Report them by name; a ticket nobody
    # can address by number is a ticket that gets lost.
    unnumbered = [r["file"] for r in rows if r["is_wo"] and r["num"] is None
                  and r["prod"] is None and r.get("ui") is None]
    if unnumbered:
        print(f"UNNUMBERED_WO {len(unnumbered)} work order(s) render as WO-? "
              f"(not an assignable key - mint a number from the CLI_LANES_WO_NUMBERS.md banner):")
        for f in sorted(unnumbered):
            print(f"    {f}")

    # WO-937: duplicate WO numbers are REPORTED, never silently renumbered - a collision
    # is its own finding. Report-only: it does not change the --check exit contract.
    by_num = {}
    for r in rows:
        if r["num"] is not None:
            by_num.setdefault(r["num"], []).append(r["file"])
    dupes = {n: fs for n, fs in by_num.items() if len(fs) > 1}
    if dupes:
        print(f"DUPLICATE_WO_NUMBERS {len(dupes)} number(s) claimed by more than one file "
              f"(flagged, not renumbered - resolve first-on-disk-and-referenced-wins):")
        for n in sorted(dupes):
            print(f"    WO-{n}: " + " | ".join(sorted(dupes[n])))
    # WO-1080: a layout/touch ticket minted from a capture that predates its own target is
    # the defect that produced WO-1075/1076/1077/1078. Reported, never repaired - the same
    # contract as DUPLICATE_WO_NUMBERS above, and deliberately NOT part of the --check exit
    # code, so a plain run's existing pass/fail contract is unchanged.
    if cited_captures:
        stale = [r for r in cited_captures if r.get("capture_verdict") == "STALE"]
        unknown = [r for r in cited_captures if r.get("capture_verdict") == "UNKNOWN"]
        fresh = len(cited_captures) - len(stale) - len(unknown)
        if stale:
            print(f"STALE_CAPTURE {len(stale)} work order(s) cite a capture log taken BEFORE the "
                  f"newest commit touching their own target file - their measured geometry "
                  f"describes a tree that has moved on (do not act on the numbers; re-run the "
                  f"capture and re-mint):")
            for r in stale:
                print(f"    {r['file']}  {r.get('capture_detail','')}")
        if unknown:
            print(f"CAPTURE_UNVERIFIED {len(unknown)} work order(s) cite a capture that could not "
                  f"be checked (absence of proof is NOT freshness):")
            for r in unknown:
                print(f"    {r['file']}  {r.get('capture_detail','')}")
        print(f"CAPTURE_PROVENANCE {len(cited_captures)} cited - {fresh} fresh, "
              f"{len(stale)} stale, {len(unknown)} unverified")

    # WO-1112: the mint numbers are the two-seat collision guard. An unreadable banner is a
    # LOUD failure, never a "?" on the page - a seat that cannot read the next free number
    # mints on top of another seat, which is the five-collision day the banner documents.
    if banner_errors:
        print("BANNER_PARSE_FAIL - CLI_LANES_WO_NUMBERS.md could not be read for mint numbers:")
        for e in banner_errors:
            print(f"    {e}")
        print("    The board renders a visible error instead of a mint number. "
              "Fix the banner (or tools/board_build.py's patterns) before minting.")
    else:
        print(f"BANNER_OK next mint - CLI: {_main_next}, UI seat: {_ui_next}")

    # ⛔ THE MARKER EMITS ON EVERY RUN, not only under --check (lead ruling 2026-08-24).
    # This repo judges gates by MARKER PRESENCE ON A FRESH LOG, never by exit code
    # (CLAUDE.md 8/16; memory `gates-report-success-without-proving-it`). A marker you only
    # get when a human remembers a second flag is not a gate - it is the exact shape of
    # tools/regression/checkin_gate.ps1, which looked like a gate while failing to parse
    # under PS 5.1 for months. A plain `python tools/board_build.py` must now leave either
    # BOARD_CHECK_OK or BOARD_CHECK_FAIL on the log, so ABSENCE stays a failure signal.
    #
    # WHAT --check STILL OWNS, and it is the only thing: the EXIT CODE. Report-only by
    # default is deliberate (a plain run must never start failing builds because a WO file
    # is sloppy); the check-in gate opts into rejection. The checks themselves are
    # unchanged and unweakened - a dirty board never prints OK.
    problems = []
    if unlabeled: problems.append(f"{len(unlabeled)} unlabeled")
    if contradictions: problems.append(f"{len(contradictions)} status contradiction(s)")
    if banner_errors: problems.append(f"{len(banner_errors)} banner parse error(s)")
    if problems:
        print("BOARD_CHECK_FAIL " + ", ".join(problems))
        return 1 if check else 0
    print("BOARD_CHECK_OK 0 unlabeled, 0 status contradictions, mint numbers readable")
    return 0

if __name__ == "__main__":
    sys.exit(main())
