# WO-1026 — IMPLEMENTATION PLAN: PvE Siege + the Defense Report artifact

**Parent WO:** `WorkOrders/WORK_ORDER_1026_raid_defense_consequence_loop.md`
**Ruling being implemented:** owner 2026-08-17 — **model (a) PvE siege**, built so **(c) ghost-PvP is a source swap**.
**Plan authored:** 2026-08-17 (research seat, read-only — no code touched; an APK build was in flight).
**Status of this document:** READY TO IMPLEMENT. Everything below is verified at source this session with
file:line citations. Nothing here is inferred from comments alone.

> ⛔ **THE STAKES ARE STILL UNRULED.** This plan ships attacks that **resolve and REPORT but take nothing**.
> §7 names the single seam where the loss ruling plugs in. Do not invent an economy rule. Do not "just take
> 10% of stockpile" — that collides with `stockpiles-cap-capacity` and the WO-947 basket ruling.

---

## 0. TL;DR for the implementing agent

1. **Do not build a spawner.** `WaveManager` already owns "hostiles attack the player's town" and is the
   only thing that does. You add a *scheduler* and a *recorder*, not a second attacker.
2. **Do not build a damage aggregator.** `WaveDamageReport.Collect()` already enumerates every damaged /
   destroyed player structure worst-first, priced. You *serialise* its output; you do not re-scan.
3. **The deliverable is a persisted, source-agnostic DATA record** (`DefenseReportRecord`) in
   `DeNelle.Core`, saved at **SaveSchema v39**, plus a panel that reads it back.
4. **The (c) upgrade is one enum field** (`AttackerSource`) plus `snapshotId` — designed in from line one,
   round-trip-proven by an oracle that serialises a `GhostSnapshot`-sourced record today.
5. Everything ships behind **`FeatureFlags.Siege` (`ff.siege`), default OFF**, so the current build's
   behaviour is byte-identical until the owner flips it.

---

## 1. SME baseline — what already exists (verified at source)

### 1.1 The town-attack authority — `WaveManager`
`Assets/_Modules/Village/Waves/WaveManager.cs` (3338 lines, assembly **`DeNelle.Village`**).

| Fact | Citation |
|---|---|
| Singleton, "active-scene wins" claim rule | `WaveManager.cs:629` (`public static WaveManager Instance`), claim logic `:632-644`, release `:654` |
| Phases `Idle/Countdown/Active/Breached/Complete/Defeated` | `:52-68` (`enum WavePhase`) |
| Kickoff is `BeginLoop()` — **the SOLE kickoff** | `:888`; the comment at `:667` states it explicitly |
| External force-start seam (dev / DEFEND button) | `ForceBeginNextWave()` `:1437`; `ForceSpawnNextWaveNow` `:1472`; `_forceSpawnNow` field `:399` |
| Wave clear → `CompleteWave()` | `:2830-2866`; `OnWaveCleared.Invoke(cleared)` at `:2851` |
| `OnWaveCleared` event (WaveNumberEvent) | declared `:312`; `OnDefeat` (UnityEvent) `:323` |
| The record-is-complete-before-listeners contract | comment `:328-341` — payout is stamped *before* `OnWaveCleared` fires |
| Payout record `WaveClearPayout` (anti-staleness keyed on WaveId) | `:~85-140`; reader `TryGetPayoutFor` |
| Roster is GENERATED, `waves.json` `enemies[]` batches are INERT | `_smartComposition` `:197`, warn `:1709-1711` |
| Spawn budget / concurrency cap (WO-1113) | `SmartSpawnBudget()` `:1977-1978` → `SmartEnemySpawner.BudgetFor(_maxSimultaneousEnemies, _liveEnemies.Count)`; reinforcement drip `:2079-2085`; shared post-spawn treatment `RegisterSmartSquad` `:1987-2016` |
| Public per-enemy spawn seam used by the FTUE | `SpawnEnemyForExternalMode(EnemyDef, Vector3, Transform heart, string)` `:567` |

**WO-1113 note:** the spawn-budget work (landed 08-16) means a wave's roster is released in *slices* —
first release plus reinforcement drips as live enemies die (`:2079-2085`). Consequence for this plan: a
siege's "attacker roster" is **not** all on screen at once, so the report's attacker unit list must be
recorded from the **composed roster**, not from a scene census at any single instant.

**Every other spawner, and why none of them is the seam** (all `DeNelle.Village`):

| Class | Why not |
|---|---|
| `SmartEnemySpawner` (`Waves/SmartEnemySpawner.cs:42`) | Positions a wave WaveManager composed. Subordinate, not an authority. |
| `TutorialWaveSpawner` (`Tutorial/TutorialWaveSpawner.cs:34`) | FTUE-only; already routes through `WaveManager.SpawnEnemyForExternalMode`. |
| `CampDefenseWave` (`World/Camps/CampDefenseWave.cs`) | The *only* existing "they counterattack your structure" system — but it attacks an **outpost on a claimed camp**, not the town, and is one-shot/persisted. Read it for prior art on `OnDefended`/`OnLost`; do not extend it. |
| `TribeManager` (`World/TribeManager.cs:316`) | Razes overworld **Settlements**, not the town. |
| `RegionMobSpawner` | Header states its roamers "never march the Heart". |
| `RaidGarrisonSpawner`, `Village2RaidController` | Player-attacks-THEM. WO-774's lane. Out of scope. |

> **DUPLICATE-AUTHORITY GUARD:** the siege must not contain a single `Instantiate` of an `Enemy`.
> §5 adds a source-scanning oracle that fails the gate if one appears.

### 1.2 The damage aggregate — `WaveDamageReport` (REUSE VERBATIM)
`Assets/_Modules/Village/Waves/WaveDamageReport.cs:46` — `public static class WaveDamageReport`.

- `Collect()` `:79` — Guard-wrapped scene scan, returns worst-first `List<Entry>`, capped at
  `MaxRows = 8` `:49`, truncation FlowTrace'd `:150-152`.
- `Entry` `:52-72`: `Name`, `DamageFraction`, `Destroyed`, `LootStolen`, `RepairCost`
  (`DeNelle.Core.Catalog.ResourceCost`), `HasCost`, `IsCollector`.
- Coverage: `WallSegment` / `Gate` / `Building` via `RepairTarget.TryWrap` `:185-202`; `ResourceCollector`
  `:95-112`; `Tower` / `DefenseTower` / `ArcaneTower` / `HarvestSite` `:117-139`. Enemy-owned garrison
  turrets excluded `:126-127`.
