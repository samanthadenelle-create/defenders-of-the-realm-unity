# GAMEPLAY GAPS — full player-journey QA walk (2026-07-26)

**Author:** QA design analyst (read-only code walk).
**Method:** the entire arc was walked as a designer — FTUE → town loop → dungeon → raid → echoes/meta →
monetization → cross-cutting feedback — with every gap grounded in `file:line` evidence read from HEAD on
`wip/village2-and-f8-tickets`. Genuine gaps are separated from things **DEFERRED-by-design** (noted per item).
**Anchors:** `docs/RAID_NORTHSTAR.md`, `docs/PAIN_POINTS_2026-07-26.md`, `CANON_GROUND_TRUTH_2026-07-26.md`,
`WorkOrders/WORK_ORDER_774_raid_loadout_deployring_naming.md`.

**Severity key:** **P0** = loop-breaking / core-system-dark / no-revenue-path · **P1** = feels-bad /
broken-promise / hollow-stakes · **P2** = polish.

---

## 0. Two canon reconciliations found during the walk

1. **Raid V1 is BUILT, not spec.** `CANON_GROUND_TRUTH_2026-07-26.md` §3 says the raid loop is "all SPEC,
   nothing built yet." That line is **STALE.** All 11 classes WO-774 names exist and the loop is reachable
   end-to-end (train → army → pick target → pre-raid → teleport → tap-deploy → auto-fight → stars/loot →
   claim). `docs/RAID_NORTHSTAR.md` already carries the corrected, code-grounded statement. Raid work is
   polish + clarity, NOT a rebuild.
2. **`ff.tutorialv2` is default ON** (`Core/FeatureFlags.cs:454`), despite `TutorialFlow.cs:26` and sibling
   comments still asserting "default OFF." The live FTUE is the data-driven V2 flow; the legacy
   `TutorialDirector` self-destructs (`TutorialDirector.cs:132-137`). `PackStore` scene-wiring is likewise
   **no longer disabled** — the UXML dependency is gone (`PackStore.cs:10-16`), so CLAUDE.md §8's "store
   scene-wiring DISABLED" note is stale.

---

## 1. TOP GAPS RANKED (mint fixes from here)

### P0 — loop-breaking / core-system-dark
| # | Gap | Where |
|---|---|---|
| P0-A | **Multi-channel queue panel is UNREACHABLE** — the headline WO-773 feature ships dark | `Core/UI/ObsidianQueueGate.cs:34` (0 callers) |
| P0-B | **"Sell time" monetization has ZERO surface** — buy-slot / instant-finish / ad-skip are service+config only, no UI, no callers | `BuildTimerService.cs:153,353-410` |
| P0-C | **Raid: no loadout** — the pre-raid selection is discarded; the field always arms the FULL roster | `SceneRouter.cs:456`; `RaidDeployVM.cs:131-134,184`; `RaidDeployController.cs:305-316` |
| P0-D | **Raid: deploy-anywhere** — no deploy ring; drop troops on the boss/Heart | `RaidDeployController.cs:28-29,267-302` |
| P0-E | **Raid: two "Deploy" verbs** — pre-raid CTA and in-raid tray both say "Deploy" (unteachable) | `RaidDeployScreen.cs:351`; `RaidDeployController.cs:586` |
| P0-F | **Raid: "razed %" copy vs garrison-kills math** (broken promise; copy-only fix) | `RaidScoring.cs:91-102`; `RaidHudController.cs:154,223`; `EndStateVM.cs:246-247` |

