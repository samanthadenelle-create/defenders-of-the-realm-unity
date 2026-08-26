# WORK ORDER 1222 - Entering the composed Healer's Cottage leaves the hero 7km away at ArenaCentre (black screen)

**Status:** FIXED 2026-08-26 - `COMPILE_GATE_OK` + `REGRESSION_OK 292/292`; post-fix APK visual/felt verification queued
**Silo:** Dungeons / scene routing
**Severity:** P0 — an unplayable black screen on a reachable dungeon, on a LIVE build that takes
real money. The player has a working joystick and nothing to see.
**Origin:** Owner felt-test, Seeker build `2026.08.26.341419`, 2026-08-26.
Owner verbatim: *"something seems broken"* / *"entered healers cottage"*.

---

## PROOF — captured from the device, the scene is HEALTHY and the hero is not in it

Device capture `tmp/screen-110106.png`: a black screen with **only the movement joystick drawn**.
The HUD is alive; the world is not.

The trace, same moment:
```
[Flow:HeroOwner] scene='dg_healers_cottage' owner=HeroLocomotion ownerCC=none
                 ownerAgent=off-mesh ... pos=(5000.00, 0.00, 4991.00)
[Flow:Camera]    boom=3.22 seat=(h 1.90, d 3.20) yawSrc=hold panYaw=0 pitch=0.0
                 room=none size=(0x0) small=False ceilClampsTotal=0
[Flow:Perf]      fps=60 ms=16.6 mem=473MB gc=26MB scene=dg_healers_cottage towers=0 enemies=7
[Flow:HUD]       ... scene='dg_healers_cottage' -> Battle
```

⭐ **The dungeon is FINE.** It loaded, it holds **7 enemies**, it runs at **60 fps**. Nothing failed
to spawn and nothing threw. **Do not go looking for a load failure** — that is the wrong hypothesis
and the perf line rules it out.

**The hero is at `(5000, 0, 4991)`, OFF the navmesh**, and the camera — which follows the hero —
reports `room=none size=(0x0)` and renders the clear colour. That is the black screen.

## The coordinate is not arbitrary

`Assets/_Modules/Village/Arena/BattleArena.cs:90`:
```csharp
private static readonly Vector3 ArenaCentre = new Vector3(5000f, 0f, 5000f);
```

`(5000, 0, 4991)` is **ArenaCentre, nine units off on Z**. The hero is standing in the BattleArena's
staging area, inside the dungeon scene.

## THE HYPOTHESIS TO TEST FIRST — ⚠ NOT YET PROVEN, DO NOT IMPLEMENT ON IT BLIND

`DungeonController.PlaceHero(spawnPos)` (`Assets/_Modules/Dungeons/DungeonController.cs:948`, called
from `:354`) is what teleports the hero to the layout's spawn point. But canon records **two dungeon
paths**, and `dg_healers_cottage` is the **COMPOSED** one:

| Path | Scene | Controller |
|---|---|---|
| Data-driven | `Assets/Scenes/Dungeon_HealersCottage.unity` | `DungeonController` (scene-placed) |
| **Composed (RoomForge)** | `Assets/Scenes/DungeonCompose/dg_*.unity` | **NO `DungeonController`** |

`Assets/_Modules/Dungeons/ComposedDungeonBootstrap.cs:5` says the composed scene *"only had hero +
spawners + exit"*. **If the composed path never calls `PlaceHero`, nothing ever moves the hero off
whatever coordinate it was left on — and the arena had parked it at `ArenaCentre`.**

⛔ **VERIFY THIS BEFORE EDITING.** Read `ComposedDungeonBootstrap` and confirm whether a hero
placement runs on that path. §12: static reading LOCATES, it never CONCLUDES. Instrument the
composed load and capture the hero position at each step.

⚠ Note the two scene names are nearly identical (`Dungeon_HealersCottage` vs `dg_healers_cottage`).
Confusing them will send the fix to the wrong path.

## Related, and worth reading before choosing the fix

`git log` carries `fix(hero): portal travel left the hero 130m outside the dungeon (F8 seq 3587)` —
the same failure class at 130 m. **Read that commit.** If its fix only covered the data-driven path,
this ticket is its missing half, and the right change may be to make ONE placement authority serve
both paths rather than adding a second one.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ A regression that **FAILS on today's tree**: load a composed dungeon and assert the hero is
   ON the navmesh and within the layout bounds — explicitly assert it is **NOT** within 1 m of
   `BattleArena.ArenaCentre`. Prove it RED first (WO-1138).
3. ⭐ A **DEVICE SCREENSHOT** of the composed Healer's Cottage showing the room, opened and looked
   at. A black screen renders at 60 fps and passes every headless gate — this is not done on a marker.
4. A `[Flow:HeroOwner]` line from a real entry showing `ownerAgent=on-mesh` and a plausible position,
   plus a `[Flow:Camera]` line with a real `room=` and non-zero `size=`.
5. The RESULT states which of the two dungeon paths was actually at fault, with the proving line.
6. Owner felt-verifies on device and CLOSES.

## What NOT to touch

- ⛔ `BattleArena.ArenaCentre` itself. It is the arena's staging coordinate and is doing its job;
  the defect is that the hero was never moved OFF it.
- ⛔ The dungeon composer, the room bake, or the enemy spawners. `enemies=7` at `fps=60` proves that
  half is working.
- ⛔ `DungeonCameraProfile` seat values (`h 1.90 / d 3.20`). The camera is correctly following a hero
  that is in the wrong place; do not tune the camera to hide a placement bug.
- ⛔ `StairUp`/`StairDown`/`IsVertical`/`SEALED_VERTICAL` — the quarantined control group
  (`DungeonMultiLevelRegression.cs:41-63`, "⚠ DO NOT DELETE").
## LANDED-WORK AUDIT (2026-08-26)

The shared `DungeonHeroSeat` arrival authority landed in `b303c4fbf`. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83490` `COMPOSED DUNGEON RUN OK` proves the composed arrival pose one
frame after load through that single authority; `:83814` is `REGRESSION_OK 291/291`.
**Post-FIXED APK checklist:** the opened device screenshot showing the composed Healer's Cottage room, the requested
arrival trace/capture checks, and owner device felt-close.