- **Consumed only by** `EndStateVM.FromWaveClear` (`Village/UI/EndState/EndStateVM.cs:451`, damage collect
  at `:470-472`).

**It is live-only and transient.** There is no serialisation path. That — not the aggregation — is the gap.

### 1.3 The end-state frame — `EndStateVM` / `EndStateView`
`Village/UI/EndState/`. Factories: `FromBattleVictory:162`, `FromBattleDefeat:279`, `FromHeroDeath:300`,
`FromGameOver:327`, `FromRaidVictory:350`, `FromOutpostVictory:400`, `FromWaveClear:451`.
`EndStateView.Show(...)` `EndStateView.cs:69` — **a new Show DESTROYS any open end-state** (`:90`, `:98`).

> ⚠ **Do NOT add a `FromSiege` end-state.** `WaveCelebrationManager.cs:179-180` already calls
> `FromWaveClear` on `OnWaveCleared`. A second `Show` in the same frame would destroy the first and the
> race would be invisible. The siege report is a **separate, re-openable panel** (§3.4), and the existing
> wave-clear banner stays exactly as it is.

### 1.4 The raid pillar's outcome object (the shape to mirror, not extend)
`Village/Troops/RaidResult.cs:21` — `Stars`, `DestructionPct`, `ElapsedSeconds`, `ClockSeconds`, `Cleared`.
Settled once at `RaidScoring.Finalize(bool cleared)` (`Troops/RaidScoring.cs:565`), consumed by
`RaidVictoryController.cs:545-561` → `EndStateVM.FromRaidVictory` → `EndStateView.Show`.

`RaidResult` is the **attack** artifact (player attacks them). `DefenseReportRecord` is the **defence**
artifact (them attacking player). They are deliberately different types: `RaidResult` is transient and has
no persistence, no attacker identity and no breach geometry. Do not overload it.

### 1.5 The offline clock — `OfflineClaimCoordinator` (the ONE authority)
`Assets/_Modules/Village/Harvest/OfflineClaimCoordinator.cs`.

- `public static class OfflineClaimCoordinator` `:122` — "the ONE authority over
  `GameState.LastHarvestClaimMs`: one read, one elapsed window, explicit fan-out, one advance + save"
  `:118-120`.
- `readonly struct OfflineClaimWindow` `:64` — `Sequence`, `Reason`, `NowUnixMs`, `WindowStartUnixMs`,
  `ElapsedSeconds`, `WasFreshClock`; helpers `CappedSeconds(capHours)` `:91`, `ExceedsCap` `:99`.
- `interface IOfflineClaimConsumer` `:107` — `OfflineConsumerName` + `ApplyOfflineWindow(window)`;
  **"MUST NOT touch `GameState.LastHarvestClaimMs` — the coordinator owns it"** `:112-113`.
- `Register` `:144` / `Unregister` `:155` / `Claim(reason)` `:192`, clock write `StampClock` `:276-299`.
- Existing consumers: `OfflineHarvestService` (`Harvest/OfflineHarvestService.cs:62`, cap
  `OfflineCapHours = 10f` `:73`), `EchoService`, `EchoRepairService`.
- The WO-1147 header (`OfflineHarvestService.cs:25-34`) records exactly why a second clock is forbidden:
  three systems each reading/writing the clock produced a frame-order coin-flip and offline Echo repair
  **never accrued once**.

> **BINDING for this plan:** the siege scheduler registers as an `IOfflineClaimConsumer`. It **never**
> reads or writes `LastHarvestClaimMs`. §5's oracle asserts this.

### 1.6 The save seam
`Assets/_Modules/Core/State/SaveSchema.cs` — `public const int CurrentVersion = 38;` at **`:41`**
(read at source this session; the file's own header `:11-12` warns that a restated version went two
versions stale — do not restate it anywhere).
- Migration table `SaveMigrator.cs:76-79` (`{35,MigrateToV35} … {38,MigrateToV38}`); implementations
  `:526`, `:627`, `:678`, `:685`.
- The house convention, verbatim from the v36/v37 changelog: **additive, nullable-on-the-wire,
  default-on-read, appended at the END of `PersistedState`** so older saves stay loadable; the migrator
  step may be a documented no-op whose only job is keeping the version triple aligned (SaveMigrator top
  step == `CurrentVersion`).
- Precedent field to copy: `[JsonProperty("everBuiltStructureIds")] public List<string> …`
  `SaveSchema.cs:642` (last field in `PersistedState`, `:643` closes it).
- Existing persisted damage: `GameState.BuildingDamage` (`Core/State/GameState.cs:102`), wire key
  `"buildingDamage"` `SaveSchema.cs:270`. **Not** a report — a per-building damage scalar. Do not
  repurpose it.

### 1.7 The panel seam
`Assets/_Modules/Core/UI/PanelRouter.cs:37` — `enum PanelId`, currently `HeroTalents=0 … DevPanel=17`
(`:110`), explicitly **append-only** ("values are load-bearing", `:110`). Values 4 and 0 are retired
holes; do not reuse them. `PanelRouter.PanelOpened` event `:219`.
Scene-independent panel precedent: `Village/UI/Manage/ManageScreenPanel.cs` + `ManageScreenBootstrap.cs`
(`PanelId.Manage = 16`, `:100`).

### 1.8 Feature-flag seam
`Assets/_Modules/Core/FeatureFlags.cs` — pattern `public static bool MapTab => Get("maptab", defaultOn:false);`
at `:676`, with a doc-comment that states *why* the default is what it is and how to flip it via
PlayerPrefs `ff.<name>`.

---

## 2. THE DATA MODEL — the heart of the ticket

New file: **`Assets/_Modules/Core/Defense/DefenseReport.cs`** — assembly **`DeNelle.Core`**,
namespace `DeNelle.Core.Defense`.

**Why Core and not Village:** the record must be persisted by `DeNelle.Core.State.SaveSchema`, and Core
references nothing but UniTask/TMP/Addressables (`Core/DeNelle.Core.asmdef`). A record type in
`DeNelle.Village` could never be a field on `PersistedState`. It also means the HUD assembly
(`HUD/DeNelle.HUD.asmdef` → Core + Data only) could render it later without breaking §5's one enforced
invariant. **Pure data. No `UnityEngine` types in the persisted fields** (no `Vector3` — see the
`worldX/worldY/worldZ` floats below; Newtonsoft round-trips `Vector3` badly and the wire must stay
inspectable JSON).

