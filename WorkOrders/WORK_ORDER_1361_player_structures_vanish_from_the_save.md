# WORK ORDER 1361 - Player structures vanish from the save

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:50:03, build 2026.09.07.358574). PRIOR STATUS: FIXED (instrument) - on Firebase App Distribution as build 2026.09.05.356468 (08:2x); closure = a founding-load capture from the device printing `+ 8 bake-owned (+ 0 unaccounted)`. The census INSTRUMENT landed + gated (COMPILE_GATE_OK, REGRESSION_OK 383/383 incl. BaseLayoutRoundTripRegression case 5): `[Flow:BuildMode] census: live=N persisted=M = R replayable + B bake-owned (+ X unaccounted)`, Fail only when X > 0. No behaviour change. Closure = a founding-load capture on the device printing `+ 8 bake-owned (+ 0 unaccounted)`. Item (3) (the 09-04 09:33 ResetToNewGame with a level-3 hero) is an OWNER QUESTION: were both START NEW taps hers? RE-SCOPED 2026-09-04 23:00: the loader-side diagnosis (section below, from every census in logs/device/*.log) shows NO record is lost - every live<<persisted census is the FOUNDING load where the 8 StrategicPlacementMigration.BakedRows stand as baked twins without a PlacedStructure, and the next hub load replays 18/18. The census line at BuildModeController.cs:523 asserts a cause it cannot see. Remaining work: (1) replace the census with the replayable-vs-bake-owned instrument in that section; (2) the owner's 09-03 9/9/17 line is in no log on this machine - confirm on-device with the new instrument; (3) SEPARATE lead - ResetToNewGame fired at 09:33 on 09-04 with a level-3 hero (freeze-20260904-095249.log:852089) - if that was not her START NEW tap, that is the real progress loss and a new ticket. The BaseLayoutRoundTrip oracle (persistence seam, 17 records) is green at HEAD.
**Silo / Lane:** Core / save + base layout persistence
**Type:** EXISTING system, silent data loss
**Minted:** 2026-09-03 (CLI). ⚠ RETROACTIVELY, on a defect that has been visible in device logs
since **2026-08-19** and was never ticketed.
**Severity:** ⛔ **P0 - this is player progress disappearing.** It outranks every cosmetic item on
the board.

## THE CAPTURED DATA

From her live session, 2026-09-03, entering build mode:

```
[Flow:BaseLayout] Enter build mode CENSUS: live PlacedStructure(s) in scene=9,
  loader.Loaded=9, persisted BaseLayout=17, scene='False'-enemyOwned.
  live << persisted = structures already gone before this build session
  (F8-39 vanish happened earlier).
```

**Seventeen structures persisted. Nine in the world. Eight gone.**

⭐ **AND IT IS A FORTNIGHT OLD, NOT NEW.** The canon sweep found the same census in the archived
device logs - `grep "Enter build mode CENSUS" logs/device/*.log`:

```
2026-08-19 20:01   0 live / 0 loaded / 8 persisted
2026-08-20 09:04   0 live / 0 loaded / 8 persisted
```

So the loader has been failing to replay persisted structures for at least two weeks, in at least two
distinct shapes (all-missing, and partially-missing). **Nobody has ever worked it.**

## ⛔ WHAT IT IS NOT - do not spend a session re-deriving this

**Destruction cannot explain it.** Per WO-1357 (`e63494ed8`), `Destructible.NotifyBroken` frees the
footprint, calls `BaseLayoutLoader.Forget`, **DROPS the persisted `BaseLayout` record**, burns the
free-build and destroys the object. So a genuine destruction **LOWERS `persisted`**. Here `persisted`
stays high while `live` falls - the opposite signature. Something is failing to REPLAY records that
are still on disk, or replaying them into a state that is then discarded.

## THE INSTRUMENT ALREADY EXISTS - read it before touching code

The census line above is the loader telling you it noticed. ⛔ **Read the captured data first**
(CLAUDE.md §12 and §11B) - the answer to "which eight, and at which step" is very likely already
derivable from `[Flow:BaseLayout]` plus the save file, without a single code change. Split it before
theorising:

- **Never loaded** - the record is on disk and `BaseLayoutLoader` skipped it. Why? An unresolvable
  structure id, a footprint that no longer fits, a catalog row that moved.
- **Loaded then destroyed** - `loader.Loaded=9` says only nine were even loaded on the 09-03 capture,
  which points AWAY from this. But the 08-19/08-20 captures read `0 loaded` with 8 persisted, which is
  a different and even starker failure. **Establish whether these are one defect or two.**
- **Persisted but never real** - records written for structures that never successfully placed, so the
  count was always a lie.

⚠ **`live << persisted` is a symptom with several possible causes and static reading will not choose
between them.** Get a capture that names the eight by id and the step they died at. If the current
trace cannot do that, ADD that before fixing anything - a future occurrence must name itself.

## Relevant, verified today

- Hub structures are **BAKED TWINS** re-skinned by `HubStructureVisualInjector` and do NOT route
  through `StructureFactory`. A census that counts `PlacedStructure` components may legitimately miss
  baked ones - **confirm what the census actually counts before treating a mismatch as loss.**
- `BuildModeController.Place` appends the `BaseLayout` record **before** the build timer starts, so a
  structure interrupted mid-build has a record and no finished object. Worth checking whether the
  missing eight were all in-flight builds.
- Save schema is **v38** - read it off `SaveSchema.CurrentVersion`, never a doc.

## Acceptance

- [ ] A capture names the missing structures by id and the exact step each was lost at. Quote it.
- [ ] Whether the 08-19/08-20 (`0 live / 0 loaded / 8 persisted`) and 09-03 (`9 / 9 / 17`) shapes are
      ONE defect or TWO - answered with evidence.
- [ ] The cause proven from data, not inferred. ⛔ No fix before that line exists.
- [ ] A player who builds N structures and relaunches has N structures. Pinned by an oracle, proven
      RED first.
- [ ] The census keeps its FlowTrace so a recurrence names itself. Never strip it.
- [ ] ⛔ **Owner felt-verifies across a relaunch and closes.** Nothing about this is provable by a
      headless gate alone.

## Loader-side diagnosis 2026-09-04 (read-only)

**Verdict from captured data: NOTHING IS LOST. The census line is mis-reading its own inputs.** Every
`live << persisted` census on disk is the FOUNDING (migration) load of a fresh save, where the shortfall
is exactly the 8 `StrategicPlacementMigration.BakedRows` records whose bodies are the BAKED storefront
twins that load - by design, `StanddownActive == false` on the migration load - and they all replay on
the very next hub load. The 08-19/08-20 and 09-03/09-04 shapes are ONE mechanism, not two defects.
No loader drop line (`Spawn: ... not in registry`, `returned null`, `Rebuild: loaded only`, `WITHHELD`,
`SKIPPED in hub scene`) exists in ANY device log on this machine - grep over `logs/device/**` +
`logs/f8-inbox/**` returns zero of them. No uncontrolled `[Flow:Structures]` teardown exists either.

### 1. The sequence, from `logs/device/freeze-20260904-095249.log` (pid 22805, this morning)

| log line | time | what it says |
|---|---|---|
| 384407 | 07:42:44.926 | `[Flow:Save] ResetToNewGame: ENTER` - **a New Game**, the save is blank |
| 386500 | 07:42:49.444 | `[Flow:BaseLayout] Bootstrap: home hub 'Main_Castle_Overworld' loaded with NO loader` |
| 391047 | 07:42:50.487 | `[Flow:BaseLayout] Start: loadOnStart=True, persisted BaseLayout=0, live loaded=0` |
| 391051 | 07:42:50.488 | `LoadFromState: ... has an empty BaseLayout - default seed stands (no replay).` (`_loadedOnce` latched) |
| 394351 | 07:42:50.917 | `[Flow:Placement] -> StrategicPlacementMigration.RunIfNeeded (one-shot writer)` |
| 394358-394410 | 07:42:50.919-.927 | `migrated forge / collector_lumbermill / collector_farm / pet-house / armorer / arcane-tower / market / jeweler -> BaseLayout` (**the 8**) |
| 394441 | 07:42:50.932 | `migration COMPLETE: 8 structure(s) -> BaseLayout, 2 skipped (no catalog row) ... standdown activates on the NEXT home-hub load.` |
| ~394950-396300 | 07:42:50.997-52.254 | `[Flow:Founding] starter placed workshop, collector_forge, lumberyard, foundry, silo, 4x tower_ground_archer` ... `starter settlement ready: added=9 existing=0 failed=0` (each via `BaseLayoutLoader.Spawn` -> LIVE) |
| (window) | 07:43:04.251 | `[Flow:Placement] migrated barracks @ (16,0,-4) -> BaseLayout` (`AdoptBakedBarracksIfNeeded`, materialised via Spawn -> LIVE) |
| 568505 | 07:50:43.069 | `Enter build mode CENSUS: live=10, loader.Loaded=10, persisted BaseLayout=18` |

**Arithmetic: 18 persisted = 8 migrated bake records + 9 starter pieces + 1 adopted barracks. 10 live = 9 + 1.
The "missing 8" are the 8 BakedRows ids, whose bodies this session are the baked twins**
(`Blacksmith_Weapons_Storefront`, `Lumbermill_Wood_Storefront`, `Windmill_Food_Storefront`,
`EchoHollow_Pets_RoamingArea`, `Forge_Armor_Storefront`, `ArcaneTower_MagicUpgrades`,
`Marketplace_Monetization`, `Jeweler_Gems_Storefront` - `StrategicPlacementMigration.cs:90-99`). A baked twin
carries no `PlacedStructure`, and the census counts `FindObjectsByType<PlacedStructure>()`
(`BuildModeController.cs:516`), so it can never see them.

**Proof they replay next load (same log, pid 30184, next launch):**
- 1066939 `09:44:44.246 Start: loadOnStart=True, persisted BaseLayout=18, live loaded=0`
- 1066943 `LoadFromState: ... replaying 18 persisted village structure(s).`
- 1079752 `09:44:44.862 Rebuild: loaded 18/18 placed structure(s) from BaseLayout.`
- 09:44:44.960-.977 `[Flow:Placement] standdown EchoHollow_Pets_RoamingArea (migrated -> BaseLayout 'pet-house')` ... x8, one per BakedRow
- 1144462 `09:48:17.418 CENSUS: live=18, loader.Loaded=18, persisted BaseLayout=18`

(The second 09-04 `10/10/18` at 09:34:21, pid 28972, is the same thing again: 852089 `09:33:36
ResetToNewGame: ENTER - hero on entry level=3` - another New Game; migration re-ran, starter re-placed.)

### 2. The 08-19 / 08-20 shape is the same mechanism (`logs/device/2026-08-20-town-freeze.log`)

- 08-19 19:47:20 (pid 6863) 3239708 `Start: persisted BaseLayout=7` -> 3243897 `Rebuild: loaded 7/7` -> 3291823 census `7/7/7`. Healthy.
- 3633926 `08-19 20:01:12 ResetToNewGame` -> 3640567 `Start: persisted BaseLayout=0` -> 3640571 empty, no replay -> `20:01:21.152 RunIfNeeded` writes **8** (`workshop, collector_lumbermill, collector_farm, pet-house, forge, arcane-tower, market, jeweler` - that build's BakedRows) -> 3646317 census `0/0/8`. Zero live because **that build had no StarterSettlementCompletion** (no `[Flow:Founding]` line exists in the file) and nothing else had been placed yet - the 8 bakes were standing in the world.
- 08-20 09:04:05 (pid 25868) 4575644 `Start: persisted BaseLayout=8` -> 4580763 `Rebuild: loaded 8/8` - **the 8 replayed on the next load, as designed.** Then 4587055 `09:04:30 ResetToNewGame` -> 4593405 `Start: persisted=0` -> migration writes 8 again -> 4599492 census `0/0/8`; 09:05:12 another reset -> 4613515 census `0/0/0`.

So every `0 live / 0 loaded / 8 persisted` line in the WO is a founding load 20-40 s after `ResetToNewGame`, never a replay that dropped records.

### 3. The 09-03 `9/9/17` line is NOT in any log on this machine

`rg "persisted BaseLayout=17" logs/` -> no hit (the owner's 09-03 session was never pulled). 17 = 8 + 9
(the starter table `Assets/Data/Canonical/starter-settlement-layout.json` has exactly 9 rows) with no
barracks adoption yet is arithmetically consistent with this mechanism, **but that is an inference, not a
capture** - the instrument in §5(b) is what closes it.

### 4. Answers to the task's numbered questions

1. **Captured sequence** - above. Before every deficit census the loader logged `persisted BaseLayout=0` +
   `empty BaseLayout - default seed stands`, i.e. it iterated ZERO records and dropped NONE; the records
   were written AFTER `Start()` by the migration writer + the starter/adoption spawners.
2. **Drop points** - all traced, none silent: `Spawn` registry miss `BaseLayoutLoader.cs:265`
   (`FlowTrace.Fail`), factory null `:301` (`FlowTrace.Fail`; `StructureFactory.Create` `:144-158` now keeps
   a pending-art proxy instead of returning null), `Rebuild` shortfall `:241-245` (`Warn`), WO-673 withhold
   `:226-233` (`Step`), hub guard `:144-153` (`Warn`). **The one path that "loses" a record without a drop
   line is not a drop: `LoadFromState` latches `_loadedOnce` (`:134-135`) on an EMPTY layout, after which
   records appended by `StrategicPlacementMigration.RunIfNeeded` are deliberately not replayed this session
   (`_migratedSceneHandle`, `StrategicPlacementMigration.cs:161` + `StanddownActive :168-178`).** The bake
   owns them until the next load.
3. **The 8 ids resolve today** - `Assets/Resources/Data/Canonical/structures-catalog.json` has exactly one
   row for each of `forge, collector_lumbermill, collector_farm, pet-house, armorer, arcane-tower, market,
   jeweler` (and for `barracks` + the 9 starter ids). `apothecary` / `jewelers-bench` have NO row, which is
   why the migration says `2 skipped (no catalog row)` - those two are never persisted, so they cannot be
   "lost" either.
4. **Hub guard refuted** - `_hubScenesNoBaseLayout = { "CastleHub" }` (`BaseLayoutLoader.cs:54-57`);
   `Main_Castle_Overworld` is not in it, and no `SKIPPED in hub scene` line exists in any log.

### 5. Deliverables

**(a) Proving lines:** `freeze-20260904-095249.log` 391047 (`persisted BaseLayout=0` at loader Start) ->
394441 (`migration COMPLETE: 8 -> BaseLayout`) -> `starter settlement ready: added=9` -> 568505 (`10/10/18`)
-> next launch 1066939/1079752 (`persisted=18`, `Rebuild: loaded 18/18`) -> 1144462 (`18/18/18`). The
loader is NOT silent at any drop; it simply never had a drop.

**(b) The ONE instrument - make the census subtract what it is not allowed to count, and name the rest.**
`Assets/_Modules/Village/BuildMode/BuildModeController.cs:515-524`, replace the block body with:

```csharp
int liveNow = FindObjectsByType<PlacedStructure>().Length;
int loadedNow = BaseLayoutLoader.Instance != null ? BaseLayoutLoader.Instance.Loaded.Count : -1;
var stEnter = GameStateService.Instance != null ? GameStateService.Instance.State : null;
var layoutNow = stEnter != null ? stEnter.BaseLayout : null;
int persistedNow = layoutNow != null ? layoutNow.Count : 0;
// Split persisted into REPLAYABLE (must have a live body) vs BAKE-OWNED this load (migration-managed
// id while standdown is inactive = the baked twin stands in for it; WO-673). Only the first is a shortfall.
var bakeOwned = new List<string>(); var missing = new List<string>();
var live = FindObjectsByType<PlacedStructure>();
if (layoutNow != null)
    foreach (var rec in layoutNow)
    {
        if (!StrategicPlacementMigration.ShouldReplayRecord(rec.itemId)) { bakeOwned.Add(rec.itemId); continue; }
        bool found = false;
        foreach (var ps in live)
            if (ps != null && ps.itemId == rec.itemId && ps.gridCell.x == rec.cellX && ps.gridCell.y == rec.cellZ) { found = true; break; }
        if (!found) missing.Add($"{rec.itemId}@({rec.cellX},{rec.cellZ})");
    }
string msg = $"Enter build mode CENSUS: live PlacedStructure(s)={liveNow}, loader.Loaded={loadedNow}, persisted={persistedNow} " +
             $"= {persistedNow - bakeOwned.Count} replayable + {bakeOwned.Count} bake-owned this load [{string.Join(",", bakeOwned)}]; " +
             $"scene='{DeNelle.Village.SceneOwnership.IsEnemyOwned}'-enemyOwned. ";
if (missing.Count > 0)
    FlowTrace.Warn("BaseLayout", msg + $"REPLAYABLE RECORDS WITH NO BODY = {missing.Count}: [{string.Join(", ", missing)}] " +
        "- THIS is a real loss; see the Rebuild/Spawn FAILED lines above for the step.");
else
    FlowTrace.Step("BaseLayout", msg + "every replayable record has a live body - no loss.");
```
What it prints on this morning's founding load: `persisted=18 = 10 replayable + 8 bake-owned this load
[forge,collector_lumbermill,collector_farm,pet-house,armorer,arcane-tower,market,jeweler]; ... every
replayable record has a live body - no loss.` A genuine drop prints `Warn ... REPLAYABLE RECORDS WITH NO
BODY = N: [id@(x,z), ...]`, naming each one - the acceptance line "a capture names the missing structures by
id". (`PlacedStructure.itemId/gridCell` are the fields `BaseLayoutLoader.Spawn` sets at `:362-363`; the WO-673
gate is the same `ShouldReplayRecord` that `Rebuild` already uses at `:226`.)

**(c) Fix:** none required for data loss - the data proves there is none. The defect is the census message
itself (`BuildModeController.cs:523`: *"live << persisted = structures already gone"*), which asserts a
cause the emitter cannot see; (b) replaces it. Two things this diagnosis does NOT prove and leaves for the
owner: (i) that the 8 baked twins were VISIBLE to her on the founding session (screenshot needed; the 09-04
log shows no `[Flow:Structures]` teardown, so nothing hid them); (ii) whether the `ResetToNewGame` at
09-04 07:42 and again at 09:33 (the second with a level-3 hero, 3 Wisdom, 1 talent) were both intentional -
if not, THAT is the actual "progress disappearing" and it is a New-Game-trigger ticket, not a loader one.

---
## RCA re-verified 2026-09-04 (QA read-only pass)
**Verdict:** STALE
**Evidence:**
- The census emitter is as cited: `Assets/_Modules/Village/BuildMode/BuildModeController.cs:516-523` counts `FindObjectsByType<PlacedStructure>().Length` vs `State.BaseLayout.Count`. `BaseLayoutLoader.Forget` at `BaseLayoutLoader.cs:175`; `Destructible.NotifyBroken` at `Destructible.cs:150`; loader FlowTrace at `BaseLayoutLoader.cs:149,161,166,242,265,301`.
- Save schema is v41, not v38: `Assets/_Modules/Core/State/SaveSchema.cs:41 public const int CurrentVersion = 41;`.
- A WO-1361 oracle now EXISTS and is COMMITTED: `Assets/Editor/Regression/BaseLayoutRoundTripRegression.cs` (841 lines) + `DataRegression.cs:1344` registration landed in `3f49e93d5` 2026-09-04 22:34 ("the production regression goes 358 -> 377 green"). Its header (`:33-36`) says: if green while the census still reads live << persisted, the loss is DOWNSTREAM of the save layer. Read from `Builds/regression.log` at its 22:31 state (before a 22:42 rewrite began): `:113612 [baselayout-roundtrip] BASELAYOUT ROUND-TRIP OK - 17 records survive save->reload (v41) and migrate (v14->v41)` and `:113715 REGRESSION_OK 377/377 suites -- 377 green, 0 red, 0 skipped`.
- Newer captured data contradicts "eight gone" (`logs/device/freeze-20260904-095249.log`): `07:42:44 [Flow:Save] ResetToNewGame: ENTER` -> `07:42:48 [Flow:Founding] choice = DEFAULT TOWN` -> `07:42:50.487 [Flow:BaseLayout] Start: persisted BaseLayout=0` -> `07:42:50.932 [Flow:Placement] migration COMPLETE: 8 structure(s) -> BaseLayout ... standdown activates on the NEXT home-hub load` -> `07:50:43 CENSUS live=10, loader.Loaded=10, persisted=18`. persisted - live = 8 = exactly the migration writer's baked-twin records (re-skinned baked objects via `HubStructureVisualInjector:ApplyAll` at 07:42:50.620, not `PlacedStructure` components until the next hub load). The 09-03 shape (17 vs 9) and the 08-19/20 shape (8 vs 0) are both an 8-record gap. This WO's own `:63-65` warned the census may miss baked twins.
- No RESULT, no superseding WO (WO-1360 `:4` cites 1361 only as a banner bump).
**What changed since the RCA:** an oracle proves the save layer keeps 17 records across save->reload; a 09-04 capture explains the gap as the one-shot migration writer's 8 default-ring records counted as persisted but not yet as PlacedStructure - by design, pending the next hub load.
**Ready for a lane?** no - the defect claim must be re-scoped first: a real vanish needs a relaunch capture where live < persisted AFTER standdown activates, for a structure the player PLACED. Files a lane would touch if re-scoped: `BuildModeController.cs:516-523` (split migrated / standdown-pending in the census), `BaseLayoutLoader.cs`.
**Pins/rulings needed:** owner confirms whether any structure she PLACED is missing after a relaunch (the 8 named in the capture are the default ring, not player builds).
