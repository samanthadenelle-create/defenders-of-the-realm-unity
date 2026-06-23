# WORK ORDER — Duplicate "Grom" companion (second body in MainCastle_Hall)

**Status: DIAGNOSED-PARTIAL — root cause NOT yet proven. Ruled-out list + one probe below.**
**Branch:** `feat/tower-core-loop` (committed build `adf1f2d9`).
**Reported:** 2026-06-16, owner F8 flag in MainCastle_Hall: *"second grom (companion wrong)."*
**Severity:** cosmetic (two Grom bodies); pre-existing logic bug (same in the source commit —
NOT introduced by the C:\EoA relocation; identical code/scenes).

## Symptom
Two "Grom" (Knight companion) bodies appear in MainCastle_Hall. Party should be one each of
Sylas (Ranger) / Elara (Cleric) / Grom (Knight) per canon join order Sylas→Elara→Grom.

## Method (instrument-first, §12)
Diagnosed from the **F8 BreakCaptureHarness** capture + `[Flow:Roster]` traces in Editor.log —
NOT theory. The harness gave the spawn sequence directly.

## RULED OUT (with evidence)
1. **StoryCompanionInjector double-spawn / broken guard — NO.**
   `Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs`. Singleton is correct: `Bootstrap()`
   skips if `Instance != null`; `Awake()` does `Destroy(this)` on a duplicate; `_companions` is
   `readonly`, never cleared. Trace: 3 Bootstraps, 0 skips → exactly ONE injector per session.
   The `alreadyLive` guard (line ~190) is sound. **`instanceCount now N` in the trace = `_companions.Count`
   (party size), NOT a duplicate counter** — a common misread; it does not indicate a double Grom.
2. **OuterWorld additive load triggering a 2nd Spawn — NO.** Trace shows
   `OnSceneLoaded(OuterWorld, Additive) isHub=False -> Spawn() if hub` correctly SKIPPING.
   `HubScenes.IsHub` Names = {Village2, MainCastle_Hall, CastleHub, CastleHub_MainKeep} — OuterWorld
   is not a hub.
3. **CompanionSpawner (tutorial) spawning a parallel body — NO.**
   `Assets/_Modules/Village/Tutorial/CompanionSpawner.cs` DELEGATES to StoryCompanionInjector
   (header: "exactly ONE companion … not duplicating"); uses `SetHeroClassOverride`, no own
   Instantiate. And it ran **0 times** this session (Editor.log).
4. **Hand-placed Grom in the scene — NO.** grep of `MainCastle_Hall.unity` for Grom / StoryCompanion /
   Knight-name / prefab-instance name override → none.
5. **GromOuterWorldReturnJoin spawning a body — NO.** It only `GameStateService.AddToParty("Knight")`
   (line 157); no Instantiate. (AddToParty is idempotent — `if (already) return;`.)

## LIVE HYPOTHESIS (unproven)
The trace shows `Spawn()` firing repeatedly with `currentlyLive=[]` — i.e. companion BODIES are
destroyed + re-spawned across the **scene seam** (hub load + OuterWorld additive + transitions).
A duplicate would result if a previously-spawned `StoryCompanion (Knight)` body **survives a
transition that the `_companions` dict believes it cleared** (dict entry nulled/removed by the
stale-despawn loop while the GameObject persists — e.g. parented under a DontDestroyOnLoad hero,
or re-parented so it outlives its origin scene). This matches the owner's read: "scene needs seamed."

## THE ONE PROBE TO RUN NEXT (cheap, ~10 min)
Add temporary FlowTrace in `StoryCompanionInjector.SpawnOne` (and the despawn loop):
- On spawn: log the new body's `gameObject.scene.name` + parent path + `GetInstanceID()`.
- On despawn: log which id is being destroyed.
- At Spawn() entry: scan `Object.FindObjectsByType<StoryCompanion>` and log COUNT + each one's
  scene/parent — this directly reveals an ORPHAN Knight body the dict isn't tracking.
Run headless or via F8 in one hub→outerworld→hub loop. The orphan's scene/parent names the fix:
either make companion bodies DontDestroyOnLoad-consistent with the dict, or reconcile bodies by a
`FindObjectsByType<StoryCompanion>` sweep at Spawn() instead of trusting the dict alone.

## Files
- `Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs` (SpawnOne ~200-228; Spawn ~147-197; guard ~190)
- `Assets/_Modules/Village/NPCs/GromOuterWorldReturnJoin.cs`
- `Assets/_Modules/Village/Tutorial/CompanionSpawner.cs`
- `Assets/_Modules/Core/HubScenes.cs`

## Do NOT
- Do NOT apply the "re-entrancy guard on Spawn()" a prior analysis suggested — the trace DISPROVED
  the race/double-call theory; that would patch a non-bug.
- Do NOT hand-edit scenes.

## Acceptance
- One probe capture identifies the orphaned body's origin (scene/parent).
- Fix makes exactly one Grom body exist in MainCastle_Hall across a full hub↔OuterWorld loop.
- Braces balanced; COMPILE_GATE_OK; owner playtest confirms a single Grom.
