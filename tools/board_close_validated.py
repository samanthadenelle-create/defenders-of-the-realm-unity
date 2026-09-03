#!/usr/bin/env python3
"""Close owner Pass-validated FIXED tickets.

PRIMARY SOURCE: proof/owner-validations.json - the durable, committed record (see
tools/owner_validations.py). Closes FIXED+Pass, bounces FIXED+Fail/Needs Work,
rebuilds BOARD.html. Prints CLOSED/BOUNCED/SKIP lines. Does not git commit.

FALLBACK, kept only for marks that never made it into the record: copy Chrome's
LevelDB out of the user profile and regex-salvage JSON fragments from the raw bytes.
That hack existed because the board's sign-offs lived in browser localStorage under a
per-commit key and the CLI had no other way to see them. It works on exactly one
desktop browser and never on the phone the owner actually validates from, which is
why the record now exists. Do not extend it; extend the record.
"""
from __future__ import annotations

import datetime
import glob
import os
import re
import shutil
import subprocess
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import owner_validations

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WO_DIR = os.path.join(ROOT, "WorkOrders")
LS_DST = os.path.join(ROOT, "tmp", "chrome-ls")
CHROME_LS = os.path.join(
    os.path.expanduser("~"),
    r"AppData\Local\Google\Chrome\User Data\Default\Local Storage\leveldb",
)


def first_status(text: str) -> str:
    m = re.search(r"^\*\*Status:\*\*\s*(.+)$", text, re.M)
    return m.group(1).strip() if m else ""


def set_status(path: str, new: str) -> bool:
    t = open(path, encoding="utf-8", errors="replace").read()
    t2, n = re.subn(r"(?m)^(\*\*Status:\*\*\s*).+$", r"\1" + new, t, count=1)
    if n != 1:
        return False
    open(path, "w", encoding="utf-8", newline="\n").write(t2)
    return True


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


def apply(found: dict[str, tuple[str, str]]) -> tuple[list[str], list[str]]:
    closed, bounced = [], []
    today = datetime.date.today().isoformat()
    stamp_close = f"CLOSED {today} — owner Pass (felt-validated)."
    for name, (verdict, note) in sorted(found.items()):
        path = os.path.join(WO_DIR, name)
        if not os.path.isfile(path):
            print("missing", name)
            continue
        t = open(path, encoding="utf-8", errors="replace").read()
        st = first_status(t)
        up = st.upper()
        if verdict == "Pass":
            if up.startswith("CLOSED"):
                continue
            if not up.startswith("FIXED"):
                print("skip not-fixed Pass", name, st[:50])
                continue
            if set_status(path, stamp_close):
                closed.append(name)
                print("CLOSED", name)
        elif verdict in ("Fail", "Needs Work"):
            if up.startswith("READY"):
                continue
            if not up.startswith("FIXED"):
                print("skip not-fixed", verdict, name, st[:50])
                continue
            note_s = (note or "").replace("\n", " ").strip()
            if len(note_s) > 160:
                note_s = note_s[:157] + "..."
            stamp = (
                f"READY TO IMPLEMENT — owner felt-test {today} {verdict}"
                + (f': "{note_s}"' if note_s else ".")
                + " Bounced from Fixed."
            )
            if set_status(path, stamp):
                bounced.append(name)
                print("BOUNCED", name, verdict, note_s[:80])
    return closed, bounced


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
    closed, bounced = apply(found)
    subprocess.check_call([sys.executable, os.path.join(ROOT, "tools", "board_build.py")])
    print("BOARD_PASS_OK closed", len(closed), "bounced", len(bounced))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
