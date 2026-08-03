# QA Test Plan — Echoes of Elarion / Defenders of the Realm **V1**

**Project:** Echoes of Elarion (Defenders of the Realm) — Unity 6 (6000.4.8f1) / URP
**Owner / PO:** Samantha Denelle (DeNelle Studios)
**Anchored to:** `CANON_GROUND_TRUTH_2026-06-26.md`, `docs/COMBAT_PIVOT_NORTHSTAR.md`, `SESSION_CANON_LOADER.md`
**Pipeline:** QA (read-only RCA) → CLI (implement + headless-verify) → PO (felt-verify + close) — `docs/TICKET_PIPELINE.md`
**Status:** Living document. Created 2026-06-28. **This supersedes the v2-port-era `docs/qa/qa-test-plan.md`** (Blaise/Mage, Avalon, dungeons, pets, Solana-devnet build order) for V1 scope — see the SUPERSEDED banner there.

> **V1 north star (what is under test):** you control **ONE hero (Knight "Grom")**. Boot → hero-select →
> **MainCastle_Hall** hub (with **OuterWorld** streamed additively) → walk into a wandering rep → **isolated
> real-time BattleArena** → **Victory + star rating + reward** → return home. Equip/inventory, the 68-node
> talent tree, the village wave/defense loop, save/load (schema **v27**), and the monetization fulfilment rails
> (pack grant, SKR cosmetics, covenant firewall) are the supporting systems. Base-/tower-defense base-building is
> **V2-gated** behind `ff.basebuilding`. ATB is a **separate, flat** mode (single hero vs static enemies).

---

## 0. How to use this plan

Every test case carries:

- **ID** — `TC-<AREA>-NN`.
- **Verify-by** — which existing headless tool proves it, OR `FELT` (PO judges in the human-path build), OR
  `GAP` (no oracle exists — see §11).
- **Marker / signal** — the exact grep token or artifact that decides PASS/FAIL.
- **Status** — `AUTO` (a headless oracle asserts it today) · `PARTIAL` (a precondition is automated, the felt
  behaviour is not) · `FELT` (PO-only, human-path build) · `GAP` (needs a new oracle, §11).

**Cadence**
- **Per commit / per CLI hand-off:** run the four batchmode gates (§1 G1–G4). All must show their OK marker.
- **Per build candidate:** run the AutoPilot fleet (§1 G6) + emit tickets (§1 G7), then harvest.
- **Whenever the PO felt-tests:** arm the F8 live-triage watcher (§1 G8) so every flag/error surfaces on the
  CLI the instant it lands (CLAUDE.md §14).
- A red marker on any gate is a regression: open a Task (ticket) per `docs/TICKET_PIPELINE.md` and route to CLI.

**Golden rule:** Unity **editor CLOSED** for every batchmode command (project lock). Judge by the **marker**, not
the wrapper exit line (the transient "license 505" line at shutdown is noise — `run-defenders` SKILL §Gotchas).

---

## 1. Headless tool inventory (the oracle families this plan maps to)

