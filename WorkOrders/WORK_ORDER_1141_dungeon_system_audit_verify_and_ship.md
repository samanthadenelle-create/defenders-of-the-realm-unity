# WORK ORDER 1141 — Dungeon system audit: verify and ship the cohesive runtime pass

**Status:** COMPLETE — OWNER ACCEPTED 2026-08-22
**Date:** 2026-08-22  
**Owner:** CLI  
**Implementation state:** Changes are present in the working tree but are intentionally uncommitted.  
**Scope rule:** Verify and commit only the explicit dungeon allowlist in this document. The working tree contains concurrent siege/loss-stakes and VFX work that does not belong to this work order.

## Closure — owner ruling, 2026-08-22

The owner marked this work order complete. The implementation was committed after a fresh compile gate and full DataRegression run that included the dungeon changes. Evidence reported by the CLI:

- fresh `COMPILE_GATE_OK`;
- DataRegression `248/250`;
- neither failing suite belongs to the dungeon lane; both are separately ticketed asset gaps;
- the dungeon allowlist was staged and committed independently from the concurrent siege and VFX lanes.

The Windows rebuild and six-target runtime matrix described below were not run as part of this closure. This is an explicit owner acceptance, not a claim that those probes passed. The owner is the sole player/felt-test authority and will reopen this WO or create focused follow-up tickets for defects found during playtesting. Accordingly, those deferred probes are no longer acceptance blockers for WO-1141.

## Intent

Take the completed dungeon audit/fix lane through independent CLI review, Unity compile/regression, Windows build, and one real runtime probe per shipping dungeon. Push only if the implementation and evidence agree.

This is a verification and shipping work order, not an instruction to accept the changes blindly. Review the implementation against the outcomes below. Make narrowly scoped corrections if required, keep those corrections inside this WO's dungeon ownership, and record any disagreement rather than weakening an oracle to match the code.

## Player-facing outcome

The composed dungeons should now behave as one coherent system:

- Every authored dungeon has an explicit recommended level and tier.
- Encounter pressure increases by dungeon tier and by descended floor.
- Deep dungeons contain a real, identifiable boss encounter with boss-grade base stats.
- A dungeon's exit is visibly sealed until its boss is defeated.
- The boss-room reward cache remains unavailable until the boss dies.
- Boss defeat is exact-once, updates runtime state, unlocks the exit, and reveals the payoff.
- Darkness ambushes cannot select or duplicate the authored boss group.
- Deep-boss loot actually evaluates boss-only entries and has a guaranteed reward line.
- Multi-floor overlap validation understands rooms that occupy more than one vertical level.
- The Windows runtime harness can target each dungeon explicitly and prove entry, room binding, enemies, boss lifecycle, player movement, and return to the hub.

## Design decisions to preserve unless review finds a concrete defect

### Difficulty model

Authored progression:

| Dungeon | Recommended level | Base tier | Expected deepest threat |
|---|---:|---:|---:|
| `dg_starter_loop` | 1 | 1 | 1 |
| `dg_hollow_roads` | 1 | 1 | layout-derived |
| `dg_sunken_vault` | 5 | 2 | 5 |
| `dg_bonecrypt` | 9 | 3 | 7 |
| `dg_ember_deep` | 13 | 4 | 9 |

An encounter with authored threat `0` inherits `base tier + descended floor`. Runtime scaling is `1 + 0.08 * (threat - 1)`, capped at threat 20. Stats and rewards scale together. Do not silently flatten this back to one global difficulty value.

Boss catalog choices:

- Sunken Vault Warden: `hollow-brute` (base HP 900)
- Bonecrypt boss: `necromancer` (base HP 1700)
- Ember Warlord: `troll-overlord` (base HP 1100)

The regression floor is base HP 800. This checks that a boss key cannot regress to a normal trash enemy while still looking syntactically valid.

### Boss lifecycle and UX

`OutpostEnemyGroupSpawner` is the runtime authority for the live group and exact-once `BossCleared`. `ComposedDungeonHost` coordinates the authored boss room, exit gate, cache reveal, and `DungeonRuntimeState`. `DungeonExitInteractable` owns the actual leave refusal and its visible seal.

The locked exit must communicate without depending on color: crossed seal geometry plus `SEALED` / `DEFEAT BOSS` wording where the existing label supports it. Unlock must remove the seal and restore the normal exit state.