### P1 — feels-bad / broken-promise / hollow-stakes
| # | Gap | Where |
|---|---|---|
| P1-A | **FTUE: first-tower step grants no crystals** — `grant.prepaidTower` exists in code but NO step sets it; likely stall on the most important build beat (clears only via 120s watchdog) | `TutorialFlow.cs:75,881-903`; `tutorial-steps.json:78` |
| P1-B | **FTUE: onboarding is a single point of failure** — only `TutorialFlow.FinishFlow`/`SkipAll` flip `Onboarded` + kick waves; a bootstrap miss = tutorial-less, wave-less, never-onboarded dead hub | `TutorialFlow.cs:802,865`; `WaveManager.cs:534` |
| P1-C | **FTUE: claim-loop is TOLD not DONE, and taught late** — `founding_echo`/`greet`/`town` complete on `dialogue.ended`, not on the taught action; claim-loop is step 5 of 7 | `tutorial-steps.json:13,53,65` |
| P1-D | **Folk's Granary: walkable portal into a contentless stub** — not gated by "built" | `DungeonWorldPortalSpawner.cs:116,585`; `DungeonStubEncounter.cs:11,54` |
| P1-E | **Raid: casualties apply ONLY on defeat, never on victory** — a won raid costs nothing; troops infinitely redeployable | `ArmyStorage.ReconcileAfterRaid` called only at `RaidDeployController.cs:474`; `RaidVictoryController.cs:154-189` never reconciles |
| P1-F | **Raid: wounded troops NEVER heal** — `ArmyStorage.TickRecovery(dt)` has ZERO callers repo-wide; army silently shrinks toward unwinnable | `ArmyStorage.cs:232-245` |
| P1-G | **Dungeon: placeholder hero vitals** hardcoded 120/60; hero choice/progression meaningless in the pillar | `DungeonController.cs:119,122,320-322` |
| P1-H | **Dungeon: no visible enemies** — rooms are empty until an invisible trigger pops a warp-to-arena fight | `EncounterTrigger.cs:420`; `DungeonController.cs:972` (WO-770.11 unbuilt) |
| P1-I | **Dungeon: FPV default-ON with unsmoothed look** — raw per-frame drag onto the pivot, no damping; motion-sickness on an unproven camera | `FeatureFlags.cs:642`; `DungeonCameraRig.cs:427-443` |
| P1-J | **Dungeon: crafting is a dead-end** — pedestal consumes ingredients, toasts + glows, but the crafted output is NEVER banked (only raw ingredients are) | `DungeonInventory.cs:149,171`; `DungeonLootGrant.cs:143-171,183` |
| P1-K | **Echoes: Crafting lane is a pickable no-op** — offered in the picker, `CraftingMult` written but read by nothing | `EchoAssignments.cs:60`; `EchoLaneBonuses.cs:19,42` |
| P1-L | **Echoes: only 1 of 4 lanes is real; 3 of 6 spirits can never reach their preferred lane** — Defense/Exploration unconsumed + un-pickable | `EchoLaneBonuses.cs:20-21`; `EchoRosterCatalog.cs:110,126,134` |
| P1-M | **Echoes: no teaching conversation for unlocks #2-6** — only the founding card teaches; the rest are pure lore, so spirits pile up idle | `EchoUnlockFeedback.cs:181-193`; `EchoRosterCatalog.cs:116-153` |
| P1-N | **Monetization: pack convenience tokens advertised but never applied** — instant-build/repair/auto-collect dropped ("no token tray yet") | `packs.json:101-105`; `PackStoreVM.cs:126-128` |
| P1-O | **Monetization: battle pass is orphaned** — no bootstrap, no panel, `PurchasePremiumPass()` uncalled, no XP feed | `BattlePassManager.cs:92-94,130` |
| P1-P | **Monetization: dual-wallet spend asymmetry** — Wood/Iron pool vs GameState Food/Crystals/Coins can drift; must reconcile before money rides the economy | `VillageEconomyRegression.cs:126` (COV-021) |
| P1-Q | **Signposting: no main-quest through-line past the tutorial** — the "main" quest is only the tutorial beats; nothing pushes the player toward the dungeon or raid pillars | `quests.json` (`elarion.welcome` = 2 tutorial stages); `QuestCatalog.cs` |