| # | Gate / tool | Invocation | PASS marker | What it proves |
|---|-------------|-----------|-------------|----------------|
| **G1** | **CompileGate** | `run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate.log` | `COMPILE_GATE_OK` | Whole tree compiles; **no NUL-byte** mount-garble (CLAUDE.md §0/§1). |
| **G2** | **DataRegression** (**THE** gate) | `…-Method DeNelle.Editor.DataRegression.RunAll` | `REGRESSION_OK <n>/<n> suites` / `REGRESSION_FAIL: <n>` | Real catalogs → objects: gear, abilities, enemies, structures, buildings, **item-capability invariants**, crafting chain, jeweler chain, **talent layout**, **armed-hero**, **hand-slot rules**, battle-closing audio+stars, weapon/armor VFX, accessories, enemy structure-sweep. (`Assets/Editor/Regression/DataRegression.cs`) |
| **G3** | **SessionRegression** | `…-Method DeNelle.Editor.SessionRegression.RunAll` | `SESSION_GUARDS_OK 6/6 checks` / `SESSION_GUARDS_FAIL` | Vendor contract, starter weapons, enemy/structure prefab resolve, **save round-trip (pet/settlement/economy/name)**, general-vendor stock. (`SessionRegression.cs`) |
| **G4** | **RegressionSuite** (per-check-in battery) | `…-Method DeNelle.Editor.RegressionSuite.RunAll` (batch exit 0/1) | `CHECKIN_SUITE_OK <p>/<n> cases` + per-case PASS list | compile, catalog parse / **byte-equal Resources↔StreamingAssets**, data parse, catalog ids, prefab resolve, perf-lint (fork-bomb), Yarn `command:` lint, **Village2 opens clean**, core wiring (WaveManager+reward fields), **4 WO-373 critical gates** (Tree@origin, WASD camera-relative, scene clean, CameraYaw authority), **castle gate exitable (NavMesh)**, dialogue prefab, battle music. (`RegressionSuite.cs`; also `VerifyCriticalGates` → `CRITICAL_GATES_OK`.) |
| **G5** | **ArenaCombatOracle** | `…-Method DeNelle.Editor.ArenaCombatOracle.Run` | `ARENA_ORACLE_OK` / `ARENA_ORACLE_FAIL` | Drives the **REAL** `BattleArena.Resolve`: victory/defeat **music requested**, **stars + reward multiplier** computed, **win reward granted** to the loadout, rarity **swing-trail** color applied. (`ArenaCombatOracle.cs`) |
| **G6** | **AutoPilot fleet** | `run-autopilot-fleet.ps1 -Count 12 -SeedStart 1000 -TimeoutMin 15` | per-run `autopilot-summary.json` + `break-log.jsonl` | Drives boot→hero→gates→vendors→economy→equip→**save round-trip**→HUD→wave→exit→**OuterWorld outpost**→**encounter→BattleArena real path** through real seams; phases self-assert via `FlowTrace.Fail`. Seeded chaos (distinct paths per seed). (`AutoPilotDriver.cs`) |
| **G7** | **AutoPilotTickets** | `…-Method DeNelle.Editor.AutoPilotTickets.Emit` | `AUTOPILOT_TICKETS_OK: <n>` | Triages the fleet break-logs → ranked, deduped `Builds/autopilot-tickets.{md,json}` (by distinct-run repro count; `AUTOPILOT_MIN_RUNS` threshold). |
| **G8** | **F8 live-triage watcher** | `bash .claude/skills/run-defenders/f8-watch.sh` (Bash tool, `run_in_background:true`); harvest via `harvest.sh` | auto-harvested `[Flow:*]`/Guard/exception context on first real capture | Surfaces every PO **F8 flag / error / softlock** live during felt-test; auto-harvests the captured trace (CLAUDE.md §14). |
| **G9** | **EditMode test suites** | `Unity.exe -runTests -batchmode -testPlatform EditMode -testResults …xml` | NUnit XML green; non-zero exit on any fail | `DeNelle.Core.Tests` (save/load + `SaveMigrator` + `SaveSchema.Validate` + RNG golden), `DeNelle.BattleATB.Tests` (combat math/turn/AI/scaling), `DeNelle.Data.Tests` (every catalog loader + stray-markup scan), `DeNelle.Wallet.Tests` (stub provider, service guards, registry). (`docs/qa/regression-suite.md`) |

> **Headless coverage ceiling (known, by design):** the fleet is `-nographics` (no pixels — `break_*.png` are
> blank) and **hub-capped** (MainCastle_Hall + a Village2 warp; the open-world walk/combat loop is exercised by
> the `WalkToOuterWorldOutpost` + `AssertEncounterBattle` warp-driven phases, not a full on-foot traversal —
> "no outpost realized — skipped" is EXPECTED on a plain hub run). Render bugs (magenta, VFX, UI fidelity) and
> UITK panels **cannot** repro headless → they are `FELT` rows. `break-log` captures **error-level only**
> (`FlowTrace.Fail`/exception/softlock/F8) — a non-error oracle must emit `FlowTrace.Fail` on violation to land.

---

