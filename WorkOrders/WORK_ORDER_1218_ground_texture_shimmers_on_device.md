# WORK ORDER 1218 - The ground shimmers and blows out on device (tiling/mip aliasing at 2670x1200)

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Silo:** Art / texture import
**Origin:** CLI observation from an owner felt-test device capture, 2026-08-26, Seeker build
`2026.08.26.341419`. Owner confirmed on being asked: **it has ALWAYS looked like this** - so there is
**no regression to bisect.** This was never right on this surface.

## PROOF (captured)

- `tmp/screen-103219.png` (2670x1200, device) - the entire grass plane renders as a dense,
  high-frequency near-neon sparkle. The noise **increases with distance from camera** and is worst
  across the mid-frame, which is the signature of a tiling texture sampled near pixel frequency with
  an absent or ineffective mip chain.
- `tmp/shield-seat-101829.png` (same session, different camera height) shows a milder version of the
  same pattern - it varies with camera pitch, which is consistent with a mip/LOD cause rather than a
  material colour cause.

## Why this is a DEVICE-ONLY finding, and why it went unseen

⭐ **2670x1200 - the Seeker's real surface - had never been rendered in this repo until `7e05e6d3`.**
Before that fix the UI capture harness only rewrote `canvas.scaleFactor` and never `Screen.*`, so the
capture filename resolution was *"a LABEL, NOT A LAYOUT."* Aliasing is a function of sampling rate
against real pixel density: at editor resolutions the mip chain hides it, and on the phone it does
not. **This class of defect cannot be seen by any headless gate** - same law as orientation.

## Diagnose before editing (§12 - do not guess which of these it is)

Read at source and report each, with the file path:
1. The ground/grass texture's `.meta` import block - **`mipmaps` enabled?** `aniso` level? filter
   mode? Android override + compression format?
2. The material's **tiling scale** on the terrain/ground mesh, and the world size it covers.
3. Whether a **mip bias** is applied anywhere.
4. Whether the Android quality settings drop aniso or texture mip limit relative to desktop.

The likely order is (1) then (2), but **the import block is a fact, not a theory - go and read it.**

## Fix

Whatever the read shows. The expected shape is a mipmap/aniso/tiling pass, NOT new art and NOT a
colour change.

⛔ **This is NOT a palette or hue question, and it must never be handed to the owner as one** - she is
red/green colourblind and never picks hues (memory: `owner-colorblind-delegate-visual-creative`). The
defect is spatial frequency and value blow-out, both of which survive a greyscale check. Do a
greyscale check as part of verifying the fix.

⚠ **Related but NOT this ticket:** WO-85's lane is *"why does the shipped terrain not read"* and the
recorded ruling there is that **value contrast must carry it, hue alone is invisible to the owner.**
If this fix changes ground value contrast, say so in the RESULT so the two do not collide.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs.
2. ⭐ **A DEVICE SCREENSHOT at 2670x1200 before and after, both opened and looked at.**
   ⛔ A green marker is NOT acceptance - headless gates cannot see this. `bb6dc010` laid an entire
   town on its side with every marker green.
3. The after-shot is taken at the **same camera pitch and position** as `tmp/screen-103219.png`, or
   the comparison proves nothing.
4. A greyscale check of the after-shot, confirming the ground still reads as ground by VALUE.
5. Owner felt-verifies and CLOSES.

## What NOT to touch

- ⛔ Any hue/palette decision - out of scope and not the CLI's call.
- ⛔ The URP pipeline asset or global quality tiers, unless the read in §Diagnose proves the cause is
  there - and then say so explicitly before changing a global.
