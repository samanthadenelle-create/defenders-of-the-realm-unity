# UI_REVIEW — the review-drop convention

A reusable pattern for reviewing UI screens: compare each **template** (the
Blink design target) against the **delivered** runtime capture, side by side,
and mark a verdict per screen. The assembler does the copying so nobody
hand-copies PNGs again.

## The loop

1. **Drop `_mapping.json`** in this folder — an array of rows:
   ```json
   [
     {
       "screen": "HeroTalents",
       "panelId": "panel_HeroTalents",
       "frame": "Talent_Tree_Panel",
       "templatePng": "Assets/UI/Blink/Talent_Tree_Panel.png",
       "deliveredShot": "panel_HeroTalents.png",
       "shotExists": true
     }
   ]
   ```
   - `templatePng` — repo-relative path to the Blink template image.
   - `deliveredShot` — filename inside the runtime `ui-shots` folder
     (`%AppData%\..\LocalLow\DeNelle\Defenders of the Realm\ui-shots\`).
   - (another step produces `_mapping.json`; if it's missing the assembler
     just prints a message and exits cleanly.)

2. **Run the assembler** (from the repo root):
   ```
   powershell -ExecutionPolicy Bypass -File build-ui-review.ps1
   ```
   It is idempotent — re-run any time. For each row it (re)creates
   `NN_screen/` with `template.png`, `delivered.png` (or a `placeholder.txt`
   if no shot exists yet), and a `FEEDBACK.md` (only when missing — your
   notes are never overwritten). It also regenerates `INDEX.html`.

3. **Open `INDEX.html`** — the single scrollable contact sheet, template
   beside delivered for every screen, with a PAIR-COMPLETE / AWAITING-SHOT
   badge. This is the fast-compare surface.

4. **Mark each `NN_screen/FEEDBACK.md`** — tick PASS or FIX and write Notes.
   Re-running the assembler preserves everything you wrote.

## What's safe

- Standalone tooling — touches only this `UI_REVIEW/` folder. No Unity, no
  git, no `.cs` edits.
- Idempotent: existing `FEEDBACK.md` files with your markup are preserved;
  images are refreshed from source each run.

Portable to any project: copy `build-ui-review.ps1`, adjust the shots path if
the app name differs, drop a `_mapping.json`, and go.