```csharp
namespace DeNelle.Core.Defense
{
    /// The provenance of the attacking force. THE (c) SEAM — see §7 of the plan.
    public enum AttackerSource
    {
        GeneratedPve  = 0,   // model (a): WaveCompositionBuilder rolled this roster
        GhostSnapshot = 1,   // model (c): a real player's snapshotted layout/army, replayed by AI
        LivePvp       = 2,   // model (b): reserved. Nothing produces this. Never delete the value.
    }

    public enum DefenseOutcome { Held = 0, Breached = 1, Overrun = 2 }
    public enum DefenseResolution { Live = 0, ResolvedInAbsentia = 1 }

    public sealed class DefenseReportRecord { /* fields below */ }
    public sealed class AttackerIdentity   { /* … */ }
    public sealed class AttackerUnitRecord { /* … */ }
    public sealed class DefenderSnapshot   { /* … */ }
    public sealed class BreachRecord       { /* … */ }
    public sealed class StructureLossRecord{ /* … */ }
    public sealed class StakesLedger       { /* … */ }
}
```

### 2.1 `DefenseReportRecord`

| Field | Type / wire key | Why it exists — and why (c) needs it |
|---|---|---|
| `RecordVersion` | `int` `"v"` | **Record-level** version, independent of `SaveSchema.CurrentVersion`. Lets the report shape evolve (a (c) upgrade adds attacker fields) without a whole-save bump every time. Start at `1`. |
| `ReportId` | `string` `"id"` | GUID. The stable key the inbox/panel selects by, and the key a future server would dedupe on. |
| `StartedAtUnixMs` / `EndedAtUnixMs` | `double` `"t0"`/`"t1"` | Ordering + "3 hours ago" copy. Unix-ms doubles match the house clock (`GameState.LastHarvestClaimMs`, `TimeSource.NowUnixMs()`). |
| `Resolution` | `DefenseResolution` `"res"` | `Live` (player watched it) vs `ResolvedInAbsentia` (simulated). Under (a)+interim only `Live` is produced — but the field exists so WO-430-F's fast-forward and (c)'s replay do not need a schema change. |
| `Outcome` | `DefenseOutcome` `"out"` | Held / Breached / Overrun. The one-line verdict the inbox row shows. |
| `WaveId` | `int` `"wave"` | Which wave ordinal produced it (the difficulty context). Under (c) this becomes the ghost's rating band; the field is neutral. |
| `DurationSeconds` | `float` `"dur"` | Compare-across-attempts axis. |
| `Attacker` | `AttackerIdentity` `"atk"` | See 2.2. |
| `Defender` | `DefenderSnapshot` `"def"` | See 2.3. **This is what makes AC-4 checkable.** |
| `Breaches` | `List<BreachRecord>` `"brk"` | See 2.4. **This is the redesign signal — the "move that tower" moment.** |
| `Losses` | `List<StructureLossRecord>` `"loss"` | See 2.5. 1:1 adapt of `WaveDamageReport.Entry`. |
| `Stakes` | `StakesLedger` `"stk"` | See 2.6. **ALL ZERO under the interim. THE SEAM.** |
| `Read` | `bool` `"read"` | Unread badge on the town door. Player-state, not outcome — hence a mutable field on the record rather than a parallel list. |

### 2.2 `AttackerIdentity` — **the (c) source swap lives here**

| Field | Type | Why |
|---|---|---|
| `Source` | `AttackerSource` | The swap. Under (a) it is written `GeneratedPve` **by the producer**, never assumed by any reader. Every consumer branches on this field or on nothing. |
| `AttackerId` | `string` | `"pve.warband.t3"` today; a player id / snapshot owner id under (c). Opaque to every reader. |
| `DisplayName` | `string` | `"Hollow Warband"` today; a player's town name under (c). The panel renders **this string** — it never composes a name from the source enum. |
| `PowerRating` | `int` | Roster strength. Under (a) derived from the composed roster; under (c) read off the snapshot. Gives the player "I lost to something 40% stronger". |
| `SnapshotId` | `string` | **Empty under (a).** Under (c) this is the key of the stored base/army snapshot the replay was driven from. Present from day one so a replay button has somewhere to point. |
| `Units` | `List<AttackerUnitRecord>` | `{ DefId, Count, Level }`. Recorded from the **composed roster** (not a scene census — WO-1113 drips reinforcements, §1.1). Under (c) this is the ghost's army list, same shape. |

> **The hardcoding ban, made concrete:** nothing outside the producer may read `Source` to decide *what
> to show*. The panel shows `DisplayName`, `PowerRating`, `Units`. It may show a small source *chip*
> ("Raiders" / "Ghost of <name>") — that is the ONLY sanctioned `Source` read in presentation, and it is
> a label lookup, not a branch in the layout.

### 2.3 `DefenderSnapshot` — what the player's base looked like at attack time

| Field | Type | Why |
|---|---|---|
| `LayoutHash` | `string` | Stable hash over the `BaseLayout` records (itemId + cell + yaw + level, sorted). **This is how AC-4 is proven headlessly**: move a structure → the next report's hash differs → "a redesign has visible effect" is a data assertion, not a vibe. Also the (c) precondition — a snapshot IS a layout. |
| `StructureCount` / `WallCount` / `TowerCount` | `int` | Cheap "your base at the time" context so an old report is legible after the player has rebuilt. |
| `HeroPresent` | `bool` | Under (a) usually true (live defence). Under `ResolvedInAbsentia` false. Explains outcome swings. |
| `Garrison` | `List<AttackerUnitRecord>` | **Empty today.** The WO-430-F (offline troop garrison) seam — same unit-record shape as the attacker so one renderer serves both sides. |

### 2.4 `BreachRecord` — **"where they broke through"**

| Field | Type | Why |
|---|---|---|
| `BreachedId` | `string` | Instance/structure id of the gate or wall segment crossed. |
| `DisplayName` | `string` | "North Gate" — so an old report reads after that gate is gone. |
| `WorldX` / `WorldY` / `WorldZ` | `float` | Plain floats, not `Vector3` (Core stays engine-type-free on the wire, and the JSON stays inspectable). Feeds a minimap pin in the panel — *the* redesign trigger. |
| `AtSeconds` | `float` | When in the assault. Ordering makes "they came in the north first" legible. |
| `AttackerDefId` | `string` | Which unit type got through — "the flyers ignored my walls". |