### P2 — polish
| # | Gap | Where |
|---|---|---|
| P2-A | Town: build QUEUES when slots full but upgrade REJECTS (inconsistent, un-CoC) | `BuildTimerService.cs:229-277` vs `BuildingUpgradeService.cs:70-75` |
| P2-B | Town: Barracks is not a placeable/upgradable catalog structure (absent from `BuildCategoryRegistry`) | `BuildCategoryRegistry.cs:178-232`; `FeatureFlags.cs:589-597` |
| P2-C | Town: Research channel mostly empty — `UnlockTier`/`LearnMagic` have no effect + are never enqueued | `JobKind.cs:40-43`; `BarracksService.cs:305-307` |
| P2-D | Dungeon: dual village→dungeon door systems not collapsed (`DungeonPortal` vs `DungeonEntrance`) | `Village/Buildings/DungeonPortal.cs`; `Village/Dungeons/DungeonEntrance.cs` |
| P2-E | Dungeon: boss back-door reveal is silent + routes to the SAME `ExitToVillage()` (no distinct reward) | `DungeonController.cs:418-422,468-476` |
| P2-F | Dungeon: FPV look-yaw not re-seeded after an interior port (brief disorientation) | `DungeonPortLink.cs:145`; `DungeonCameraRig.cs:337` |
| P2-G | Echoes: harvest silo caps at ~4h → weak "come back tomorrow"; overnight surplus silently discarded | `EchoService.cs:73,302-309` |
| P2-H | Echoes: progression is single-axis (harvest income) and fully solved once 6 owned + set to Harvest | `EchoService.cs:119,419-444` |
| P2-I | Echoes: CS-1 equipped ring/amulet not in the authoritative/cloud save (PlayerPrefs covers local reload only) | `SaveSchema.cs:435,441`; `GameState.cs` (no field); `GearLoadout.cs:232-246,380-393` |
| P2-J | Echoes: founding card teaches "wood/iron/grain" task vocab the picker no longer exposes | `EchoRosterCatalog.cs:108`; `EchoCardVM.cs:161` |
| P2-K | Monetization: no persistent HUD store/gem button — store reached only via a merchant NPC talk (WWCD gap) | `PackStoreBootstrap.cs:67`; `dialogues.json:721` |
| P2-L | Monetization: rewarded ad is a pure stub (grants instantly) + unresolved ethos conflict | `RewardedAdManager.cs:97-100` |
| P2-M | Monetization: Jupiter swap panel fully orphaned (no gameplay caller, no hub bootstrap) | `JupiterSwapService.cs:143`; `JupiterSwapBootstrap.cs:51-56` |
| P2-N | Juice: resource collect fires a VFX celebration + gain popup but NO coin SFX (silent success on the core CoC action) | `CollectorStackView.cs:367`; `ResourceCollector.cs:166-196` |

---

## 2. Area-by-area detail

### 2.1 First-time onboarding / FTUE
Route: Title → HeroSelect → PetSelect → Login → FoundingChoice → hub (MainCastle_Hall) → in-hub V2 tutorial.

**Working / correct (not gaps):**
- **Enemy gate is CORRECT — no P0.** The peace window holds only while the hero is in-town-early
  (`TutorialFlow.HostilesSuppressedForTutorial`, `TutorialFlow.cs:148-166`), consumed by `WaveManager.BeginLoop`
  (`WaveManager.cs:637-641`). The teaching wave at `founding_defend` fires AFTER the tower step via
  `TutorialWaveSpawner` (bypasses the gate). No path spawns ambient enemies before the tower step, and none
  leaves the world permanently empty (post-onboard auto-arm at `WaveManager.cs:533`). The old "enemies never
  spawn the whole run" bug is genuinely fixed by decoupling suppression from `!Onboarded` alone.
- **Founding choice is mechanically meaningful** (not cosmetic): "Default Town" migrates the baked ring into
  movable records and skips `skipIfPrebuilt` build-steps (still applying grants); "Build Your Own" runs them in
  full (`FoundingChoiceController.cs:214-251`; `TutorialFlow.cs:427-435`).
- **No hard softlock** in the chain: every step is skippable + a 120s watchdog auto-advances.
- **Deferred:** the venture-out / arena back-half was scrapped ("END-AFTER-DEFEND", `TutorialFlow.cs:120-133`);
  the dangling `ArenaWin`/`TickStagedEncounter` code is inert, not a live gap.

