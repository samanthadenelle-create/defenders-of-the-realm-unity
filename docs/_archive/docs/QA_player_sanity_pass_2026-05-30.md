# QA — Player Sanity Pass (static code audit)

**Date:** 2026-05-30
**Author:** PO / QA agent
**Build under review:** `Builds/Windows/DefendersOfTheRealm.exe` (boots to Village), per PIPELINE_STATE.md.

## Method & caveat

This is a **static, code-level player-journey audit**, NOT a runtime playtest. I could not
launch the build. Every issue below is traced to a `file:line` in the current source on
`C:\Users\Kayden-Laptop\Documents\defenders-unity`. Where I say a player "experiences" something,
that is the predicted behaviour from reading the code path — it should be confirmed in a real
playtest, but each item has a concrete code basis, not a guess.

Already-filed bugs are referenced, not re-filed:
- **WO-125** — dragon unhittable + Heart-fall = no defeat
- **WO-126** — magenta materials, barn-in-wall, blue crystal-mine cube, gate color
- **WO-127** — "Manage All Towers" upgrade-UI stale-level desync
- **WO-130** — ATB pills + broken loop (spec in progress)

I walked the new-player journey in order: first launch → move/look → build/upgrade towers →
start wave / fight / clear / reward → take damage / Heart / win-lose → economy → ATB & dungeons →
save/resume → HUD → unfinished/stubbed surfaces.

---

## P0 — blocks a playable demo / breaks the core promise

### P0-A. The first-run tutorial (OnboardingFlow) is built but never wired — there is no FTUE at all
- **Player experiences:** A brand-new player drops straight into the village with **no teaching**.
  Worse, because onboarding never completes, the **cold-open cinematic replays on every launch**
  (it is gated on `GameState.Onboarded`, and nothing ever flips that flag in normal play).
- **Root cause:** `OnboardingFlow.cs` is a passive, integrator-driven component (by design it cannot
  reference the Village/HUD assemblies). Its own integrator notes say the **Village scene must wire**
  `OpenBuildMenuRequested`, `BeginWaveRequested`, `TutorialClosed`, `NotifyTowerBuilt`,
  `NotifyPetPlaced` (`OnboardingFlow.cs:535-577`). A repo-wide search shows **zero references** to
  `OnboardingFlow`, `BeginWaveRequested`, `TutorialClosed`, `NotifyTowerBuilt`, or `NotifyPetPlaced`
  outside `OnboardingFlow.cs` itself. `VillageController.cs` (`VillageController.cs:141-146` Start)
  wires gate openers, the Heart HUD bridge and dungeon entrances — but **not the tutorial**. The
  fix it documents (`Finish()` → `GameStateService.FinishOnboarding()`, `OnboardingFlow.cs:452-484`)
  therefore never runs.
- **Next step:** **New WO.** Add the tutorial UIDocument + `OnboardingFlow` to the Village scene
  (via `VillageSceneBuilder`, the serialization bottleneck) and wire the five seams per its own
  integrator notes. This also fixes the cold-open-replay bug as a side effect. Note: `OnboardingFlow`
  drives `TutorialOverlay.uxml` — per PIPELINE_STATE §8 "UXML in builds does NOT work," so this
  likely needs a **code-built overlay**, not the UXML, to render in a player build.

### P0-B. The village hero cannot take damage or die — there is no hero lose condition
- **Player experiences:** Enemies that reach the hero do nothing to him; the hero is immortal in the
  village. Combined with WO-125 (Heart-fall does not trigger defeat), **the village is unloseable**.
- **Root cause:** The hero rig the builder assembles gets `HeroBodySwapper`, `HeroLocomotion`,
  `HeroAbilities`, `HeroAbilityInput` (`VillageSceneBuilder.cs` hero-systems block, ~3413-3485) but
  **never adds `HeroHealth` or `HeroHitReaction`** — a grep of `VillageSceneBuilder.cs` for
  `HeroHealth`/`HeroHitReaction` returns **0**. `HeroHealth.cs` (with full damage/death/GameOver flow
  at `HeroHealth.cs:125-205`) exists but is only used in the dungeon/ATB hero builders, not the
  village. So the only village lose path is the Heart — and per WO-125 that path is broken.
- **Next step:** **New WO** (pairs with WO-125). Decide the intended village lose model (Heart-only,
  or Heart + hero-down) and either wire `HeroHealth` onto the village hero or explicitly document the
  hero as invulnerable in town. If hero can die, also add the missing hero-HP bar (see P1-G).

