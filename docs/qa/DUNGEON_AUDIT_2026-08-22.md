# Dungeon systems audit — 2026-08-22

## Verdict

The initial audit found a healthy structural fleet but an incomplete composed boss/difficulty contract. The authorized follow-up implements those missing contracts; final full-gate and per-portal player evidence is recorded below as it completes.

Dungeon identifiers were deliberately not renamed. The balance implementation is additive and data-driven.

## Implemented follow-up

- Dungeon layouts now author one shared `tier` and `recommendedLevel` source. Encounter threat inherits from dungeon tier plus vertical floor depth, using the same conservative +8% per threat step already established by the real-time arena.
- Starter Loop remains tier 1 / level 1; Sunken Vault is tier 2 / level 5; Bonecrypt tier 3 / level 9; Ember Deep tier 4 / level 13.
- Hollow Warden now fields the catalog `hollow-brute`; Ember Warlord fields `troll-overlord`; authored boss display names override the catalog title.
- A composed boss spawner owns a living-enemy set and emits exactly one boss-clear event. The composed host records that in the existing `DungeonRuntimeState` authority.
- Exits inside the boss room are sealed until boss clear. The locked state uses a large crossed seal plus the words `SEALED / DEFEAT BOSS`, not colour alone.
- Boss-room hoard containers are hidden until boss clear, preventing loot-and-run theft. No parallel reward currency or duplicate boss payout was added.
- Boss-source containers now include `bossOnly` loot lines. Their captured roll is deposited once rather than being silently rerolled on the direct-deposit path.
- `dungeon-deepboss` guarantees one `heartwood_core`; all legendary lines retain their independent 20% chances.
- Darkness ambushes cannot select a boss spawner and accidentally create another boss.
- `RoomPrefabMeta` now declares vertical occupancy. The overlap test evaluates true XYZ interval penetration and includes a bite case for a room embedded in the upper storey of a stairwell.
- AutoPilot accepts `-Dungeon <id>` and has a composed-scene branch covering portal selection, hero combat capability, room-bound live spawns, boss-clear lifecycle, and real D-pad movement.

## Scope

- Hand-authored playable dungeon: `Dungeon_HealersCottage`
- Gated stub: `Dungeon_FolksGranary`
- Composed content: `dg_starter_loop`, `dg_sunken_vault`, `dg_bonecrypt`, `dg_ember_deep`, `dg_hollow_roads`
- Composed engineering/control scenes: `dg_descent_probe`, `dg_stairwell_probe`, `dg_stair_rig`
- Systems reviewed: portal routing, scene/build registration, graph reachability, room ownership, movement/NavMesh return, stairs, doors, keys, locks, entrances, exits/extracts, traps, enemy families/counts/stats, boss lifecycle, ambushes, run state, loot-table math, chest/cache persistence, and larder delivery.

## Evidence run

- `COMPILE_GATE_OK` in `Builds/dungeon-audit-compile-gate.log`.
- Current Windows player built successfully: `[build] SUCCESS`.
- Full data regression: 245/247 suites passed. Both failures are outside dungeon logic: `staff_A` sheathe orientation and three raid-wall material assignments. Every registered dungeon suite passed.
- Two current-player `DungeonLoop` runs passed Healer's Cottage from hub entry through encounter, victory, return pose, on-NavMesh settlement, movement recovery, and lighting restoration.
- StreamingAssets and Resources copies of all eight `dg_` graphs and layouts are byte-identical.

## Fleet findings

