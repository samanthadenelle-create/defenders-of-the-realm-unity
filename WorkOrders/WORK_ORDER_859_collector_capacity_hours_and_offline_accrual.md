# WORK ORDER 859 — Per-collector capacity in hours + offline collector accrual

**Status:** READY TO IMPLEMENT
**Author:** read-only RCA agent (§13), orchestrated by CLI, 2026-08-04
**Silo:** Economy / Harvest (Village). No scene files. No HUD files.
**Depends on:** `35485f31` (phantom-income gate), WO-834 (`everBuiltStructureIds`, v36), WO-855 (rate re-scale)
**Siblings:** WO-857 (town bank cap, `storageCapacity`) — **disjoint mechanism, disjoint files**.
WO-900 (the full tell) — parallel lane, same batch-gate.

---

## 1. Goal

A collector standing in the player's town produces **whether the player is in the hub, in a dungeon, in
a raid, or has the app closed**, into its own capped pending pool, and stops when that pool is full.
The cap is expressed in **hours of production** so it stays correct across levels, echo counts and future
rate changes. No new income route, no new wallet path, **no save bump**.

---

## 2. RCA

**R1 — no offline/away accrual exists.** Income requires `ResourceBuildingHarvester.Update` (`:110-213`)
to run with the collector registered. The harvester is a scene GameObject added by
`BuildingUpgradePanelMvvmBootstrap.cs:63-76` (`MoveGameObjectToScene`, not DDOL), suppressed only in
enemy-owned scenes (`:44`). Collectors unregister on unload (`ResourceCollector.cs:127-132`). Nothing
integrates elapsed wall-clock for collectors — verified by grepping every consumer of
`GameState.LastHarvestClaimMs`: `OfflineHarvestService.cs:139/145/180`, `EchoService.cs:263/424`,
`ArmyStorage.cs:85`, `GameStateService.cs:461/552/963`. **Zero collector consumers.**

**R2 — the away window cannot be taken from the OHS clock.** `OfflineHarvestBootstrap.cs:22-29` installs
`OfflineHarvestService` DontDestroyOnLoad, so its `Start` claim (`:89-94`) fires **once per app run** and
`OnApplicationPause(false)` (`:96-102`) only on mobile foreground return. A hub->dungeon->hub round trip
triggers **no claim and no clock advance**.
⚠ Existing ordering fragility, do not worsen: `EchoService.ClaimOffline` reads `LastHarvestClaimMs` from a
one-frame-deferred coroutine (`EchoService.cs:236-240`) while `OfflineHarvestService` advances it from its
own one-frame-deferred coroutine (`:110-119`). Order is script-execution-order dependent. **Do not add a
third consumer to that race.**

**R3 — the cap is authored against rates that no longer exist.** `repo.capacity` = farm **1000**,
lumbermill **800**, forge **600**. `RepoProps.cs:183` states the intent: *"a farm at ~150/min fills 1000 in
~7 min"* — true pre-WO-855 (farm L1 = 9,000/hr). Post-WO-855 farm L1 = **936/hr**
(`ResourceBuildingProgression.cs:189, 248-284`). **The comment is stale by ~9.6x.**

Hours-to-full today (`ComputeCapacity` = `catalogCap x (1 + 0.5x(level-1))`, `ResourceCollector.cs:274-277`):

| | L1 hrs | L3 hrs | L5 hrs |
|---|---|---|---|
| Farm (food) | **1.07** | 0.93 | **0.57** |
| Lumbermill (wood) | **1.11** | 0.97 | **0.61** |
| Forge (iron) | **1.39** | 1.17 | **0.71** |

**The curve runs BACKWARDS.** Capacity grows x3 from L1->L5 while throughput grows x5.6, so *upgrading a
collector shortens how long it can run unattended*. With the echo multiplier
(`ResourceBuildingHarvester.cs:174-180`, `amount x EchoService.GlobalHarvestMultiplier` = `EchoCount`),
a 6-echo L5 farm fills in **5.7 minutes**.

**In-repo precedent for the fix** — `EchoService.cs:142-149` already solved this exact bug for the silo:
*"Rate folds the FULL specialization aggregate while capacity only carried the count spine... Scaling
capacity by the SAME multiplier basis as rate keeps fill-time ~ SiloCapHours."*

