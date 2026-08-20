# WORK ORDER 980 — Dungeon camera framing after the fix: blown-out wall, hero as silhouette

**Status:** DONE — owner-confirmed fixed and verified 2026-08-19.
**Lane:** Dungeons / presentation
**Minted:** 2026-08-10 (CLI), from the WO-968 after-fix proof run

---

## 1. What happened

WO-968's camera fix is **proven working** — the rig now follows and rotates with the hero
(43 heartbeats, 15 distinct poses, `body=CinemachineThirdPersonFollow(enabled=True)` throughout).
That claim is closed.

**This ticket is about what the working camera now shows.** In
`docs/proof/2026-08-10-dungeon-headed-AFTER-camera-fix/`:

- `03_walk_end.png` and `08_final.png` are dominated by a **blown-out, near-white wall surface**
- the hero renders as an almost **pure black silhouette** against a torch directly behind him
- very little of the room is legible; the frame reads as wall rather than as a dungeon you are
  moving through

## 2. Why this is a separate ticket and not part of WO-968

*"The camera follows the hero"* and *"the player can see where they are going"* are **two different
claims, and only the first is proven.** Folding this into WO-968 would let a proven fix carry an
unproven one on its back.

It is also plausibly **not a defect at all**: a hero backlit by a torch producing a silhouette is
ordinary, and the previous camera was parked across the room, so every dungeon screenshot anyone has
looked at recently was taken from the *wrong* place. This is the first time anyone has seen the
intended over-the-shoulder framing.

## 3. ⛔ Owner ruling required before any work

**Do not tune anything until she answers.** She is red/green colourblind, so the question is asked in
terms of *behaviour*, never hue:

> Walking through a dungeon, can you tell where you are going and where the walls are — or does the
> screen read as a bright blur with your hero as a dark shape?

Depending on the answer this is either **CLOSE AS WORKING AS INTENDED**, or a defect whose fix lives
in one of:

- torch intensity / falloff (the light, not the camera)
- bloom / post-exposure in the dungeon volume
- camera distance or height on the over-the-shoulder rig
- hero rim-lighting so the silhouette separates from the background

## 4. ⚠ What NOT to do

- **Do not touch `DungeonCameraRig`'s follow logic or `HealBodyStage`.** That is a proven fix; if
  framing needs changing, change the framing parameters or the lighting — not the mechanism that
  makes the camera work at all.
- **Do not tune against these screenshots alone** without her ruling. They were captured with
  `SendInput` at a fixed set of beats, at whatever spot the walk ended — not composed shots.
- Do not reintroduce the old parked camera to "see more of the room". That was the bug.

## 5. Acceptance criteria

- [ ] Owner has ruled: working-as-intended, or a named defect.
- [ ] **If a defect:** a headed re-shot at the same eight beats shows the room legible and the hero
      distinguishable from the background, with the before/after pair kept side by side.
- [ ] A greyscale check on the after-shots (the standing gate for visual work, since meaning must
      never rest on colour alone).
- [ ] `DungeonCameraRig`'s follow/heal logic unchanged.

## 6. Evidence

- **Before (camera frozen):** `docs/proof/2026-08-10-dungeon-headed/`
- **After (camera working):** `docs/proof/2026-08-10-dungeon-headed-AFTER-camera-fix/`

The pair is the whole argument — in the before set the hero is *gone from frame* by `08_final`; in
the after set he is centre-frame, and the compass moves (S → SW) where it previously read S in all
eight shots.