`WaveManager` already detects the inner-wall-ring breach (file header, `Assets/_Modules/Village/Waves/WaveManager.cs:14-18`) and hands the breaching roster to the ATB scene. **The breach detector already exists — hook it, do not write a second one.** Locate the existing breach call site before writing (grep `Breached` / `BreachedIds` / `WavePhase.Breached` in `WaveManager.cs`) and emit one `BreachRecord` there.

### 2.5 `StructureLossRecord` — 1:1 adapter of `WaveDamageReport.Entry`

| Field | Type | Source |
|---|---|---|
| `DisplayName` | `string` | `Entry.Name` |
| `DamageFraction` | `float` | `Entry.DamageFraction` |
| `Destroyed` | `bool` | `Entry.Destroyed` |
| `IsCollector` | `bool` | `Entry.IsCollector` |
| `LootStolen` | `int` | `Entry.LootStolen` — **already an existing mechanic** (`ResourceCollector.LastLootStolen`, `WaveDamageReport.cs:107`). It is NOT a new stake; it is today's behaviour being recorded. Do not confuse it with §2.6. |
| `RepairWood/Iron/Food/Crystals` | `int` | Flattened `Entry.RepairCost` — `DeNelle.Core.Catalog.ResourceCost` is a Core type so it *could* be embedded, but flattening keeps the wire stable if that struct changes. |
| `HasCost` | `bool` | `Entry.HasCost` — cost omitted, never faked (the existing contract, `WaveDamageReport.cs:68`). |

`Entry` carries no world position. **Add nothing to `WaveDamageReport`** in this WO beyond, if trivially
available at the call site, capturing the structure's transform position into the adapter — the adapter
lives in Village and has the `Component` in hand at `AddStructure`/`AddRepairables`. If that turns out to
require reshaping `Entry`, **defer it**: breach pins (§2.4) already give the player the failure point;
loss pins are polish.

### 2.6 `StakesLedger` — **all zero, and self-describing about why**

| Field | Type | Why |
|---|---|---|
| `Wood`/`Iron`/`Food`/`Crystals`/`Magic` | `int` | What the attack TOOK. **Every one is 0 under the interim.** Basket-shaped per WO-947 so a later ruling has somewhere to land without a schema change. |
| `StakesRuleId` | `string` | `"none.interim.wo1026"` today. A future ruling stamps its own id (`"stakes.stockpile.wo1XXX"`). **This makes an old report self-describing**: a report written under the interim can never be mis-read as "the player lost nothing that day" by a build that has stakes. |

---

## 3. The systems

### 3.1 `SiegeScheduler` — cadence, offline, scene handling
**New:** `Assets/_Modules/Village/Waves/SiegeScheduler.cs` — assembly **`DeNelle.Village`**.
`MonoBehaviour`, singleton-lite (`Instance`), lives on the hub. Implements
`DeNelle.Village.IOfflineClaimConsumer`.

**Cadence source of truth:** a new persisted `GameState.LastSiegeUnixMs` (§4) + an in-memory
`SiegePressure` counter. Interval is **config, not save state** (mirrors `BuildTimerConfig.queueDepthPerLine`,
CLAUDE.md §8): a serialized `[SerializeField] float _siegeIntervalHours = 6f;` with a `[Min]`, plus
`_maxPendingSieges = 1`.

**Online path (the only path that spawns):**
1. Every N seconds (a cheap `InvokeRepeating`, not `Update`) the scheduler asks: is `FeatureFlags.Siege`
   on? is `WaveManager.Instance` non-null and in `WavePhase.Idle` or `Countdown`? is onboarding finished
   (the `!Onboarded` gate — memory `enemies-never-spawn-tutorial-onboarded-gate`; reuse the exact
   predicate `WaveManager.BeginLoop` already checks at `WaveManager.cs:912`, do not write a second one)?
   is the active scene the hub (`Main_Castle_Overworld`)? is build mode closed?
2. If yes **and** pressure > 0 (or `now - LastSiegeUnixMs >= interval`), it opens a `SiegeSession`
   (§3.2), stamps `LastSiegeUnixMs = now`, decrements pressure, and calls
   **`WaveManager.Instance.ForceBeginNextWave()`** (`WaveManager.cs:1437`). *That is the entire spawn
   integration.* No composition change, no roster change — WO-1026 §5 lane isolation is respected
   literally.
3. If any gate fails, it **defers** — `FlowTrace.Step("Siege", "deferred: <reason>")`. Never a silent skip.

**Offline path — DELIBERATELY NOT A SIMULATION (design decision, flag to the owner):**
`ApplyOfflineWindow(window)` does **not** resolve battles. It converts the away window into
**pressure**: `pending = clamp(floor(window.CappedSeconds(_offlineCapHours) / intervalSeconds), 0, _maxPendingSieges)`.
On return the siege then happens **live**, at the gate, with the player watching.

*Why:* resolving a siege in absentia under the interim would write a report whose `Losses` and `Stakes`
are both empty — a record that says nothing happened. That is worse than no record: it teaches the player
the system is noise. Making the away time *produce the attack you come home to* is honest, needs no
combat sim, and keeps `WaveManager` the single spawn authority. `DefenseResolution.ResolvedInAbsentia`
and `DefenderSnapshot.Garrison` exist in the model so WO-430-F's fast-forward sim drops in at exactly
this method with no data change.
**Owner-facing:** this is the one design choice in this plan the ruling did not cover. If the owner wants
"you were raided while away, here is the report" instead, that is WO-430-F and it needs the stakes ruling
first — an absentee raid with no consequence is a strictly empty record.

**Other scenes:** the scheduler exists only in the hub and gates on the active scene, so a raid/dungeon/
battle scene simply never fires one. Pressure survives (it is derived from `LastSiegeUnixMs`), so a long
raid session comes home to a siege. `WaveManager.Instance`'s "active-scene wins" rule (`:632-644`) means a
stale instance can never be the target.

### 3.2 `SiegeSession` — the live recorder
**New:** `Assets/_Modules/Village/Waves/SiegeSession.cs` — assembly **`DeNelle.Village`**.
Plain class (not a MonoBehaviour), owned by `SiegeScheduler`.

