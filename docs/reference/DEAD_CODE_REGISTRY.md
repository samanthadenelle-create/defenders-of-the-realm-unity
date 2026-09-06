# Dead Code Registry — built but never wired

**Status: KNOWN DICTIONARY** (durable registry, per memory `audit-outputs-as-known-dictionaries`).
Created 2026-08-16. Read-only sweep — **no code changed, no Unity run, no commit.**

Companion to `docs/reference/STACK_UTILIZATION_2026-08-09.md` (the *file*-level "what did we build that
never got plugged in?" pass). **This is the member-level pass** — the exhaustive category-4 sweep
(*public methods with zero call sites*) that the 2026-08-15 cross-silo sweep flagged as its one
remaining gap. It also covers enums, ScriptableObjects, catalog rows, feature flags, PlayerPrefs keys,
addressables and UXML.

> ## ★ The one-line finding
> **The most valuable dead code in this tree is not code at all — it is a stale *copy* of a data file.**
> 360 finished, priced, arted gear rows are invisible to the player because the wrong copy of the
> catalog wins at load. Nothing is broken, nothing errors, no test fails. It is the purest possible
> example of the pattern this registry exists to catch.

---

## ⛔ Read this before you delete anything on this list

**A wrongly-declared-dead system that then gets deleted is a far worse outcome than a missed one.**
Every row was checked against these false-positive classes, and each row states which checks ran.

| # | Class | Why a name-grep lies |
|---|---|---|
| **F1** | **Reflection / string binding** | `AdminOverlay` reaches Village types by reflection **by design** (the asmdef forbids the reference); the regression harness binds by string |
| **F2** | **Unity lifecycle** | `Awake/Start/Update/OnTriggerEnter`, `[RuntimeInitializeOnLoadMethod]`, `[ContextMenu]` are engine-invoked. **22 of 38 zero-reference Village files self-install this way and ARE LIVE** |
| **F3** | **Scene / prefab GUID refs** | a MonoBehaviour attached in a scene has no `.cs` caller; UnityEvent wiring lives in YAML as `m_MethodName:` |
| **F4** | **`[MenuItem]` / batchmode** | invoked by string from `*.ps1` / `*.py` / `tools/` |
| **F5** | **Interface dispatch** | the implementation has no direct caller; the *interface* does |

**Five more false-positive classes were discovered *during* this sweep** — they are why the raw
machine counts below are not findings lists:

| # | Class | Example found |
|---|---|---|
| **F6** | **Extension-method holders** | `EnumTokens` is called as `kind.ToToken()`, never `EnumTokens.ToToken()` |
| **F7** | **Same-file use** | 652 of 843 candidates in one category were used in their own file — **77%** |
| **F8** | **Nested-DTO attribution** | 117 of 142 "dead types" were nested payloads of a live outer class |
| **F9** | **Method-group delegates** | `PanelRouter.Register(PanelId.X, OpenOverlay)` passes the method with no parentheses |
| **F10** | **Type inference** | `ClassResourceDef` is consumed as `var res = AbilityCatalog.ResourceFor()` |

---

## Classification key

| Class | Meaning | Action |
|---|---|---|
| ★ **WIRE IT** | finished and valuable, one connection from working | the payoff class |
| **DELETE IT** | superseded — *the replacement is named with `file:line`* | safe to remove |
| **KEEP, DOCUMENT WHY** | deliberately dormant: a seam awaiting a consumer, a fallback, a type pinned by a regression | **deleting these breaks something** |
| **OWNER RULING** | dead because a design question was never answered | one owner sentence unblocks it |

---
---

# PART 1 — ★ THE TOP TEN, RANKED BY VALUE TO THE OWNER

Ranked by *what the player or the store sees*, not by line count.

---

## 1. ★ WIRE IT — 360 gear rows are shipped, priced, arted, and invisible

**The single highest-value finding in the sweep. No code change — a file copy.**

The canonical catalogs are dual-copied, and `Assets/_Modules/Core/Data/CanonicalJson.cs:9-18` states
the rule: Resources loads **first** (WebGL-safe), StreamingAssets is the desktop fallback —
**"Keep them in sync; Resources wins at load time."** They are not in sync:

| catalog | Resources (**wins**) | StreamingAssets (shadowed) | unreachable |
|---|---|---|---|
| `weapons.json` | **96** | **435** | **339** |
| `armor.json` | **24** | **45** | **21** |
| `accessories.json` | 10 | 10 | 0 ✓ |
| `consumables.json` | 17 | 17 | 0 ✓ |

*Verified independently at source by this seat — row counts parsed directly, and the "Resources wins"
rule read at `CanonicalJson.cs:9-18`.*

The Resources copies are **strict subsets** (`ONLY-in-Resources = 0`), so re-syncing is purely additive
and loses nothing. The shadowed rows are **finished content, not stubs** — every one of the 339 carries
a full field set (`name`, `kind`, `job`, `hand`, `damageType`, `rarity`, `damageMult`, `reach`,
`req.level`, the four `buy*` costs, `flavor`, `makersMark`) **and names a shipped asset**:

- **Prefabs** — `"loadVia": "addressable"`, e.g. `gear/weapon/Axe1h_01`. `Assets/AddressableAssetsData/AssetGroups/Gear.asset` holds **426** `m_Address` entries; `Axe1h_01`, `Axe1h_02`, `Sword1h_01` each resolve.
- **Icons** — `"iconPath": "ItemIcons/blink_axe1h_01"` → `Assets/Resources/ItemIcons/` holds **984 files, 850 of them `blink_*`**.

**Missing wire:** re-run the dual-copy sync for `weapons.json` and `armor.json`. **No code change.**
**Player gain:** the gear pool goes from 96 weapons / 24 armor to **435 / 45** — an entire art pack of
loot, vendor stock and drops that is built, priced and addressable-packed but invisible.
**Pair with a regression:** `Assets/Editor/Regression/DataWebRegression.cs` already pins the dual copy
for other files but not row counts on these two.

> ⚠ **Assume other pairs have drifted too.** Only these four were diffed. A full dual-copy row-count
> diff across all 65 shadowed files is the obvious next sweep.

---

## 2. 🔴 P0 SHIP BLOCKER — the dungeon portal cannot load on Android

Not dead code — **stale build output** — but found by this sweep and it outranks everything else.

- `Library/com.unity.addressables/aa/Android/Android/` holds **6 bundles and no `dungeon_assets_all*`**; files dated **Jul 15 / Jul 19**.
- `Builds/WebGL/StreamingAssets/aa/WebGL/` **does** hold `dungeon_assets_all_98d3c012…bundle`.
- The `Dungeon` group was created **Aug 14** (`Assets/AddressableAssetsData/AssetGroups/Dungeon.asset.meta`) — *after* the last Android addressables build.

**Consequence:** on an APK built from this tree the catalog advertises `dungeon/exit/portal` but the
bundle is absent, so `PortalStructure.LoadAsync` (`Assets/_Modules/Core/World/PortalStructure.cs:113`)
fails for both callers — `Assets/_Modules/Dungeons/DungeonExitInteractable.cs:350` and
`Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs:919`.

**Why it shipped silently, and why it can't be hotfixed:**
`Assets/AddressableAssetsData/AddressableAssetSettings.asset:61` — `m_BuildAddressablesWithPlayerBuild: 0`
(nothing forces a rebuild when groups change) and `:20` — `m_BuildRemoteCatalog: 0` (no post-ship path
to fix a stale bundle set).

**Action:** re-run the Android addressables build before any store submission. **OWNER RULING** on
whether to enable `m_BuildAddressablesWithPlayerBuild`.

---

## 3. ★ WIRE IT — the Screen Shake toggle does nothing, in *both* directions

**What the player sees:** they open Settings → Comfort, tap Screen Shake, the switch animates, the
label flips to OFF, the value persists across restarts — **and the screen keeps shaking.** Every
explosion, every boss death, every dungeon entry. There is no way to turn it off. It is an
accessibility control, so it fails exactly the people who need it.

Two independent breaks:

- **Written, never read** — `dotr-settings-screen-shake`, const `Assets/_Modules/Settings/SettingsModel.cs:66`, written `:218`, driven by the UI at `Assets/_Modules/Settings/SettingsController.cs:456`. Its getter mirrors onto `ScreenShakeSetting.Enabled` (`SettingsModel.cs:341`, written only at `:266`) — **read by nothing in all of Assets.** Its own doc at `SettingsModel.cs:28` claims *"a static gameplay reads."* Gameplay does not.
- **Read, never written** — `camerashake`, read at `Assets/_Modules/Village/Buildings/Tower.cs:1235` (`GetInt("camerashake", 1)`), **zero writers project-wide**. Already noted at `docs/reference/SAVE_SCHEMA_FIELD_MAP.md:402`.

**Blast radius:** `CameraShakeBridge.Shake` (`Tower.cs:1231`) funnels ~15 sites — `Tower.cs:710,759,1086`;
`Enemy.cs:741,742`; `EliteVFXController.cs:90,96,162,163,164,205`; `PortalVFXController.cs:811`;
`DungeonSceneBootstrap.cs:38`.

**Missing wire (one line):** `SettingsModel.ApplyScreenShake()` (`SettingsModel.cs:264-267`) must also
write `PlayerPrefs.SetInt("camerashake", ScreenShake ? 1 : 0)`. A direct type reference will **not**
compile — `DeNelle.Village.asmdef` does not reference `DeNelle.Settings` — so the PlayerPrefs key **is**
the correct seam, not a workaround.

⚠ **Second defect on the same fix:** `Assets/_Modules/Village/Audio/WaveMusicController.cs:121` calls
`SmartMobileCamera.Instance?.Shake(...)` **directly, bypassing the bridge**, so wave-start shake would
still ignore the setting. Route it through `CameraShakeBridge.Shake`.

*This is the only settings key the UI surfaces that nothing consumes. Master volume
(`SettingsModel.cs:246`) and quality tier (`:258`) both work.*

---

## 4. ★ WIRE IT — `ff.gaitforensics` ships **ON**, writing a CSV every frame

- **Default:** `Assets/_Modules/Village/Hero/HeroGaitForensics.cs:74` — `PlayerPrefs.GetInt("ff.gaitforensics", 1)`. **ON.**
- **Writers:** none. It is **not declared in `FeatureFlags.cs`**, so no dev menu, no UI, not URL-activatable. The only off-switch is a manual registry/plist edit.
- **Cost:** in `LateUpdate` (`:128`), every frame — a 20-field boxed `string.Format`, a `StreamWriter.WriteLine` to `persistentDataPath/gait-forensics.csv` (`:201`, opened `:102,:105`), plus a `GetCurrentAnimatorClipInfo` allocation. At 60 fps ≈ **1,200 boxed structs/second** of GC pressure and an unbounded file growing all session.
- **It ships.** It lives in `DeNelle.Village` with no `#if` guard and no asmdef define constraint — contrast `DeNelle.DevTools.asmdef`, which *is* stripped from release. **This runs on the release Seeker APK.**
- Its own header (`:34-36`) says *"default-off once root cause ships."* That never happened.

**Fix per CLAUDE.md §12:** add a real `FeatureFlags` entry with `defaultOn: false`. **Flag it off —
never strip the calls.**

*Every other opt-in diagnostic key in the tree defaults OFF (`dev.console`, `diag.castlenav`,
`dev.playerbot`, `autopilot.seed`) — which is exactly why those are correct and this one is the bug.*

---

## 5. ★ WIRE IT — the Heart of Elarion never changes colour

The town's centrepiece has a complete, authored visual state machine that is never applied.

- `Assets/_Modules/Village/Heart/HeartController.cs:124-132` authors **7 `HeartStateVisual` rows** — Serene violet → Vigilant blue → Warning amber → Danger orange → Critical red → Boss purple → Victorious white, each with emissive / pulse / halo.
- `:176 GetVisual` and `:173 CurrentVisual` are the finished readers. **Nothing reads them.**
- `SetState` (`:186-189`) only *records* the enum. The doc at `:183-184` says: *"Week 4+ this kicks off the 600ms colour/emissive ease; Week 3 it just records the value."* Week 4 never landed.
- Serialized `_crystalRenderer` (`:101`) and `_treeRenderer` (`:104`) are never read, and are **unassigned in the live hub scene** — `Main_Castle_Overworld.unity:12586-12587` → `{fileID: 0}`. `_stateEaseSeconds: 0.6` is authored and unused.

**Missing wire (two-part — code + renderer resolution, not a one-liner):** resolve the crystal renderer
(child lookup or scene assignment) and lerp its material colour/emissive toward `CurrentVisual` over
`_stateEaseSeconds` in `Update`.
**Player gain:** the town centre visibly reddens as the Heart takes damage and goes white on victory —
the game's strongest ambient threat read, currently invisible.

**Corroborating dead data in the same system:** `heart.json` has **no loader at all**. `heart.json:3`
`"maxHp": 160` contradicts the hardcoded `[SerializeField, Range(0f,100f)] _hp = 100f`
(`HeartController.cs:97`) — the attribute would clamp 160→100 even if loaded. Also dead:
`regenPerSecondOutOfCombat` (`:5`) and the three `phases[].hpThreshold` (`:9,15,21`), which
`DeriveStateFromHp` (`:226`) hardcodes instead.

---

## 6. ★ WIRE IT — the Jeweler polish chain: players are banking rough stones with nowhere to spend them

**The supply side is already live.** `Assets/_Modules/Dungeons/DungeonController.cs:528,571-573` grants
`ing_rough_stone` and writes `DungeonRunPayout.LastPolishScore` on **every completed run**, and
`Assets/_Modules/Dungeons/DungeonExitInteractable.cs:996` calls it for composed dungeons.

**The consumer side is finished and unreachable.** `Assets/_Modules/Village/Crafting/JewelPolishService.cs`
— `TryStartPolish` (`:198`, `:211`), `TryStartRePolish` (`:235`), `DescribeOddsLines` (`:458`) — plus a
complete `JewelPolishConfirmPanel.cs:74`. All zero-caller.

**The one missing wire:** a **"Polish" tab or button inside `JewelerPanelMvvm`**
(`Assets/_Modules/Village/Items/JewelerPanelMvvm.cs`, opened by
`Assets/_Modules/Village/Buildings/BuildingInteractable.cs:426` → `PanelId.JewelerCrafting`).
`JewelerPanelMvvm` contains **zero occurrences of the string "polish."**

**Player gain:** closes the dungeon → gem → ring economy loop. Right now the loop's middle is missing
and the currency accumulates forever.

*(`InputItemIdOf` at `:357` and `EnsureCancelHook` at `:594` are internally load-bearing — KEEP. The
catalog's `PolishSeconds`/`RePolishSeconds` light up the moment this is wired.)*

---

## 7. ★ WIRE IT — a complete, quest-gated gear-crafting tier progression with no door

`Assets/_Modules/Village/Crafting/GearCraftingService.cs` — **`CanCraft` and `Craft` have zero callers
repo-wide**, as do `EffectiveWeaponMult` (`:122`) and `EffectiveArmorDefense` (`:136`).

`gear-recipes.json` holds **8 authored recipes**, including the Act-IV legendary reforge gate.
`WorkshopCraftVM` reads a **different** catalog (`CraftingRecipeCatalog` / `crafting-recipes.json`) and
never touches `GearCraftingRecipeCatalog`.

**This seat settled the agent's one open question at source.** The recipes are *deliberately*
quest-gated and the quest layer knows it — `quests.json:3` states that quest ids are **"DELIBERATELY
unchanged: they key save state and gear-recipes.json requiresQuestId."** So the data is authored, the
quest ids are preserved specifically to key it, and **the consumer was never built.**

**Related, and a genuine OWNER RULING:** `Assets/_Modules/Village/Crafting/RecipeUnlocks.cs` (WO-850)
records recipe unlocks and says so explicitly in its own header — *"this type only RECORDS unlocks. It
gates NOTHING… do not 'finish' this without an owner ruling."* Correctly dormant; leave it.

---

## 8. ★ WIRE IT — arena/raid opponents have walls and towers but **zero defenders**

`Assets/_Modules/Village/Arena/DefensePatternLibrary.cs` is **whole-file dead** — `DefensePattern`
(`:42`), the class (`:65`), `RandomLayout()` (`:92`), `RandomPattern()`, `TotalCost`. Two independent
agents found it; the second located the precise wire.

**The one missing wire** — `Assets/_Modules/Village/World/Camps/EnemyOutpost.cs:335-336`:
```csharp
var placed = GameStateService.Instance?.State?.ArenaDefense;
if (placed == null || placed.Count == 0) return;   // no defense placed -> no-op
```
`RandomLayout()` returns exactly `List<PlacedDefenderData>` — same type, same assembly. Falling back to
it when `placed` is empty is a two-line change.

**Player gain:** the 3 seeded arena opponents (`Assets/_Modules/Village/Arena/ArenaCatalog.cs:28`)
currently get structures from `ArenaMode.GetDefenderRecipe` (`ArenaMode.cs:212,:301`) but **no defender
units**. Wiring this gives each opponent one of 7 hand-tuned, build-time cost-validated strategies
(Chokepoint / Ranged Line / Healer-Backed Wall / Caster Nest / Balanced / Swarm / Fortress).

**Provenance:** `WorkOrders/WORK_ORDER_389_arena_defense_system.md:50` — *"`DefensePatternLibrary`
(7 templates = AI-sync seed) ✅ BUILT & committed (`0b6c8dd`); still to build: Arena Rating + the
value→composition AI-sync scaler."* WO-389 wants a scaler on top; `RandomLayout()` alone delivers
varied defenses today.

**UNSURE, one detail:** `SpawnDefenders()` reads the *player's own* `GameState.ArenaDefense` even when
staging an *opponent* (`EnemyOutpost.cs:311-323`) — an MVP shortcut. The cleanest wire is a
defender-layout parameter on `ConfigureArena` (`:196`) rather than the raw empty-list fallback.
**Settles on:** owner intent on whether an AI opponent should mirror the player's placed defense.

---

## 9. ★ WIRE IT — dynamic difficulty computes four multipliers that nothing reads

The **input half is wired**: `WaveManager.cs:2755` calls `DynamicDifficulty.RecordEncounter(sample)`,
and `WaveManager.cs:426-442` carries a long comment about a prior task fixing exactly this shape
(*"the math was oracle-proven but INERT because nothing ever called RecordEncounter"*).

**The output half was never done.** `Assets/_Modules/Core/Difficulty/DynamicDifficulty.cs:110,119,122,125`
— `Pressure`, `EnemyCountMultiplier`, `BossHpMultiplier`, `BossDamageMultiplier`. A grep of
`Assets/_Modules/` for all four, excluding `Core/Difficulty/`, returns **zero hits**; the only consumers
are the difficulty math itself, its regression and its tests.

**Missing wire:** `WaveManager` roster sizing multiplies enemy count by `EnemyCountMultiplier`; the boss
spawn path scales HP/damage by `BossHpMultiplier`/`BossDamageMultiplier`. `Pressure` is documented
(`:109`) *"for a HUD telegraph"* — no HUD reads it.
**Player gain:** the game currently records that a player is steamrolling or drowning, computes the
correct response — and spawns the identical wave regardless.

**Same system, second break — bosses ignore difficulty entirely.** `Enemy.cs:114 BaseContactDamage`
exists *specifically* for the boss re-base; `Enemy.cs:794-801 SetBaseStats` documents being called
*"again, with the pinned HP, at the boss-HP-pin site."* **That call site does not exist.** `SpawnOne` —
the only path applying the boss pin (`WaveManager.cs:2431-2437 OverrideMaxHp`) — calls neither
`SetBaseStats` nor `ApplyDifficulty`. Wire: after `:2433`, add
`enemy.SetBaseStats(pinnedBossHp, enemy.BaseContactDamage)` + `ApplyDifficulty(...)`.

---

## 10. ★ WIRE IT — the bank is full and the game only tells you *after* it eats your resources

`Assets/_Modules/Core/Economy/TownBankCapacity.cs` (737 lines). The **cap works** — `ClampGrant` is wired
(`EconomyService.cs:425,428,443,446,463`; `OfflineHarvestService.cs:330`). But **nothing outside the
file ever asks what the cap is.**

`HasHeadroom` (`:451`) states its own contract verbatim at `:445-450`: *"The collector 'tap to collect'
tell MUST read this and say 'Bank full' instead of 'tap to collect'."* **Nothing does.**

**What the player experiences today:** tap → the pending pool vaporises → a toast arrives **after** the
loss (`BankOverflowToastPresenter.cs:64`). That is the exact footgun the docstring says the seam exists
to prevent. `WorkOrders/WORK_ORDER_900_collector_full_tell.md` is *"PARTIAL — sec.3 shipped; sec.4 HUD
chip deferred"* — §3's `CollectorStackView` shipped, but the tell never consults headroom.

**Second missing surface:** no HUD reads `MaxOf` (`:407`) or `RoomFor` (`:436,:443`) — there is no
`wood 1,980 / 2,000` anywhere, so a player cannot know they are near the ceiling until income silently
starts disappearing.

**KEEP, do not confuse with the above:** `Apportion` (`:599`), `Preview` (`:628`), `TryGetSlot` (`:646`),
`InstanceKeyOf` (`:664`), `OrderSlots` (`:568`), `Fill` (`:541`) and the `StorageSlot` struct
(`:155-176`) are the **WO-903 pallet fill-stacks seam**, and
`WorkOrders/WORK_ORDER_903_storage_pallet_fill_stacks.md` is **`Status: NOT STARTED`**. Deliberate
pre-built seam.

---
---

# PART 2 — ★ WIRE IT (the rest)

| # | What | Where | The one missing wire | Player gain |
|---|---|---|---|---|
| 11 | **Cathedral "Learn \<spell\>" unlocks nothing** | `HeroTalentModifiers.cs:389,398,406` — all three zero-caller | one loop in `HeroLoadoutVM.BuildChoices` (`HeroLoadoutVM.cs:266`) calling `ForEachBuildingUnlockedSpell` | the Cathedral upgrade delivers the spell it advertises. The data side is fully live (`GameModifiers.cs:92 UnlockSpell`, merged by `ModifierService.cs:193`); the regression states the symptom outright — `ModifierKeyCoverageRegression.cs:462-464`: *"the tier reads 'Learn \<spell\>' … and **unlocks nothing**."* |
| 11b | ↳ sibling | `HeroTalentModifiers.cs:381 MageMaxHpBonusPct` | same shape, in `HeroHealth.TalentHpBonus` | `HeroHealth.cs:169` composes max HP from `MaxHpMultiplier` only, so the Cathedral's `mageHpBonusPct` is silently discarded. Its doc at `:380` **falsely claims** it is consumed. |
| 12 | **Talent respec — the owner's own F8 report, half-fixed** | `HeroSkillTreeVM.cs:656 RespecCost`, `:659 CanRespec`, `:669 RespecStatus`, `:676 Respec()` | two controls in `HeroSkillTreePanelMvvm.Render` | The VM comment names the driver: *"Mirrors the legacy TalentTreePanel.OnRespecClicked path so the LIVE MVVM panel surfaces the same in-game respec (**owner F8: 'no respec option'**)"* (`:649-650`). **The VM half shipped; the view half did not.** *(`TalentTreePanel` itself was DELETED 2026-09-06 by WO-1430 — it was a doorless UI-Toolkit screen; the missing view half is still owed on `HeroSkillTreePanelMvvm`.)* |
| 12b | ↳ quick-swap | `HeroSkillTreeVM.cs:1339,1342,1346` | a 4-slot row bound to `QuickSlots` | **UNSURE** — `HeroLoadoutPanelMvvm` already ships a full hot-swap screen, so this may be a deliberately deprioritised *duplicate* entry point. The respec half has no such duplicate and is the unambiguous gap. |
| 13 | **6 of 7 UI themes unreachable** | `themes.json:56,108,160,212,264,316` | one settings binding — `Theme.SetTheme` (`Theme.cs:156-160`) has **zero callers**; `:171` pins `_activeKey` to the default | **246 authored colour values that can never render.** A shipped theme picker — the accessibility affordance the data was authored for. ⚠ See DELETE #23: the `Theme` class itself is unreachable, so this needs the palette re-pointed at `UiStyle`/`ElarionUi` first. |
| 14 | **An 800-gold perk that grants nothing** | `building-tiers.json:13` — `arcane-wellspring`, `"goldCost": 800`, `"modifiers": {}` | an unlock flag on `GameModifiers` beside `AutoCollect`/`Forgefire` (`ModifierService.cs:170-175`) | All 16 other perks have populated modifiers; `CompileFor` (`:144-155`) folds them generically, so `{}` is a no-op. A live 800-gold sink stops paying out nothing. (`HealingFountain` *is* a live `behaviorId` in `structures-catalog.json`.) |
| 15 | **Daily-quest anti-drought weighting is unparsed** | `daily-quests.json:8-9` — `noOneGotThisFloorDays: 14`, `noOneGotThisFloorWeightBoost: 0.30` | add both to `DailyQuestCatalogData` (`DailyQuests.cs:59-67`) and a recency term to selection | Newtonsoft drops them silently. Selection is pure weight (`:38`) with no recency, so a player re-rolls into the same few quests forever — the 41 hand-written quest lines never get seen. |
| 16 | **Hero asset loads pay two synchronous catalog stalls to learn nothing** | `HeroAssetLoader.cs:108-116` | hoist the probe result into a local instead of re-probing at `:85` | `AddressableRegistered<T>` calls `LoadResourceLocationsAsync(...).WaitForCompletion()` — a real main-thread block — and it runs **twice per asset** (`:70`, then `:85` for a diagnostic re-probe). No `Heroes/` address exists in any group, so **every hero mesh and texture load pays two blocking lookups** before falling through to `Resources.Load` at `:95`. |
| 17 | **`HarvestSite.UnassignPet` — a latent yield bug** | `HarvestSite.cs:178` | wire the pet recall/despawn path to it | ⚠ **Not just dead code.** `CalculateYield()` (`:219`) computes `petBonus = AssignedCount * YieldPerAssignedPet`, and `_assignedWorkers` is **never pruned** — `PetHarvestBootstrap.cs:224` assigns, nothing unassigns. A recalled or destroyed pet leaves a stale `Transform` inflating yield permanently. |
| 18 | **Mine nodes hide their remaining reserve** | `MineNode.cs:238 ReserveFraction`, `:302 ExtractsRemaining` | a world-space node readout | Their docs say *"for the world UI / settlement readout"* and *"Read-only progress for the fill indicator."* The sibling `ExtractFraction` (`:305`) **is** bound; these two are not. Players cannot see how much is left before a node mines out. |
| 19 | **Gear lore never reaches the player** | `GearAppraisal.cs:56 FullText`, `:149`, `:152` | a tooltip / vendor-detail panel calling `FullText()` | `Appraise()` **is** live (`GearCatalog.cs:508,521,528`; the `ShopVM.cs` call sites went with that file when WO-1430 deleted it 2026-09-06) — only the multi-line lore text is unused, so the WO-300 Elarion maker's-mark lore (Emberhand / Oathweld / Heartwood / Last-Pressing) is invisible. Only the price shows. |
| 20 | **Troop upgrades are invisible** | `TroopController.cs:179 UpgradeLevel`, `:185 UnlockedStatuses` (and `:182 UnlockedAbilities`) | a raid-HUD troop tooltip | WO-771.9 upgrades apply invisibly — the player cannot tell an upgraded unit from a baseline one. |
| 21 | **Small HUD readouts, each one binding** | `RaidScoring.cs:112 GarrisonTotal`; `ArmyMusterService.cs:136 TrainLineRoom`; `ItemIdentity.cs:137 GlyphOf` | bind beside their live siblings | *"12/18 defenders down"* instead of a bare number; *"3 slots free"* before the player queues and is refused; authored ASCII glyphs instead of a blank inventory cell. |
| 22 | **The capped structure-toughness read is bypassed by a hand-rolled copy** | `HeroTalentModifiers.cs:221` has no production caller | `WallSegment.StructureToughnessReduction` should delegate to it | `WallSegment.cs:441-452` re-implements the same clamp inline, and *that* copy is what `Gate.cs:251` and `WallSegment.cs:309` call. Numerically identical **today** — but it is a two-source clamp, and `TalentStrategyRegression.cs:472-489` pins the version **nobody calls** while the one in the damage path is unguarded. *(Also KEEP — pinned by reflection; do not delete.)* |

---
---

# PART 3 — DELETE IT (replacement named)

## 23. 🔥 Two whole theme files are dead — and one absorbed a P0 fix **yesterday**

**`Assets/_Modules/Core/UI/ShopTheme.cs` — the entire 443-line file.**
`grep -rln "ShopTheme"` returns exactly two files: itself, and three *comment* mentions in `ElarionUi.cs`
(`:7,:45,:108`). Zero call sites. All six checks run — no extensions, no reflection, no lifecycle, no
scene refs, no method-group registration.

**Replaced by** — both intended consumers migrated from UI Toolkit to code-built uGUI:
- `Assets/_Modules/HUD/CosmeticShopPanel.cs:258` `ElarionUiKit.BuildObsidianModal(…)` + `:350` `BuildObsidianButton(…)`
- `Assets/_Modules/Wallet/PackStore.cs:155`, `:388` — its header at `:11` states the migration outright

ShopTheme's API is `VisualElement`-typed, so it is **structurally incapable** of styling the uGUI panels
that replaced it. Not a dormant seam — unreachable.

> ⚠ **The cost is already being paid.** The last two commits touching this file are `5cd259465`
> (gold-on-black restyle) and `b1aca1688` (**2026-08-15**, "enlarge 6 tiny UITK closes", MinTouchPx
> floor). `docs/SME/VISUAL_TOUCH_CONTRAST_AUDIT_2026-07-14.md:45,112` files `ShopTheme.cs:287
> StyleCloseButton` as a **P0 touch-target defect**, and it was duly fixed 34px → 148px —
> **in a file no build renders.** `docs/MASTER_CATALOG/core.md:372` already flags the file as *"duplicate
> of the palette, slated to fold into UiStyle"*; the fold is complete on the consumer side and only the
> corpse remains.

**`Assets/_Modules/Core/Theme/Theme.cs` — whole file dead.** **No file in the repo except `Theme.cs`
itself contains the string `DeNelle.Core.Theme`.** The three apparent hits in `UiStyle.cs:248,250,254`
resolve to a **different `Theme`** — `UiStyle.cs` is in `namespace DeNelle.Core.UI` (`:30`) with no
`using DeNelle.Core.Theme`, and those are string constants, not `Color` tokens. Replaced by
`UiStyle.cs` + `ElarionUi.cs`. Same React-port residue as ShopTheme (header `:1-19` describes it as a
port of `src/lib/themes.ts`).

**Three UI-Toolkit leftovers on the otherwise-live `ElarionUi`:** `:313 MakeTitle`, `:384 Lighten`,
`:387 Darken` — zero external callers; ShopTheme mirrors all three (`:148,403,410`). Fold into the same
cleanup.

⚠ **Sequencing:** WIRE #13 (the theme picker) depends on this palette. Re-point the picker at
`UiStyle`/`ElarionUi` **before or with** this deletion.

## 24. `Assets/_Modules/Data/MasterAssetCatalog.cs` — the whole 56-line file

Global namespace, no asmdef. All five checks: no reflection; not a MonoBehaviour; **zero `.asset`
instances across all 9,353 scene/prefab/asset files** (it has `[CreateAssetMenu]` at `:4` — nobody ever
made one); no tooling reference; not an interface.

**Replaced by:** `Assets/_Modules/Village/Catalog/StructureFactory.cs:40` (*"the ONE creation path for
catalog structures (WO-148)"*) and `Assets/_Modules/Village/Buildings/BuildingCatalog.cs:155` (the
canonical `buildings.json` surface). The project moved wholesale from SO catalogs to canonical-JSON;
this is the last SO holdout. `docs/MASTER_CATALOG/misc-modules.md:214` already suspected it — *"the
2026-06-12 'no consumer found' flag was NOT re-audited this pass."* **This audit settles it.**

**Do not delete alongside:** `docs/MASTER_ASSET_REFERENCE.md` is a useful human prefab-key table (only
its header line 3 goes stale). Update `Assets/_Modules/Data/README.md` (the folder becomes empty) and
`CORE_ARCHITECTURE_PLAN.md:64`.

## 25. `AddressableUIManager` + `SkinController` — unreachable classes

Neither GUID appears in any of the 9,049 prefabs/scenes; neither is ever `AddComponent`-ed.
`AddressableUIManager` requests labels `UI-Core`/`UI-Debug`/`UI-Menus`/`UI-Tower`
(`Assets/_Modules/Core/UI/AddressableUIManager.cs:39-42`) that **do not exist** — the only labels in the
project are `default`, `Locale`, `Locale-en` (`AddressableAssetSettings.asset:106-110`).
`AddressablesMemoryProfiler` is transitively dead with `SkinController`.

⚠ **Deleting `AddressableUIManager.cs` breaks a regression** — `UiSurfaceProbeRegression.cs:202-230`
source-lints that exact file path for required tokens. Update or retire it in the same change.

## 26. Runtime localization is 100% dead

`LocalizationSettings` / `LocalizedString` / `GetLocalizedString` appear in **exactly one file
project-wide — `Assets/Editor/LocalizationBuilder.cs`, editor-only.** Real strings come from
`Assets/StreamingAssets/Data/Canonical/en.json` via `CanonStrings.cs:39` and `HeroCanonNames.cs:35`.
The 3 Locale groups ship 3 bundles for nothing (15,625 B — trivial), but **deleting them also drops the
Unity Localization package's runtime assemblies from the player**, which is the real win.

## 27. Data files superseded by hardcoded C#

| File | Dead rows/fields | Replaced by |
|---|---|---|
| `audio-mix.json` | whole file, no loader | `MusicTrackRegistry.Defs`, `Assets/_Modules/Audio/MusicTrack.cs:143+`, volumes as consts `:122-142`. Values match one-for-one — `audio-mix.json:4` ≡ `MusicTrack.cs:146`. **Editing the JSON changes nothing: a trap for the next audio pass.** |
| `towers.json` | `slotsPerZone` (`:14`), `slotFanAngleRadians` (`:15`), `slotRadiusOffset` (`:16`), `zones[].sectorAngleRadians` (`:4,5,6`) | inlined at `BuildModeController.cs:2321,2350` — whose comment *claims* to use "its towers.json tier." The comment is the drift. Separately, zone ids `ice/fire/aether` cannot bridge to `DamageElement {Aether, Flame, Ice}` (`IDamageable.cs:132`) — `fire` ≠ `Flame`, nothing maps them. |
| `walls.json` | 4 KayKit glTF mesh paths (`:12-13,22-23,32-33,43-44`), `halfSize` (`:3`) | hardcoded ladder at `WallTierData.cs:60-101`; geometry comes from catalog prefabs. `WallsFileJson` (`:130`) declares 3 of 8 authored per-tier keys. The two ladders also **disagree at every index** — `WallTier {Wood=1, Iron=2, ReinforcedSteel=3}` vs JSON `1="Stone Wall", 2="Steel Wall"`, plus a JSON level-`0` row with no enum member. |
| `daily-quests.json:4` `resetAtLocalMidnight: true` | not on the DTO | `DailyQuests.cs:414 LocalDateString()`. **Accidentally truthful, which is worse than dead** — setting it `false` silently does nothing. |
| `ad-creatives.json` | 8/8 rows + 11 fields | nothing needed — `DataWebRegression.cs:112-115` already calls it *"DEBT, NOT A DESIGN … a REMOVAL CANDIDATE, not a sanctioned exception."* **The ruling exists; nobody executed it.** |

## 28. Retired-canon landmines in `canon-strings.json`

`:9 avalonEpithet` and `:23 guardianOfTheLantern`. CLAUDE.md §7 retires "Avalon" (DESIGN-DECISIONS #1);
`docs/qa/bug-log.md:55` (BUG-019) records the Lantern motif dropped with `en.json` *"purged of all
Lantern/Avalon/Guardian refs"* — **`canon-strings.json` was not purged alongside it.** Unreferenced
today; live hazards for a future `Canon("…")` call.

## 29. Small superseded members

| Member | Replaced by |
|---|---|
| `ProjectileArtCatalog.ImpactForElement` (`:80`), `ForSpellOrb` (`:103`), `ForTowerName` (`:112`) | the 3D VFX path — `PooledProjectile.cs:80 ProjectileVFXCatalog.ReskinFlying`, `:134 SpawnImpact`. Owner ruling quoted in-tree at `PooledProjectile.cs:14-17`. ⚠ **Do not delete siblings `ForElement` (`:61`) / `ForArrow` (`:93`)** — `ArtResourceRegression.cs:90-99` asserts them. |
| `BuildMenuVM.UpgradePriceFor` (`:623`) | `BuildMenuVM.UpgradeQuoteFor` (`:695`), consumed at `BuildMenu.cs:680`. Its own doc says to use the authority instead. |
| `ResourceBuildingState.CurrentYield` (`:82`) | `CurrentEffectiveYield` (`:94`), which folds the WO-430 perk — consumed `ResourceCollector.cs:522,569`, `ResourceBuildingHarvester.cs:217`. Update the `<see cref>` at `:91`. |
| `ClaimableCamp.ConfigureAsEnemyOutpost` (`:125`), `IsEnemyOutpost` (`:123`) | `Assets/_Modules/Village/World/Camps/EnemyOutpost.cs:52`, owned by `RaidOutpostSystem.cs:35`. The stub's body is comment-only TODOs; its parent `WORK_ORDER_111` is **CLOSED — SUPERSEDED**. |
| `HeroEquipment.TryEquipDemoWeapon` (`:147`) | `EquipmentController.cs:62` + `GearLoadout.EquipWeaponById` (`:1109`) + `EquipVM`. ⚠ **Delete the METHOD only** — the class is still constructed by `EquipmentPanel.cs:437,439`. |
| `GearCatalog.AllAccessories()` (`:618`) | `Accessories` — its own docstring calls it an alias. ⚠ **Its last PRODUCTION consumer is gone:** the `ShopVM.cs:319-320` reads named here went with that file when WO-1430 deleted it (2026-09-06), so as of today the only caller left is `ArmedHeroInvariantRegression.cs:699` — a suite. Repoint that suite at the property and the alias can go. |
| `WaveClearPayout.LineCount` (`WaveManager.cs:120`) | `EndStateVM.cs:651-673` re-derives the count inline. Low value; a doc-only fix is defensible. |
| `SettingsModel.HasExplicitQuality` (`:202`) | ordering already handles it — `SeekerBootstrap.cs:61` is `BeforeSceneLoad`, `SettingsBootstrap.cs:65-71` is `AfterSceneLoad` and re-applies. Dead property, no bug. |
| 13 shadcn/ui heritage theme tokens | `chart-1`…`chart-5`, 8× `sidebar-*` — React v1 port residue. There is no sidebar and no chart. |

## 30. Five feature flags that gate nothing

Verified by grepping `FeatureFlags.<Name>` across ~1,950 `.cs`: **zero reference sites outside
`FeatureFlags.cs`.**

| Flag | Declared | Default | Gated what — now gone |
|---|---|---|---|
| `RuntimeWorldSeam` | `:202` | false | class `RuntimeRegionGate`, **which no longer exists**. Replaced by `MergedWorld` (`:402`), live at `SceneRouter.cs:150,471` |
| `GateBeacon` | `:436` | false | `RuntimeRegionGate.BuildGateBeacon` — same deleted class |
| `OutpostCaves` | `:447` | **true** | `CavePortalBuilder` — no such file. Its sibling `DungeonPortals` (`:463`) **is** live (`DungeonWorldPortalSpawner.cs:224,229`) — that contrast makes this one's silence provable |
| `BattleHud9Zone` | `:261` | **true** | `BattleHud9Zone.Create()` is a retired shim returning null (`BattleHud9Zone.cs:46-49`, called `BattleArenaHud.cs:46`). Replaced by `HudKitController.cs` |
| `CastleEditorBridgeSeam` | `:429` | false | **the doc comment is false.** `:425-429` claims the deck is disabled to stop a double-navmesh stack, but `CastleHubBuilder.AddCastleBridgeSeam()` (`:1839`) is called **unconditionally** from `:1637`. Editor-bake path, no player impact |

## 31. UXML/USS — 19 of 24 files are dead weight

**5 shipped-then-disabled** (bound in a shipping scene, then neutered at runtime — dead weight *plus* a
misleading scene graph):

| File | Scene | Disabled by |
|---|---|---|
| `TitleScreen.uxml` | `Title.unity:295` | `TitleController.cs:145-160`; code-built at `:175`; the field at `:72-74` calls it *"the retired Title UIDocument"* |
| `HeroSelectScreen.uxml` | `HeroSelect.unity` | `HeroSelectController.cs:185-189` disables the doc, code-builds at `:193` |
| `PetSelectScreen.uxml` | `PetSelect.unity` | `PetSelectController.cs:213` **clears** anything cloned in, then code-builds |
| `DungeonHud.uxml` | `Dungeon_HealersCottage.unity` | `DungeonHudController.cs:142-188` fully code-built (WO-1005) — stale binding |
| `VillageHud.uxml` | `Dungeon_Demo.unity:54` | `VillageHudController.cs` has no `UIDocument` reference at all — stale binding |

**14 referenced by nothing** (zero GUID hits across 11,750 files): `BattleHUD.uxml/.uss` (superseded by
`BattleHudUgui.cs:33`; a purge tool already exists at `Assets/Editor/Maintenance/PurgeAtbBattleUiDocument.cs`),
`DevPanel.uxml/.uss` (`DevBootstrap.cs:99-104` states the controller *"does NOT read DevPanel.uxml"*),
`BuildMenu.uxml/.uss` + `PackStore.uxml/.uss` + `TutorialOverlay.uxml/.uss` (sole consumer is
`VillageSceneBuilder` targeting `Assets/Scenes/Village.unity` — **that scene is deleted**), plus
`VillageHud.uss`, `Theme.uss`, `SelectScreen.uss`, `TitleScreen.uss`, `CraftingPanel.uss`,
`DungeonHud.uss`, `JupiterSwapPanel.uss`.

**Total across all 24 UXML/USS: ~131 KB** — trivial for build size. **Ranked here on bug risk, not
bytes** (see OWNER RULING #34 and #35 for the two that are actively live).

---
---

# PART 4 — KEEP, DOCUMENT WHY (deleting these breaks something)

## 32. Pinned-by-regression — the `PetTaskController` shape

| Thing | Pinned by | Note |
|---|---|---|
| `EchoAssignments.SetLevel` (`:232`) | `EconomySweepRegression.cs:307-343` | **Bidirectionally guarded** — fails if the method is **deleted** (`:319-320`) *and* fails if a production caller **appears** (`:336-343`). `EchoResourcePickerRegression.cs:348-353` fails if a `Lv N` chip reappears, citing *"WORK_ORDER_738 owner pin 2."* **This is the correct treatment of an undesigned axis — no action.** |
| `AdShowOutcome` (`IAdService.cs:87`), `AdShowResult` (`:98`) | `AdServiceSeamRegression.cs:89-94,:106` | Asserts the file still declares `interface IAdService`, `ShowRewarded`, `NullAdService`, and still distinguishes `LoadFailed` from `NoFill`. Deleting fails the WO-912 §10.5 seam guard. **`IAdService` has zero consumers in `Assets/_Modules` — the whole ad seam is deliberately dormant awaiting a provider.** |
| `ResourceBuildingState.ResetAll` (`:276`) | `DualFamilyLevelResetRegression.cs:125` | **Actively fails the build if any code calls it.** `DualFamilyLevelResetMigration.cs:32` records that it *"would have been the one-liner"* and was rejected because it wipes non-dual-family buildings and revokes Magic-gated tech. `ResetLevelToOne` (`:257`) is the sanctioned replacement. |
| `EchoRepairService` `BankedWork`/`LastOfflineGain`/`LastOfflineCountedSeconds` (`:134,146,150`); `OfflineClaimCoordinator` `ClaimCount`/`ConsumerCount`/`ResetForTests` (`:130,136,306`) | `OfflineClaimFanOutRegression.cs:100,122,133,160,166-181,242` | Diagnostic surfaces held by the suite |
| `HeroTalentModifiers.StructureToughnessReduction` (`:221`) | `TalentStrategyRegression.cs:472-489` (reflection) | Also WIRE #22 — pin it *and* wire it |
| `TowerUpgradeButton` (`SetTargetTower :74`, `OnUpgradeClicked :94`) | `HudUiRegression.cs:114` | **Same shape as `TowerEmpowerButton`, confirmed** — GUID `bc5caaf…` absent from all scenes/prefabs, so the whole MonoBehaviour is unreachable. But it self-documents at `:88-92`: the canonical affordance is the shared proximity HUD context button, and it is *"Kept (cost-enforced) only so an authored upgradeUIPrefab, if re-introduced, stays safe."* Deleting means deleting `TowerUpgradeVM` and `TowerUpgradeVMTests.cs` too — **an owner call, not a sweep call.** |
| `TutorialHudOverlay` | `OneGuideBodyRegression.cs:45-50,359-361` | Explicitly protected from deletion |
| `TutorialSignals.GuideGateReached` / `FirstGearAdded` (`:61,74`) | JSON step data + `TutorialStepReachabilityRegression.cs:731` | String-bound, not dead — good pattern |

## 33. Deliberate seams awaiting a consumer

- **`TownBankCapacity` occupancy cluster** — the WO-903 seam; that WO is `NOT STARTED`. (See #10.)
- **`ArenaContracts.cs`** (`:40,63,71,78,85,97,112,120`) — the file is *entirely* unconsumed; **zero files anywhere import `DeNelle.Core.Arena`**. *(Correction to the raw scan: `ArenaResult`/`HeroSpec`/`Vec3` scored "live" only via **name collisions** — `ArenaResult` is an enum at `ArenaMode.cs:43`, `HeroSpec` a private struct at `HeroAnimatorFactory.cs:98`.)* This is the owner-ratified "JSON in, JSON out" boundary: `WORK_ORDER_482:18` is **DESIGN LOCKED**, `:16` — *"the data boundary LANDED… the dedicated arena SCENE and the SceneDirector lifecycle do NOT exist."* Not wireable in isolation. ⚠ `WORK_ORDER_482` has a **duplicate number** (per its own line 16) — numbering hygiene for the banner.
- **`AddressablesGroupConfig.cs`** — **all 7** config types are dead (not just the 4 flagged): `AddressablesGroupConfigBase:32`, `VFXGroupConfig:118`, `AudioGroupConfig:144`, `MarketplaceGroupConfig:173`, plus `TowersGroupConfig:46`, `HeroesGroupConfig`, `PetsGroupConfig`. Zero `.asset` instances; `Assets/Configs/Addressables/` does not exist. The live path is address-string based (`HeroAssetLoader.cs:64,72`) — **exactly what this file's header says never to do.** Kept because it is named canon as a migration target: `docs/THIN_CLIENT_STREAMING_ARCHITECTURE.md:22` (*"unpopulated scaffolding"*) and `:79`. **Add a banner** noting the header's rule is currently violated by the live path.
- **`ILeaderboardSource`** (`LeaderboardService.cs:89`) — implemented in-file by `LocalStubLeaderboardSource:125`. Documented backend-swap seam at `:18-21`.
- **`StoreStockService.SetRemoteProvider`/`HasRemote`** (`:94,103`) — WO-429 offline-first seam awaiting a backend GET endpoint (`:20-24`).
- **`FlowTrace.Only` (`:85`) / `Mute` (`:96`)** — zero callers, **must not be touched.** The `Enabled` doc instructs seats to *"Use `Mute`/`Only` to narrow a noisy category rather than disabling the master switch"* — they are the sanctioned alternative to the forbidden action. Sibling `AllOn` **is** called (`ArenaCombatOracle.cs:110`). CLAUDE.md §12 binding.
- **`TutorialFlow.PressureHeld`** (`:112`) — the code agrees it is unused and says so on purpose (`:105-111`). The real gate is `WaveLoopSuppressedForTutorial` (`:217`), read at `WaveManager.cs:777,910,1109`. Also shape-asserted by `DataRegression.cs:1130`. **Do not delete.**
- **`SceneRouter` dungeon/raid consts + `GoPatriciaLight`** (`:179,196-201,383`) — `SceneRoutingRegression.cs:140-153` emits these as **NOTES, never failures**, and names the state honestly: *"removed route … GoPatriciaLight is DANGLING."* Not hollow. PatriciaLight is a DELETE candidate on canon grounds (§8) but that is an **owner ruling** — the const, `PatriciaLightParams`, `PendingPatriciaLight` and `GoPatriciaLight` go together or not at all.
- **`DailyQuestCatalog.Reload`** (`:120`) — one of a five-member family (`CraftingRecipeCatalog`, `ChatPhraseCatalog`, `TroopStatResolver`, `CurrencySkinResolver` all have the same). Retire the family together or not at all.
- **`RecipeUnlocks`** — records unlocks, gates nothing, header explicitly forbids "finishing" it without a ruling.
- **`en.json` — 194 of 229 keys unreachable *by construction*.** `CanonStrings.LoadMap` (`:129-137`) does a flat `TryGetValue` (`:151`); there is no enumeration path, so only the 28 keys named by a C# literal can ever resolve. Largest dead blocks: `heartVoice.*` (17), `victory.*` (12), `wave.warning.*` (12), `tutorial.first*` (11), `heartDamage.*` (9), `keeperAmbient.*` (8), `tooltip.*` (~30), `swap.*` (11). **Keep** as a staged localization seam — but document that **authoring a key here has zero runtime effect until a call site exists.**
- **`castle.liftY`** — read by 9 runtime sites, written only by the editor bake (`WorldMergeBuilder.cs:430,459`). Correct bake-time → runtime seam.
- **`LockOn`** (`FeatureFlags.cs:378`) — default false, **21 real runtime branch sites**, both writers release-stripped. Pinned by `RangedFacingLockRegression.cs:26,138,156-164,179`; deliberately off pending felt-test (mobile-nausea risk). Worth flagging that 21 dead branches ride on a switch nobody on device can flip.
- **`MapTab`, `RealmStorePurchase`, `RewardedAdSkip`, `Arena`/`Colosseum`, `PetCombat`, `DungeonFpv`/`DungeonCameraIso`, `GateTraversal`, `WeaponGripInfer`, `StakeDemo`/`SkrPreview`** — all deliberately off and correctly documented at their declarations.
- **`CustomDialogue`** (`:164`, default true) — ⚠ **OFF is now a trap, not a fallback:** `:159-164` states YarnSpinner is FULLY REMOVED, so `ff.customdialogue=0` leaves **no dialogue system at all**. Keep, but correct the "reversible" framing in the doc comment.
- **Dev opt-ins that correctly default OFF:** `dev.console` (`DevBootstrap.cs:77`), `diag.castlenav` (`CastleNavTopologyDiag.cs:51`), `dev.playerbot` (`PlayerBot.cs:40`), `autopilot.seed` (`AutoPilotDriver.cs:7029`).
- **`ItemInventory` / `ItemDropSystem` lane** — ships dark **by design** (header `ItemInventory.cs:10-12`); `ClearSession` early-returns on `!ItemDropSystem.Enabled` (`:114`).
- **`canon-strings.json:40 titleTagline`** — deliberate redundancy, and `:42` says so.

## 34. Self-installing MonoBehaviours — **the biggest trap in the whole sweep**

Confirmed `[RuntimeInitializeOnLoadMethod]`; all are whole-file-zero-reference and **all are LIVE**:

`PointerInterceptDiagnosticBootstrap` (`:16`, attr `:18`) · `ScrappedDecorRemover` (`:35`, attr `:42`) ·
`GameGuidePanelBootstrap` (`:24`, attr `:26`) · `StakeRewardsDemoDriver` (`StakeRewardsDemoBootstrap.cs:55`,
installed `:49` behind `FeatureFlags.StakeDemo`) · `StructureDamageVisuals` · `CastleDefensePlansService`
(`:43-52`) · `BankOverflowToastPresenter` · `ComposedDungeonBootstrap` · `ResourceCollectorBootstrap`

Plus these self-installed sub-MonoBehaviours, all live: `AuraPulse`, `EchoWispDrift`, `GateWarp`,
`CardTapGuard`, `HudRailGutter`, `CastlePlansGlint`, `QuestCastInteractable`, `TorchWardenInteractable`,
`DungeonTreasureBeacon`, `BiomeDropAnnouncer`, `PortalArtWearer`, `HarvestSiteRaider`,
`HeroAimIKReceiver`, `EventSystemWatchdog`, `PopulationGrowthBridge`, `UICaptureBootPoller`,
`WebTraceSinkDriver`, `KayKitProofMover`, `AtbSwapPoseVerifier`, `HeroAbilitiesHudBridgeBootstrap`.

> **A naive "no references → delete" sweep would gut working systems.** This is the reason the F2 check
> is mandatory.

## 35. AutoPilot-only callers are **live tooling**, not dead code

42 declarations are called only by `AutoPilotDriver` / DevTools — that is how this project verifies the
game headlessly (CLAUDE.md §8 pre-ship gates). Never delete:
`BuildModeController.HasArmedEntry/ProbeArmedGhostCell/ProbeArmedPlacementAt/RequestUiPlaceConfirm/ArmById`,
`HudCompassWidget.EnemyProviderWired/EnemyMarkCount/ActiveTickCount/TryGetFirstActiveTickSize/ForceProviderPoll`,
`TutorialFlow.PhaseName/IsFinished/RanThisSession/CurrentStepId`, `OverworldEncounterSpawner.*`,
`BattleArena.StagedEnemies`.

Likewise ~55 declarations are **editor build-tool-only** (`VillageSceneBuilder`, `RaidBaseGenerator`,
`RoomForge*Builder`, `HeroAnimatorFactory`) — these **build the shipped scenes and animators**. Also live.
`ActionKeywords.cs` (16 flagged) is exactly this: consumed by `BuildOrcHumanoidController.cs`,
`KnightPackageControllerBuilder.cs`, `HeroAnimatorFactory.cs`, `MotionCasterWindow.cs`.

## 36. Dungeons is a **parked warm pillar**, not an orphan field

The torch/oil/darkness risk-reward pillar is ~90% built and deliberately parked behind a demo. Treat
zero-reference Dungeons members as dormant-by-design unless a specific WO says otherwise. *(This
cluster's dedicated verification did not return — see the coverage boundary.)*

---
---

# PART 5 — ⚠ HOLLOW COVERAGE: green tests that prove nothing

**A distinct and more dangerous class than dead code.** `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`
catalogues hollow *trace fields*; these are hollow *assertions in the regression suites* — a different
axis, and **none of the four is listed there.**

### H-A. `HeroLocomotion.SelfMayWriteTransform` asserts `!x != x`
`Assets/_Modules/Village/Hero/HeroLocomotion.cs:403` — `=> !foreignOwnsTransform`. Zero production
callers. The real gate is written **inline** at `:977`, with the stand-down at `:995-999`.
`DungeonMoverOwnershipRegression.cs:115-139` (`Case1_OneOwner`) is built entirely from this function:
it asserts `(true)==false`, `(false)==true`, and that the two are complements — **a tautology. No
realistic broken state of hero transform ownership can make `[one-owner]` fail.** This is the
WO-968/WO-1016 two-mover law. Partially mitigated by a source-text grep at `:244`, but a string grep is
not the predicate.
→ **WIRE IT.** One line: make `:977`'s consumers read `SelfMayWriteTransform(foreignOwnsTransform)`.
Then `Case1` pins the predicate the game actually runs, at zero cost.

### H-B. `ObsidianQueueHud.OpenWorkQueue` — a "reachability seam" suite pinning a method nothing reaches
`Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:48`. Reached **only by reflection** from
`ObsidianQueueRegression.cs:196,279-282`, both of which `GetMethod("OpenWorkQueue", …)` and fail if it
is *absent*. `HudKitController.cs:877` states the fact plainly: *"ObsidianQueueHud.OpenWorkQueue had
zero live callers."* The file's own header (`:15`) calls it *"public static for regression
reachability"* — **a method made public so a test can find it, then tested for being findable.** Per
CLAUDE.md §7 the single Queues entry is now the `Upgrade`→`PanelId.Manage` bar face.
→ **OWNER RULING** (WO-911 Q10/Q13): delete both the method and the two assertions, **or** point them at
the live entry (`HudKitController.OnManageAction` → `ObsidianQueueGate.RequestToggle`). As written the
suite is green and proves nothing about whether a player can open the queue.

### H-C. `HudActionBarModel.RaidsDimMessage` — the suites pin a refusal string the player never sees
`Assets/_Modules/Core/HudModel/HudActionBarModel.cs:249-266`. Zero production readers —
`HudKitController.ApplyRaidsDim` (`:1473-1487`) reads `RaidsDimmed`, `RaidsFaceLabel` and
`RaidsDimReason`, never this. The copy the player gets is a **second, independent literal** at
`RaidSelectionScreen.cs:124-125` — and **the two have already drifted** (*"…to start a raid."* vs
*"…then open Raids."*). `HudActionBarModelTests.cs` and `RaidsDiscoverabilityRegression.cs` assert the
dead copy, **so the live string can break with the suite still green.**
→ **DELETE IT.** Replacement `RaidSelectionScreen.cs:124-125`; repoint both suites there (the WO-1008
docstring itself names that surface as authoritative).

### H-D. `DungeonRuntimeState.MarkBossDefeated` — a second, unused writer of the boss flag
`Assets/_Modules/Dungeons/State/DungeonRuntimeState.cs:452-458`. Zero production callers. The live
writer is `ResumeAfterEncounter` (`:377`), which sets `_bossDefeated = true` **inline at `:382`**. Six
production readers depend on the flag (`DungeonController.cs:541,557,576,614,701` — boss back-door
reveal, run-grade star, payout gate). `DungeonRuntimeStateResetTests.cs:62` sets it via the dead writer,
so the assertion exercises a path the game never takes.
→ **DELETE IT** (replacement `:382`), **or better**, have `:382` call `MarkBossDefeated()` so there is
one writer.

### Bonus — stale instrumentation that lies in the *safe* direction
`HeroAbilities.cs:1511` and `:2152` both print *"NOTE: inert until HeroHealth.TakeDamage reads
DamageTakenMultiplier."* **It does read it**, at `HeroHealth.cs:548`. The trace tells a reader a live
feature is dead. Cheap fix.

---
---

# PART 6 — OWNER RULING (dead pending an unanswered design question)

| # | Thing | The question that was never answered |
|---|---|---|
| 37 | **`ff.blinkarmor` → ≈5.6 MB of unreachable armor shipping to the store** | `FeatureFlags.cs:52`, `defaultOn: false`, readers `HeroArmorVisual.cs:105,128` + `PartyShopVM.cs:445,456`, **zero writers, no UI, not URL-activatable**. Both entry points hard-return, so the only consumer of `gear/armor/*` addressables (`HeroArmorVisual.cs:199`) can never execute. 25 skinned prefabs ≈ **45% of the 12,520,213-byte Android gear bundle**. Context at `:47-51`: the 2026-06-22 pivot junked Blink armor. **Drop the entries or revive the feature?** *(The dead-weight fact needs no ruling — only the direction does.)* |
| 38 | **0 of 69 feature flags are reachable in a release build** | The only in-game toggle UI is `OwnerDevToolsOverlay.cs:265-272` (5 flags) — and it **gates itself out of existence** at `:88`: `if (!FeatureFlags.DevResourceTool) return;`, where `DevResourceTool` is default-false (`:340`) with **zero writers**. Bootstrap deadlock. The 6 editor MenuItems are `UNITY_EDITOR`-only; `AdminOverlay`'s toggle sits behind `DevHotkeys` (also default-false, zero writers); `DeNelle.DevTools.asmdef` is `UNITY_EDITOR \|\| DEVELOPMENT_BUILD` so the Settings→DevPanel door doesn't exist in release; URL activation is allow-listed to 3 keys (`:912-936`). **Net: no flag can be changed by any in-game route on device.** That may be the right posture for a store build — but every *"reversible — just flip the PlayerPrefs"* comment in `FeatureFlags.cs` is **untrue on device**, and there is no on-device hotfix lever. |
| 39 | **`JupiterSwapPanel` — an unflagged crypto surface in the zero-crypto store build** | `JupiterSwapBootstrap.cs:90` `Resources.Load<VisualTreeAsset>("JupiterSwapPanel")` → `:118-122` builds a `UIDocument`, fired from `[RuntimeInitializeOnLoadMethod]` at `:61`. It lives under `Assets/_Modules/Web3/Resources/`, so it **ships unconditionally**. Auto-spawns on `Title`, `HeroSelect`, `PetSelect` (`:51-55`) and **any** `Dungeon_*` scene (`:130-132`). **No feature flag gates it.** This collides head-on with the store-hardening ruling that flipped `ff.skrpreview` and `ff.realmstorepurchase` OFF precisely so no crypto surface ships (`FeatureFlags.cs:575-578`). Being UXML, it most likely renders **blank** — either outcome is bad in front of a store reviewer. **Flag-gate it, or was it intentionally exempt?** |
| 40 | **`CraftingPanel.uxml` binds null on device** | `CraftingPanelController.cs:124-132` queries the UXML-cloned tree with **no code-built fallback** (it only warns at `:117-121`). Authored into the scene by `DungeonSceneBuilder.cs:1630-1648`; bound in `Dungeon_HealersCottage.unity`, **which is in build settings** (`EditorBuildSettings.asset:21`). **The player interacts with a dungeon crafting pedestal and gets an empty panel** — no recipe, no ingredients, no Craft button, no Close button, and no way to dismiss it except the pedestal's own close event. **Not** superseded by `CraftingPanelMvvm` — `PanelId.ConsumableCrafting` (`PanelRouter.cs:64-66`) is the *inventory alchemy* lane; this is the *dungeon pedestal* lane (`CraftingPedestal.cs:87`). Different feature. Its sibling was already ported for exactly this reason — `DungeonHudController.cs:4-10` calls itself *"the last UXML/UIDocument surface."* **It wasn't.** → **WIRE IT** (a port, ~1 file). |
| 41 | **`EnemyWeapons` — enemies spawn permanently weaponless** | `FeatureFlags.cs:245`, default false, one reader `EnemyFactory.cs:188`, zero writers. Unblocks only when the Offset Forge grip is perfected (`:238-245`). **A visible combat-readability issue, not just dead code.** |
| 42 | **`CampaignManager` — is the wave-goal campaign layer still canon?** | `CampaignManager.cs:97,110,124,142`. The component **is** instantiated (`WaveSystemBridgeBootstrap.cs:56`), so it is lifecycle-live — but **there is no `CampaignData` ScriptableObject anywhere in the repo**, and `WaveSystemBridgeBootstrap.cs:10` admits it *"no-ops safely until its assets are assigned."* DEF-68 sits in `docs/WO_ROI_TRIAGE.md:73` under *"Deferred / low-priority… post-grant."* Its completion detection also counts `WaveManager.OnWaveCleared`, which the smart-composition rework may have changed underneath it. **Still canon now that the loop is town→raid→dungeon?** Do not delete without a ruling; do not wire without a `CampaignData` asset. |
| 43 | **`enemy-roles.json` — 34 rows unread, blocked on an unmade taxonomy call** | No loader (only a doc comment at `EnemyTaxonomy.cs:79`). `creatures[].atkScale` (25 rows from `:15`) appears in no `.cs`. **Three role vocabularies share zero tokens:** this file's 9, `enemies.json`'s 5 (`caster/elite/brute/grunt/skirmisher`), and the `EnemyRole` enum (`WaveEnemyGroup.cs:43` — `Tank/Healer/DPS/Ranged/MiniBoss`). The bridge at `WaveData.cs:173` maps **`caster→Healer`, tagging every ranged caster a healer.** |
| 44 | **`armor.json` — 15 rows of perks no DTO field can receive** | `:106,124,142,162,…` e.g. *"Extended stun on Shield Bash when HP is above 75%."* `ArmorDef` (`GearCatalog.cs:175-231`) has `saga`, `flavor`, `makersMark` — **no `perk`** — and `LoadJson` uses `MissingMemberHandling.Ignore` (`:653`), so they vanish silently. These describe **mechanics that do not exist.** Does armor carry active perks? |
| 45 | **`ad-placements.json` — the whole monetization surface, authored and unwired** | No runtime loader (editor gate only, `AdPlacementCovenantRegression.cs:41`). Dead: `global.adProvider` (`:23` = `"stub"`), `hardDailyCap` (`:22`), `defaultCooldownSeconds` (`:21`), `respectDoNotSell` (`:24`), `covenantLine` (`:25`), `placements[].dailyCap`, `rewards[].maxStack` (`:35`), and 4 reward rows. Behaviour hardcoded at `FeatureFlags.cs:645` and `BuildTimerConfig.cs:92`. `adProvider: "stub"`, **no SDK selected, WO-912 D3 open**. `:26 _hardDailyCapNote` defers the brake number to the owner explicitly. |
| 46 | **`en.json` `.alt.*` variant families — the repo says the ruling is owed** | `heartVoice.alt.*` (12, `:63-74`), `heartDamage.alt.*` (4, `:82-85`), `defeat.alt.*` (2, `:104-105`). `docs/port-notes/canon-data.md:33`: *"`heartVoice.*` from the bible and `heartVoice.alt.*` from story.ts … **A design decision is needed before ship on which layer is authoritative for the Unity port.**"* Both layers dead until it lands; **neither should be deleted first.** |
| 47 | **`DailyQuestService.ForceRollToday` (`:313`) — a dev affordance with no button** | Zero callers. Its XML doc claims *"used by AdminOverlay / dev tools"* — `AdminOverlay.cs` binds eight methods by reflection (`:423,537,577,620,773,783,896,941`) and **none is this one.** Add the dev button, or drop the method and correct the doc. |
| 48 | **`TutorialHudOverlay.HideObjective` (`:77`) / `HideHint` (`:94`)** | The only path that *shows* them is `DialogueCommandSink.cs:189-190` (`set_hud_objective`/`set_hud_hint`), and **there is no clearing verb** — so a set objective could never be cleared. But **no JSON in the repo authors any of these verbs** (grepped every `.json` for all four — zero hits), and the sink's unmatched verbs fall to a `FlowTrace.Warn` no-op (`:201-204`). The FTUE's live surface is `ObjectiveStripUi`, and `ObjectiveBannerUi.cs:4-9` carries a *"⚠ RETIRED… ZERO callers"* banner. **Is the `DialogueCommandSink` HUD-verb surface being revived (add clear verbs, keep these) or retired with `ObjectiveBannerUi` (all four verbs + these two go together)?** |
| 49 | **`cosmetics.json:54-55` — a one-row schema divergence** | `pet-aether-twilight` alone carries `meshPath` and `specialSale: true`; neither is on `CosmeticDef` (`CosmeticCatalog.cs:29-53`). Either cosmetics carry meshes and sale flags (then 36 rows lack them) or they do not (delete these two). **No WO mentions `specialSale`.** |
| 50 | **`realm-map.json:108 withering.edgeBorder`** | Not on the DTO. `:107` explains: *"atmospheric only, never a punishing timer (the cozy covenant forbids FOMO countdowns). Later it becomes the visual home of the Weekly Realm Threat event."* The border ships independently of the event; **render it now?** |
| 51 | **`lore-fragments.json:76 placeholderNote` — a ship-blocker being silently dropped** | Content is load-bearing: *"Replace paragraph 2 with sourced prose or cut the Hidden Vault stone before ship."* **Rename to `_placeholderNote`** to match the repo's underscore-is-inert convention so it stops being invisible. Same class: `motion-castings.json:178,193,215,227 vfxNote`. |

---
---

# PART 7 — METHODOLOGY AND COVERAGE BOUNDARY

## What was actually run

A mechanical reference index over **1,947 `.cs` files** (declarations extracted from `Assets/_Modules`,
`Assets/Editor`, `Assets/Data`; references counted across all of `Assets/`, plus `tools/`, `WorkOrders/`,
`docs/`, `api/`, and all `.unity` / `.prefab` / `.asset` / `.json` / `.ps1` / `.py` / `.uxml` / `.uss`).
**14,375 declarations** indexed. Machine candidates were then handed to **nine parallel verification
agents**, each required to state which false-positive checks it ran and to cite `file:line` at source.

| Machine stage | Raw count | After verification |
|---|---|---|
| Declarations indexed | 14,375 | — |
| Zero external `.cs` reference | 2,866 | — |
| …after auto-filtering F1–F5 mitigations | 1,048 | — |
| Dead **methods/properties** | 377 | ~40 real |
| Dead **types** | 142 | **25 reported; 117 were nested-DTO noise** |
| Production decls referenced **only** by test/editor/AutoPilot | 843 | **~92% false positive**; 191 survivors, ~45–60 actionable |
| Ambiguous-name members re-checked by qualified `Type.Member` | 392 (static-owner subset) | **74% artifact**; 4 real finds |

## ⚠ Three defects in my own tooling, stated plainly

1. **Line numbers in the generated candidate lists are offset** (comment-stripping shifted them before line computation). Agents re-read every citation at source, so **the `file:line` in this document are verified** — but do not trust the scratch lists.
2. **The counter pools references by NAME across classes.** 40% of declarations (5,849 of 14,375) share a name with another declaration, so their counts are meaningless. **This is a recall gap, not a precision gap** — it *under*-reports. Tonight's known-true findings `EchoAssignments.SetLevel`, `TownBankCapacity.HasHeadroom` and `ResourceBuildingState.ResetAll` were all **missed** by the first pass for exactly this reason; they are in this document only because agents were pointed at their files directly.
3. **Owner-attribution slip.** The extractor assigns each member to the most recent `static class` header seen, so once a file declares one static class near the top, every later `sealed class`/`struct` member inherits that owner. This produced ~63 of 88 artifacts in the qualified pass (`Types.cs`, `FirebaseAuthService.cs`, `DailyQuests.cs`, `SaveMigrator.cs`).

**The correct check, learned from this sweep:** *before* auditing members, ask whether **any file in the
repo imports the type's namespace at all.* Whole-file reachability found two of the four real static-class
finds (`ShopTheme`, `Theme`) that member-level analysis missed.

## Exhaustive vs. sampled — honestly

**Exhaustive:**
- All 86 canonical JSONs under `Assets/Resources/Data/Canonical/` (incl. `dungeons/`, `dungeon-graphs/`, `dungeon-layouts/`, `dialogue/`, `tutorial/`) — every field path and row id, with the loader identified per file *before* any dead-call, including hand-walked `JObject` outliers.
- All 69 flags in `FeatureFlags.cs`; all 417 `PlayerPrefs` call sites (const-identifier and wrapper keys traced by hand).
- All 24 `.uxml`/`.uss`, GUID-grepped across 11,750 files.
- All addressable group/schema files and every `.cs` Addressables call site; 61,233 `.meta` GUIDs.
- 1,005 enum members across 232 declarations; 37 SO types against all 292 `.asset` files.
- The four gear catalogs' dual-copy row counts (**verified twice — by agent and by this seat**).

**Sampled or not covered — do not read absence here as absence of findings:**
- 🔴 **Two verification clusters did not return before this document was written: the `Assets/_Modules/Core` method cluster (~104 candidates — `ServerConfig.cs` alone has 16, and looks like a complete remote-config / live-ops seam: sales, events, maintenance mode, boss drop tuning) and the Dungeons / Pets / BattleATB / HUD cluster (~40).** Both lists exist at `…/scratchpad/methods.md`. **These are the largest known gaps.**
- **`Assets/StreamingAssets/Data/Canonical/` (65 files)** — analyzed only as the losing half of the dual copy. The one gap that mattered was closed (finding #1), but **the other 61 shadowed files were not diffed row-by-row.** Given weapons drifted 96↔435, **assume other pairs have drifted too.** This is the highest-value next sweep.
- **`Assets/Editor` dead code** — filtered out of the strong-candidate set by my own mitigation pass as "editor-only." Not audited. Low player value, but stale bake tooling is a real hazard.
- **Scene/prefab GUID sweep for MonoBehaviour class rows** — spot-checked, not exhaustive (a full-tree grep timed out). **Treat every `class X` row as unverified on this axis until that sweep runs.** UnityEvent `m_MethodName:` YAML was checked in the Village clusters (result: project UnityEvent wiring occurs **only** in vendor LeanTouch sample scenes) but not repo-wide.
- **"DTO field with zero read sites"** — hand-verified for only four types. Not run systematically across ~200 DTO fields; a bare `.FieldName` grep collides too often across 19 assemblies to trust unsupervised. Needs per-class scoping.
- **Bundle-internal composition** — `catalog.bin` was not decompiled; the 41/45/11% weapon/armor/hero split is attributed by source-dependency folder and is an approximation.
- **Enum members selected by integer in serialized scene data** — serialized enums store as ints, so a scene may select a member with no code reference. Flagged where it could not be ruled out.

## Prior art

`docs/reference/DATA_CLASS_MAP.md` (2026-08-09) **held at HEAD everywhere it was spot-checked.** Catalog
findings 1, 2, 3, 4, 5, 9, 12, 13, 16, 21 are new to it; the rest confirm or extend it. The
360-shadowed-rows figure **exceeds** the ~356 that doc estimated, and armor has drifted further than it
records.

---

## Suggested execution order

1. **#2** (Android addressables rebuild) — ship blocker, no design input needed.
2. **#1** (re-sync the two gear catalogs) — a file copy; largest player-visible gain in the document.
3. **#3, #4** (Screen Shake wire; flag gait-forensics off) — one line each, both fix live defects.
4. **#39, #40** (Jupiter swap gate; CraftingPanel port) — store-review and dungeon-crafting risk.
5. **#37, #38** — owner rulings that unlock ≈5.6 MB and the on-device hotfix lever.
6. **#5–#10** — the WIRE IT headliners, each a self-contained ticket.
7. **#23–#31** — deletions, *after* #13's palette is re-pointed.
8. **Then:** the two unreturned clusters, and the full 61-file dual-copy diff.
