# STACK UTILIZATION — 2026-08-09 (the REVERSE audit)

**Status: KNOWN DICTIONARY** (durable registry, per memory `audit-outputs-as-known-dictionaries`).
Companion to `AUDIT_2026-08-09.md`. That one asks **"what is broken?"** — this one asks the inverse:

> **"What did we BUILD that never got plugged in?"**
> *(owner, 2026-08-09: "we did implement it, but we never followed it through to a hook.")*

**Method.** Four read-only agents, one per layer (Core / Village / Data / Presentation). Every claim is
grep- or GUID-sweep backed; each agent named the search it ran. Sunday-housekeeping steps 3-4.

**Definitions — the strictness is the point.**
- **CONSUMED** = at least one caller on a path a player can reach in a shipped build.
- **NOT consumed** = referenced only by tests, only by editor tooling, only by its own regression
  suite, only in comments, or behind a default-OFF flag with no player-reachable way to enable it.
- **DECLARED-UNUSED** = deliberately staged ahead, V2-gated, documented control group. **Legitimate.**
- **ORPHAN** = built, forgotten, indistinguishable from working. **This is the problem class.**

---

## 0. ★ THE HEADLINE — the file count is the wrong metric

**Village: ~2.6% of files are orphans (16 of 611). But capability utilization is ~74% — 11 of 42
discrete capabilities are dark.**

> *"The layer's failure mode is not dead files — it's LIVE FILES WITH DARK CAPABILITIES."*

A dead file is obvious and harmless. A live class with four of six capabilities unreachable is
invisible, because the class **is** running — so nobody suspects the rest. Every large finding below is
this shape.

⚠ **AND THE COROLLARY THAT PROTECTS THE CODEBASE: 22 of 38 zero-reference Village files self-install
via `[RuntimeInitializeOnLoadMethod]` and ARE LIVE** — `QuestCastNpcInjector.cs` (546 L),
`CastleWallNavObstacleInstaller.cs` (447 L), `AlertIntelSystem.cs` (232 L), `KillComboTracker.cs` (277 L).
**A naive "no references -> delete" sweep would gut working systems.** Any pruning pass MUST check
`[RuntimeInitializeOnLoadMethod]` and scene/prefab GUIDs before touching a file.

---

## 1. UTILIZATION BY LAYER

| Layer | Measure | Live | Notes |
|---|---|---|---|
| **Village** | files | 595/611 (**97.4%**) | misleading — see capabilities |
| **Village** | **capabilities** | 31/42 (**~74%**) | **the real number** |
| **Core** | contracts (112 examined) | 76 LIVE (**68%**) / 26 DECLARED (23%) / **~10 orphan systems + 6 flags + 1 panel + 9 save fields** | orphans are ~9% of contracts but a disproportionate share of build cost |
| **Core** | `CoreServices` slots | **4 of 7** | `Population`, `Jupiter`, `SceneLinkResolver` all register at runtime and are **never read** |
| **Core** | FeatureFlags (**65**, not 62) | 34 LIVE / 25 DECLARED-OFF / **6 ORPHAN** (**52%** live) | `FeatureFlags.cs` is the best-documented file in the repo — the declared bucket is genuinely healthy |
| **Data** | catalogs | 55 LIVE / 4 PARTIAL / **7 INERT** of ~66 | ~390 KB authored JSON reaching zero code |
| **Presentation** | Hovl VFX keys | 62/140 (**44%**) | 78 keys, zero code reference |
| **Presentation** | VFXType ordinals | 76/95 (**80%**) | 10 zero-ref + 9 reachable only from VFXManager's own fallback |
| **Presentation** | VFXManager capabilities | 11/15 (**73%**) | `PlayCasting`, `PlayDungeon`, static `Play`/`PlayAt` dead |
| **Presentation** | animator controllers | 25/28 (**89%**) | |
| **Presentation** | PanelId | 16/16 (**100%**) | every usable id has a registrar + opener |
| **Presentation** | `ElarionUiKit` builders | 36/47 (**77%**) | |
| **Presentation** | project `.uxml` | 6/12 bound in a build scene (**50%**) | cross-ref AUDIT §3a |

---

## 2. ★ ORPHANS RANKED BY BUILD COST — "where did the investment go"

