# UI rework — Seeker device review, 2026-08-04

**For:** the UI seat. **Numbers to be assigned by the owner** (UI-seat block 860–899).
**Status:** SPEC — findings + intent. Not implemented.
**Captured on:** Seeker `SM02G4061955851`, 2340×1080 landscape, build installed 22:20 (commit `829e5585`).
**Owner:** *"the one on screen now I think needs a little rework on styling."*

**These are real device captures, not headless renders.** `RunCaptureHeadless` renders code-built panels
only — it cannot see world-space UI, native aspect, or real DPI text scaling. Several defects below are
invisible to it by construction, which is why they survived.

| File | Screen |
|---|---|
| `01-title-screen.png` | Title / main menu |
| `02-post-title.png` | (post-title) |
| `03-town.png` | Town / play HUD |
| `04-rumor-board.png` | Brom's Rumor Board (quests) |
| `05-rumor-board-b.png` | Brom's Rumor Board, second state |
| `06-combat-hud.png` | Combat HUD (dungeon/arena) |
| `07-skills-panel.png` | Grom (Knight) Skills |
| `08-portal-magenta.png` | Dungeon portal - REBUILD (section 5) |

---

## ⚠ 0. READ THIS FIRST — the recurring failure class

**Four of these screens show content overflowing or overlapping its container.** That is the same defect
this project has hit repeatedly (WO-841 / WO-852 fraction-band culling), and its documented root cause is:

> **Text and content bands must be FIXED PIXELS >= the font line height — never a fraction of parent.**

A layout expressed in percentages looks correct in the editor at one aspect and culls, clips or overlaps
at 2340×1080. **Every fix below must use fixed-pixel bands.** If a rework replaces one fraction layout
with another, it will regress the moment the aspect changes again.

Binding on all of it: `MinTouchPx = 112`; **text-encoded state, never colour alone** (the owner is
red/green colourblind); ASCII-only TMP strings (no glyph icons — LiberationSans SDF has no clock/symbol
glyphs, they render as tofu); strict MVVM, the `[ui-mvvm]` ratchet is armed (`HardFailOnNew = true`);
**no new reflection bridge**, no new `static_gate.py` allowlist entry; landscape only.

---

## 1. `07-skills-panel.png` — BROKEN, fix first

This is not a styling issue. The panel is structurally failing.

- **The skill grid overflows its container on BOTH sides.** The leftmost node is cut off at the left panel
  edge; the rightmost node is cut off at the right. The grid is wider than the frame that holds it.
- **A label renders UNDER an icon.** `Universal / any class` reads as `Univers[icon]y class` — a skill
  node is drawn on top of the text.
- **Cancel / CONFIRM / Respec are drawn OVER the grid** and over the ability list beneath it.
- **`Emberbrand Thro` is truncated** (`Throw`), and ability slot `4` is empty/clipped by the Respec button.
- **CONFIRM's green fill bleeds past its own button bounds** to the right.
- **Three button styles in one row** — Cancel (plain), CONFIRM (green fill), Respec 300c (grey box).

**Intent:** the grid must scroll or reflow inside a fixed region; the action row must be its own band
below the content, not floating over it; the ability list needs a reserved band that cannot be
overlapped. One button treatment, differentiated by emphasis, not by three different chrome styles.

*(The "Calls and notifications will vibrate" pill is an Android system toast — not ours.)*

---

## 2. `04-rumor-board.png` / `05-rumor-board-b.png` — Brom's Rumor Board

**Bug:** the **tab strip is clipped**. `* All` / `Story` / `Daily` / `G` — the fourth tab is cut off where
the right detail panel's edge crosses it. Tab row and detail panel overlap.

**Styling:**
- **The frame and the contents disagree.** An ornate metal frame (rivets, scrollwork, corner detail)
  wrapping flat black rectangles with plain text. The frame promises a crafted board; the contents read as
  a debug list. This mismatch is most of why it looks unfinished.
- **Roughly half the panel is dead space.** One quest, then a large empty black region in both columns.
  The board is sized for content it does not have, so an early-game player sees an empty box rather than
  "you're early."
- **Three visual languages for the same class of information:** `Crystals 150` / `Food 20` outlined chips,
  `Story Quest` / `New` outlined differently, and the quest row a filled black bar.
- **`Close` floats** — centred at the bottom straddling both columns and overlapping the frame edge,
  rather than sitting in consistent panel chrome.

**KEEP:** `* All` marks selection with **both** an asterisk and an underline — text-encoded, not colour
alone. **Do not replace this with a colour highlight.** It is correct as-is and it is the pattern the rest
of the rework should follow.

---

## 3. `06-combat-hud.png` — Combat HUD

**The enemy nameplate is assembled from four disconnected pieces:** the `Orcish Warrior` label, an empty
black bar, the actual green/blue HP bar offset to its right, and `Lv 8` floating far right. They do not
align into one plate. A ragged/torn edge graphic overlaps the assembly.

- **The hero and Heart plates carry the same ragged edge artifacts** — grey jagged shapes at the right end
  of each bar that read as broken sprites rather than deliberate damage-styling.
- **`Echoes 1/6`** floats between the two plates with a stray gold rule, in no established band.
- **The right edge is ungrouped:** `Flee` (plain grey box), `Echoes` (different grey box), free-floating
  circular ability icons at inconsistent sizes and spacings, `Dodge/Attack` in a circle, and a weapon
  slider — no alignment, no grouping, no shared chrome.