Boss-room `BreakableContainer` objects start hidden and become active only after the authored boss clears. Confirm the spatial room association is correct in every deep dungeon.

### Loot correctness

The two canonical `loot-tables.json` copies must remain byte-identical. The deep-boss `heartwood_core` line is now guaranteed (`chance: 1.00`). Boss-source tables must roll with `includeBossOnly=true`.

The direct-deposit path must deposit the captured roll exactly once. It must not perform a second hidden roll when the first result is empty. This fixes two separate defects: boss-only lines were previously excluded, and empty direct-deposit results were silently rerolled.

### Vertical geometry

`RoomPrefabMeta.occupiedLevels` is the authored vertical footprint. A compatibility value of `0` infers two levels for `StairwellRoom` and one for other rooms. Overlap checks must compare actual vertical intervals. Keep the regression bite case where an upper-floor room placed inside a two-level stairwell is rejected.

## Explicit file ownership / staging allowlist

Only the following implementation and evidence files belong to this dungeon lane:

### Regression and editor tooling

- `Assets/Editor/Regression/DungeonEncounterFamilyRegression.cs`
- `Assets/Editor/Regression/DungeonMultiLevelRegression.cs`
- `Assets/Editor/RoomForge/DefaultStairwellRoomBuilder.cs`
- `Assets/Editor/RoomForge/DungeonBaker.cs`
- `Assets/Editor/RoomForge/GraphDungeonComposer.cs`

### Runtime

- `Assets/_Modules/DevTools/AutoPilotDriver.cs`
- `Assets/_Modules/Dungeons/ComposedAmbushDirector.cs`
- `Assets/_Modules/Dungeons/ComposedDungeonHost.cs`
- `Assets/_Modules/Dungeons/DungeonExitInteractable.cs`
- `Assets/_Modules/Dungeons/DungeonRoomBinder.cs`
- `Assets/_Modules/Dungeons/RoomForge/DungeonBakerChecks.cs`
- `Assets/_Modules/Dungeons/RoomForge/DungeonComposeLayout.cs`
- `Assets/_Modules/Dungeons/RoomForge/RoomPrefabMeta.cs`
- `Assets/_Modules/Village/Enemies/OutpostEnemyGroupSpawner.cs`
- `Assets/_Modules/Village/Items/ItemDropSystem.cs`
- `Assets/_Modules/Village/World/BreakableContainer.cs`
- `run-autopilot-fleet.ps1`

### Canonical data — both mirrors are required

- `Assets/Resources/Data/Canonical/dungeon-graphs/dg_bonecrypt.json`
- `Assets/Resources/Data/Canonical/dungeon-graphs/dg_ember_deep.json`
- `Assets/Resources/Data/Canonical/dungeon-graphs/dg_hollow_roads.json`
- `Assets/Resources/Data/Canonical/dungeon-graphs/dg_starter_loop.json`
- `Assets/Resources/Data/Canonical/dungeon-graphs/dg_sunken_vault.json`
- `Assets/Resources/Data/Canonical/dungeon-layouts/dg_bonecrypt.json`
- `Assets/Resources/Data/Canonical/dungeon-layouts/dg_ember_deep.json`
- `Assets/Resources/Data/Canonical/dungeon-layouts/dg_hollow_roads.json`
- `Assets/Resources/Data/Canonical/dungeon-layouts/dg_starter_loop.json`
- `Assets/Resources/Data/Canonical/dungeon-layouts/dg_sunken_vault.json`
- `Assets/Resources/Data/Canonical/loot-tables.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-graphs/dg_bonecrypt.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-graphs/dg_ember_deep.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-graphs/dg_hollow_roads.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-graphs/dg_starter_loop.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-graphs/dg_sunken_vault.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_bonecrypt.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_ember_deep.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_hollow_roads.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_starter_loop.json`
- `Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_sunken_vault.json`
- `Assets/StreamingAssets/Data/Canonical/loot-tables.json`

### Documentation

- `docs/qa/DUNGEON_AUDIT_2026-08-22.md`
- `WorkOrders/WORK_ORDER_1141_dungeon_system_audit_verify_and_ship.md`

## Explicit exclusions

Do not stage a file merely because it is dirty. In particular, this WO does **not** own:

- `Assets/Editor/Regression/DataRegression.cs` — concurrent registrations are owned by other lanes; the dungeon suites were already registered.
- Any siege, loss-stakes, defense-report, feature-flag, save-schema, resource-collector, or siege regression file.
- Any elite/boss VFX, surface-impact VFX, mirror manifest, VFX material, VFX texture, `HitSurface`, or related regression file.
- `BOARD.html`, `CLI_LANES_WO_NUMBERS.md`, or another WO/RESULT.
- `Assets/Editor/Regression/BreakableContainerChestRegression.cs`; its current dirty state is not part of this lane.
- Generated `Logs/`, `dev/`, cache folders, builds, or `tools/__pycache__/`.

If another lane edits one of the allowlisted shared runtime files before staging—especially `OutpostEnemyGroupSpawner.cs`, `ItemDropSystem.cs`, or `BreakableContainer.cs`—inspect the patch hunk by hunk. Stage only dungeon-owned hunks, or coordinate a clean lane split. Do not overwrite or absorb another lane's work.

## Review checklist before running Unity

1. Inspect every allowlisted diff; reconcile it to a stated outcome above.
2. Confirm braces and file encoding are intact for touched C# files; no NUL bytes.
3. Parse `run-autopilot-fleet.ps1` with the PowerShell parser.
4. Confirm each Resources canonical file is byte-identical to its StreamingAssets counterpart.
5. Confirm all boss enemy IDs resolve through the actual enemy catalog and meet the HP floor.
6. Confirm `BossCleared` is exact-once and every `Enemy.Died` subscription has a lifecycle-safe unsubscribe/cleanup path.
7. Confirm a pre-baked spawner receives the new runtime threat/display fields; the fix intentionally does not require every scene to be rebaked.
8. Confirm the ambush director filters boss groups.
9. Confirm locked exits reject the real interaction path, not merely alter presentation.
10. Confirm boss-cache reveal and loot deposit do not duplicate rewards across re-entry or repeated callbacks.

## Unity verification sequence

Do not begin until the user says **go** and the user's Unity/Library rebuild process has fully released the project.

Run gates sequentially from the repository root. Judge wrappers by fresh marker and log contents, not process exit code alone.

```powershell
.\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName dungeon-final-compile.log -ExpectMarker COMPILE_GATE_OK

.\run-unity-method.ps1 -Method DeNelle.Editor.Regression.DungeonEncounterFamilyRegression.RunAll -LogName dungeon-final-encounter-family.log -ExpectMarker DUNGEON_ENCOUNTER_FAMILY_OK

.\run-unity-method.ps1 -Method DeNelle.Editor.Regression.DungeonMultiLevelRegression.RunAll -LogName dungeon-final-multilevel.log -ExpectMarker DUNGEON_MULTILEVEL_OK

.\run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName dungeon-final-data-regression.log
```

Method namespace spelling must be verified against source before execution; use the actual fully qualified method if the declaration differs. Do not edit an oracle merely to make a failing implementation pass.

Expected full DataRegression baseline before the concurrent lanes finish was 245/247, with two unrelated ownership groups: unresolved `staff_A` sheathe orientation and three raid wall-tier embedded materials. Re-read the fresh output because concurrent siege/VFX work may change the count. Classify every failure by owner; no dungeon failure is acceptable for this WO.

## Build and runtime matrix

Before rebuilding Windows, resolve and verify that the exact deletion target is `D:\eoa\Builds\Windows`; remove only that directory if present. Then run:

```powershell
.\build-windows.ps1
```

Require the build's real success marker and a fresh executable.

Run `DungeonLoop` once per target, sequentially, preserving evidence from each run before the fleet script rotates its run logs:

```powershell
.\run-autopilot-fleet.ps1 -Count 1 -SeedStart 11410 -TimeoutMin 10 -Phases DungeonLoop -Dungeon HealersCottage
.\run-autopilot-fleet.ps1 -Count 1 -SeedStart 11411 -TimeoutMin 10 -Phases DungeonLoop -Dungeon dg_starter_loop
.\run-autopilot-fleet.ps1 -Count 1 -SeedStart 11412 -TimeoutMin 10 -Phases DungeonLoop -Dungeon dg_hollow_roads
.\run-autopilot-fleet.ps1 -Count 1 -SeedStart 11413 -TimeoutMin 10 -Phases DungeonLoop -Dungeon dg_sunken_vault
.\run-autopilot-fleet.ps1 -Count 1 -SeedStart 11414 -TimeoutMin 10 -Phases DungeonLoop -Dungeon dg_bonecrypt
.\run-autopilot-fleet.ps1 -Count 1 -SeedStart 11415 -TimeoutMin 10 -Phases DungeonLoop -Dungeon dg_ember_deep
```

