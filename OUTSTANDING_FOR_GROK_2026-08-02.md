# OUTSTANDING WORK — for Grok prioritization
**Generated 2026-08-02 by the CLI seat. Grok has full code access — every item carries a file:line so you can verify before ranking.**

Three buckets: **IN FLIGHT** (agents writing code right now — don't re-file these), **OUTSTANDING** (found, diagnosed, nobody assigned), **DECISIONS** (blocked on the owner, not on work).

Nothing below is speculation. Every item was verified against source by a read-only audit tonight. Where an audit's premise turned out to be wrong, that's noted inline.

---

## A. IN FLIGHT — 6 lanes, do not re-file

| Lane | Scope |
|---|---|
| 1 | Magenta raid troops (MagentaGuard never sweeps runtime spawns) + no-animation on raid troops (instrumenting) |
| 2 | Combat P0s: pooled-caster statues, EnemyBrain state surviving pooling, enemies attacking their own wounded, tactical layer dead in the live wave path, raid garrisons with no brain, Perfect Hit always true, enemy pool leak |
| 3 | Tutorial `founding_hollow` softlock (arms in enemy-owned Village2 where build is impossible; Lumberyard/Lumbermill id drift; watchdog credits fake completion) |
| 4 | Wallet identity: Android login hard-softlock, constant-seeded stub wallet address, email login binding Firebase UID as the save key |
| 5 | Cloud save 401 both directions (`api/` server half) |
| 6 | Check-in gate runs 22 suites instead of 86; three classes emit the same `REGRESSION_OK` marker; register ~10 unregistered oracles |

**Fixed tonight, uncommitted:** level-1 Mage spawning unarmed (`GearCatalog.BestWeapon` returned a shield; `GearLoadout.EnforceHandSlots` evicted it and never refilled). Both halves fixed + armed-hero invariant restored.

---

## B. OUTSTANDING — diagnosed, unassigned

### B1. Live defects that mislead or rob the player

| # | Item | Where | Why it matters |
|---|---|---|---|
| 1 | **Improving a shield spends real resources and does nothing.** `GearProgression.Improve` accepts shields (they're `weapons.json` rows), charges wood/iron, writes `GameState.GearLevels`, prints "improved to Lv 5" — but `GearLoadout.ApplyStats` reads the off-hand **raw**: `offHandDefense = EquippedOffHand.defense`, no level scaling. | `PartyShopVM.cs:588,657`, `GearLoadout.cs:424`, `GearProgression.cs:222` | Legendary L5 shield = 3500 wood + 1750 iron for **zero** effect. Silent, repeatable. |
| 2 | **The armor-defense cap the player sees (0.9) is not the cap the engine applies (0.70).** 13 display sites clamp at 0.9; `GearLoadout.cs:427` clamps the applied value at 0.70. `HeroHealth.cs:543` uses a third number (0.95) for talent DR. | `GearLoadout.cs:427` vs `EquipVM.cs:390`, `ShopVM.cs:362,555,620,754`, `PartyShopVM.cs:1418,1439` | Shop shows "+85% def", engine grants 70%. Also: a BiS knight now **saturates** 0.70, so the final legendary reforge delivers half what it charges for. |
| 3 | **`BuildMenu` runs a fake wallet.** `GetMaterialCount` is `case "wood": return 20; case "stone": return 5;` — hardcoded. Those constants are shown to the player as their balance, gate `CanAfford`, and are never deducted. | `BuildMenu.cs:644-662, 345` | Every tower priced in wood/stone is effectively **free**, on the FTUE build beat. Comment says "material inventory is not tracked yet" — both stores exist. |
| 4 | **`BuildMenu` issues an unverified spend.** `OnConfirmBuild` builds a cost from its own table and calls `BuildModeController.ChargeLedger`, which **discards `TrySpend`'s bool**. Placement proceeds `prepaid: true` even when the ledger declined. | `BuildMenu.cs:380-418`, `BuildModeController.cs:2725-2732` | Live free-tower path. |
| 5 | **Arena/outpost loot awards armor the hero can't wear.** Both roll on level+rarity only — neither calls `GearCatalog.ArmorFitsClass`; both fall back to job `"any"`. The weapon halves *do* gate. | `BattleArena.cs:2378-2391`, `EnemyOutpost.cs:792-805` | Mage is awarded heavy armor, `GearLoadout.cs:352` silently drops it on the next refresh. Player sees a reward, then nothing. |
| 6 | **Respec is a non-atomic transaction inside a View.** `TalentTreePanel` spends crystals via `EconomyService.TrySpend`, *then* calls `WisdomCurrencyService.RespecHero`. If the service is null the crystals are gone and nothing happens. | `TalentTreePanel.cs:339-353` | |
| 7 | **`HeroEquipHud` doesn't arm on the live hub.** Its private hub list is `MainCastle_Hall \|\| Village2 \|\| CastleHub` — `Main_Castle_Overworld` is missing. | `HeroEquipHud.cs:53` | Exactly the bug `HubScenes` was created to prevent, still alive in one file. `HubScenesTest.cs` sits next to it. |
| 8 | **`ResetToNewGame` does not wipe `Zones` or `Settlements`.** `Settlements` is simply absent from the body; `Zones` calls `EnsureZoneGraph` which early-returns when non-empty, so it can only backfill, never reseed. | `GameStateService.cs:861-965, 597` | "Start New" opens on a pre-explored realm with the previous save's claimed/razed nodes and 3-day lockouts. `ResetCarveOutTest.cs` has zero assertions for either. |
| 9 | **Archer Tower can't hit air despite the data saying it can.** `canHitAir: true` is authored at **entry** level on 4 rows; `CatalogEntry` has no such field and the parser is `MissingMemberHandling.Ignore`. Only `repo.canHitAir` is real — and it's `false`. | `structures-catalog.json`, `CatalogEntry.cs:29-66`, `CatalogBootstrap.cs:109` | Same silent-drop class as the Cathedral mage keys. |
| 10 | **Cathedral tier modifiers are not cumulative, so upgrading REVOKES spells.** `ModifierService.Compute` applies only the current tier's def, so tiers must restate everything. `arcane-tower` T4 authors `cataclysm` and drops `frost-nova`, `manaweave`, `arcane-bolt` plus manaMax/manaRegen/hpBonus/shell. | `building-tiers.json`, `ModifierService.Compute` | A fully-upgraded Cathedral is **worse** than tier 3. Data fix, not code — tower keys already restate correctly (T3/T4 both carry `towerDamageMult`). |
| 11 | **Shields/Warden's Grace/Arcane Shell are all still felt as zero if `HeroHealth.TakeDamage` doesn't read `DamageTakenMultiplier`.** Verify against tonight's HeroHealth edit before ranking — I believe this closed, but a second lane's report still lists it open. | `HeroAbilities.cs:~1069`, `HeroHealth.TakeDamage` | |

### B2. Performance / stability

| # | Item | Where |
|---|---|---|
| 12 | **`FlowTrace` evaluates its string arguments even when disabled.** Every interpolated trace line allocates on mobile regardless of the flag. | `Core/Diagnostics/FlowTrace.cs` — P0 for the Seeker |
| 13 | **VFX catalogs reference gitignored packs by GUID.** Hollowed VFX on any machine but the owner's; nothing in the gate catches it. Same class: the URP conversion state of Supercyan/polyperfect/Quaternius is machine-local and one re-import from regressing. | `.gitignore:128,137,288` |
| 14 | **Untextured walls read as pink/lavender and no detector exists.** `steel/wood/iron_wall.fbx` carry laptop-absolute `.fbm` paths; no `.fbm` folder exists; `externalObjects: {}`. Result is a valid URP material with **no albedo** → white → lavender under the hub's blue-violet ambient. The real textures ARE tracked at `Resources/Walls/Textures/` under non-matching names. | This is a **white** bug, not a magenta bug — WO-838 misfiled it |
| 15 | **~60 `Shader.Find` sites, 14 ending in `?? Shader.Find("Standard")`.** Standard is NOT in Always Included, so those return null → `new Material(null)`. Plus `Simple Lit`, `Unlit/Color`, `Particles/Standard Unlit`, `Skybox/Procedural` all resolvable-in-editor, strippable-in-build. | `GraphicsSettings.asset:30-48` is the ground truth |

### B3. Architecture — the structural root cause

| # | Item | Where |
|---|---|---|
| 16 | **46 presentation files live inside `DeNelle.Village`.** There is no asmdef wall between the shop UI and `WaveManager`. Every item in B1 (#3,#4,#6) is only *possible* because of this. The fix is one new `DeNelle.Village.UI.asmdef` referencing Core only — that makes those violations impossible to compile rather than merely detectable. | 60 presentation files in Village vs 36 in HUD |
| 17 | **The MVVM oracle has three holes and pushed a violation downhill.** `BarracksPanelVM` now does `new GameObject(...).AddComponent<BarracksPanel>()` — a ViewModel constructing its own View — with a comment saying it was moved there to make the View pass the lint. Holes: (a) `if (vmRoutes) continue` skips any file containing `IPanelViewModel`, which is where `BuildMenu`'s fake wallet hid; (b) UI Toolkit views aren't candidates at all (38 files invisible, incl. `TalentTreePanel`); (c) nothing lints the VM side. | `UiMvvmConformanceRegression.cs:189,56-60`, `BarracksPanelVM.cs:183-189` |
| 18 | **HUD reflects across the asmdef edge to invoke purchases.** `CosmeticShopPanel` has 34 reflection sites incl. `TryPurchase`. Root cause: `IEconomy` exists but lives in `DeNelle.Village`, and `CoreServices` has no Economy slot. | `CosmeticShopPanel.cs:110-150,187`, `AdminOverlay.cs:667`, `HelpMenu.cs:304`, `OwnerDevToolsOverlay.cs:271` |
| 19 | **`HubScenes` exists and is bypassed 14+ times** — 8 verbatim copies of a private `IsCastleHubScene`, 4 copies of the `RaidBase` prefix rule, 14 bare `"Main_Castle_Overworld"` literals. **Trap:** `HubScenes.IsHub` uses substring `Contains`; every local copy uses `==`. Fix `IsHub` first, then consolidate. | `HubScenes.cs:32-34` |
| 20 | **`scene-configs.json` is parsed three times** by three classes with two different keys and only one `Invalidate()`. `HubScenes` and `SceneOwnership` are near-identical duplicate parsers. | `HubScenes.cs:99`, `SceneOwnership.cs:86`, `SceneConfigCatalog.cs:156` |
| 21 | **28 near-identical catalog loaders**, already disagreeing on severity (20 `LogError`, 5 `LogWarning`, 1 `FlowTrace.Fail`, 1 `Warn`). A missing catalog is a hard error in some modules and invisible in others. One `CanonicalJson.ReadCatalog<T>()` collapses all 28. | |
| 22 | **Three `ResourceCost` structs, five resource enums.** Ordinals differ (`HarvestResource.Crystals=0` vs `ResourceType.Iron=0`), so any accidental `(int)` cast silently corrupts. | `RepoProps.cs:21`, `EconomyService.cs:82`, `ResourceBuildingProgression.cs:58` |
| 23 | **Dead surface in Core:** 7 unused Addressables ScriptableObjects, 8 unused arena contract types, `PursuitBattleProbe`, `ArcaneTowerDiag`, `SeedTree`; plus whole features (referral, promo UI, stake panel) with no consumer outside Core. Cheapest deletion on the list. | |

### B4. Coverage / process debt

| # | Item |
|---|---|
| 24 | **UI capture covers 11 of ~76 panels.** `UI_CAPTURE_OK` proves a panel rendered, never that it looks right — and the guide capture only ever shoots the default-selected tab, so the new glossary tabs are unproven. |
| 25 | **31 WOs ≥760 marked IMPLEMENTED with no RESULT file.** |
| 26 | **`CLI_LANES_WO_NUMBERS.md` still lists WO-850 as OPEN** though `0bb46258` shipped it. |
| 27 | **Monetization caps are authored and unenforced:** `ad-placements.json` `global.hardDailyCap: 12`, per-placement `dailyCap`, `maxStack`, `rewardSinks`, `respectDoNotSell` — no C# reader. Highest-value item in the dead-key sweep. |
| 28 | Other dead authored keys: `heart.json` hpThreshold/regen, `daily-quests.json` reset rules, `towers.json` slot geometry, `walls.json` meshes, `enemy-roles.json` scales, `audio-mix.json` fades. |

---

## C. DECISIONS — blocked on the owner, not on work

| # | Decision | Context |
|---|---|---|
| D1 | **Raid posture: hero-led vs spectator.** Recommendation is hero-led + one doc edit to `RAID_NORTHSTAR.md:29`. Spectator costs 3-4 days and *removes* the only interactive element until interior content exists. **Every item that would make a raid worth watching is required under either model** — so the posture question is free to defer. |
| D2 | **The armor cap: 0.70 or 0.90?** (B1 #2). The code move is safe; the number is a balance call. |
| D3 | **Perfect Hit: real timed input, or fold the multiplier into base damage?** Lane 2 will pick one and justify it — overrule if you disagree. |
| D4 | **Guest cloud-save: server-side guest identity, or guests stay local?** Lane 5 will pick one. Testers are being recruited now, so this is time-sensitive. |
| D5 | **The terminal economic sink.** Four options costed: forward outposts you can LOSE (recommended — largest existing-code ratio, and attrition makes it unbounded for free), async PvP bases (unbounded but 4-8 weeks + unsolved server authority), a second town (bounded, big price tag), deeper upgrade curves (2 days, fixes arithmetic not feeling — recommended as a *safety valve* regardless). **Blocker for A, B and C is the same one field:** `PlacedStructureData` has no site key. |
| D6 | **Biomes (WO-829): Phase 1 only, or the full five-scene program?** Phase 1 (map-side biome identity — node tints, epithets, Withering edge) is one PR and is the only part felt before WO-827 lands. Five region scenes without travel are five unreachable scenes plus APK weight. **WO-827 is the real blocker.** |

---

## D. The one cross-cutting seam

**Nothing in this game can damage a wall, gate, or enemy tower.** `IDamageable` and `IDamageableStructure` are disjoint interfaces; `WallSegment.cs:28` and `Gate.cs:45` implement only the structure one, and `TroopController.NearestHostile` filters on the other.

That single gap blocks *both* long-range roadmaps: raids can never be about bases under either posture, and a player-authored base another player attacks has nothing to attack. **~2-3 days, and it is the prerequisite hiding under both halves.** If one architectural item gets built this week, this is the one to rank first.