### P0-C. Tower build economy is split across THREE unsynced wallets — costs shown ≠ costs paid, rewards never refill the spend wallet
- **Player experiences:** The Build menu shows e.g. "Flame Tower — ◆150" and a ✓/✗ against the
  player's crystal count. But placing the tower spends from a **different, hidden 50-crystal wallet**,
  at a **different cost**, and **wave rewards never land in that wallet**. The numbers a player reads,
  spends, and earns do not agree with each other.
- **Root cause — three separate stores:**
  1. **`BuildMenu.CrystalBalance`** reads `GameState.Resources.Crystals` (`BuildMenu.cs:855-869`) and
     its `CanAfford` checks the variant cost (Flame 150, etc., the hard-coded `Variants` table
     `BuildMenu.cs:129-135`). But `OnConfirmBuild` (`BuildMenu.cs:581-600`) only calls
     `TowerPlacementSystem.StartPlacing(data)` — **it never deducts crystals from GameState.**
  2. **`TowerPlacementSystem.PlaceTower`** does the actual spend via
     `EconomyService.Instance.Spend(_selectedTower.cost)` (`TowerPlacementSystem.cs:189-192`), where
     the cost is `DevTower.cost`, not the 150 the menu showed.
  3. **`EconomyService`** is a standalone in-memory stub that bootstraps to a flat **`_crystals = 50`**
     (`EconomyService.cs:99,117-130`) and **never reads or writes `GameState`** — confirmed: no
     `GameState`/`Resources.` references in the file. Meanwhile wave rewards go to
     **`CrystalEconomy.AddCrystals`** (`WaveManager.cs:852`), and `CrystalEconomy` correctly
     round-trips through `GameState` (`CrystalEconomy.cs:106-123`). So rewards + the Build-menu
     display share one store; the placement spend uses an entirely different one that never refills.
- **Net:** the visible economy (HUD/BuildMenu/rewards) and the spend economy (placement) are two
  different currencies. The player can run the placement wallet to 0 with no way to refill it, while
  the HUD still shows hundreds of crystals.
- **Next step:** **New WO (P0).** Make `EconomyService.Crystals` the single source of truth backed by
  `GameState.Resources.Crystals` (read on bootstrap, write on spend/grant), OR route tower placement
  through `CrystalEconomy.TrySpend`. Also reconcile the cost: BuildMenu's `Variants` table (150) vs
  `DevTower.cost` must be one number. Material costs are also fake (see P1-E).

---

## P1 — major UX / progression breakage; demo is rough but limps

### P1-D. Ability HUD badges say "Q W E R" but the actual hotkeys are "1 2 3 4"
- **Player experiences:** The four ability buttons are labelled **Q / W / E / R** (and the project's
  own task language calls them "Q/F/E/R"). A player pressing those keys gets nothing; the abilities
  only fire on the **number row 1/2/3/4**.
- **Root cause:** HUD hard-codes the badges `SlotKeys = { "Q", "W", "E", "R" }`
  (`VillageHudController.cs:132`). Input maps the slots to `digit1Key..digit4Key` /
  `Alpha1..Alpha4` (`HeroAbilityInput.cs:48-66`) — W is deliberately reserved for WASD movement
  (`HeroAbilityInput.cs:4-6`). The ability data adds a third name: `abilities.json` labels slot 2
  `"key": "F"`. Three different names (HUD "W", input "2", data "F") for one slot.
- **Next step:** **New WO (small).** Pick the canonical hotkeys, then make the HUD badge, the input
  map, and `abilities.json.key` agree. (Recommend showing "1 2 3 4" badges to match what actually
  works, or rebind input to Q/E/R/F and update the badge.)

### P1-E. Tower build material costs (Wood/Stone) are cosmetic — never tracked, never spent
- **Player experiences:** The build card shows "Wood 20 ✓", "Stone 5 ✓" with reassuring checkmarks,
  implying a resource economy. None of it is real — wood/stone are never deducted and the counts are
  faked.
- **Root cause:** `BuildMenu.GetMaterialCount` is a **stub** returning hard-coded `wood → 20`,
  `stone → 5` (`BuildMenu.cs:692-700`), and `CanAfford` checks against those constants
  (`BuildMenu.cs:688-690`). The placement spend (`TowerPlacementSystem`) only spends crystals, never
  materials. So the material economy is pure UI theatre.
- **Next step:** Covered partially by the resource pillar (WO-111 / WO-124 resource HUD). **New WO or
  fold into WO-111:** either wire material counts to `EconomyService` (Wood/Stone/Iron already exist
  there, `EconomyService.cs:96-98`) and deduct on placement, or hide the material rows until the
  resource economy is live so the UI stops promising a system that does nothing.

