# Intro Image Slates — generation guide (WO-561)

The opening intro is a **5-slate, ~30-second, skippable** cinematic built in code
(`Assets/_Modules/DialogueUI/IntroSequencePlayer.cs`). Each slate is one full-screen
image + one caption, held ~5.5s, with a dip-to-black between beats. Tap advances a
beat; the Skip button (or any key) jumps straight to hero select.

**Owner action:** generate the 5 images below and drop them at the listed Resources
paths. The player loads them by name — no scene wiring. A missing image degrades to
caption-on-black (a LogWarning, never a crash), so the intro always runs.

## Drop location & format
- Path: `Assets/Resources/Intro/<name>.png` (import as **Sprite (2D and UI)**).
- The code does `Resources.Load<Sprite>("Intro/<name>")` — match the names EXACTLY.
- Aspect: authored landscape but displayed full-screen **stretched** (`preserveAspect = false`),
  so compose with safe margins and keep the focal subject centred. The caption sits in a
  black + gold band across the lower ~17% — keep that zone clean / low-detail.
- Recommended source: 1920×1080 (or larger 16:9). Mobile/portrait will stretch; avoid
  text baked into the image.

## Art direction (keep consistent with `docs/ART/GAME_COVER_ART_DIRECTION.md` + `docs/NARRATIVE/STORY_BIBLE_POLISH.md`)
Painterly fantasy, warm gold light vs. cold grey "Dimming" mist, the Heart of Elarion
as a luminous **world-tree** at the centre of every beat. The Hollow Ones read as
**sorrowful, broken** — not snarling monsters (grief, not evil). Tragic, hopeful, not grim-dark.

---

## Slate 1 — The Heart ablaze
- **File:** `Assets/Resources/Intro/intro-heart-ablaze.png`
- **Hold:** 5.5s
- **Caption:** *"Once, the Heart of Elarion blazed — a world-tree whose light was the breath of all living things."*
- **PROMPT:** A colossal luminous world-tree at the centre of a thriving fantasy realm at golden hour, radiant amber light pouring from its canopy and roots, lush green valleys and a small stone village beneath it, motes of golden life-light drifting upward, painterly concept-art, warm and sacred, cinematic wide shot, volumetric god-rays, no text. Lower third kept dark and simple for a caption band.

## Slate 2 — The Dimming
- **File:** `Assets/Resources/Intro/intro-dimming.png`
- **Hold:** 5.5s
- **Caption:** *"Then came the Dimming: a grief older than memory, and the Heart's light began to fail."*
- **PROMPT:** The same world-tree now half-darkened, its golden glow guttering and bleeding out into a creeping cold grey mist, colour draining from the land, one half still faintly warm and the other desaturated and ashen, sorrowful painterly fantasy, the light visibly being siphoned away into the dark, cinematic, melancholy, no text. Lower third kept dark for a caption band.

## Slate 3 — The Hollow Ones
- **File:** `Assets/Resources/Intro/intro-hollow-ones.png`
- **Hold:** 5.5s
- **Caption:** *"The Hollow Ones rose — not monsters, but the broken, drawn to the last warmth they could feel."*
- **PROMPT:** A procession of hollow, translucent, sorrowful humanoid silhouettes drifting through cold grey mist toward the faint glow of the dimming world-tree, faces empty and mournful rather than monstrous, an orc legion's tattered war-banner looming behind them in the gloom, painterly fantasy, tragic and eerie, muted desaturated palette with a single distant warm ember, cinematic, no text. Lower third kept dark for a caption band.

## Slate 4 — The Knight's call
- **File:** `Assets/Resources/Intro/intro-knight-call.png`
- **Hold:** 5.5s
- **Caption:** *"One answered. A knight, Grom, sworn to carry a single ember back into the dark."*
- **PROMPT:** A lone armored knight (Grom) seen from behind/three-quarter, silhouetted against the dim world-tree, cupping a single glowing golden ember in one gauntleted hand that lights his face and pauldrons, sword and shield on his back, resolute and weary, painterly fantasy, dramatic rim light from the ember against cold blue-grey dusk, cinematic, hopeful, no text. Lower third kept dark for a caption band.

## Slate 5 — The reclaim (title card beat)
- **File:** `Assets/Resources/Intro/intro-reclaim.png`
- **Hold:** 6s
- **Caption:** *"Drive back the dark. Let the Heart grow. Reclaim the light of Elarion."*
- **Overlay (drawn in code):** title "DEFENDERS OF THE REALM" + subtitle "Echoes of Elarion" in gold over the upper-middle of this slate — leave the top-centre relatively open.
- **PROMPT:** The knight Grom stepping forward toward the world-tree as a sliver of golden light returns to its trunk and the grey mist begins to recede, faint glowing spirit-echoes (small luminous motes/wisps) stirring and rising around the roots, warmth pushing back into a desaturated land, painterly fantasy, triumphant and hopeful sunrise palette, cinematic wide shot, upper-centre kept open for a title, lower third kept dark for a caption band, no text.

---

## Notes for the engineer / tuning
- Slate data (paths, captions, holds) lives in `IntroSequenceDriver.Slates` in
  `IntroSequencePlayer.cs`. Captions are canon-sourced from `STORY_BIBLE_POLISH.md`.
- Total ≈ 5×5.5 + 6 ≈ 28.5s of holds + ~0.35s dips ≈ **~30s**. Adjust `Hold` values to retime.
- Triggered by the Title screen "Play Intro" button via `IntroLauncher.Play` (unchanged seam).
- Caption chrome = black band (`0.02,0.02,0.025,0.72`) + a 3px gold rule (`ElarionUi.Gold`),
  matching the Obsidian black+gold panel canon.