**Gaps:** P1-A (first-tower affordability), P1-B (bootstrap single-point-of-failure), P1-C (claim-loop told/late),
P1-D (Folk's Granary stub). Plus P2: objective text implies actions the step doesn't require (`founding_greet`
"Talk with Sylas" auto-plays; `founding_defend` "clear the wave" auto-spawns — `TutorialFlow.cs:488-489,516-526`);
watchdog can mask several broken steps as ~10 min of dead banners; spotlight anchors for HUD/build keys
(`hud.build_button`, `build.card.lumberyard`) are unverified — if unregistered the "point at what to tap" cue
silently no-ops. Note: `OnboardingFlow` coach-marks are dead scaffolding wired only into the abandoned
`Village.unity`, never the live hub (`CompanionMeetingTrigger.cs:7`).

### 2.2 Core town loop — build → collect → upgrade → unlock
**Verdict: the loop spine is CLOSED and functional.** Build (`BuildModeController.cs:1663-1804`, affordability
gate + shortfall message + first-build freebie + 50% sell refund) → collect (`ResourceBuildingHarvester.cs:59-147`
→ `ResourceCollectorService.CollectAll`) → upgrade (`BuildingUpgradeService.TryUpgrade`) → unlock (VillageTier
gate). **No hard economic softlock** — free-first-build + level-1 income + sell refund give recovery from zero.
- **Multi-channel queue is GENUINELY real** (pain-point 3.2 resolved): independent Builder/Train/Research
  channels, each with own slots + FIFO (`ObsidianQueueState.cs:33-54`; `ObsidianQueueEngine.Resolve` per-channel).
  "Can train while a wall upgrades" is satisfied. **Offline-fair catch-up exists** (`BuildTimerService.cs:96,563-575`)
  — a real retention hook.

**Gaps:** P0-A (queue panel unreachable — the whole multi-channel view + reorder is invisible), P2-A/B/C.

### 2.3 Dungeon pillar
**Verdict: functional end-to-end loop, no P0 softlock.** Exits are always reachable (Cottage spawns a normal
exit at entry-room centre; composed dungeons auto-inject a return via `DungeonExitSpawner`); combat handoffs
roll back the lock on failure (`EncounterTrigger.cs:349-366`); lore/craft/checkpoint toasts + the lore modal are
wired (WO-770.4/770.7). Combat routes to real-time `BattleArena` (`ff.dungeonrealtime` default TRUE).

**Gaps:** P1-G (placeholder vitals), P1-H (no visible enemies), P1-I (FPV default-on unsmoothed), P1-J (crafting
dead-end), plus the P1-D Granary stub. P2: P2-D (dual doors), P2-E (silent boss door + same destination), P2-F
(FPV yaw drift post-port). Note FPV *does* take anti-nausea measures (pitch clamp, no head-bob, joystick-half
reserved, over-the-shoulder forced in fights) — the gap is specifically the un-damped look layer shipped as default.

### 2.4 Raid V1 (CoC PvE deploy loop)
**Verdict: BUILT end-to-end and playable** (see §0.1). The four WO-774 P0s are all real and confirmed
(P0-C loadout, P0-D deploy-ring, P0-E naming, P0-F copy). Two army-economy gaps WO-774 did NOT call out:
P1-E (victory never applies casualties) and P1-F (`TickRecovery` never called → wounded never heal — borderline
P0, degrades toward unwinnable).
- **Deferred (NOT gaps):** deterministic fixed-point `RaidSim` / async PvP / server re-sim (V2); scout report,
  2× speed toggle, army presets, post-raid shields, structure-% destruction (WO-774 ladder P1/P2/V1.5).
- Flags verified correct: `ff.raidwalk` OFF (`FeatureFlags.cs:88`), `ff.overworldencounter` OFF (`:154`),
  `ff.barracks` ON (`:597`).

### 2.5 Echoes / progression / meta / retention
**The headline collection system is ~75% stubbed.** One working lane (Harvest — `EchoLaneBonuses.HarvestBonusMult`
is the only consumed multiplier); three inert (P1-K Crafting pickable-no-op, P1-L Defense/Exploration unconsumed +
half the roster unplaceable); per-unlock teaching absent (P1-M). Progression is single-axis and capped (P2-G/H).
- **Retention hooks that ARE present:** offline harvest + `WelcomeBackPopup`, silo dump-to-claim, offline-fair
  build/train/research timers, 3-slot daily quests with reroll + reward (`DailyQuests.cs`), rewarded-ad daily-cap
  skips. **Deferred by design:** NO login streak/calendar/energy ("no streak guilt, no FOMO", `DailyQuests.cs:176`);
  Defense/Exploration lanes deliberately hidden pending design (owner ruling 2026-07-24). So the "come back
  tomorrow" hook exists but leans entirely on the ~4h silo + 3 dailies.
- CS-1 nuance (P2-I): accessories DO survive local reload via PlayerPrefs; what's broken is the authoritative/
  Neon-cloud save path — dead `SaveSchema` fields, no device-migration / cloud-restore round-trip.

### 2.6 Monetization touchpoints
**The crypto layer being stubbed is correct (V1 = zero crypto).** The real problem: **the TIME-selling model the
design nominates as V1's revenue engine has no player surface** — P0-B (buy-slot/instant-finish/ad-skip are
service+config only), P0-A (the queue panel that would host them can't be opened), P1-N (pack convenience tokens
never applied). The store shelf renders but is browse-only ("Coming soon" in release, correct); battle pass is
orphaned (P1-O); Glimmer currency + Cosmetic Shop ARE functional. Dual-wallet asymmetry (P1-P) must be reconciled
before real money rides the economy.

