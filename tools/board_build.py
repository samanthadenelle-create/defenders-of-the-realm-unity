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
import os, re, glob, html, time, datetime, sys

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

def is_work_order(basename):
    """True for a real work order (numbered or legacy-unnumbered); False for a companion doc."""
    return bool(_WO_FILENAME.match(basename))

# ── status bucketing (keyword priority order) ─────────────────────────────────
def bucket_of(status_text, has_result, is_wo=True):
    # Companion docs are out of the status workflow entirely - never Unlabeled, never a defect.
    if not is_wo: return "Doc"
    s = (status_text or "").upper()
    if "SUPERSEDED" in s or "CLOSED" in s or "CANCELLED" in s: return "Closed"
    if has_result or "DONE" in s or "IMPLEMENTED" in s or "COMPLETE" in s: return "Done"
    if "BLOCKED" in s: return "Blocked"
    if "READY" in s: return "Ready"
    if "DRAFT" in s or "SPEC" in s or "NOT STARTED" in s or "PROPOSAL" in s: return "Spec"
    return "Unlabeled"

BUCKET_ORDER = ["Ready", "Blocked", "Spec", "Unlabeled", "Done", "Closed", "Doc"]
BUCKET_COLOR = {"Ready": "#e0b341", "Blocked": "#d06060", "Spec": "#7fa8d9",
                "Unlabeled": "#999999", "Done": "#6fae6f", "Closed": "#777777",
                "Doc": "#a98fd0"}

def parse_banner():
    """Next-free WO numbers from the numbering authority (the two-block table)."""
    path = os.path.join(ROOT, "CLI_LANES_WO_NUMBERS.md")
    main = ui = None
    try:
        text = open(path, encoding="utf-8", errors="replace").read()
        m = re.search(r"\|\s*\*\*main line\*\*\s*\|[^|]*\|\s*\*\*(\d+)\*\*", text)
        u = re.search(r"\|\s*\*\*1000[^|]*\*\*\s*\|[^|]*\|\s*\*\*(\d+)\*\*", text)
        main = m.group(1) if m else None
        ui = u.group(1) if u else None
    except OSError:
        pass
    return main, ui

def parse_wos():
    rows = []
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
        num_m = re.match(r"WORK_ORDER_(\d+)", base)
        num = int(num_m.group(1)) if num_m else None
        title_m = re.search(r"^#\s+(.+)$", text, re.MULTILINE)
        title = title_m.group(1).strip() if title_m else base
        title = re.sub(r"[*`]", "", title)
        status_m = re.search(r"^\*\*Status:?\*?\*?:?\s*(.+)$", text, re.MULTILINE)
        status = re.sub(r"[*`]", "", status_m.group(1)).strip() if status_m else ""
        has_result = base in results
        is_wo = is_work_order(base)
        rows.append({
            "num": num, "file": base, "title": title, "status": status,
            "bucket": bucket_of(status, has_result, is_wo), "result": has_result,
            "is_wo": is_wo, "mtime": os.path.getmtime(path),
        })
    return rows