### P1-F. "Upgrade Tower" screen in BuildMenu is a stub + reads the wrong object (stale Lvl 1)
- **Player experiences:** Open BuildMenu → Upgrade Tower → every tower shows "Lvl 1" no matter how
  many times it was upgraded, and clicking "Upgrade" just logs and shows "arrives in a later update."
- **Root cause:** The screen enumerates `Building` components and prints `Building.Level`
  (`BuildMenu.cs:617-648`), a field never mutated by any upgrade path; placed towers are actually
  `Tower` components. The Upgrade button is an explicit stub (`BuildMenu.cs:668-673`).
- **Next step:** **Covered by WO-127** (repoint this screen at the live `Tower` list and call
  `Tower.Upgrade()`). No new WO needed; flagging that the button itself is also a no-op, not just the
  label — WO-127 should make the action real, not only fix the display.

### P1-G. No hero-HP and no pet-status display in the village HUD
- **Player experiences:** Once hero damage is wired (P0-B), the player has no way to see hero health
  or pet state. Today there is a Heart-HP bar, a crystal counter and a mana bar, but no hero-life bar.
- **Root cause:** `VillageHudController` binds heart-hp, crystal-count and mana
  (`VillageHudController.cs:358-365`) but has **no hero-HP or pet-status element** (grep for
  `hero-hp`/`SetHeroHp` returns nothing). This matches the BUGLOG 2026-05-24 item #H ("design gap,
  not a break").
- **Next step:** **New WO (small, additive).** Add a hero-HP bar + pet-status widget to the
  code-built HUD. Gate behind P0-B (hero must be able to take damage for the bar to mean anything).

### P1-H. Non-boss waves award zero reward, and the wave-clear banner shows a misleading "+N ◆"
- **Player experiences:** Clearing a normal wave appears to pay out ("WAVE n REPELLED  +500 ◆"), but
  the "+500" is just the player's **current balance**, not what they earned. Most waves pay **nothing**
  — only boss-interval waves roll a chance-based drop.
- **Root cause:** `AwardWaveCrystals` only credits on `waveId % BossInterval == 0` and then only on a
  `DropChance` roll, plus an optional event bonus (`WaveManager.cs:817-852`). Ordinary waves grant 0.
  The banner value is `CurrentCrystals()` = the live `GameState` balance
  (`WaveFeedbackDirector.cs:98,118-124`), so it reads like a reward even when nothing was earned.
- **Next step:** **New WO.** Give every cleared wave a real, deterministic base reward and pass the
  *actual delta* into `ShowWaveClearBanner`, not the running total. (Note `IVillageHud.ShowWaveClearBanner`
  is `(int waveId, int enemiesDefeated, string flavour)` — the value being passed is crystals, so the
  banner semantics are also muddled.)

### P1-I. A breach abandons the rest of the wave with no clear feedback
- **Player experiences:** If even one enemy reaches the inner ring, the player is yanked into a
  Last-Stand/Defend choice; when they return, the **rest of that wave is gone** and they get no reward
  for the enemies they did kill. Can feel like the wave "vanished."
- **Root cause:** `TriggerBreach` → `EnterDefendTower`/`EnterAtbBattle` kills/clears all remaining
  live enemies and the apex boss (`WaveManager.cs:912-943`, `951-977`); the abandoned remainder is by
  design but there is no partial-clear reward or messaging.
- **Next step:** **New WO (design).** Decide whether a breach should pay partial rewards and surface a
  clear "village breached — make your stand" beat so the wave disappearing reads as intentional.

---

## P2 — polish, confusion, or known-stub surfaces

### P2-J. Dungeons / ATB are still placeholder ("pills")
- **Player experiences:** Entering a dungeon or breach battle lands in a capsule-combatant placeholder
  rather than a real encounter.
- **Root cause:** `BattleController` is explicitly "Week-2 placeholder scope … one hero capsule, one
  enemy capsule" (`BattleController.cs:20-21,60-65`); `Dungeon_FolksGranary.unity` is a thin stub
  (`DungeonStubBuilder.cs`). `Dungeon_HealersCottage.unity` is more built (~82k lines vs ~25k).
- **Next step:** **Covered by WO-130** (ATB pills/broken loop). Dungeon content is a separate content
  WO (was filed as WO-23 in the BUGLOG); confirm it is still tracked.

### P2-K. First-wave countdown is a long idle with no prompt (compounds the missing FTUE)
- **Player experiences:** The wave loop auto-starts and the player waits out the prepare countdown
  (45s first wave, then 300s, ×difficulty — Normal ≈ 5 min) with no instruction on what to do
  meanwhile.