**R4 (P0) — the existence gate has a live BACK DOOR that `35485f31` did not close.**
`ResourceCollectorBootstrap.EnsureFallbackCollector` (`:60-89`) stands up a live `ResourceCollector` for
farm, lumbermill and forge **unconditionally**, gated only on `ResourceCollectorRegistry.Get(id) != null`
(`:62`). It never consults `EverBuiltStructureIds`. `ResourceBuildingHarvester.MayHarvest` returns true the
instant `liveCollectorPresent` is true (`:236`). So:

- **Blank town (WO-834):** `ResourceCollectorBootstrap.Init` is `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`
  (`:16-23`) and runs **before** `BaseLayoutLoader` spawns placed structures -> registry empty -> three
  fallbacks created -> gate OPEN -> **a town with nothing in it earns again.**
- **Hub -> dungeon:** hub collectors unregister on unload; `WireScene` fires for the dungeon (`:25`, `:36-54`)
  -> fallbacks created -> the harvester **does** spawn in dungeons (they are not enemy-owned) -> **full town
  income accrues while the player is in a dungeon.** The exact defect the removed direct-grant was blamed
  for, surviving via a different route.

⚠ **These are STATIC READS, not captured data. §12: prove headless BEFORE editing.** The proving pair on a
blank-town run: `[Flow:Harvest] fallback-farm ...` (`ResourceCollectorBootstrap.cs:87-88`) together with
`[Flow:Harvest] existence gate OPEN for 'farm' (liveCollector=yes, everBuilt=[<empty>])`
(`ResourceBuildingHarvester.cs:134-137`).

---

## 3. Design call: raids/dungeons and app-close are ONE case

**They behave identically, and the mechanism does not care which it was.** The fiction is "your town keeps
working"; the player cannot tell an app-close from a dungeon run, and any difference is an exploit surface
(leave via the door that pays). Architecturally, keying on the collector's own last-accrual stamp collapses
both into one path — no dependence on service ordering, no dependence on which scene the harvester is alive
in, and no third consumer in the R2 race.

---

## 4. Mechanism

**Per-collector last-accrual stamp, self-applied on wake, paid through the existing `Accrue`.**

- **New PlayerPrefs key** beside the two the collector already owns:
  `dotr.collector.lastaccrual.<buildingId>`, in the same `LoadState`/`SaveState` pair
  (`ResourceCollector.cs:323-335`). **No GameState field, no `SaveSchema` change, no version bump** —
  precedent: pending and HP already persist this way.
- **`OnEnable`**, after `LoadState()` + registration: if the stamp is >0 and `now > stamp`, compute
  `awaySec`, derive `amount`, call the **existing** `Accrue(amount)`. Stamp 0 (fresh collector) -> seed to
  now, accrue nothing (mirrors `OfflineHarvestService.cs:142-148`).
- ⚠ **The stamp advances to `now` on EVERY accrual attempt, including when `Accrue` clamps at capacity and
  adds nothing. THIS IS THE HIGHEST-RISK DETAIL IN THE WO.** If the stamp freezes while full, then the
  moment the player taps Collect the collector instantly refills from the frozen backlog and the cap ceases
  to bound anything. Mirrors "always advance the clock even on a zero haul",
  `OfflineHarvestService.cs:176-181`.
- **Rate source must be SHARED, not duplicated.** Extract the online tick's amount math
  (`ResourceBuildingHarvester.cs:166-194`: `CurrentEffectiveYield` -> echo `GlobalHarvestMultiplier` ->
  `harvestRate` talent) into a public static `ResourceBuildingHarvester.EffectiveYieldPerTick(string id)`;
  `Update` and the offline path both call it. One authority per concern — the offline path must never
  re-implement the multiplier stack.
- **Overflow guard, not a balance cap:** clamp `awaySec` to 30 days before multiplying, purely so a tampered
  clock cannot overflow `Accrue`'s `int`. **Not** the design cap.

### Why this respects the existence gate
Offline accrual pays **only** through `Accrue`, which requires `CanAccrue` -> `IsActive` ->
`IsAlive && !_broken` (`:50-51, 153-157`). A collector component exists only because a structure was placed
(`StructureFactory.cs:744-752`). Banking still requires `Collect()` -> `EconomyService.GrantSpendable`
(`:180-210`). No new route to the wallet.

**…which is exactly why R4 must be fixed IN THIS WO.** `EnsureFallbackCollector` must gate on
`GameStateService.Instance?.State?.HasEverBuilt(catalogId)` (`GameState.cs:550-556`), resolving ids via
`ResourceBuildingHarvester.CatalogIdsForBuilding(id)` (`:264-282`) — the same resolution the gate uses, so
the two can never disagree. Without this, offline accrual becomes the back door.