- `Open(int waveId, AttackerIdentity attacker, DefenderSnapshot defender)` — stamps `StartedAtUnixMs`,
  captures the defender snapshot + layout hash **before the first spawn**.
- `RecordBreach(...)` — called from the existing `WaveManager` breach detection.
- `Close(DefenseOutcome outcome)` — subscribed to `WaveManager.OnWaveCleared` (`:312`) → `Held`, and to
  `WaveManager.OnDefeat` (`:323`) → `Overrun`; `Breached` when at least one `BreachRecord` exists but the
  wave was cleared. **Close calls `WaveDamageReport.Collect()` exactly once** and adapts each `Entry` to a
  `StructureLossRecord`, then hands the finished `DefenseReportRecord` to the ledger.
- **Ordering:** subscribe to `OnWaveCleared` and let `WaveCelebrationManager` keep its existing
  subscription. `WaveManager.cs:328-341` guarantees the payout record is complete before any listener
  runs, so order between the two listeners does not matter for correctness. Do not reorder them.

### 3.3 `DefenseReportLedger` — persistence, ring-buffered
**New:** `Assets/_Modules/Core/Defense/DefenseReportLedger.cs` — assembly **`DeNelle.Core`**,
`namespace DeNelle.Core.Defense`. Static.

- `Append(DefenseReportRecord)` → writes into `GameState.DefenseReports`, trims oldest beyond
  `MaxRetained = 10`, marks the state dirty and saves through the existing `GameStateService` save path
  (do **not** add a second save trigger).
- `All()`, `TryGet(reportId)`, `UnreadCount()`, `MarkRead(reportId)`.
- Trim is **FlowTrace'd**, never silent (`WaveDamageReport.cs:148-152` is the house precedent).

### 3.4 `DefenseReportPanel` — the surfaced report
**New:** `Assets/_Modules/Village/UI/Defense/DefenseReportPanel.cs` + `DefenseReportPanelBootstrap.cs` —
assembly **`DeNelle.Village`**. Code-built UI (UXML does not work in builds — CLAUDE.md §8). Copy the
structure of `Village/UI/Manage/ManageScreenPanel.cs` + `ManageScreenBootstrap.cs` verbatim, including the
scene-independent bootstrap registration.

- Registers `PanelId.DefenseReport = 18` (§4).
- **List → detail.** List rows: outcome verdict, attacker `DisplayName`, relative time, unread dot.
  Detail: attacker unit list, a **breach map** (the simplest thing that reads: a top-down plate with a pin
  per `BreachRecord`, labelled by `DisplayName`), the loss list with repair costs, and the stakes line —
  which under the interim reads **"Nothing was taken."** (an explicit statement, not a blank).
- **Colourblind law:** every state carried by TEXT ("DESTROYED", "damaged 40%", "HELD"), never colour
  alone. `EndStateVM.cs:449-450` is the precedent to match.
- **Door:** an entry the player can reach from town. Recommended: the existing Builders-chip-style right
  column is full; the cheapest honest door is a **badge on the Heart interaction** / a Manage-screen tab.
  ⚠ **Do NOT add an 8th action-bar face** — CLAUDE.md §7 spends paragraphs on why the bar is capped at
  six visible (`HudActionBarModel.MaxVisibleFaces`). **Bring the door choice to the owner** rather than
  minting one; ship the panel registered and openable via `PanelRouter.Open(PanelId.DefenseReport)` and
  the DevPanel while the door is decided.

---

## 4. Every file, with its assembly

Read each `.asmdef` before editing — CLAUDE.md §5's table is a subset, and `DeNelle.Village.asmdef`
legitimately references `BattleATB`/`AI`/`Cosmetics`/`Data`/`Pets`/`Wallet`/`Audio`.

### CREATE

| File | Assembly | What |
|---|---|---|
| `Assets/_Modules/Core/Defense/DefenseReport.cs` | `DeNelle.Core` | All §2 types. Pure data, no `UnityEngine` types on persisted fields. |
| `Assets/_Modules/Core/Defense/DefenseReportLedger.cs` | `DeNelle.Core` | §3.3. |
| `Assets/_Modules/Village/Waves/SiegeScheduler.cs` | `DeNelle.Village` | §3.1. Implements `IOfflineClaimConsumer`. |
| `Assets/_Modules/Village/Waves/SiegeSession.cs` | `DeNelle.Village` | §3.2. |
| `Assets/_Modules/Village/Waves/DefenseReportBuilder.cs` | `DeNelle.Village` | The adapter: `WaveDamageReport.Entry[]` → `StructureLossRecord[]`; composed roster → `AttackerUnitRecord[]`; `BaseLayout` → `DefenderSnapshot` + `LayoutHash`. **This is the only file that writes `AttackerSource.GeneratedPve`.** |
| `Assets/_Modules/Village/UI/Defense/DefenseReportPanel.cs` | `DeNelle.Village` | §3.4. |
| `Assets/_Modules/Village/UI/Defense/DefenseReportPanelBootstrap.cs` | `DeNelle.Village` | Scene-independent registration. |
| `Assets/Editor/Regression/DefenseReportContractRegression.cs` | `DeNelle.Editor` | §5 oracle 1. |
| `Assets/Editor/Regression/SiegeCadenceRegression.cs` | `DeNelle.Editor` | §5 oracle 2. |
| `Assets/Editor/Regression/SiegeSpawnAuthorityRegression.cs` | `DeNelle.Editor` | §5 oracle 3. |

### MODIFY (minimal, surgical)