- **Root cause:** `WaveManager._autoStart = true` and `Start()` calls `BeginLoop()` immediately
  (`WaveManager.cs:140-141,290-292`); countdown base is 300s for later waves
  (`WaveManager.cs:376-381`). There is a "Trigger Wave" button (`VillageHudController.cs:415-442`,
  via `ForceBeginNextWave`) but a new player won't know to use it without the FTUE (P0-A).
- **Next step:** Largely resolved once P0-A wires the tutorial (it holds Wave 1 until the player is
  taught and gives a "Begin Wave 1" CTA). No separate WO required if P0-A lands; otherwise consider a
  visible "Start Wave" prompt by default.

### P2-L. Store / marketplace is intentionally disabled in-scene
- **Player experiences:** No working in-village store; the monetization surface is unreachable.
- **Root cause:** Known + documented — `BuildMarketplace` is commented out in `VillageSceneBuilder`
  pending its own PanelSettings + a code-built UI (PIPELINE_STATE §5; CC_MONETIZATION_RECONCILIATION).
- **Next step:** **Covered** — parked WO-22 (store re-enable). Not a new finding; noted so the demo
  scope is honest about it.

### P2-M. Reflection-based cross-assembly bridges (pre-existing pattern, watch for fragility)
- **Player experiences:** Occasional "button does nothing" / "bar doesn't update" risk if a method
  signature drifts, because several HUD↔gameplay links resolve by method name at runtime.
- **Root cause:** `HeartHudBridge` resolves `SetCrystals`/`SetHeartHp` via `System.Reflection`
  (`HeartHudBridge.cs:35,97`); `HeroHealth.HandleDeath`/`FindGameOverUi` resolve `GameOverUI` by type
  name across assemblies (`HeroHealth.cs:172-205`); `AdminOverlay` invokes `ForceBeginNextWave`
  reflectively. This is the project's established bridge pattern, not new — but it's brittle (a renamed
  method silently no-ops). CLAUDE.md §10 specifically flags *new* reflection in bridges.
- **Next step:** No WO now. Note for maintenance: prefer the `CoreServices.Hud` interface path over
  reflection where an interface already exists (e.g. `IVillageHud.SetCrystals`).

---

## Top 5 to fix for a playable demo (prioritized)

1. **Economy unification (P0-C)** — make tower placement spend the same wallet the HUD shows and
   wave rewards fill. Without this the core build→earn→spend loop is incoherent. *New WO.*
2. **A real win/lose state (P0-B + WO-125)** — wire hero damage/death and/or fix the Heart-fall
   defeat so the village can actually be lost. Today it cannot. *New WO pairs with WO-125 (filed).*
3. **First-run tutorial wiring (P0-A)** — wire `OnboardingFlow` into the Village scene; also kills the
   cold-open-replay bug and the silent-countdown problem (P2-K). *New WO.*
4. **Ability hotkey labels (P1-D)** — the HUD telling players to press Q/W/E/R when only 1/2/3/4 work
   makes combat feel broken on contact. Cheap, high-impact. *New WO (small).*
5. **Honest wave rewards (P1-H) + tower upgrade UI (P1-F / WO-127)** — give cleared waves a real
   payout shown as a true delta, and make "Upgrade Tower" actually upgrade. *P1-H = new WO; P1-F
   covered by WO-127 (filed).*

**Already have WOs:** WO-125 (Heart/dragon — pairs with #2), WO-127 (tower upgrade UI — part of #5),
WO-130 (ATB pills — P2-J), WO-22 (store — P2-L). **Need new WOs:** economy unification (P0-C),
hero-damage/lose model (P0-B), onboarding wiring (P0-A), ability-key labels (P1-D), real wave rewards
(P1-H), and the smaller P1-E (fake material costs) / P1-G (hero-HP bar).

---

## Things that look healthy (so the demo isn't all red)

- **Save/resume** is solid — PlayerPrefs envelope with migration + schema validation, saved before
  scene changes (`GameStateService.cs:148-215`, `SceneRouter.cs:150,175`).
- **Crystal display + wave reward** share one store (`CrystalEconomy` ↔ `GameState`) — the desync is
  specifically the *placement* spend (`EconomyService`), not the whole economy.
- **Wave loop robustness** — stuck-enemy failsafe, NavMesh snap on spawn, breach fallback so a breach
  is never a dead end (`WaveManager.cs:655-657,723-749,888-895`).
- **Gate proximity opening, Heart collider, pet motion smoothing** were all fixed in prior passes
  (BUGLOG 2026-05-24 resolution list).
- **Scenes referenced by SceneRouter all exist** in Build Settings (Title, HeroSelect, PetSelect,
  Village, both dungeons, ATBBattle, PatriciaLightMode) — no missing-scene crash on transition.