### Crystals
Collectors yield Food / Wood / Iron only (`ResourceBuildingProgression.cs:248-284`); no collector row
authors Crystals. `Collect()`'s `HarvestResource.Crystals` arm (`:196`) is unreachable for these three.
**No crystal path opens.** Pinned by case 11.

---

## 5. The cap and its arithmetic — capacity becomes HOURS, not units

**Change `ComputeCapacity`** (`ResourceCollector.cs:268-302`): replace the level multiplier
`1.0 + 0.5 x (level-1)` (`:276`) with a **throughput-proportional** multiplier:

```
baseCap = repo.capacity                                   // authored L1 units (field unchanged)
scale   = effectiveYieldPerHour(level, echoes) / effectiveYieldPerHour(1, echoes=1)
cap     = baseCap x scale x (1 + collectorCap talent)     // WO-676 term unchanged, :293-300
```

Include level yield/interval and the echo `GlobalHarvestMultiplier`. **Exclude the `harvestRate` talent** —
deliberately, mirroring `EchoService.cs:146-148` ("capacity is `collectorCap`'s seam, not `harvestRate`'s"),
so both capacity systems obey one rule.

**Effect: hours-to-full is CONSTANT at every level and echo count**, equal to
`repo.capacity / yieldPerHour(L1)`. Capacity still grows on upgrade — *more* than before (x5.6 at L5 vs x3
today) — so "upgrade to hold more" is strengthened.

**Then retune the three data values.** L1 rates: farm 936/h, mill 720/h, forge 432/h (basket 2,088/h).

| Target | farm | mill | forge | basket/cycle | vs Echo silo (4h) |
|---|---|---|---|---|---|
| 1h (today) | 1000 | 800 | 600 | 2,400 | silo L1 = 480 |
| 4h (match silo) | 3,744 | 2,880 | 1,728 | 8,352 | one coherent window |
| **8h (RECOMMENDED)** | **7,500** | **5,760** | **3,456** | **16,716** | twice-a-day loop |

**Recommendation: 8h.** WWCD — CoC collectors hold enough that a morning and an evening check-in both pay,
and the storage/collector pair is what forces you to spend or come back. 8h gives a clean twice-a-day
rhythm and sits just above the silo's 4h so collectors read as the primary faucet. **Owner confirm — these
are data, not code.**

**The per-collector capacity IS the offline cap. Do NOT add a time-based cap.** `Accrue` clamps to
`Capacity` (`:169`), so 8h, 3 days or 3 weeks all yield exactly the pool. One mechanism instead of two, it
is what CoC does, and the offline window is tuned by the same dial as the online collect loop.

⚠ **Flood check for owner sign-off.** Echo silo capacity is
`SiloCapHours x BaseRatePerHour x EchoCount x AggregateHarvestMultiplier` (`EchoService.cs:149`): 480 at
1 echo, >=17,280 at 6. Collectors at 8h: 16,716 early; at L5/6-echo the scaled cap holds 8h of a 70,200/h
basket ~ **561,600**. **Late game the collector basket becomes ~30x the silo.** Intended shape (collectors
are the engine, silo is the workforce bonus) — but see it before signing off. **4h halves it** and still
doubles today's window. Early game is unambiguous: 16,716 + 480.

---

## 6. Files to edit

| File | Change |
|---|---|
| `Assets/_Modules/Village/Buildings/Progression/ResourceCollector.cs` | last-accrual stamp in `LoadState`/`SaveState`; advance unconditionally in `Accrue`; `OnEnable` catch-up; `ComputeCapacity` -> throughput-proportional |
| `.../ResourceBuildingHarvester.cs` | extract `public static int EffectiveYieldPerTick(string id)` from `:166-194`; `Update` calls it. No online behaviour change |
| `.../ResourceCollectorBootstrap.cs` | **R4 P0 fix**: gate `EnsureFallbackCollector` on `HasEverBuilt` of the resolved catalog ids |
| `Assets/{Resources,StreamingAssets}/Data/Canonical/structures-catalog.json` | retune `repo.capacity` on the three collector rows. **Both copies — parity is oracle-checked** (`CollectorIncomeRegression.cs:312-314`) |
| `Assets/_Modules/Core/Catalog/RepoProps.cs` | doc only: `:177-188` "~150/min fills 1000 in ~7 min" is stale by 9.6x |
| `Assets/Editor/Regression/CollectorIncomeRegression.cs` | cases 7-11 |