| File | Assembly | Change |
|---|---|---|
| `Assets/_Modules/Core/State/SaveSchema.cs` | `DeNelle.Core` | `CurrentVersion` **38 → 39** at `:41` with a changelog entry in the house style. Append to `PersistedState` **after** `EverBuiltStructureIds` (`:642`): `[JsonProperty("defenseReports")] public List<DefenseReportRecord> DefenseReports;` and `[JsonProperty("lastSiegeUnixMs")] public double LastSiegeUnixMs;`. Nullable/default-on-read. Add a finite-guard for `lastSiegeUnixMs` next to the `lastHarvestClaimMs` guard (`:789-790` region). |
| `Assets/_Modules/Core/State/SaveMigrator.cs` | `DeNelle.Core` | Add `{ 39, MigrateToV39 }` to the table at `:76-79`; implement `MigrateToV39` as a **documented no-op** that seeds an empty list + 0 clock (the `MigrateToV37` precedent, `:685`), keeping the version triple aligned. |
| `Assets/_Modules/Core/State/GameState.cs` | `DeNelle.Core` | Add `DefenseReports` (List) + `LastSiegeUnixMs` (double) fields with initializers (mirror `LastHarvestClaimMs`, `:171`). |
| `Assets/_Modules/Core/State/GameStateService.cs` | `DeNelle.Core` | Copy the two new fields in the three existing copy sites (the `:482 / :573 / :1007` pattern used for `BuildingDamage` and `:492 / :583 / :1012` for `LastHarvestClaimMs`); New Game reseeds report list empty + clock 0. |
| `Assets/_Modules/Core/UI/PanelRouter.cs` | `DeNelle.Core` | Append `DefenseReport = 18` after `DevPanel = 17` (`:110`). **Append-only — never renumber.** |
| `Assets/_Modules/Core/FeatureFlags.cs` | `DeNelle.Core` | Add `public static bool Siege => Get("siege", defaultOn: false);` with a doc-comment stating: default OFF because the loss stakes are unruled; flip via PlayerPrefs `ff.siege`. |
| `Assets/_Modules/Village/Waves/WaveManager.cs` | `DeNelle.Village` | **Two lines only.** At the existing inner-ring breach detection, emit `SiegeSession.Current?.RecordBreach(...)` (null-safe, no-op outside a siege). Optionally expose the composed roster as a read-only accessor if one is not already public. **No composition change, no scheduling logic, no report logic in this file** (WO §5). |
| `Assets/Editor/Regression/DataRegression.cs` | `DeNelle.Editor` | Three registration lines, copying `:316` exactly: `if (!DefenseReportContractRegression.Run(out var defRepReason)) failures.Add(defRepReason); else log.AppendLine("[defense-report] " + defRepReason);` and the same for `[siege-cadence]`, `[siege-spawn-authority]`. Seat them next to the `[offline-harvest]`/`[offline-fanout]` pair (`:316-317`). |

### DO NOT TOUCH
`WaveCompositionBuilder`, `SmartEnemySpawner`, `waves.json`, `RaidScoring`, `RaidResult`,
`RaidDeployController`, `RaidVictoryController`, `EndStateVM`, `EndStateView`,
`WaveCelebrationManager`, `CampDefenseWave`, `OfflineClaimCoordinator`, `OfflineHarvestService`.
Every `.unity` scene (CLAUDE.md §3 — the panel bootstrap is scene-independent by design).

---

## 5. Instrumentation (§12) — permanent, never stripped

**FlowTrace system tag: `"Siege"`.** One tag, so a single log filter shows the whole subsystem.

| Site | Call |
|---|---|
| `SiegeScheduler` armed / registered as offline consumer | `FlowTrace.Step("Siege", $"scheduler armed — interval={h}h flag={FeatureFlags.Siege}")` |
| Every cadence evaluation that DEFERS | `FlowTrace.Step("Siege", $"deferred: {reason} (phase={phase} scene={scene} onboarded={b})")` — **the single most valuable line in this system.** "The base is never attacked" is exactly the class of bug this WO exists to fix; a deferral that logs nothing recreates it. |
| Siege armed → `ForceBeginNextWave` | `FlowTrace.Step("Siege", $"ARMED wave={id} attacker={name} source={src} power={p}")` |
| Offline window → pressure | `FlowTrace.Step("Siege", $"offline window {sec}s -> pressure {n} (capped={b})")` |
| Each breach | `FlowTrace.Step("Siege", $"BREACH {name} @({x:F0},{z:F0}) t={t:F1}s by {defId}")` |
| Session close | `FlowTrace.Step("Siege", $"CLOSED outcome={o} breaches={n} losses={m} dur={d:F1}s")` |
| Ledger append + trim | `FlowTrace.Step("Siege", $"report {id} appended ({count}/{MaxRetained}); trimmed {k}")` |
| Panel open + which record | `FlowTrace.Step("Siege", $"report panel opened id={id} unread={n}")` |
| Save round-trip on load | `FlowTrace.Step("Siege", $"loaded {n} defense reports (schema v{SaveSchema.CurrentVersion})")` |
| Anomalies | `FlowTrace.Warn("Siege", …)` — no `WaveManager.Instance`; a session closed with no matching open; a record whose `Stakes` are non-zero while `StakesRuleId == "none.interim.wo1026"` (a contradiction that must scream). |
| Hard stops | `FlowTrace.Fail("Siege", …)` — ledger append threw; the record failed to serialise. |

**`Guard.Try` wrappers (mandatory, no silent catches):** the whole `SiegeSession.Close` body; the
`WaveDamageReport.Collect()` call (already Guard'd internally — wrap the *adapt* loop with
`Guard.TryEach` so one malformed entry skips rather than losing the whole report); the layout-hash
computation; every panel row build; `DefenseReportLedger.Append`.

**Never strip these.** CLAUDE.md §12: instrumentation is permanent; flag it off (`FlowTrace.Enabled`),
never delete it.

---

## 6. Regression oracles

Registration pattern is **exactly** `DataRegression.cs:316`. Each oracle is
`public static bool Run(out string reason)` in `namespace DeNelle.Editor`, mirroring
`Assets/Editor/Regression/OfflineHarvestRegression.cs`. Copy that file's **headless state install**
(`TryInstallHeadlessState` / `TrySetInstanceStatic`, reflection over `GameStateService._state` +
`_instance`, restored in a `finally`, PlayerPrefs `"dotr-save"` snapshot/restore) — editmode batchmode
never runs `Awake`, and a bare `AddComponent<GameStateService>()` leaves `Instance` null. Where a seam is
missing, **NAMED SKIP (`return true`)**, never a false FAIL — that file's own rule.

### Oracle 1 — `DefenseReportContractRegression` (`[defense-report]`)
1. A fully-populated `DefenseReportRecord` (breaches, losses, units) serialises and deserialises through
   `SaveSchema.JsonSettings` with **field-for-field equality**.
2. **The (c) proof:** the same record with `Attacker.Source = AttackerSource.GhostSnapshot` and a non-empty
   `SnapshotId` round-trips **identically** — asserting that model (c) needs no schema change.
3. `AttackerSource.LivePvp` also round-trips (reserved value is not dropped by the enum converter).
4. Ledger ring buffer: append `MaxRetained + 3`, assert count == `MaxRetained` and the **oldest** were
   dropped (newest retained).