def build_html(rows):
    main_next, ui_next = parse_banner()
    counts = {b: 0 for b in BUCKET_ORDER}
    for r in rows: counts[r["bucket"]] += 1
    stamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

    def row_html(r):
        num = f"WO-{r['num']}" if r["num"] is not None else ("DOC" if not r["is_wo"] else "WO-?")
        age = datetime.datetime.fromtimestamp(r["mtime"]).strftime("%Y-%m-%d")
        color = BUCKET_COLOR[r["bucket"]]
        res = ' <span class="res">RESULT</span>' if r["result"] else ""
        # filename is in the search text so companion docs stay FINDABLE by name - that
        # discoverability is the whole reason they are bucketed rather than dropped (WO-937 A).
        search = " ".join((num, r["file"], r["title"], r["status"])).lower()
        return (f'<tr class="row" data-bucket="{r["bucket"]}" '
                f'data-text="{html.escape(search)}">'
                f'<td class="num"><a href="WorkOrders/{html.escape(r["file"])}">{num}</a></td>'
                f'<td class="title">{html.escape(r["title"][:110])}{res}</td>'
                f'<td><span class="badge" style="border-color:{color};color:{color}">'
                f'{r["bucket"]}</span></td>'
                f'<td class="status">{html.escape(r["status"][:80])}</td>'
                f'<td class="age">{age}</td></tr>')

    rows_sorted = sorted(rows, key=lambda r: (BUCKET_ORDER.index(r["bucket"]), -(r["num"] or 0)))
    body_rows = "\n".join(row_html(r) for r in rows_sorted)
    filters = "".join(
        f'<button class="fbtn" data-f="{b}" style="border-color:{BUCKET_COLOR[b]}">'
        f'{b} <span class="cnt">{counts[b]}</span></button>' for b in BUCKET_ORDER)

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
 .fbtn{{background:#1e2027;border:1px solid #555;color:#ccc;padding:6px 12px;margin-left:6px;
        border-radius:14px;cursor:pointer}} .fbtn.off{{opacity:.35}}
 .cnt{{color:#888}}
 table{{border-collapse:collapse;width:100%;margin-top:14px}}
 td{{padding:6px 10px;border-bottom:1px solid #24262e;vertical-align:top}}
 .num a{{color:#e0b341;text-decoration:none;white-space:nowrap}}
 .badge{{border:1px solid;border-radius:10px;padding:1px 9px;font-size:12px;white-space:nowrap}}
 .status{{color:#999;font-size:12px}} .age{{color:#666;font-size:12px;white-space:nowrap}}
 .res{{background:#2c4a2c;color:#9c9;font-size:10px;padding:1px 6px;border-radius:8px;margin-left:6px}}
</style></head><body>
<h1>Echoes of Elarion — Work Order Board</h1>
<div class="sub">Generated <b>{stamp}</b> from the repo (WorkOrders/*.md) — the repo is the source of
 truth, this page is a view. Regenerate: <b>python tools/board_build.py</b>
 &nbsp;|&nbsp; Next mint — CLI: <b>{main_next or "?"}</b>, UI seat: <b>{ui_next or "?"}</b></div>
<div class="canon">{canon_links}</div>
<input id="q" placeholder="Search number / title / status...">{filters}
<table><tbody id="tb">
{body_rows}
</tbody></table>
<script>
const q=document.getElementById('q'), rows=[...document.querySelectorAll('.row')];
const active=new Set({BUCKET_ORDER!r});
function apply(){{const t=q.value.toLowerCase();
 rows.forEach(r=>{{r.style.display=(active.has(r.dataset.bucket)&&r.dataset.text.includes(t))?'':'none'}})}}
q.addEventListener('input',apply);
document.querySelectorAll('.fbtn').forEach(b=>b.addEventListener('click',()=>{{
 const f=b.dataset.f; if(active.has(f)){{active.delete(f);b.classList.add('off')}}
 else{{active.add(f);b.classList.remove('off')}} apply()}}));
</script></body></html>"""

def main():
    # WO-1011: --check makes the vocabulary ENFORCEABLE. An Unlabeled row is a defect in the
    # WO file (its **Status:** line contains no canonical keyword), and the board renders it
    # faithfully as "Unlabeled" — which reads like a category rather than a mistake. With this
    # flag the check-in gate can reject the drift instead of drawing it. Report-only by default:
    # a plain run must never start failing builds because a WO file is sloppy.
    check = "--check" in sys.argv

    rows = parse_wos()
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
              f"canonical keyword (READY / DONE / BLOCKED / SPEC / CLOSED). Fix the WO file:")
        for r in unlabeled[:40]:
            print(f"    WO-{r.get('num','?')}  {r.get('file','?')}  status={r.get('status','') !r}")
        if len(unlabeled) > 40:
            print(f"    ... and {len(unlabeled) - 40} more")
    if check:
        if unlabeled:
            print(f"BOARD_CHECK_FAIL {len(unlabeled)} unlabeled")
            return 1
        print("BOARD_CHECK_OK 0 unlabeled")
    return 0

if __name__ == "__main__":
    sys.exit(main())