---

## 7. Acceptance criteria

1. A collector whose stamp is N hours old accrues `min(N x effectiveRate, Capacity)` on wake, **never** more.
2. **The stamp advances even at cap.** Fill to cap -> wait 4h -> Collect -> wait 0s -> pending ~0, **not**
   refilled from a frozen backlog.
3. A fresh collector (stamp 0) seeds to now and accrues nothing that launch.
4. A clock set backwards yields 0 — never negative, never a re-claim
   (mirror `OfflineHarvestService.cs:152-154`).
5. Hub->dungeon->hub and app-close->relaunch credit the **same** amount for the same wall-clock gap.
6. Hours-to-full within +/-5% of target at L1, L3, L5 and at 1 and 6 echoes.
7. **A blank town accrues ZERO, online and offline** — proven by a headless capture showing no
   `fallback-*` trace and `existence gate CLOSED` for all three ids.
8. No `EconomyService` / `ResourceLedger.Credit` reference added to `ResourceCollector` or the harvester
   (already oracle-enforced, `CollectorIncomeRegression.cs:189-196`).
9. `COMPILE_GATE_OK` + `COLLECTOR_INCOME_OK` + `REGRESSION_OK <n>/<n> suites`.

---

## 8. Regression — extend `CollectorIncomeRegression` (`COLLECTOR_INCOME_OK`)

Natural home: registered at `DataRegression.cs:459`, already owns the existence-gate truth table and the
catalog map, and its case 6 asserts the level-1-accrues rule this WO depends on.

- **7 `[offline-capped]`** — `min(away x rate, cap)` for away = 1h / 8h / 30d at L1/L3/L5 never exceeds cap,
  equals `rate x away` below it.
- **8 `[stamp-advances-at-cap]`** — source assert: the stamp write in `Accrue` is **outside** the
  `if (_pending > before)` block. **This catches the highest-risk regression.**
- **9 `[capacity-hours-stable]`** — hours-to-full equal within tolerance at L1/L3/L5 and echo 1/6; fails if
  the level scale reverts to a constant multiplier.
- **10 `[fallback-gated]`** — source assert: `EnsureFallbackCollector` references `HasEverBuilt`.
- **11 `[no-crystal-faucet]`** — no `OrderedIds` entry yields `Crystals`, no Collector row routes to a
  crystal resource. **Durable enforcement of the owner's uncapped-crystals ruling — a test, not a comment.**

---

## 9. What NOT to touch

- **`storageCapacity`, `IsStorageContainer`, the wallet clamp, resource-dock `current/max` chips,
  `lumberyard`/`foundry`/`silo`** — WO-857. **Do not clamp `EconomyService.Grant`.**
- **`GameState.LastHarvestClaimMs` / `OfflineHarvestService`** — do not add a third consumer to the R2 race.
  Do not change the 10h or Echo 4h caps.
- **`EchoService` / the silo** — separate faucet, separate cap.
- **`ResourceCollectorService.CollectAll`, `AutoHarvestService`, the Collect tap** — unchanged.
- **The direct-grant removal from `35485f31`** — do not reintroduce any wallet path in the harvester.
- **`SaveSchema` / `SaveMigrator` / `CurrentVersion`** — no bump. If an implementer thinks one is needed,
  **stop and escalate**; the design exists to avoid it.
- **Any `.unity` scene file.**

---

## 10. §15 canon updates (same commit)

- `CANON_GROUND_TRUTH_<date>.md` — collectors accrue offline; capacity is hours-derived; the fallback gate.
- `docs/MASTER_CATALOG/village-systems.md:220` — says the silo and OHS "share the clock only"; add the
  collector path. `:130` lists `CollectorStackView (437)` as if live — see WO-900.
- `docs/MASTER_CATALOG/core.md` — `RepoProps.capacity` semantics: units -> hours-derived.
- `Assets/Resources/Data/Economy/offline-storage.json` — `_sources` names only OHS/EchoService.
- `docs/qa/GAMEPLAY_GAPS_2026-07-26.md:79` — cites `CollectorStackView.cs:367` as if the VFX fires.
- `KEY_FACTS.md` / `SESSION_CANON_LOADER.md` — one line.