5. `MarkRead` persists; `UnreadCount` matches.
6. **The stakes guard:** every record the *production* builder can emit has `Stakes` all-zero and
   `StakesRuleId == "none.interim.wo1026"`. This oracle **fails the gate** the day someone adds an
   economy rule without a ruling — which is precisely the failure mode the WO forbids.
7. Save version triple: `SaveSchema.CurrentVersion == 39` and `SaveMigrator`'s top step == 39.
8. **Layout sensitivity (AC-4):** two `DefenderSnapshot`s built from `BaseLayout` collections that differ
   by one moved structure produce **different** `LayoutHash`; identical collections in a different order
   produce the **same** hash (order-independence — the hash must sort).

### Oracle 2 — `SiegeCadenceRegression` (`[siege-cadence]`)
Drives `SiegeScheduler` against a controlled clock (`GameState.LastSiegeUnixMs`), with no scene:
1. Fresh clock (`<= 0`) → seeds to now, produces **0** pressure (no giant retroactive first siege).
2. `3 x interval` elapsed with `_maxPendingSieges = 1` → pressure clamps to 1.
3. Backwards clock (last set to the future) → pressure 0, no throw, clock re-stamped (monotonic guard —
   the `OfflineHarvestRegression` case 3 precedent).
4. `ApplyOfflineWindow` **never** mutates `GameState.LastHarvestClaimMs` (snapshot before, assert after).
   This is the WO-1147 invariant (`OfflineClaimCoordinator.cs:112-113`).
5. With `FeatureFlags.Siege` OFF the scheduler arms nothing and calls no `WaveManager` entry point.

### Oracle 3 — `SiegeSpawnAuthorityRegression` (`[siege-spawn-authority]`) — the duplicate-authority guard
A **source-scanning** oracle (precedent: the `.cs`-scanning suites already in `Assets/Editor/Regression/`,
e.g. `HubSceneLiteralRegression`, `BannedVfxRegression`):
1. Under `Assets/_Modules/Village/Waves/Siege*.cs` and `Assets/_Modules/Core/Defense/*.cs`, **no**
   `Instantiate(` and **no** `SpawnEnemyForExternalMode` — the siege never spawns; it only asks
   `WaveManager`.
