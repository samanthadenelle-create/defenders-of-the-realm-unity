import io, os

ROOT = r'D:\eoa'

BANNER_OLD = '> ## \u26a0 RECONCILED 2026-08-22 (CLI): main line next free = **1142**.'
BANNER_NEW = (
    '> ## \u26a0 RECONCILED 2026-08-22 (CLI): main line next free = **1145**.\n'
    '> *(CLI seat minted **WO-1142**, **WO-1143**, **WO-1144** and bumped 1142 -> 1145 in this SAME\n'
    '> edit. **1142** = Structures/ArcaneSpire_1 + "arcane tower" fail to resolve, losing a building\n'
    '> every boot (80 errors x 8/8 autopilot runs). **1143** = the siege catapult renders oversized and\n'
    '> vertical in raids (owner felt-test). **1144** = truncated + colliding HUD labels seen in the\n'
    '> same capture. All three from the 2026-08-22 headed autopilot run.)*')

WO1142 = """**Status:** READY TO IMPLEMENT - HIGH (live, reproduces 8/8)

# WORK ORDER 1142 - Structure art fails to resolve: a building is lost on every boot

**Minted:** 2026-08-22 (CLI, banner bumped 1142 -> 1145 in the SAME edit)
**Lane:** Core / Addressables + structure art. **Class:** LIVE DEFECT, reproduced 8/8.
**Found by:** the 2026-08-22 headed autopilot fleet (`run-autopilot-fleet.ps1 -Graphics`), 8 runs,
seeds 1-8. Evidence: `Builds/autopilot-tickets.md`, per-run `player.log`.

## THE MEASURED FAILURE - not inferred, captured

```
[Flow:VisualFactory] model not found via Addressables OR Resources: 'Structures/ArcaneSpire_1'
   - returning null (caller falls back). Check the address exists in the Structure_Art group
     and that its bundle is uploaded to the CDN.
[Flow:Structure]     'tower_arcane_spire': visual 'Structures/ArcaneSpire_1' not found / failed
     to skin - destroying empty root, returning null (caller falls back).
[Flow:BaseLayout]    Spawn: StructureFactory.Create returned null for entry 'tower_arcane_spire'
     at cell (5,17) - structure NOT built (ONE BUILDING LOST; check the entry's prefabPath).
```

**80 errors, reproduced in 8 of 8 runs.** A second address fails the same way:
`Structures/arcane tower` (15 errors x 8 runs) - note the SPACE in that address, which is worth
checking on its own.

## WHY THIS MATTERS MORE THAN A MISSING MESH

**A building the player BUILT does not exist after a reload.** `StructureFactory.Create` returns
null, so `BaseLayoutLoader` skips the entry entirely - the structure is not built, its footprint is
not claimed, and nothing on screen says anything went wrong. The player placed and PAID for a
tower that is silently absent next session.

⚠ **This is the CLAUDE.md section 16 trap one layer in.** Section 16 exists because remote art with
no local fallback "installs perfectly, launches perfectly, and plays" with capsule enemies and no
error on screen. Here the game does not even substitute a placeholder - it drops the building.

## WHAT IS ALREADY KNOWN (verify each; do not trust this list)

- `Structures/ArcaneSpire_1` IS authored in `Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset`
  (verified 2026-08-22), alongside `ArcaneSpire_2`, `_3` and their `_Albedo` rows.
- So the address EXISTS in the group - which makes this most likely a BUNDLE/UPLOAD or a
  build-target problem, not an authoring gap. The trace names both possibilities itself.
- A `StandaloneWindows64` catalog was regenerated and pushed to R2 on 2026-08-22 (`R2_PUSH_OK 2
  uploaded`, `R2_PARITY_OK 42 verified`). The failing run used a Windows build from 09:07, i.e.
  BEFORE that push. **Re-run the fleet against a FRESH Windows build first** - this may already be
  fixed, and proving that costs one build.
- ⛔ `R2_PARITY_OK` verifies every object THIS BUILD'S CATALOG NAMES. If the catalog itself never
  named ArcaneSpire's bundle, parity is green and the asset is still missing. Parity is not
  coverage - check the catalog's contents, not just its verification.

## SCOPE

1. Rebuild Windows, re-run `run-autopilot-fleet.ps1 -Graphics`, and see whether it still reproduces.
2. If it does: determine whether the bundle exists in `ServerData/StandaloneWindows64` and whether
   the catalog names it. Do the same for Android - **the owner plays on the Seeker**, and a defect
   that only shows on Windows is a different (lower) priority than one that ships to the device.
3. Investigate `Structures/arcane tower` separately - a space in an address is suspicious and may be
   a data typo rather than a missing bundle.
4. ⛔ **Do NOT "fix" this by restoring a Resources fallback.** `Assets/Resources/Structures` was
   deliberately deleted (section 16); re-adding a local copy re-creates the duplicated-state problem
   the CDN move solved.

## ACCEPTANCE

- [ ] A fresh fleet run produces ZERO `model not found` errors for structure addresses
- [ ] No `structure NOT built` line in any run
- [ ] Verified on the ANDROID target too, not only Windows
- [ ] If a building genuinely cannot resolve, it FAILS LOUD (visible placeholder or an on-screen
      notice) rather than silently vanishing - a lost building must never be indistinguishable from
      a building the player never placed
"""