## 2. Boot → Hero-Select → Hub → Arena → Victory → Reward (the V1 spine)

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-LOOP-01 | App boots to gameplay (Title→HeroSelect→PetSelect→MainCastle_Hall) | G6 `BootToGameplay` (loads MainCastle_Hall direct, UI flow skipped headless) + FELT (full UI path) | summary `BootToGameplay=ok`; FELT: scenes advance, no hang | PARTIAL |
| TC-LOOP-02 | Hero-select carousel renders; **Knight selectable** (WO-559, Knight-only V1) | FELT (UITK carousel, human path) | PO: class column + rotating hero + specs; Knight chooses | FELT |
| TC-LOOP-03 | Hero resolves + is controllable in the hub | G6 `ResolveHero` | summary `ResolveHero=ok`; `[Flow:Auto] hero '…' at …` | AUTO |
| TC-LOOP-04 | Hero is **armed at spawn** (no unarmed Knight, WO-425) | G2 armed-hero invariant; G3 starter-weapons | `REGRESSION_OK`; no `BestWeapon('knight',1) returned NULL` | AUTO |
| TC-LOOP-05 | Hero animates (animator re-cache fix, WO-581) | FELT | PO: idle/run/attack clips play, no T-pose | FELT |
| TC-LOOP-06 | **OuterWorld** streams in additively under the hub | G6 (BootToGameplay triggers WorldSceneLoader); G4 `castle-gate-exitable` | summary boot ok; `REGRESSION_OK` (NavMesh path hub→seam) | AUTO |
| TC-LOOP-07 | Hub→OuterWorld **seam crossing** warps the hero (RuntimeRegionGate) | G6 `AssertHeroCrossing` / `AttemptExitCastle` | `CROSSED '<id>' — real warp jump`; no `NO warp` Fail | AUTO |
| TC-LOOP-08 | Wandering rep spawns in OuterWorld via the **real** spawner | G6 `AssertEncounterBattle` (`OverworldEncounterSpawner.ForcePopulateForTest`) | `real rep '…' spawned`; no `NO OrcRep_* spawned` Fail | AUTO |
| TC-LOOP-09 | Touch the rep → **drops into isolated BattleArena** (real watcher engage) | G6 `AssertEncounterBattle` | `dropped to battle (BattleInProgress=true)`; no `did NOT drop to battle` Fail | AUTO |
| TC-LOOP-10 | Arena stages the **orc family** — skinned mesh + Orc animator (no capsule/T-pose) | G6 `AssertEncounterBattle` | `N orcs spawned, all skinned + Orc-rigged`; no `fell back to a CAPSULE` Fail | AUTO |
| TC-LOOP-11 | Win resolves → **victory music** + **star rating** + **reward multiplier** | G5 ArenaCombatOracle; G2 battle-closing | `ARENA_ORACLE_OK`; `REGRESSION_OK` (stars 60s→3×1.50 … 200s→1×1.00) | AUTO |
| TC-LOOP-12 | Loss resolves → **defeat music** (not silent) | G5; G2 battle-closing audio | `ARENA_ORACLE_OK`; no `Resources.Load<AudioClip>("defeat") is NULL` | AUTO |
| TC-LOOP-13 | Win **grants a reward** into the hero loadout (XP/gear) | G5 (`GrantWinReward` FlowTrace) | `ARENA_ORACLE_OK` (GrantWinReward line present) | AUTO |
| TC-LOOP-14 | Battle resolves and **returns the hero home** to the engagement spot | G6 `AssertEncounterBattle` | `resolved=true heroReturn=<≤5m>`; no `loop stuck` / `NOT returned` Fail | AUTO |
| TC-LOOP-15 | **Victory screen** shows the crown/star tier row (Task #41) | FELT | PO: star row + reward read correctly | FELT |
| TC-LOOP-16 | Arena spell-cast VFX is not the placeholder purple cubes (Task #44) | FELT | PO: cast VFX acceptable | FELT |
| TC-LOOP-17 | Battle HUD ↔ combat binding (Start Wave→HUD, countdown, auto-attack, lock-on) — Task #43/#37 | FELT (+ G6 `AssertCombatInvariants` for the wave-defense window) | PO: HUD reads live HP/timer; `AssertCombatInvariants=ok`/`N/A` | PARTIAL |

---

## 3. Equip / Inventory / Gear

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-EQUIP-01 | weapons.json + armor.json + accessories.json map to non-empty, named objects | G2 (gear + accessories checks) | `REGRESSION_OK`; no `deserialized to 0 objects` / `null/empty id or name` | AUTO |
| TC-EQUIP-02 | Accessories: exactly **10**, within non-legendary caps, every entry has an icon | G2 `CheckAccessories` | no `expected 10` / `>= 0.20 cap` / `no iconPath` Fail | AUTO |
| TC-EQUIP-03 | **Hand-slot rules**: 1H+shield coexist; 2H clears off-hand; shield-over-2H falls back to a 1H (never unarmed) | G2 `CheckHandSlotRules` (drives real `GearLoadout`) | no `[hand-slot]` Fail lines | AUTO |
| TC-EQUIP-04 | Item-model **capability invariants** (Weapon/Armor=Carriable\|Equippable, Consumable=Carriable\|Usable, never AI) | G2 `CheckItemCapabilities` | no `must retain Carriable\|…` / `BOTH Carriable and AI` Fail | AUTO |
| TC-EQUIP-05 | Equipping actually **changes the hero loadout/stat** | G6 `AssertEquip` | summary `AssertEquip=ok`; no equip Fail | AUTO |
| TC-EQUIP-06 | Owned gear **populates the inventory grid** (WO-573/WO-578) | FELT + GAP (no oracle reads the inventory VM grid) | PO: portrait + owned rows render | FELT/GAP |
| TC-EQUIP-07 | Gear Preview paper-doll: central 3D hero + Obsidian slot plates; off-hand=shields only (WO-582) | FELT | PO: layout matches Obsidian character reference | FELT |
| TC-EQUIP-08 | Off-hand slot **delineated** (sword/shield/1H), one ring/amulet | G2 hand-slot (off-hand exclusivity) + FELT (visual delineation) | hand-slot pass + PO visual | PARTIAL |
| TC-EQUIP-09 | Ring + amulet equip **persists** across save/load (schema v26) | G9 Core save round-trip; G6 `AssertSaveRoundTrip` | EditMode green (`equippedRingId`/`equippedAmuletId`) | AUTO |
| TC-EQUIP-10 | Gear store stock matches the **vendor contract** (forge=Weapon, armorer=Armor, market=Potion, jeweler=Armor) | G3 vendor-contract; G6 `AssertVendorContracts` | `REGRESSION_OK`; no `VendorStockContract.AllowedFor` mismatch / contract violation | AUTO |
| TC-EQUIP-11 | A buy **deducts cost AND grows inventory** | G6 `AssertEconomyDeduct` | summary `AssertEconomyDeduct=ok` | AUTO |
| TC-EQUIP-12 | Rarity reads through **swing-trail (weapon)** + **rim-light (armor/accessory)** VFX mapping | G2 `CheckWeaponVfx` + `CheckArmorVfx` | no `same trail color` / `does not escalate` / `!= GoldColor` Fail | AUTO |

---

## 4. Talents (68-node tree v2 — 3 heroes × 20 + 8 shared)

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-TAL-01 | hero-talents.json **layout integrity**: x/y both-or-neither, positions in 0..1, every prereq/edge id resolves | G2 `CheckTalentLayout` (knight/ranger/mage + shared) | no `hero-talents.json:` Fail lines | AUTO |
| TC-TAL-02 | Talent catalog loads all trees (Data.Tests loader) | G9 `DeNelle.Data.Tests` | EditMode green | AUTO |
| TC-TAL-03 | Talent panel opens + actuates without throwing (HUD panel) | G6 `OpenEachHUDPanel` | summary `OpenEachHUDPanel=ok`; no panel Fail in break-log | PARTIAL |
| TC-TAL-04 | **Talent effects apply** to the hero (passive stat + active skill unlock) at runtime | GAP (no oracle drives `HeroTalentModifiers` apply→stat delta) | — | GAP |
| TC-TAL-05 | Quick-swap assign + passive/active clarity (WO-574) | FELT | PO: assign sticks, passive vs active legible | FELT |
| TC-TAL-06 | Allocated talents **persist** across save/load | GAP (confirm a save field guards allocation) + FELT | — | GAP/FELT |

---

## 5. Wave / Arena defense loop (Village2 raid target)

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-WAVE-01 | Village2 scene **opens clean** (0 missing scripts, hero present) | G4 `scene-opens-village2` + `critical-gate-scene-no-errors` | per-case PASS | AUTO |
| TC-WAVE-02 | **Core wiring**: WaveManager present + WO-330 reward fields; EconomyService type resolves | G4 `core-wiring-village2` | per-case PASS | AUTO |
| TC-WAVE-03 | Tree of Life / Heart sits at **world origin** | G4 `critical-gate-tree-origin` | per-case PASS | AUTO |
| TC-WAVE-04 | Start Wave → wave **begins**, phase advances | G6 `TriggerWave` (`WaveManager.ForceBeginNextWave`) | summary `TriggerWave=ok` (or `N/A` in a no-wave hub) | AUTO |
| TC-WAVE-05 | **Combat invariants** during the wave: hero HP never negative while alive, ≥1 tower fired, ≥2 enemy types | G6 `AssertCombatInvariants` | summary `=ok`/`N/A`; no invariant Fail | AUTO |
| TC-WAVE-06 | Garrison roster (village2_stronghold) builds via the **canonical EnemyFactory path** — no magenta, no tipped troll | G6 `DiagGarrisonRoster` | `roster built N/N, magenta=0, tipped=0`; no `magenta=true` | AUTO |
| TC-WAVE-07 | Every catalog **enemy id resolves to a real prefab** (no tinted-capsule fallback) | G2 `CheckEnemies`; G3 enemy-models | no `Resources.Load…("Enemies/<m>") is NULL` Fail | AUTO |
| TC-WAVE-08 | Every **structure/tower visualPrefabPath resolves** | G2/G3/G4 prefab-resolve | no `loads NULL (structure would build with no mesh)` Fail | AUTO |
| TC-WAVE-09 | Wave-clear **pays out** (reward formula) → build/upgrade **spends** | GAP (RegressionSuite footer: needs PlayMode); partly G6 economy-deduct | PlayMode follow-up not built | GAP |
| TC-WAVE-10 | Walls **block via NavMesh carve** (enemies route through chokepoints, don't tunnel thin gaps) | FELT + GAP (no nav-tunnel oracle) | PO: enemies path the maze | FELT/GAP |
| TC-WAVE-11 | Battle music tracks resolve (combat not silent) | G4 `battle-music-resolves` | per-case PASS | AUTO |
| TC-WAVE-12 | Base-building stays **V2-gated** off by default (`ff.basebuilding`) | GAP (no flag-default oracle) + FELT | — | GAP/FELT |

---

## 6. Save / Load (schema v27)

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-SAVE-01 | Save round-trips every persisted field (all 41+) | G9 `DeNelle.Core.Tests` `SaveLoadRoundTripTest` | EditMode green | AUTO |
| TC-SAVE-02 | **SaveMigrator** v1→v27 chain transforms old saves with no data loss; rejects newer/NaN versions | G9 `SaveMigratorTest` | EditMode green | AUTO |
| TC-SAVE-03 | `SaveSchema.Validate` clamps NonNeg/Finite + rejects NaN/Infinity with the field path | G9 `SaveSchemaValidateTest` | EditMode green | AUTO |
| TC-SAVE-04 | `Reset()` carve-out preserves wallet/social, wipes progression | G9 `ResetCarveOutTest` | EditMode green | AUTO |
| TC-SAVE-05 | **Live play → quicksave → reload** preserves wallet/roster/quest | G6 `AssertSaveRoundTrip` | summary `AssertSaveRoundTrip=ok` | AUTO |
| TC-SAVE-06 | Headless save round-trip preserves pet/settlement/economy/pet-name | G3 `CheckSaveRoundTrip` | `REGRESSION_OK` | AUTO |
| TC-SAVE-07 | Echo workforce (echoCount + siloResources + wavesCompleted, v25) survives reload incl. offline clock | G9 Core (v25 fields) + FELT (offline accrual) | EditMode green; PO: offline harvest credited | PARTIAL |
| TC-SAVE-08 | Building tiers + research perks (v23/v24) survive reload → compiled into GameModifiers | G9 Core (v23/v24 fields) | EditMode green | AUTO |
| TC-SAVE-09 | Wall-mounted defense seating (v27: worldY + wallMounted) persists on the wall top | G9 Core (v27 fields) + FELT | EditMode green; PO: defender stays on wall | PARTIAL |
| TC-SAVE-10 | New Game → quit → relaunch resumes identical state (HP, resources, wave, ownership) | FELT (full app kill/relaunch) | PO: state restores, no corrupt-save crash | FELT |
| TC-SAVE-11 | Pack-purchase entitlement persists (OwnedItemIds round-trips after `service.Save()`) | G9 Core + §7 fulfilment chain | EditMode green; `Store … recorded owned` (no entitlement Fail) | AUTO |

---

## 7. Monetization fulfilment — pack grant, SKR, covenant firewall

> **Architecture (read `Assets/_Modules/Wallet/`):** `PackStore.Purchase` → `WalletService.Pay` (devnet
> `StubWalletProvider`, no Solana SDK needed) → on `result.Ok` → `ApplyPackContents` (economy top-up +
> `OwnedItemIds` + `GameStateService.Save()`). The grant path **self-reports** via `FlowTrace.Fail` on every
> failure mode (no GameState, entitlement not recorded post-grant, purchase threw after charge). SKR is a
> third currency rail (`CurrencyKind.Skr`) plus a cosmetic store (`skr_store.json`) and staking
> (`skr_staking.json`). **Devnet is the hard default; Mainnet is owner-gated.**

### 7a. Pack grant / fulfilment

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-MON-01 | packs.json maps to the 5 packs (Hearth Spark→Founder's Vow) with per-currency amounts | G9 `DeNelle.Wallet.Tests` / `DeNelle.Data.Tests` (PackCatalog) | EditMode green | AUTO |
| TC-MON-02 | Purchase flow: connect → Pay → confirm → **ApplyPackContents** on the stub | G9 `StubWalletProviderTest` + `WalletServiceTest` | EditMode green | AUTO |
| TC-MON-03 | On confirmed pay, **economy top-up lands** (crystals/food/coins into Resources) | GAP (no oracle drives PackStore.ApplyPackContents end-to-end) + FELT | — | GAP/FELT |
| TC-MON-04 | On confirmed pay, **SKU + cosmetic SKUs recorded owned** and the SKU survives save | GAP (assert `OwnedItemIds.Contains(sku)` after grant) + §6 TC-SAVE-11 | — | GAP/PARTIAL |
| TC-MON-05 | **No-GameState / entitlement-not-recorded self-report** fires (paid-but-no-grant is never silent) | FELT via G8 (the FlowTrace.Fail lands in break-log) + GAP (no positive oracle) | break-log carries `ApplyPackContents: … entitlement did NOT take` if it regresses | GAP |
| TC-MON-06 | Owned pack shows **"Owned"** (no double-charge) | G9 (IsOwned logic via state) + FELT | EditMode green; PO: re-buy blocked | PARTIAL |
| TC-MON-07 | Purchase **failure path** (cancel/insufficient) charges nothing + shows a clean error | G9 `StubWalletProviderTest` insufficient-funds path | EditMode green | AUTO |

### 7b. SKR rail (cosmetic store + staking)

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-SKR-01 | `skr_store.json` loads; every entry is **cosmetic / convenience** with a pointer `grant` (no inlined binary) | GAP (no SKR-store loader oracle) | — | GAP |
| TC-SKR-02 | SKR balance debit/credit/affordability (ArenaWalletService) is consistent | G9 (if covered) + GAP (no dedicated SKR-wallet oracle) | — | GAP |
| TC-SKR-03 | `skr_staking.json` config loads; `spendableNeverAutoLocked`, 48h cooldown, rebate **gated off** until legal sign-off | GAP (the JSON references a `SkrStakingRegression` build gate that **does not exist as a .cs**) | — | **GAP (named-but-absent oracle)** |
| TC-SKR-04 | Staking copy contains **no forbidden finance language** (APY/yield/returns/interest/guaranteed) | GAP (no string-scan oracle over the staking copy) | — | GAP |

### 7c. Covenant firewall (cozy-covenant compliance)

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-COV-01 | "You are never required to spend anything. Ever." renders verbatim in the store | G6 `OpenEachHUDPanel` (store opens) + FELT (text present) | summary store ok; PO: line present | PARTIAL |
| TC-COV-02 | **No combat-power for sale** — every pack/SKR grant is cosmetic/economy/convenience (TIME-SAVING only) | **GAP** (no oracle scans pack + SKR grant kinds for a combat/stat `kind`) | — | **GAP (highest-value new oracle)** |
| TC-COV-03 | **No loot boxes / gacha / randomized purchase / energy systems** | GAP (no structural scan; manual review today) + FELT | — | GAP/FELT |
| TC-COV-04 | Network is **Devnet by default; Mainnet hard-blocked** (`WalletService.DefaultNetwork == Devnet`; `SolanaWalletProvider.SendPayment` refuses Mainnet) | G9 `WalletServiceTest` (devnet default + mainnet guard) | EditMode green | AUTO |
| TC-COV-05 | **No secrets in repo** — `wallets.json` holds only public base58 addresses; zero keys/seed phrases | G9 `WalletRegistryTest` + GAP (git-history scan not automated here) | EditMode green | PARTIAL |
| TC-COV-06 | Rewards Distributor address shown for **transparency**, never used as a payment destination | G9 (RewardsDistributorAddress surface) + FELT | EditMode green; PO: label present, not a recipient | PARTIAL |

---

## 8. Cross-cutting integrity (every build candidate)

| ID | Test | Verify-by | Marker / signal | Status |
|----|------|-----------|-----------------|--------|
| TC-XC-01 | Tree compiles; no NUL-byte garble | G1 | `COMPILE_GATE_OK` | AUTO |
| TC-XC-02 | Canonical JSON copies **byte-equal** (Resources ↔ StreamingAssets) | G4 `catalog-byte-equal` | per-case PASS | AUTO |
| TC-XC-03 | No stray agent markup in any canonical JSON | G9 `DeNelle.Data.Tests` integrity scan | EditMode green | AUTO |
| TC-XC-04 | No per-frame **fork-bomb / re-entrant-submit** smell | G4 `perf-lint-reentrancy` | per-case PASS | AUTO |
| TC-XC-05 | No invalid Yarn `<<command: …>>` prefix | G4 `yarn-command-prefix` | per-case PASS | AUTO |
| TC-XC-06 | No duplicate landmine classes (DoorController, Core.Debug/Addressables shadow) | G4 `no-duplicate-landmines` | per-case PASS | AUTO |
| TC-XC-07 | Castle home-hub is **NavMesh-exitable** (the recurring "can't leave the castle" bug) | G4 `castle-gate-exitable` | per-case PASS | AUTO |
| TC-XC-08 | Fleet run produces **zero confirmed bug/hang tickets** | G6 + G7 | `AUTOPILOT_TICKETS_OK: 0` (0 confirmed) | AUTO |
| TC-XC-09 | No render artifacts misread as bugs (magenta/VFX) — confirm via human path, not fleet | FELT (G7 filters `-nographics` artifacts) | PO: visuals clean | FELT |
| TC-XC-10 | UI fidelity: Blink Obsidian frames / shared kit / chrome (Task #40) | FELT | PO: panels match Blink template canon | FELT |

---

## 9. Coverage summary

| Area | Cases | AUTO (headless today) | PARTIAL | FELT / GAP |
|------|-------|----------------------|---------|------------|
| §2 Boot→Arena→Reward spine | 17 | 11 | 2 | 4 |
| §3 Equip / Inventory | 12 | 9 | 2 | 1 |
| §4 Talents | 6 | 2 | 1 | 3 |
| §5 Wave / Arena defense | 12 | 8 | 0 | 4 |
| §6 Save / Load | 11 | 8 | 2 | 1 |
| §7 Monetization (pack/SKR/covenant) | 17 | 5 | 4 | 8 |
| §8 Cross-cutting | 10 | 8 | 0 | 2 |
| **Total** | **85** | **51** | **11** | **23** |

~**60%** of V1 is asserted by an existing headless oracle today. The biggest automated-coverage holes are
**monetization fulfilment depth**, the **covenant firewall**, **talent-effect apply**, and the **PlayMode
wave-economy loop** — see §11.

---

## 10. Run order (the standing QA cycle)

1. **Gate** (CLI, editor closed): G1 → G2 → G3 → G4 → G5. Require every OK marker. Any `REGRESSION_FAIL` /
   `ARENA_ORACLE_FAIL` → open a ticket, route to CLI, do not advance.
2. **EditMode suites** (G9): run on integration commits touching Core/ATB/Data/Wallet.
3. **Build** the Windows player (wipe `Builds/Windows` first — `run-defenders` SKILL §Run step 3).
4. **Drive** (G6): `run-autopilot-fleet.ps1 -Count 12 -SeedStart 1000`. **Emit** (G7) → read
   `Builds/autopilot-tickets.md`. **Harvest** (`harvest.sh`).
5. **Felt-test handoff** (PO): CLI **arms G8** (`f8-watch.sh`, background) before the PO plays; triage every fire
   LIVE from the auto-harvested trace (CLAUDE.md §14). PO closes felt rows; CLI never self-closes (§13).

---

## 11. Gaps needing new oracles (prioritized)

Ordered by risk × how cheaply a headless oracle closes it. Each is a candidate WO; follow the DataRegression /
SessionRegression / ArenaCombatOracle pattern (real object in → assert → one `REGRESSION_*` marker, emit
`FlowTrace.Fail` per violation so it lands in break-log).

1. **`CovenantFirewallRegression` — covenant compliance (TC-COV-02/03, TC-SKR-03/04).** *Highest value.*
   Enumerate every `PackDef` content grant **and** every `skr_store.json` / `skr_staking.json` grant `kind`;
   **HARD-FAIL** if any grant resolves a combat/stat kind (weapon stat, defense, damage, HP-as-power) rather
   than cosmetic / economy-currency / convenience-token. Also string-scan the staking copy for the
   `forbiddenLanguage` set (APY/yield/returns/interest/guaranteed) and assert `skrRebateEnabled == false` until
   sign-off. **The staking JSON already claims a `SkrStakingRegression` build gate "rejects one" — it does not
   exist as a `.cs`. Build it.** This makes the cozy-covenant promise a gated invariant, not a manual review.

2. **`PackFulfillmentOracle` — pack grant end-to-end (TC-MON-03/04/05).** On a stub-confirmed purchase, drive
   `PackStore.ApplyPackContents` against a real `GameStateService`, then assert: economy deltas landed exactly,
   the pack SKU + every cosmetic SKU is in `OwnedItemIds`, and the entitlement **survives a Save()→Load()**.
   Negative case: with no GameState, assert the existing `FlowTrace.Fail` self-report fires (paid-but-no-grant is
   never silent). Closes the "player charged with nothing to show" class.

3. **`TalentEffectOracle` — talent apply (TC-TAL-04/06).** Allocate sample nodes via `HeroTalentModifiers`,
   assert the resulting stat deltas / active-skill unlocks apply to a real hero loadout, and that the allocation
   round-trips save/load (confirm/add the guarding save field). Today talent **layout** is gated (G2) but the
   **effect** is inference-only.

4. **PlayMode wave-economy loop (TC-WAVE-09).** The documented next QA layer (RegressionSuite footer): a
   `DeNelle.Tests.PlayMode` assembly that runs `WaveManager.ForceBeginNextWave` → asserts ≥1 Enemy spawns →
   wave clear raises EconomyService by the WO-330 formula → `TrySpend` deducts on a real placement. Needs a baked
   NavMesh + the RuntimeInitialize bootstrappers; run via `-runTests -testPlatform PlayMode`.

5. **Inventory-VM render oracle (TC-EQUIP-06).** Assert the inventory ViewModel exposes the owned-gear rows +
   portrait the grid binds to (data-empty vs built-but-invisible split, per CLAUDE.md §12) — the headless
   complement to the WO-573/WO-578 felt fixes.

6. **NavMesh nav-tunnel / chokepoint oracle (TC-WAVE-10).** Path-query that enemies route through the intended
   chokepoints and cannot tunnel thin/ornate wall gaps (carve, not physics) — generalizes the existing
   `castle-gate-exitable` NavMesh verify to the Village2 maze.

7. **Feature-flag default oracle (TC-WAVE-12).** Assert the V1 flag posture as data: `ff.basebuilding` OFF,
   `ff.singlehero`/`ff.knightonly` ON, arena-trio OFF — so a stray default flip is caught headless rather than
   shipping a V2 system into V1.

8. **Secrets / git-history scan (TC-COV-05).** Extend `WalletRegistryTest`'s public-address-only assertion with
   a repo + git-history scan for private keys / seed phrases as a CI step (currently the runtime registry is
   gated but history is not).

**Hard ceilings (not closable headless — keep as FELT, surface via G8):** hero-select carousel + UI fidelity /
Blink chrome (TC-LOOP-02, TC-EQUIP-07, TC-XC-10), animation/VFX feel (TC-LOOP-05/16), victory-screen polish
(TC-LOOP-15), full app-kill save resume (TC-SAVE-10), and the verbatim store-copy render (TC-COV-01). The fleet
is `-nographics`; these are the PO's felt-verify domain (CLAUDE.md §13).

---

_Tend the Heart. Hold the dark._