2. `SiegeScheduler.cs` contains **no** `LastHarvestClaimMs`.
3. `DefenseReportPanel.cs` contains **no** `WaveDamageReport` reference — the panel reads the persisted
   record, never re-scans the scene (this is what keeps (c) a source swap; a panel that re-scans the live
   town can never render a ghost's report).
4. Exactly **one** file writes `AttackerSource.GeneratedPve` (`DefenseReportBuilder.cs`) — the
   "do not hardcode the attacker is generated" ruling, enforced.

---

## 7. Acceptance criteria — headlessly checkable

| # | Criterion | How it is checked headlessly |
|---|---|---|
| A1 | `COMPILE_GATE_OK` | `DeNelle.Editor.CompileGate.Run` marker present, log fresh (memory `gates-report-success-without-proving-it` — verify the MARKER and log mtime, never the exit code) |
| A2 | `REGRESSION_OK <n>/<n> suites` from `DataRegression.RunAll`, with `[defense-report]`, `[siege-cadence]`, `[siege-spawn-authority]` all logged | the three registration lines in `DataRegression.cs` |
| A3 | A record survives a session | Oracle 1 §1 (round-trip) + a save/load through the real `GameStateService` |
| A4 | Model (c) is a source swap | Oracle 1 §2/§3 — a `GhostSnapshot` record round-trips with no schema change |
| A5 | Redesign has visible effect | Oracle 1 §8 — moved structure ⇒ different `LayoutHash` on the next report |
| A6 | Nothing is taken | Oracle 1 §6 — `Stakes` all-zero, `StakesRuleId` is the interim id |
| A7 | No duplicate spawn authority | Oracle 3 |
| A8 | No offline-clock second writer | Oracle 2 §4 |
| A9 | Zero change to raid-attack behaviour | `git diff --stat` touches no file under `Village/Troops/` or `Village/World/Camps/` |
| A10 | Zero change to wave composition | `git diff` on `WaveManager.cs` is the two breach lines only; `WaveCompositionBuilder.cs` and `waves.json` untouched |
| A11 | Default-off | with `ff.siege` absent, a headless boot logs `"scheduler armed — … flag=False"` and no `ARMED` line ever |
| A12 | The report is legible | `UI_CAPTURE_OK` + **open the PNGs** of the panel list and detail (memory `screenshots-are-primary-evidence-for-visual-defects`; CLAUDE.md §8). Greyscale-check the breach map — the owner is colourblind (memory `owner-colorblind-delegate-visual-creative`); state must be in TEXT. |
| A13 | Owner felt-verify | *"Does losing feel like it was my fault, and do I know what to change?"* — WO §7.4. PO closes, not CLI (CLAUDE.md §13). |

---

## 8. ★ THE SEAM — where the unruled loss consequence plugs in

**Exactly one method, in exactly one file:**

```
Assets/_Modules/Village/Waves/DefenseReportBuilder.cs
    -> private static StakesLedger BuildStakes(SiegeSession session, List<StructureLossRecord> losses)
```

Today this method is **six lines**: return a `StakesLedger` with all buckets 0 and
`StakesRuleId = "none.interim.wo1026"`, and `FlowTrace.Step("Siege", "stakes: none (interim — unruled)")`.

When the owner rules, the change is:
1. Implement the ruled arithmetic **inside `BuildStakes`** and stamp the new `StakesRuleId`.
2. Apply the debit through the **existing** wallet path (`EconomyService` / `GameStateService`) at the
   `SiegeSession.Close` call site — **one** debit call, guarded, traced. Never a second economy writer.
3. Update Oracle 1 §6 to assert the new rule id and the new arithmetic.

**Nothing else moves.** The record already has the basket (§2.6), the panel already renders a stakes line
(it says "Nothing was taken." today), the save shape already carries it, and the migrator does not bump —
`StakesLedger` is already in the v39 wire. That is the whole point of authoring the zero ledger now
instead of omitting it.

**What must NOT be pre-built while the ruling is open:** any shield/immunity timer, any revenge target,
any trophy/rating number (WO §5 — these are (b)/(c) balancing mechanics and mean nothing under (a)), and
any interaction with storage caps or the WO-947 basket split.

---

## 9. Contradictions with the WO's own text (it predates the ruling)

1. **§1's gap table is right but the §2 framing is wrong.** §2 says "what is missing is the mirror and the
   record". The **mirror already exists**: `WaveDamageReport.Collect()` +
   `EndStateVM.FromWaveClear` already show the player what their base lost, worst-first, priced, every
   wave clear. What is missing is **persistence, an attacker identity, breach geometry, and a re-openable
   surface**. An implementer who reads §2 literally will rebuild the aggregator. **Do not.**
2. **§3's table says (a) "Reuses `WaveManager`. Nothing new server-side."** True on the server axis,
   understated on the client axis: `WaveManager` is **live-only** — it has no scheduled entry point, no
   offline path, and no notion of "who" is attacking. The scheduler + session + builder are genuinely new
   code. (a) is the cheapest option, not a free one.
3. **§5 "Any change to `WaveManager` composition or the smart-roster rules" is out of scope** — this plan
   honours that, but §4.1's "what attacked" requires reading the composed roster. If no read-only accessor
   exists, adding one is an **accessor**, not a composition change. Say so in the RESULT file.
4. **§4.1's "where it entered" has no existing producer.** The breach *detection* exists
   (`WaveManager.cs:14-18`) but records no position and no time. The two-line hook in §4 is unavoidable
   and is the only `WaveManager` edit sanctioned by this plan.
5. **The WO does not mention `WORK_ORDER_430_offline_troop_garrison_defense.md`** ("WO-430-F", Status:
   *SPEC — queued post-V1*), which overlaps heavily: offline garrison defence, a deterministic
   fast-forward of N offline waves, broken-structure repair loop, return summary screen. **These two WOs
   will collide if 430-F is picked up independently.** This plan deliberately leaves the hooks for it
   (`DefenseResolution.ResolvedInAbsentia`, `DefenderSnapshot.Garrison`,
   `SiegeScheduler.ApplyOfflineWindow`) and builds none of its simulation. 430-F also **cannot proceed
   before the stakes ruling** — its whole design is "breaching waves leave structures BROKEN", which is a
   stake. Recommend the board marks 430-F **blocked on the stakes ruling and on WO-1026**.
   *(Also note: WO-430 is a six-way number collision; this file is "WO-430-F" per its own 2026-08-16
   grooming banner.)*
6. **A raid-lane defect found in passing, NOT in this WO's scope:** the raid **retreat / clock-expiry**
   path settles a `RaidResult` (`RaidDeployController.cs:571 Finalize(false)` → `:575 GrantRetreatLoot`)
   and shows **no end-state screen at all** — a raid that times out ends silently. That is WO-774's lane.
   File it separately; do not fix it here.

---

## 10. Risks

1. **`WaveManager` is a 3338-line serialization bottleneck** (CLAUDE.md §9 names
   `VillageSceneBuilder`; `WaveManager` behaves the same way in practice). Two lines is the entire budget.
   Any pressure to put scheduling or reporting logic inside it must be refused — that is how this becomes
   a second wave authority.
2. **`EndStateView.Show` destroys the open end-state** (`EndStateView.cs:90,98`). If anyone "helpfully"
   adds `EndStateVM.FromSiege`, it will silently destroy the wave-clear banner that
   `WaveCelebrationManager.cs:179` shows in the same frame. The siege report is a separate panel. Guard
   this in review.
3. **Save-version races.** `CurrentVersion` is a single const three seats can bump. If another WO lands
   v39 first, this becomes v40 — read `SaveSchema.cs:41` at implementation time, never from this plan.
4. **The `ff.siege` default.** Shipping default-ON before the stakes ruling means the owner felt-tests a
   loop with no consequence and correctly reports it as hollow. Default OFF; the owner flips it
   deliberately.
5. **Onboarding gate.** Memory `enemies-never-spawn-tutorial-onboarded-gate` records a *recurring* bug
   where the real gate is `!Onboarded`, not the dead `pausePressure` flag. The scheduler must reuse
   `WaveManager`'s existing predicate (`:912`), not write a second one, or this bug returns wearing a new
   hat.

---

## 11. Verify (the gate sequence)

1. Brace-balance check on every `.cs` touched (CLAUDE.md §1).
2. `DeNelle.Editor.CompileGate.Run` → `COMPILE_GATE_OK` (+ NUL-byte scan, WO-434).
3. `DataRegression.RunAll` → `REGRESSION_OK <n>/<n> suites`, with the three new tags in the log.
4. `RunCaptureHeadless` → `UI_CAPTURE_OK`; **open the PNGs** of the report list + detail.
5. Headless play: force `ff.siege=1`, force a siege, assert a record is appended, reload, assert it reads
   back with the same `ReportId` and `LayoutHash`.
6. Hand to PO for felt-verify + close (CLAUDE.md §13 — CLI never closes).

---

## 12. What this plan could NOT verify

- **The exact breach call site inside `WaveManager`.** The file header documents the inner-wall-ring
  breach and the `SceneRouter.GoBattle(BattleParams{ Wave, BreachedIds, … })` hand-off
  (`WaveManager.cs:14-18, 28-32`), and `WavePhase.Breached` exists (`:66`), but the implementing agent
  must open the actual detection method and confirm it has the breaching `Enemy` and the crossed
  structure in hand before writing `RecordBreach`. If it only has `BreachedIds` (enemy ids) and no
  structure reference, `BreachRecord.BreachedId`/`DisplayName` may need to come from the nearest
  `Gate`/`WallSegment` at the crossing point — a small resolver, still inside `SiegeSession`.
- **Whether the composed roster is already publicly readable.** `_smartComposition` (`:197`) and
  `WaveCompositionBuilder` produce it; no public accessor was confirmed in the sections read.
- **The `BaseLayout` record shape for the layout hash.** `PlacedStructureData` is documented in the v27
  and v36 changelog entries as carrying `itemId/cell/yaw/level/worldY/wallMounted`; the field names were
  not opened at source this session. Confirm before writing the hash.
- **The right in-town DOOR for the panel.** Deliberately unresolved — see §3.4. Owner decision.
- **Nothing was compiled or run.** An APK build held the Unity lock for the whole of this session, so no
  gate, bake, or headless run was executed. Every claim above is from source reading with citations; none
  is from a test run.