| # | Orphan | Cost | Why it reaches nothing |
|---|---|---|---|
| 1 | **`widget-params.json`** | **337 KB, ~4,000 authored values** | **SCHEMA MISMATCH, and a FALSE GREEN.** Reader expects `name` + `nodes[]` with flat props; file authors `prefab` + `file` + `objects[]` with nested `rect{}`/`image{}`. `JsonUtility` yields 58 prefabs with `name == null`, every lookup falls through to hardcoded constants — **while logging "widget-params loaded: 58 prefabs"** (`ElarionUiKitObsidian.cs:239`, `:256`) |
| 2 | **78 Hovl VFX keys** | 78 of 140 rows | zero code reference — incl. 55 `PP_*` ParticlePack rows and authored combat keys `DragonFire_Cast/_Impact`, `Thunderbolt_Cast`, `Dash_Blink`, `Aegis_Cast`, `MageMeoteorAOE_Cast` |
| 3 | **The whole rewarded-ad economy** | `ad-placements.json` 3×13 + 5 rewards + 3 covenant blocks; `ad-creatives.json` 6×10 | **only reader is a regression suite** (`AdPlacementCovenantRegression.cs:41`). A covenant enforced on data no runtime loads |
| 4 | **`HeroTalentModifiers` — the Steward economy lane** | 14 of 29 accessors | `CollectorCapBonus`, `RepairCostReduction`, `BuildTimeReduction`, `SalvageBonus`, `WaveRewardBonus`, `TowerDamageBonus`, `TowerRangeBonusMeters` + the Mage spell-unlock set — **zero callers** |
| 5 | **`EnemyTypeVfxSet` + `EnemyVfxSet_Default.asset`** | telegraph, per-type audio, cast/projectile/impact overrides, ranged tint | GUID `e6cfb68d…` -> **0 prefab/scene hits**; `Enemy.cs:339` `_typeVfxSet` never assigned. Sole creator is an editor tool |
| 6 | **`enemy-roles.json`** | 9 roles × 4 + 25 creatures × 7 | only a doc comment (`EnemyTaxonomy.cs:79`) + `DataWebRegression`. The live `EnemyRole` is a **separate hardcoded Village enum** |
| 7 | **`skr_store.json`** | 7 SKUs + 3 acquisition packs | an entire second-currency storefront, no runtime loader |
| 8 | `DragonCinematicFlyby.cs` | 355 L | only attacher is `VillageSceneBuilder.Content.cs:329`; GUID absent from every `.unity` |
| 9 | `audio-mix.json` | 10 crossfades + 5 volume nudges | `AudioService` implements the concept with hardcoded values |
| 10 | `WallTierData.cs` | 215 L | editor-tooling only (3 `Assets/Editor/WallTools/*` callers) |
| 11 | `PetAuraVFX.cs` | 214 L | its own builder calls itself "the only caller"; no prefab carries it |
| 12 | `DefensePatternLibrary.cs` | 193 L | 0 refs, incl. from `ArenaDefenseSetupController` |
| 13 | Tower-empowerment UI entry | 244 L (`TowerEmpowerButton` + `EmpowermentDebugTrigger`) | 0 refs — the whole entry point is dark |
| 14 | `weaponskill-animations.json` | 29 × 10 | editor-only (`KnightPackageControllerBuilder.cs:69`) |
| 15 | `BlinkOrc` / `BlinkOrcBoss` controllers | 2 controllers | GUIDs -> 0 asset refs; `"BlinkOrc"` string -> 0 hits |
| 16 | **`Core/Ads/IAdService.cs`** | ~190 L: interface + `NullAdService` + 3 result enums | zero runtime consumers — **`RewardedAdManager` never routes through it.** Highest ceremony-to-reach ratio in Core. The flag is declared-off; the **seam being unwired is NOT declared** |
| 17 | **`Core/Addressables/**`** | 5 files: group configs, `SkinController`/`ISkinnable`, `HeroAssetLoader`, `HeroTextureLoader`, `AddressablesMemoryProfiler` | **a whole asset-streaming layer with 0 code refs and 0 scene refs.** Corroborates the WO reconciliation: no `Heroes` group exists, 314 `Resources.Load` remain, `GroupAndMigrateHeroes` never run |
| 18 | **`Core/Promo` + `Core/Referral`** | service + a full uGUI screen **each** | built end-to-end **including UI**, never spawned. GUIDs `cc8ab86…`, `f290397…` -> 0 scene/prefab refs |
| 19 | **`CoreServices.SceneLinkResolver` + `SceneLink.cs` + host** | a data-driven world-graph router | self-bootstraps, registers, and is **never asked to route**. `CoreServices.cs:176` even documents the intended `TravelTo(id)` call nobody makes |
| 20 | **`Core/Arena/ArenaContracts.cs`** | 7 DTOs describing the whole arena handoff | unused **even by the arena code that IS on** |
| 21 | `Core/World/WardContent.cs` + `WorldContent.cs` | tuned danger⇄reward tables | a documented "owner wires the round-trip" that never landed |
| 22 | **9 `SaveSchema` fields + 3 setters** | migration + validation + round-trip paid on **every save** | `Contacts`, `Inbox`, `LastInboxSyncAt`, `BlockedCodes`, `AtbLossStreak`, `VoiceOvers`, `MovementStyle`, `BreachStyle`, `Zones` — zero readers outside `Core/State/**`; `SetMovementStyle`/`SetBreachStyle`/`SetVoiceOvers` have **no callers at all** |

