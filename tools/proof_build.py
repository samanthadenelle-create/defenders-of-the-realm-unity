#!/usr/bin/env python3
"""
proof_build.py - generate PROOF.html, the owner's evidence page.

OWNER DIRECTIVE 2026-08-26 (explicit, standing):
    "After each run you are REQUIRED to show image matching each item completed.
     All screenshots will be placed on a page i can access from the board as a
     image i click and it opens as a page with the proof. You will repeat this
     process till every story from the ready column is in the fixed bucket with
     proof for me to review."

So this page is not a nicety - it is the acceptance surface. A ticket is not
done until it has an entry here that the owner can look at.

DESIGN RULES
  * PROOF IS AN IMAGE, not a claim. A row without an image file on disk renders
    as MISSING and says so loudly. Never render an absent proof as a tick.
  * NOT EVERY TICKET HAS A VISUAL. Oracle work (a detector, a band, a
    de-duplication) has no screenshot. Those carry kind="oracle" and show the
    gate evidence instead - labelled as oracle proof, NOT dressed up as a
    screenshot of nothing. Honesty about the KIND of proof is the point.
  * EVERY ROW STATES HOW IT WAS CAPTURED - device / headless / editor / oracle.
    An editor capture is weaker evidence than a device capture and the page must
    not blur them. The owner plays on a Seeker; a thing that looks right in the
    editor has not been proven on the device.
  * The manifest is data (proof/manifest.json), so a run appends to it rather
    than rewriting this file.

USAGE
    python tools/proof_build.py            # regenerate PROOF.html from manifest
    python tools/proof_build.py --check    # report rows whose image is missing
"""

import html
import json
import os
import sys
from datetime import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANIFEST = os.path.join(ROOT, "proof", "manifest.json")
OUT = os.path.join(ROOT, "PROOF.html")

KIND_LABEL = {
    "device": ("DEVICE", "Captured on the Seeker - the strongest proof."),
    "headless": ("HEADLESS", "Captured by RunCaptureHeadless. Real UI, no device."),
    "editor": ("EDITOR", "Captured in a headed editor session. NOT device proof."),
    "oracle": ("ORACLE", "No visual exists. Gate evidence stands in its place."),
}


def load():
    if not os.path.exists(MANIFEST):
        return {"runs": []}
    with open(MANIFEST, encoding="utf-8") as f:
        return json.load(f)


def rel(p):
    """Path as the browser will see it, relative to the repo root."""
    return p.replace("\\", "/")


def build(data):
    runs = data.get("runs", [])
    rows = [(r, it) for r in runs for it in r.get("items", [])]
    total = len(rows)
    missing = [
        (r, it) for r, it in rows
        if it.get("kind") != "oracle"
        and not os.path.exists(os.path.join(ROOT, it.get("image", "") or "\0"))
    ]

    parts = [
        "<!doctype html><html><head><meta charset='utf-8'>",
        "<meta name='viewport' content='width=device-width,initial-scale=1'>",
        "<title>Proof - Echoes of Elarion</title><style>",
        "body{background:#15171c;color:#ddd;font:15px/1.5 system-ui,Segoe UI,sans-serif;margin:0;padding:22px}",
        "h1{color:#e0b341;font-size:22px;margin:0 0 4px}",
        ".sub{color:#8b8f99;margin:0 0 20px}",
        ".run{border:1px solid #3a3d48;border-radius:10px;background:#191b21;padding:16px;margin:0 0 18px}",
        ".run h2{margin:0 0 2px;font-size:17px;color:#e0b341}",
        ".meta{color:#8b8f99;font-size:13px;margin:0 0 14px}",
        ".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:14px}",
        ".card{border:1px solid #484c59;border-radius:8px;background:#20232b;overflow:hidden}",
        ".card a{display:block;color:inherit;text-decoration:none}",
        ".card img{width:100%;display:block;background:#000}",
        ".card .cap{padding:9px 11px}",
        ".wo{color:#e0b341;font-weight:600}",
        ".kind{display:inline-block;font-size:11px;letter-spacing:.06em;border:1px solid #5a5f6d;",
        "border-radius:4px;padding:1px 6px;margin-left:6px;color:#aab}",
        ".why{color:#9aa0ad;font-size:13px;margin-top:4px}",
        ".missing{border-color:#a04b4b}.missing .cap{background:#2a1c1c}",
        ".oracle{background:#000;color:#7fd18f;font:12px/1.45 Consolas,monospace;padding:12px;white-space:pre-wrap;word-break:break-word}",
        ".warn{border:1px solid #a04b4b;background:#2a1c1c;border-radius:8px;padding:12px;margin:0 0 18px}",
        "</style></head><body>",
        "<h1>Proof</h1>",
        f"<p class='sub'>{total} item(s) across {len(runs)} run(s). "
        f"Generated {html.escape(datetime.now().strftime('%Y-%m-%d %H:%M'))}. "
        "Click any image to open it full size.</p>",
    ]

    if missing:
        parts.append("<div class='warn'><b>%d item(s) claim proof but the image is not on disk.</b> "
                     "They are marked MISSING below and must not be read as done.</div>" % len(missing))

    for run in reversed(runs):  # newest first
        parts.append("<div class='run'>")
        parts.append(f"<h2>{html.escape(run.get('title','Run'))}</h2>")
        parts.append("<p class='meta'>%s</p>" % html.escape(run.get("meta", "")))
        parts.append("<div class='grid'>")
        for it in run.get("items", []):
            wo = html.escape(str(it.get("wo", "?")))
            title = html.escape(it.get("title", ""))
            kind = it.get("kind", "device")
            klabel, ktip = KIND_LABEL.get(kind, ("?", ""))
            why = html.escape(it.get("why", ""))
            img = it.get("image", "")
            present = bool(img) and os.path.exists(os.path.join(ROOT, img))

            if kind == "oracle":
                body = "<div class='oracle'>%s</div>" % html.escape(it.get("evidence", "(no evidence recorded)"))
                cls = "card"
            elif present:
                body = "<a href='%s' target='_blank'><img src='%s' alt='%s'></a>" % (
                    html.escape(rel(img)), html.escape(rel(img)), wo)
                cls = "card"
            else:
                body = "<div class='oracle'>PROOF MISSING - no image at %s</div>" % html.escape(rel(img) or "(none)")
                cls = "card missing"

            parts.append(f"<div class='{cls}'>{body}<div class='cap'>"
                         f"<span class='wo'>{wo}</span><span class='kind' title='{html.escape(ktip)}'>{klabel}</span>"
                         f"<div>{title}</div>"
                         + (f"<div class='why'>{why}</div>" if why else "")
                         + "</div></div>")
        parts.append("</div></div>")

    parts.append("</body></html>")
    return "\n".join(parts)


def main():
    data = load()
    if "--check" in sys.argv:
        bad = 0
        for run in data.get("runs", []):
            for it in run.get("items", []):
                if it.get("kind") == "oracle":
                    continue
                p = it.get("image", "")
                if not p or not os.path.exists(os.path.join(ROOT, p)):
                    print(f"  MISSING  WO-{it.get('wo')}  {p or '(no image)'}")
                    bad += 1
        print(f"PROOF_CHECK_{'FAIL' if bad else 'OK'} {bad} missing")
        return 1 if bad else 0

    with open(OUT, "w", encoding="utf-8") as f:
        f.write(build(data))
    n = sum(len(r.get("items", [])) for r in data.get("runs", []))
    print(f"PROOF_BUILD_OK {OUT} - {n} item(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
