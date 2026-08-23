**Status:** FIXED 2026-08-23 — shipped in `51de6bd31` (the catapult `FitHeight` split), capture-verified. AWAITING OWNER FELT-TEST TO CLOSE.

# WORK ORDER 1143 - The siege catapult renders oversized and vertical in raids

**Minted:** 2026-08-22 (CLI, banner bumped 1142 -> 1145 in the SAME edit)
**Lane:** Troop visuals. **Class:** felt-test defect. **Assigned:** Codex seat.
**Provenance:** owner, 2026-08-22: *"In the raid i did the catapult was oversized and standing
vertical instead of horizontal"*.

## ⛔ NOT REPRODUCED YET - INSTRUMENT BEFORE YOU CHANGE ANYTHING

A headed autopilot fleet was run (`-Graphics`, 8 runs) and **did NOT reproduce this**: the runs
broke in the town on an unrelated structure-art failure (WO-1142) and never deployed a catapult.
The only catapult line in the log is the content warmer, not a spawn.

So everything below is a CANDIDATE LIST, not a diagnosis. CLAUDE.md section 12 is binding here:
static reading LOCATES, it never CONCLUDES. **Capture the catapult actually rendering before
editing.** `TroopFactory` already emits a `[Flow:TroopVisual]` line naming the model, the resolved
Resources path, the yaw and the siege flag - get one deploy on screen and read it.

## THE CANDIDATES, strongest first (each verified at source 2026-08-22)

1. **ASYNC ARRIVAL vs a ONE-SHOT SKIN.** `StructureContentWarmer` logs, verbatim: *"'Structures/
   Catapult' arrived ASYNC after 0.0s and is now RESIDENT ... **the next skin attempt will use
   it**."* A troop is skinned ONCE at deploy. If the address is not resident at that moment the
   skin falls back - and an unfitted, unrotated fallback body is EXACTLY "oversized and vertical".
   This is the best fit for both halves of the symptom at once.
2. **It is the only troop skinned as a STRUCTURE.** `TroopFactory` picks
   `SkinOptions.Structure(bodyHeight)` for siege units and `SkinOptions.Enemy(bodyHeight)` for
   everything else - a different fit path, and the catapult is the only siege unit.
3. **Its art is a BUILDING.** `troop-catapult.model = "Structures/Catapult"`; every other troop
   uses a character prefab (`SC_Footman`, `Knight`, `NPCs/KayKit/Cleric`). The source is
   `KayKit Medieval Hexagon Pack/.../buildings/*/building_tower_catapult_*.fbx` - a *tower*
   catapult, authored upright as a building, which is a plausible cause of "standing vertical".
4. **⛔ THE DEF CANNOT EXPRESS THE FIX.** `TroopDef` exposes only `modelYaw`, and the factory
   applies `Quaternion.Euler(0f, def.ModelYaw, 0f)` - **yaw only**. Vertical-vs-horizontal is a
   PITCH problem, so NO value of `modelYaw` can correct it. If the source prefab's pitch is wrong,
   the fix is a rotation the data model currently has no field for. Say so rather than tuning yaw.

## SCOPE

- Reproduce with a catapult actually deployed, and SCREENSHOT it (memory
  `screenshots-are-primary-evidence-for-visual-defects`: for a visual defect the screenshot IS the
  data; FlowTrace shows what the code believes, the screenshot shows what the player sees).
- Fix the cause the capture names - not the first plausible candidate above.
- If the fix needs a pitch/scale field the def lacks, that is an owner-visible schema/data change:
  name it, do not smuggle it in.

## ACCEPTANCE

- [ ] Screenshot of a deployed catapult at correct scale and orientation, next to a footman for scale
- [ ] The captured trace line that PROVED the cause is quoted in the RESULT
- [ ] If async arrival was the cause, the fix covers EVERY troop using an addressable model, not
      just the catapult - it is the only one today, and that is exactly why it went unnoticed
- [ ] Owner felt-verify in an actual raid