---

## 3. ★ HALF-WIRED — more common than orphans, and far more misleading

| System | Live | Dark |
|---|---|---|
| **`HeroTalentModifiers`** | 15 accessors | **14** (§2 #4) |
| **`EnemyRole` branches** | Tank, Healer, MiniBoss | **DPS and Ranged fall through to default.** The ally-heal branch at `EnemyBrain.cs:1385` is **commented out**. Composition emits **5** roles into an AI that distinguishes **3** |
| **Echo lanes** | Harvest | Crafting / Defense / Exploration write-only — **DECLARED**, `EchoLaneBonuses.cs:21-23` says "STUB (unconsumed)" |
| **`VFXManager`** | 11 entry points | `PlayCasting`, `PlayDungeon`, static `Play`, static `PlayAt` |
| **`EliteVFXController`** | statics `SpawnVfxFor`, `PlayDeathShake` | instance path — GUID in **0** prefabs/scenes |
| **`AudioService`** | 28 methods | `SetVolume` for Sfx/Ui/Voice is a **no-op** — `GameAudioMixer.mixer` has `m_ExposedParameters: []` and one `Master` group |
| **`SfxId`** | 16/16 have a call site | **10 fire ONLY via the VFX->SFX pairing inside `VFXManager`** — if the paired VFXType is one of the 19 dead ordinals, the sound never plays |
| **`OrcHumanoid_Mage/_Tank/_Warrior`** | loaded at runtime | **1 named state each vs the base's 42** — effectively empty shells |
| **`motion-castings.json`** | `knight`, `orc`, `orc-tank` | **9 of 12 targets are `inherits`-only stubs** — mage, ranger, cleric and every enemy family play the `humanoid` baseline |
| **`hero-talents.json`** | ~33 of 83 nodes | **~50 nodes have no gameplay effect.** Unhandled effect types: `summon`, `onEvent` (×4), `shieldStrength`, `manaCostReduction`, `wisdomPerLevel`, `stealth` (×2), `critChance` (×2), `attackSpeed` |
| **`realm-map.json`** | `id`, `title`, `gate`, `mapPoint` | `biome`, `propSet`, `waveCount`, `elementBias`, `clearReward`, `adjacency`, `dungeonRegion`, `description` deserialized and never read |
| **`hud-areas.json`** | 21 of 29 widget ids `Register`'d | 8 built via a different path -> **not covered by the posture-occupancy regression** |

---

## 4. ⚠ A GUARD THAT IS GREEN ON A PROMISE THE RUNTIME DOES NOT KEEP

`TalentStrategyRegression.cs:147` asserts the `buildtime` talent maps to "BuildTimerService duration
calc". **`BuildTimeReduction` has no caller in `BuildTimerService.cs`.**

An oracle certifying a wire that does not exist. This is the existence-vs-consumption failure **inside a
guard** — the exact thing the guards are for. Treat any oracle asserting a *mapping* with suspicion
unless it resolves the consumer.

---

## 5. ★ THE INVERSE ORPHAN — reached but UNAUTHORED

The mirror image, and arguably sharper: `EnemyDef.AggroRadius`, `GroupStaggerDelay`, `GlimmerReward`,
`Movement` are **declared and READ at runtime** (`Enemy.cs:596`, `:606`) — but **no row in
`enemies.json` authors any of them.** Every enemy silently runs code defaults.

So flying/air-tower targeting and the Glimmer drop are **live code paths data has never exercised**.
Not "built and unreached" but "reached and unfed".

---

## 5b. ⚠ TWO PANELS THAT LOST THEIR DOOR

- **`PanelId.HeroLoadout` — ORPHAN.** Registered (`HeroLoadoutPanelMvvm.cs:61`) and spawned
  (`HeroSkillTreePanelBootstrap.cs:48`), but the only `Open(PanelId.HeroLoadout)` in the repo is
  `UICaptureMode.cs:276` — **editor capture**. `HeroSkillTreePanelMvvm.cs:1041` says it outright:
  *"The old Equip button that opened a second loadout screen is GONE."* **The panel outlived its door.**
- **`PanelId.RealmMap` — unreachable in a store build.** Its two openers are `InventoryUIBuilder.cs:333`
  behind `ff.maptab` (**default OFF**) and `DevPanelController.cs:870` (**DevTools, stripped in
  release**). So the WO-826 Realm Map ships but cannot be opened by a player.

*(Note the layer disagreement: the Presentation agent scored PanelId 16/16 on "has a registrar +
opener"; the Core agent scored 14/16 on "has a PLAYER-REACHABLE opener." The stricter reading is the
useful one — and the gap between them is exactly the existence-vs-consumption distinction.)*

---

## 6. WHAT IS LEGITIMATELY DECLARED-UNUSED (do not "fix" these)

- Echo Crafting / Defense / Exploration lanes — `EchoLaneBonuses.cs:21-23` names each stub and its
  intended consumer.
- `EliteVFXController`'s instance path — owner-annotated at `Enemy.cs:692-700`.
- `BattleHUD.uxml` — superseded by `BattleHudUgui.cs:152` ("Self-contained… No UIDocument / UXML").
- The pair-model dungeon graphs (`dg_stair_rig`, `dg_descent_probe`) — explicit "⚠ DO NOT DELETE"
  control group (`DungeonMultiLevelRegression.cs:41-63`).
- Authored `waves.json` batches — displaced by `_smartComposition: 1`, an **owner ruling**, though
  `WaveManager.cs:1643-1658` still carries an "OPEN OWNER RULING (WO-783)" marker.

---

## 7. THE PATTERN, AND THE ONE GUARD THAT WOULD CATCH IT

Across four independent layers the same shape recurs: **a well-designed generic system, built to spec,
never bound to a caller.** The architecture is not the problem — the *last wire* is.

Which is why an EXISTENCE assertion is nearly useless on this codebase (of course the system exists;
it was built well) and a **CONSUMPTION assertion** is where the value is:

> **A registered capability with no runtime consumer should be a RED GATE, not a quiet asset.**

Proposed, in leverage order:
1. **`[stack-utilization]` oracle** — walks public capabilities in Core/Village and fails on a NEW
   zero-consumer entry, with today's list as a dated ratcheted baseline (same shape as the VFX
   `KnownCatalogExposure` ratchet). Must special-case `[RuntimeInitializeOnLoadMethod]` self-install.
2. **Catalog-consumption assert** — every authored field in a canonical catalog has a reader, or is
   listed in a declared-unused set with a reason. Would have caught `widget-params.json` on day one.
3. **Mapping-oracle audit** — any regression asserting "X maps to Y" must resolve Y's consumer (§4).

---

## 8. PROVENANCE + LIMITS

Four read-only agents, 2026-08-09, against HEAD. No agent edited code or ran a gate.
**Explicitly NOT determinable statically, and stated as such by the agents rather than guessed:**
whether the 62 referenced Hovl keys actually *render* (`PlayKey` no-ops silently on a null prefab);
which animator states are ever *entered* (params are set via hashes across many callers); whether
`Dungeon_Demo` / `Dungeon` / `KayKitChallengeOutpost` are reachable by a player route; per-flag default
resolution against a player-reachable toggle. Raid/troop files were deliberately skipped — another seat
held them in-flight.