- **The bottom ability bar is a different UI language again** — dark blue-grey rounded panel with square
  icons, versus the circular icons on the right edge.
- **`LOCKING`** is a gold-outlined box, a fifth treatment.

**Intent:** one enemy plate as a single composed unit; one chrome language for actionable buttons; the
right edge grouped into a deliberate column with consistent sizing.

---

## 4. `01-title-screen.png` — Title

**Mostly good** — the landscape art now loads correctly (fixed 2026-08-04 in `f1f5f593`; the file at
`Resources/Title/Title_L.jpg` was a *portrait* image, so the game pillarboxed it).

- **BUG: `Connect Wallet` is clipped** off the top-right corner. Not a capture crop — the button runs off
  the screen edge. On a device with rounded corners and a camera cutout this is worse. **It needs a safe-
  area inset.**
- Thin black bars remain at left and right. The source art is a **1.49 ratio against the Seeker's 2.17**;
  filling edge-to-edge needs new artwork at ~2340×1080, not a code change. **Do not "fix" this by
  switching to cover/crop** — the owner ruled fit-to-screen on 2026-07-16 and the title text is baked into
  the art, so cropping cuts the title off.

---

## 5. `08-portal-magenta.png` — THE DUNGEON PORTAL: REBUILD IT

**Owner:** *"we need that portal to look way better. I know we have some amazing aura stuff from the VFX
Unity demo."* — and, decisively: ***"the point is the whole thing needs redone."***

**SCOPE: this is a REBUILD, not a repair.** Do not fix the material, verify it renders, and close the
ticket — the owner has ruled the portal itself is wrong. The current arch is a flat rectangular frame
with no depth, no threshold, no sense that it leads anywhere. It should read as a way INTO somewhere:
frame, an active threshold surface, and an aura that makes it a landmark you can navigate toward.

**This is the deliverable. The material finding below is CONTEXT — it explains why the current one looks
as bad as it does, and it stops someone shader-swapping it and declaring the ticket done.**

⚠ That specific magenta is
**Unity's missing/incompatible-shader colour.** The portal has no working material — it is rendering the
error material, plus what look like a second set of broken materials as the blue blocks inside the arch.
**Adding aura VFX on top would still leave a magenta frame.** Fix the material FIRST, then dress it.

This is the project's known pink-material failure mode, documented in at least four places:
`CastleHubBuilder.cs:2233` (*"renders MAGENTA in URP (pink ground)"*),
`CastleWallKitSpawner.cs:52` (*"If pieces show magenta, run Defenders > Art > Fix Polyperfect URP
Materials"*), `CastleBuilderTester.cs:263`, `EnsureShadersIncluded.cs:46`. Memory `never-inference-fix`
records three wasted cycles guessing at the castle "pink floor" before one headless dump named it.

**Candidate materials** (a Tripo import, so a non-URP shader is the likely cause):
- `Assets/Resources/Structures/Materials/Portal_To_Dungeon_basecolor.mat`
- `Assets/Art/TripoStructures/Materials/Portal_To_Dungeon_basecolor.mat`

⚠ **`MagentaGuard.cs` EXISTS and did not catch this.** Determine why — it is either not run on this
asset path, not run in this scene, or scoped to a set the portal is not in. **Whatever the material fix
is, the guard should have caught it; widening the guard is part of the fix, not a follow-up.** A guard
that misses the thing it exists to catch is worse than no guard, because it buys false confidence.

**The VFX the owner is asking for already ships.** `Mirza Beig / Particle Systems / Ultimate VFX /
Prefabs / Loop/` contains: `pf_vfx-ult_demo_psys_loop_ghostPortal`, `…_ghostPortal2`, `…_portalBlue`,
`…_portalBlueTutorial`, `…_portalOrange`. **Use these — do not author new VFX.** Route through the
existing `VFXManager` (pooled, quality-gated) rather than instantiating particle prefabs directly, and
respect the WO-753 one-owner teardown rule so a destroyed portal does not orphan its effect.

**Order of work, and it matters:**
1. **Design the portal** — the owner ruled the whole thing is redone, so start there, not at the shader.
2. Whatever art it ends up with, its material must be **URP-compatible** (see above) or it ships magenta
   again regardless of how good the design is.
3. **Widen `MagentaGuard`** so this class cannot ship again. Part of the fix, not a follow-up.
4. Dress it with the aura from the existing Ultimate VFX pack.

**Reference frames for the rebuild:** `08-portal-magenta.png` is the current state. The portal sits in
open ground with nothing else competing for attention, so it carries the whole "there is somewhere to go"
read on its own. It should be visible and legible from across the field at 2340x1080, not just up close.

---

## 6. Suggested split

These are separable and can be worked in parallel — but **§1 (Skills) is the only one that is broken
rather than unpolished**, and should go first regardless of how the rest are grouped.

1. Skills panel — structural fix (overflow, z-order, truncation)
2. Rumor Board — clipped tab (bug) + frame/content styling pass
3. Combat HUD — nameplate composition + right-edge grouping
4. Title — safe-area inset for `Connect Wallet` (small; could ride with any of the above)

**`UI_CAPTURE_OK` is necessary but NOT sufficient here.** Every item above was invisible to headless
capture. Verify on the Seeker at 2340×1080 — `adb shell screencap` then look at the PNG.