WO1143 = """**Status:** READY TO IMPLEMENT (owner felt-test 2026-08-22)

# WORK ORDER 1143 - The siege catapult renders oversized and vertical in raids

**Minted:** 2026-08-22 (CLI, banner bumped 1142 -> 1145 in the SAME edit)
**Lane:** Troop visuals. **Class:** felt-test defect. **Assigned:** Codex seat.
**Provenance:** owner, 2026-08-22: *"In the raid i did the catapult was oversized and standing
vertical instead of horizontal"*.

## \u26d4 NOT REPRODUCED YET - INSTRUMENT BEFORE YOU CHANGE ANYTHING

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
4. **\u26d4 THE DEF CANNOT EXPRESS THE FIX.** `TroopDef` exposes only `modelYaw`, and the factory
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
"""

WO1144 = """**Status:** READY TO IMPLEMENT

# WORK ORDER 1144 - Truncated and colliding HUD labels in the town

**Minted:** 2026-08-22 (CLI, banner bumped 1142 -> 1145 in the SAME edit)
**Lane:** HUD / UI. **Class:** presentation defect, captured.
**Evidence:** `autopilot-runs/*/break_24_error.png` from the 2026-08-22 headed fleet run
(reproduced identically in all 8 runs). THE SCREENSHOT IS THE DATA.

## WHAT THE CAPTURE SHOWS

1. **`"Tap to collec"`** - the Collectors chip (top right) truncates mid-word. Not an ellipsis, a
   cut. Reads as a rendering fault rather than an abbreviation.
2. **`"Manag..."`** - the Manage face on the bottom action bar is ellipsised while every sibling
   (Build / Bag / Raids / Quests) fits. It is the longest label in a fixed-width slot.
3. **"TIER UP! Initiate"** renders across the world tree at the screen centre, overlapping the
   scene and unreadable against it.
4. **"Wave 1" / "Next wave in 45s"** collides with the "Start Now" button directly beneath it -
   two live elements occupying the same band.

## NOT IN SCOPE - VERIFIED WITH THE OWNER

The bar shows FIVE faces (Build / Bag / Raids / Quests / Manage) and canon section 7 says six.
**That is correct behaviour: Talk is CONTEXT-GATED and appears only near an NPC** (owner,
2026-08-22). Do not "restore" a sixth face.
*(Section 7's wording does not mention the gating, which is what made it read as a defect. Worth an
owner-confirmed canon touch-up, but not part of this ticket.)*

## CONSTRAINTS

- Code-built uGUI only - UXML does NOT work in player builds.
- `MinTouchPx = 112` - a label fix must not shrink a touch target below the floor.
- Landscape; the capture is 2670x1200. Verify any fix at more than one aspect - a label that fits
  at this width may still cut at another.
- The owner is RED/GREEN COLOURBLIND: never resolve an overlap by recolouring alone; move it,
  reflow it, or give it its own band.
- \u26d4 Player-facing sentences come from `canon-strings.json` (both copies, byte-identical,
  ASCII). If a string needs shortening, shorten it THERE - never inline at the call site.

## ACCEPTANCE

- [ ] No truncated word on the Collectors chip or the action bar at 2670x1200 and at one other aspect
- [ ] "TIER UP!" does not overlap the world tree or the wave banner
- [ ] The wave countdown and "Start Now" do not share a band
- [ ] Verified by SCREENSHOT, not by reading layout code - this ticket exists because the numbers
      looked fine and the frame did not
"""


def write(path, text):
    p = os.path.join(ROOT, path)
    io.open(p, 'w', encoding='utf-8', newline='\n').write(text)
    print('minted', os.path.basename(p))


b = os.path.join(ROOT, 'CLI_LANES_WO_NUMBERS.md')
s = io.open(b, encoding='utf-8').read()
assert BANNER_OLD in s, 'banner anchor not found'
io.open(b, 'w', encoding='utf-8', newline='\n').write(s.replace(BANNER_OLD, BANNER_NEW, 1))
print('banner bumped 1142 -> 1145')

write('WorkOrders/WORK_ORDER_1142_structure_art_fails_to_resolve.md', WO1142)
write('WorkOrders/WORK_ORDER_1143_catapult_oversized_and_vertical.md', WO1143)
write('WorkOrders/WORK_ORDER_1144_truncated_and_colliding_hud_labels.md', WO1144)