| Area | Result | Evidence / qualification |
|---|---|---|
| Graph connectivity | Pass | All eight composed graphs have a valid entry, no unreachable nodes, no dangling edges, and no duplicate node IDs. |
| Layout/graph parity | Pass | Room counts agree for every composed graph/layout pair; all dual copies hash identically. |
| Entry and return routing | Pass | Every designated `exitRoomId` names an authored room. Ember Deep intentionally seats its only true exit in `warlord_keep`; the other content layouts return at entry and may provide one back extract. |
| Keys and locks | Pass | Sunken Vault, Bonecrypt, and the descent probe place the matching key on the entry side of the locked edge. The gated destination has no bypass route. |
| Doors and room ownership | Pass with future-risk note | Registered door, socket, room-owner, confinement, and composed-pillar suites pass. Current graphs have no duplicate connection IDs. `CommonDungeonDoor`'s static claim key is not dungeon-qualified, so future duplicated connection IDs could collide across additively loaded dungeons. |
| Stairs / multilevel movement | Pass with oracle gap | 14/14 multilevel checks pass and authored stairwell coordinates show no present room embedded in a stairwell volume. `RoomPrefabMeta` has no vertical extent, so `RoomsOverlap` cannot detect that future authoring error. |
| Runtime movement | Partial pass | Healer's Cottage passed entry, fight, return, NavMesh, live mover, D-pad movement, lighting, and pose ownership twice. The current AutoPilot chooses one portal and therefore does not certify each composed content dungeon end to end. |
| Encounter placement | Pass | Authored room encounter counts, family tokens, min/max group counts, room seating, wake radius, and confinement are present and regression-guarded. |
| Difficulty progression | **Fail** | Deep layouts increase pack count/family weight, but composed enemies use unscaled `enemies.json` stat blocks and `ComposedAmbushDirector` is always tier 1. The hand-authored real-time path uses threat 1 for normal fights and 3 for bosses; recommended level is presentation, not scaling. |
| Composed bosses | **Fail** | `isBoss` forces count 1 and MiniBoss AI role only. The Hollow Warden is the base 156-HP `hollow-warrior`; Ember Warlord is the base 280-HP `ogre`; Bonecrypt uses the authored 1,700-HP `necromancer`. No shared composed boss-clear lifecycle gates the exit or grants a boss-clear reward. A player can evade a composed boss and use the nearby exit/extract. This conflicts with WO-1001's accepted boss-scaled, exit-gated contract. |
| Random/darkness encounters | Partial | Darkness increases the random encounter rate, but every composed dungeon configures the table as tier 1, so dungeon depth/identity does not affect ambush size/rate tier. |
| Loot catalogs | Pass | Dungeon chest/container table IDs resolve; Resources and StreamingAssets catalogs match; rolls are independent and data-driven. |
| Loot delivery | Pass in player lifecycle | `VillageInventory` bootstraps after scene load and persists. The grant path warns and drops rewards if the singleton is absent; this is reachable in isolated tests/editor invocation but not demonstrated in the normal player lifecycle. |
| Reward curve | Needs owner ruling | Approximate expected item counts per roll: `crate-common` 3.45, `barrel-common` 2.35, `chest-rare` 5.25, `dungeon-hollow` 3.77, `dungeon-miniboss` 4.06, `dungeon-deepboss` 2.10, `dungeon-chest` 7.23. `dungeon-deepboss` has about an 8.26% empty-roll chance; its lower count carries higher-value boss-only materials and boss rooms also contain authored hoard chests. Do not tune this without deciding whether a boss payoff may ever be empty. |
| One-shot/persistence behavior | Pass | Chest-open, first-clear cache, defeat, real-time settle, state-reset, return, toast, and treasure regressions pass. The deepest cache's fixed supply is deterministic; its recipe component is first-clear-only. |

## Required corrections before “perfect”

### P0 — complete the composed boss contract

Create one shared composed-boss lifecycle rather than special-casing each dungeon:

1. Expose a group/boss-clear signal from `OutpostEnemyGroupSpawner` based on the actual spawned enemies reaching zero alive.
2. Have the composed run owner record boss defeated exactly once.
3. Keep the true/back exit unavailable until that state is true where the layout declares a boss-gated exit. The locked state needs an obvious word + shape treatment, not colour alone.
4. Decide whether the boss-clear event grants `dungeon-deepboss`, unlocks the existing hoard chest, or both. Avoid accidental double payment.
5. Add regression coverage proving the exit starts locked, trash deaths do not unlock it, boss death unlocks it once, reload/resume cannot re-pay, and evade-to-exit fails.

### P1 — author one difficulty source of truth

Add explicit dungeon/encounter difficulty data instead of deriving it from room names or hardcoding `tier: 1`. At minimum it should drive:

- base enemy stat multiplier or authored threat;
- darkness ambush tier;
- boss multiplier/definition;
- reward scaling, if difficulty is meant to affect reward;
- a display/recommended-level value sourced from the same record.

Suggested initial shape (values deliberately omitted): dungeon `recommendedLevel`, dungeon `tier`, encounter `depth`, encounter/boss `threat`. Preserve group-count differences already authored. Felt-tune values in a separate balance pass.

### P1 — certify every shipping portal at runtime

Extend the dungeon AutoPilot phase to accept a dungeon ID/portal ID, then run the same entry → traversal → fight → settle → exit assertions for Healer's Cottage plus all five shipping composed portals. Do not count probe/control scenes as shipping coverage.

### P2 — close the vertical-overlap oracle gap

Add authored vertical bounds (or a declared occupied Y interval) to `RoomPrefabMeta` and make `RoomsOverlap` use 3D occupancy. Retain the current valid stacked-stair behavior and add a bite test for a room inserted inside a two-storey stairwell.

### P2 — pin reward intent

Add expected-value and empty-roll regression bands after the owner rules whether deep-boss rewards may roll empty. Existing tests validate mechanics and IDs, not the intended progression curve.

## Prefix migration guidance

Defer renaming until the corrections above are stable, then land it as a standalone commit. Recommended namespace:

- shipping scenes/data: `dg_<name>`
- probes: `dg_probe_<name>`
- shared room/prefab assets: `dg_common_<name>`

The migration is safe only if `.meta` files/GUIDs are preserved and all string references are migrated atomically: build settings, graph/layout IDs and both canonical copies, portal catalog/authored portal IDs, `Resources.Load` paths, scene routing, status/save keys, first-clear keys, tests/regressions, documentation, and any analytics IDs. For persisted keys, keep read aliases from legacy IDs and write only the new canonical ID. Run an `rg` zero-old-reference audit plus compile, full data regression, a fresh player build, and per-portal runtime coverage before removing aliases.

## Audit boundary

Headless runs do not certify pixels. The player run also reported shader/material guard noise around portal planes and Healer's Cottage pickup motes, but `-nographics` can create shader false positives. Those require a graphics-enabled screenshot/F8 pass and are not treated here as proven dungeon rendering defects.