For composed combat dungeons, require evidence of:

- correct requested portal/dungeon selection;
- successful entry;
- room-bound spawners and at least one live enemy;
- an authored boss spawner in each deep dungeon;
- boss death through the real `Enemy.Kill`/death lifecycle and observed `BossCleared`;
- actual player displacement driven through the real D-pad path;
- `DUNGEON_LOOP_PROBE` success verdict;
- clean return to the hub;
- no `FlowTrace.Fail`, fatal exception, or hidden timeout.

`HealersCottage` is the legacy baseline. `dg_hollow_roads` may legitimately have a different combat/boss expectation, but it must still enter, move, and return cleanly. Do not manufacture enemies solely to satisfy a generic probe.

## Evidence deliverable

Update `docs/qa/DUNGEON_AUDIT_2026-08-22.md` with:

- reviewed commit base and final commit hash;
- exact compile/regression/build markers and fresh log paths;
- full DataRegression pass/fail count with unrelated failures named;
- one row per runtime target with seed, verdict, boss evidence where applicable, movement evidence, return-to-hub evidence, and log location;
- any changes the CLI made during verification and why;
- explicit statement that Resources/StreamingAssets canonical mirrors hash-match;
- explicit statement that staged files match this allowlist.

## Commit and push protocol

1. Re-run `git status --short` immediately before staging.
2. Stage with explicit pathspecs from the allowlist. Never use `git add -A`, `git add .`, or a broad directory path that captures concurrent files.
3. Inspect `git diff --cached --stat` and `git diff --cached` in full.
4. Compare `git diff --cached --name-only` against this allowlist. Any unexplained path is a stop condition.
5. Confirm unstaged concurrent work remains present and untouched.
6. Commit only after every dungeon gate is green and runtime evidence is recorded.
7. Suggested commit message: `feat(dungeons): enforce scaling boss gates and runtime coverage`
8. Push the current intended branch only after confirming its upstream. Do not create, merge, rebase, or force-push without separate user direction.

## Acceptance criteria

- [ ] CLI independently reviewed all dungeon-owned diffs.
- [ ] Compile gate emits fresh `COMPILE_GATE_OK`.
- [ ] Encounter-family regression emits fresh `DUNGEON_ENCOUNTER_FAMILY_OK`.
- [ ] Multi-level regression emits fresh `DUNGEON_MULTILEVEL_OK`.
- [ ] Full DataRegression has no dungeon-owned failure; unrelated failures are named, not hidden.
- [ ] Resources and StreamingAssets dungeon graphs, layouts, and loot table are byte-identical by pair.
- [ ] Windows build succeeds from a clean `Builds/Windows` target.
- [ ] All six runtime targets pass their applicable `DungeonLoop` contract.
- [ ] Boss exits remain locked before boss death and unlock after the exact-once boss-clear event.
- [ ] Boss cache is unavailable before clear and available after clear without duplicate loot.
- [ ] Difficulty rises across authored dungeon tiers and descended floors.
- [ ] Multi-level stairwell overlap bite case passes.
- [ ] Cached diff contains only allowlisted files/hunks.
- [ ] Concurrent siege and VFX work remains unstaged and unchanged by this commit.
- [ ] Audit evidence is updated, committed, and pushed with the implementation.

## Stop conditions

Stop and report rather than improvising if:

- the user has not said **go** or Unity is still rebuilding/importing;
- a canonical mirror pair differs for reasons outside this WO;
- another lane has overlapping hunks in an allowlisted shared file;
- a dungeon oracle fails and the proposed response is to derive its expectation from the changed constant;
- boss clear, loot, or exit unlock can fire twice;
- the runtime probe reaches the wrong dungeon despite `-Dungeon`;
- the proposed commit contains any non-allowlisted path;
- the upstream branch is ambiguous or pushing would require force.