### 2.7 Cross-cutting feedback / juice / signposting
**Feedback layer is largely healthy:** damage numbers + combat text (`DamageNumberSpawner`, `CombatTextLayer`,
`CombatFeedbackManager`), wave-win juice (victory sting + "WAVE n REPELLED" banner + Heart pulse + wall-repair
nudge, `WaveFeedbackDirector.cs`), wave-imminent alert (vignette + sting + haptic), build-reject toast + denied buzz
(`BuildFeedbackToast.cs`), broad `GameSfx` library, resource-gain popup + collect celebration VFX. Signposting exists
via the tutorial objective banner, a quest-tracker HUD icon → Rumor Board (`QuestTrackerHud.cs`), and daily-quest chips.

**Gaps:** P1-Q (no main-quest through-line past the tutorial toward the dungeon/raid pillars — the rich vendor
quests in `quests.json` are side/gear flavor, many referencing mechanics of uncertain wiring), P2-N (silent
resource collect SFX). Minor: the quest tracker is minimized to an icon (owner ruling) so there's no persistent
on-screen "what next" outside the tutorial — the Rumor Board is one tap behind an easily-missed medallion.

---

## 3. Highest-leverage fix order (recommendation)
1. **Surface the queue + its time-sink buttons** (P0-A + P0-B together) — one HUD entry point to
   `ObsidianQueueGate` plus buy-slot / instant-finish / ad-skip buttons on the queue panel. Fixes the invisible
   headline feature AND the missing revenue path in one lane.
2. **Raid V1 felt-slice = WO-774** (P0-C/D/E/F) + fold in P1-E/P1-F (wire `TickRecovery`; reconcile casualties on
   victory) so the army economy actually has stakes.
3. **FTUE resilience + affordability** (P1-A restore a `prepaidTower` grant on `founding_defense`; P1-B a
   force-finish fallback if the flow never enters a step) — protect the first 10 minutes.
4. **Echo meta honesty** (P1-K/L/M) — either wire Crafting to a real consumer or pull it from `PickableLanes`,
   and add a per-unlock teaching beat for #2-6.
5. **Signposting through-line** (P1-Q) — a main quest chain that hands the player from town → first dungeon →
   first raid, so the pillars aren't discovered by accident.
6. Dungeon felt holes (P1-G vitals, P1-I FPV default, P1-J crafting output) and the Folk's Granary gate (P1-D).

---
*Single QA report, 2026-07-26. Read-only walk; no code/data changed. Evidence cited from HEAD on
`wip/village2-and-f8-tickets`. Deferred-by-design items are labeled and excluded from the gap ranking.*
