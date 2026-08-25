# WORK ORDER 831 — Echo Emergence Sprite Beat (2D unlock cutscene-lite)

**Status:** BLOCKED — on owner/art delivery. The code is implemented (2026-08-02, wired with the Guard fallback; see `WORK_ORDER_831_echo_emergence_sprite_beat.RESULT.md`), but the 6 emergence PNGs under `Assets/Resources/Echoes/Emergence/` are owner/art-supplied and **still absent at HEAD — the directory does not exist** (verified 2026-08-24). The beat degrades to the portrait until they land.

*(Board note 2026-08-24: bucket corrected Done → **BLOCKED**. The row led with IMPLEMENTED while its own text said the art had never arrived, so it rendered as finished work; the missing directory was checked at source this pass. Gates: unverified by this pass — the line's "pending gates" claim is 22 days old.)*
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** VFX/Presentation (§9) — self-contained, no gameplay/economy dependency. Runs parallel to WO-830.
**Sibling:** WO-830 (affinity/synergy economy). Do NOT couple; this is presentation only.

---

## 1. Why (owner intent, verbatim)
> "would it be possible to add small cutscenes of these echos emerging from the tree and coming to life, and pull
> from the lfs?" … clarified: **"leave the sprite as 2D — would only be to introduce new sprite and advance dialog."**

**Plain reading:** NOT a 3D cutscene. A lightweight **2D beat** at Echo unlock — show a new "emerging from the
Heart-tree / coming to life" sprite and advance the dialogue into the existing awakening card. Stays true to the
canon ruling (owner 2026-07-17): **"Echoes are portrait-card spirits, NOT 3D models."** No 3D, no video file.

## 2. Read-first — the current unlock flow
- `Assets/_Modules/Village/Harvest/EchoUnlockDialogue.cs` already builds the awakening card: portrait (from
  `EchoRosterCatalog.LoadPortrait`), name, `Flavor`, a "Tell me more" → `Lore` swap, and buttons
  ("I accept your power" / "Close" / "Tell me more"). It is invoked at unlock (`EchoService.AnnounceFoundingEcho`
  for #1; the wave-unlock bridge for #2–6). This is the file to extend — do NOT greenfield a new dialogue system.
- Portraits live at `Assets/Resources/Echoes/Portraits/<PortraitName>.(png|jpg)`, loaded as runtime sprites
  (`EchoRosterCatalog.LoadPortrait`, Guard-wrapped, text-fallback on missing). `.png/.jpg` are **LFS-tracked**
  (`.gitattributes`), so new sprite art rides LFS automatically — just add the files under `Resources/Echoes/`.

## 3. The design to build
Add a **pre-card emergence beat** to `EchoUnlockDialogue`:
1. On unlock, FIRST show an **emergence sprite** for that Echo — art of the spirit rising/forming out of the
   Heart-tree ("coming to life"), 2D. One intro dialogue line (e.g. "The Heart stirs… a keeper wakes.").
2. **Advance on tap** (a "Continue ▸" / tap-anywhere) into the EXISTING awakening card (portrait + flavor + buttons).
   The advance is the whole "cutscene" — introduce sprite → advance dialogue → land on the current card.
3. Optional polish (cheap, in-scope if trivial): a short fade/scale-in on the emergence sprite so it "emerges"
   rather than hard-cuts. Reuse existing kit tween/CanvasGroup fade; NO new tween lib, NO video, NO Timeline.

**Asset plan (LFS):** one emergence sprite per Echo (6 total) OR a single shared "emerging-from-tree" frame the
card tints/masks by element — `OWNER CONFIRM`: default = **one per Echo** (each spirit's own emergence read; matches
"introduce new sprite"). Place under `Assets/Resources/Echoes/Emergence/<PortraitName>_emerge.png` and load with the
same Guard-wrapped `Resources.Load<Texture2D>` + `Sprite.Create` pattern as `LoadPortrait` (text/portrait fallback
if the art is absent, so the beat NEVER blocks the unlock).

**Copy:** one short intro line per Echo (ASCII, colorblind-safe TMP) — reuse the roster entry's tone. Can live as a
new `EmergeLine` field on `EchoRosterEntry` (identity data, like `Flavor`/`Lore`).

## 4. Files to edit
- `Assets/_Modules/Village/Harvest/EchoUnlockDialogue.cs` — add the emergence beat + advance-to-card state.
- `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs` — add `EmergeLine` (+ emerge sprite name if not derived).
- `Assets/Resources/Echoes/Emergence/*.png` — new 2D emergence art (LFS; owner/art supplies — CLI wires with
  a Guard fallback so a missing file degrades to the existing portrait, never a blank/hang).

## 5. Acceptance criteria (headless + felt)
- [ ] At each Echo unlock, the emergence sprite + intro line show FIRST, then advance-on-tap into the awakening card.
- [ ] Missing emergence art degrades gracefully to the current card (Guard fallback; `[Flow:Echo]` warn, no hang).
- [ ] 2D only — no 3D model, no `VideoPlayer`, no `.mp4`. Honors the portrait-spirit canon.
- [ ] Headless UI-capture (`RunCaptureHeadless`, editor CLOSED) renders the new beat state without overlap/clipping,
      landscape + the mobile resolutions (per the UI review standard).
- [ ] Founding Echo (Aldwin, #1) and a wave-unlock Echo both play the beat (both entry paths covered).

## 6. Do NOT
- Do NOT introduce 3D Echo models or video (owner ruling; mobile/WebGL video decode is unreliable — run-defenders gotchas).
- Do NOT block or delay the unlock if art is missing (Guard fallback mandatory).
- Do NOT couple to WO-830 (economy) — presentation lane only.

## 7. OWNER CONFIRM (defaults; veto any — non-blocking)
1. One emergence sprite per Echo (default) vs. a single shared tinted frame.
2. Who supplies the 6 emergence sprites — owner/art drop into `Resources/Echoes/Emergence/`? (CLI wires the loader
   + fallback regardless, so code can land before final art.)
