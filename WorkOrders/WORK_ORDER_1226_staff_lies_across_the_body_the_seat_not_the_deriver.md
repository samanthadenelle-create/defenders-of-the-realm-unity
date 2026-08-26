# WORK ORDER 1226 - The staff lies across the body. Fix the SEAT, not the deriver - six attempts fixed the wrong half.

**Status:** READY TO IMPLEMENT
**Silo:** Gear seating / attachment orientation
**Origin:** Owner felt-test, 2026-08-26, across TWO builds (`2026.08.26.341419` and `.342290`).
Owner verbatim: *"why is the staff still horizontal during fights?"* -> *"thought we fixed that many
times"* -> later, on the new build: *"weapon combat still horizontal"*.

⚠ **PROCESS NOTE, recorded because it cost two hours:** the CLI diagnosed this at ~11:00, said it
would mint the ticket, and did not. It sat in conversation as an RCA with no board entry until the
owner hit it again. An un-minted ticket is invisible to the READY queue and therefore to the
orchestration hook.

---

## PROOF — captured from the owner's device, TWICE, on two different builds

**Build `.341419`, 10:33:**
```
[Flow:Equip] parent-scale compensate: main-hand id='mage_oak' mesh='staff_A'
             parentBone='CC_Base_R_Hand' renderers=2(inactive=0)
             -> worldBounds=(1.519, 1.401, 1.624)
```

**Build `.342290`, 12:17:**
```
[Flow:Equip] parent-scale compensate: main-hand id='tripo_staff_a' mesh='staff_A'
             parentBone='SheatheSocket_HipMain' renderers=2(inactive=0)
             -> worldBounds=(1.318, 1.509, 1.169)
```

⭐ **A STAFF IS MEASURING AS A CUBE, on two builds, on two sockets, on two item ids.** For
comparison this repo's own recorded measurement of a sheathed staff is `worldBounds=(0.079, 0.097,
1.265)` — a thin rod with an unmistakable long axis. When the bounds come back near-isotropic there
IS no longest axis, so anything asking "which way does this point?" gets an arbitrary answer.

⭐ **`renderers=2(inactive=0)`.** Two renderers are being measured together. A second renderer — an
effect quad, a glow, a trail — would inflate a rod into exactly this box. **CHECK THIS FIRST.**

**And the code believes it is correct** — same capture, same frame:
```
[Flow:Equip] sheathed long axis on 'Hero (Blaise)': tiltFromVertical=0deg
             (must read ~0; ~90 means it is lying across the body)
             longAxisDotUp=-1 src=PER-MESH derived
             why=grip-origin/taper AMBIGUOUS on Y (relGap=0.019 < 0.15) — neither end reads as
             the pointy one; grip-origin on Y: |-end|=0.2275 |+end|=1.0364 relGap=0.64
             -> hilt at -Y socket='SheatheSocket_HipMain'
```

The instrumentation spells out the failure it was written to catch — *"~90 means it is lying across
the body"* — and then reports **0**. Screenshots: `tmp/screen-104240.png` (staff horizontal across
the back), `tmp/test-skill-121743.png` (in combat, new build).

## ⭐ WHY SIX PREVIOUS FIXES DID NOT HOLD

`git log` carries at least six: `fix(equipment): reset stale staff grip compensation` ·
`fix(hero): sheathed weapons seat per-mesh instead of one global sign` · `fix(ui): preserve hero
portraits and correct sheathed weapon` · `fix(hero): seat knight shield on grip` · `fix(gear): the
Knight's sword and shield seat correctly, drawn and sheathed` · `The ballista tips because a flag
was read instead of the mesh being measured`.

**Nearly all of them tuned the MEASUREMENT. The measurement is arriving at a defensible answer.**
`grip-origin` resolves cleanly at `relGap=0.64` (hilt at −Y) — only the *taper* test is ambiguous
(`relGap=0.019`), and the code correctly falls through to grip-origin. It then lands wrong anyway.

This repo already wrote down this exact shape on 2026-08-16:
> *"derivation did NOT save the bow: its held rotation was 90 degrees wrong at the ATTACH SEAT — a
> different failure from the grip POSITION, which measured correct. **Derivation is not
> self-proving.**"*

**So: instrument the SEAT, not the deriver.** A value can be derived correctly and land wrong one
transform up the chain. Capture the prop's world rotation at each step — deriver output, mount
local, socket world, parent bone — and find which step introduces the ~90°.

## The owner's rule, and what it maps onto

Owner, 2026-08-26: ***"the pointed object is Y top, flat is bottom."*** That is the archetype rule
`ARCHITECTURE_PRINCIPLES` §4 already states (longest axis → +Y, base → origin) and it is what
grip-origin is already computing. Use it to VALIDATE the seat, not to replace the deriver.

## Known landmines

- ⛔ **`staff_A` is WO-1136's known-unmeasurable mesh** — geometrically symmetrical, `relGap 0` on
  both taper and grip tests when measured there. WO-1136 explicitly forbids fixing it by flipping
  the global `_sheatheLongAxisSign`: *"that only moves the defect to the other heroes."* The trace
  repeats the warning inline. **Do not flip the global field.**
- ⛔ **Shipped props may have mesh Read/Write OFF**, which makes vertex-based approaches SILENTLY
  INERT ON DEVICE while looking right in the editor. Derive from `mesh.bounds`.
- ⚠ **DRAWN vs SHEATHED are different code paths.** The `.341419` capture is `CC_Base_R_Hand`
  (drawn); the `.342290` one is `SheatheSocket_HipMain` (sheathed) and the log shows the carry state
  flipping DRAWN → SHEATHED. Both show the cube. Establish whether one path, or both, is at fault —
  and say which in the RESULT. Six prior fixes mostly touched SHEATHED.
- ⛔ **WO-966 (dungeon −90 root yaw) is untouchable until ruled** — two facing systems tuned against
  each other manufacture a third bug.
- ⛔ Do NOT touch the eight structure `-90` rows. Different lane, and they are correct.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. ⭐ **A DEVICE SCREENSHOT of the hero holding the staff in combat, opened and looked at.**
   ⛔ Headless gates cannot see orientation — `bb6dc010` laid a whole town on its side with every
   marker green. This ticket is NOT done on a marker, and it is not done on a trace line either:
   `tiltFromVertical=0deg` is what the current, broken build already prints.
3. ⭐ A regression that FAILS on today's tree, asserting the SEATED world rotation — not the derived
   value. Prove it RED first (WO-1138).
4. The RESULT states whether `renderers=2` was the bounds inflation, and which transform introduced
   the rotation error, with the proving line.
5. Owner felt-verifies on device and CLOSES.

## What NOT to touch

- ⛔ The global `_sheatheLongAxisSign` (WO-1136).
- ⛔ `WeaponOrientHelper`'s shield-substantiation fix (WO-1215, committed) — different lane.
- ⛔ The structure orientation channels (`entry.orientation`, `HubStructureVisualInjector.pitchDeg`).
- ⛔ Offset Forge, for Tripo assets: `TripoAxisBake.cs:143-158` regex-rewrites an authored `x:-90`
  to `0.0` on baked rows, so a correction parked there is actively erased.
