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
def bucket_of(status_text, has_result, is_wo=True):
    # Companion docs are out of the status workflow entirely - never Unlabeled, never a defect.
    if not is_wo: return "Doc"
    s = (status_text or "").upper()
    if "SUPERSEDED" in s or "CLOSED" in s or "CANCELLED" in s: return "Closed"
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
    if s.lstrip().startswith("FIXED"): return "Fixed"
    if has_result or "DONE" in s or "IMPLEMENTED" in s or "COMPLETE" in s: return "Done"
    if "BLOCKED" in s: return "Blocked"
    if "READY" in s or "IN PROGRESS" in s: return "Ready"
    # PARKED / FUTURE / LATENT are all "real, understood, deliberately not scheduled" - the same
    # shape as SPEC, and all three were in live use on WO status lines while landing in Unlabeled
    # (owner-reported 2026-08-23: WO-1148 "FUTURE - not scheduled", WO-1140 "LATENT GUARD - NOT AN
    # ACTIVE DEFECT"). Bucketing them is right BECAUSE they are considered decisions, not gaps.
    if ("DRAFT" in s or "SPEC" in s or "NOT STARTED" in s or "PROPOSAL" in s
            or "PARKED" in s or "FUTURE" in s or "LATENT" in s): return "Spec"
    return "Unlabeled"

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
        mon_tag = bool(re.match(r"WORK_ORDER_\d+[_-]MON[_-]", base, re.IGNORECASE))
        prod_m  = re.match(r"WORK_ORDER_PROD-(\d+)", base, re.IGNORECASE)
        num_m   = re.match(r"WORK_ORDER_(\d+)", base)
        mon     = None
        prod    = int(prod_m.group(1)) if prod_m else None
        num     = int(num_m.group(1)) if (num_m and not prod_m) else None
        title_m = re.search(r"^#\s+(.+)$", text, re.MULTILINE)
        title = title_m.group(1).strip() if title_m else base
        title = re.sub(r"[*`]", "", title)
        status_m = re.search(r"^\*\*Status:?\*?\*?:?\s*(.+)$", text, re.MULTILINE)
        status = re.sub(r"[*`]", "", status_m.group(1)).strip() if status_m else ""
        has_result = base in results
        is_wo = is_work_order(base)
        mtime = os.path.getmtime(path)
        created, created_est = resolve_created(text, base, mtime, added_dates)
        try:
            age_days = (today - datetime.date(*map(int, created.split("-")))).days
        except ValueError:
            age_days = 0
        rows.append({
            "num": num, "prod": prod, "mon_tag": mon_tag, "file": base, "title": title, "status": status,
            "bucket": bucket_of(status, has_result, is_wo), "result": has_result,
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

    if check:
        problems = []
        if unlabeled: problems.append(f"{len(unlabeled)} unlabeled")
        if banner_errors: problems.append(f"{len(banner_errors)} banner parse error(s)")
        if problems:
            print("BOARD_CHECK_FAIL " + ", ".join(problems))
            return 1
        print("BOARD_CHECK_OK 0 unlabeled, mint numbers readable")
    return 0

if __name__ == "__main__":
    sys.exit(main())
