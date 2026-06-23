# WORK ORDER 131 — Economy Wallet Unification (single crystal source of truth)

**Status:** READY TO IMPLEMENT
**Priority:** P0 — blocks the core build → earn → spend loop
**Date:** 2026-05-30
**Source:** docs/QA_player_sanity_pass_2026-05-30.md (P0-C)
**Lane:** Combat/Economy (code only — no scene files)

---

## Symptom

The Build menu shows e.g. "Flame Tower — ◆150" with a ✓/✗ against the player's
crystal count, but placing the tower spends from a **different, hidden, in-session
wallet** at a **different cost**, and wave rewards **never land in the wallet that
placement drains**. The numbers a player reads, spends, and earns do not agree.
A player can drain the placement wallet to zero while the HUD still shows hundreds
of crystals — with no way to refill the placement wallet.

---

## Root cause (verified file:line)

There are **three independent crystal stores**:

1. **Display + affordability (GameState-backed).**
   `BuildMenu.CrystalBalance` reads `GameStateService.State.Resources.Crystals`
   (`Assets/_Modules/Village/Buildings/UI/BuildMenu.cs:855-869`).
   `BuildMenu.CanAfford(v)` checks the hard-coded `Variants` table (Flame 150, etc.)
   (`BuildMenu.cs:129-136`, check at `:688-693`).
   But `OnConfirmBuild` only calls `TowerPlacementSystem.StartPlacing(data)` —
   **it never deducts anything from GameState** (`BuildMenu.cs:581-601`).

2. **The actual spend (EconomyService, GameState-independent).**
   `TowerPlacementSystem.PlaceTower` spends via
   `EconomyService.Instance.Spend(_selectedTower.cost)` and gates on
   `EconomyService.Instance.CanAfford(_selectedTower.cost)`
   (`Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs:164-166, 189-192`).
   `_selectedTower.cost` is `TowerData.cost = 150`
   (`Assets/_Modules/Core/Data/TowerData.cs:26`).
   **Refinement vs the QA note:** `Spend(int)` and `CanAfford(int)` are the
   **Wood-only deprecated overloads** (`Assets/_Modules/Village/EconomyService.cs:185, 189`).
   So placement actually drains **Wood** from EconomyService's pool (starts at 200,
   `EconomyService.cs:96`), NOT crystals — even more divergent than "a 50-crystal stub."

3. **EconomyService is GameState-independent and in-session only.**
   It bootstraps to `_wood 200 / _stone 150 / _iron 80 / _crystals 50`
   (`EconomyService.cs:96-99`), self-bootstraps a DontDestroyOnLoad singleton
   (`EconomyService.cs:117-130`), and **never reads or writes GameState**
   (confirmed: no `GameState`/`Resources.` references in the file; its own header
   says "PERSISTENCE: in-session only", `EconomyService.cs:27-29`).

4. **Wave rewards land in a fourth path — GameState.**
   `WaveManager.AwardWaveCrystals` → `CrystalEconomy.Instance.AddCrystals`
   (`Assets/_Modules/Village/Waves/WaveManager.cs:851-852`), and `CrystalEconomy`
   correctly round-trips through `GameState.AetherCrystals`/`Resources.Crystals`
   via `GameStateService` (`Assets/_Modules/Village/CrystalEconomy.cs:106-121, 77-100`).

**Net:** the visible economy (BuildMenu display + HUD + wave rewards) is one store
(GameState); the placement spend is an entirely separate in-session pool that
nothing ever refills.

---

## Fix (precise)

Make **GameState the single source of truth** for the build economy and route
**every spend and grant** through `CrystalEconomy` (which already persists to
GameState). EconomyService becomes a thin facade over GameState rather than its
own in-memory pool.

1. **Tower placement spends from GameState.**
   In `TowerPlacementSystem.cs`:
   - `CanPlace` (`:164-166`): replace the `EconomyService.Instance.CanAfford(...)`
     check with `CrystalEconomy.Instance?.CanAfford(_selectedTower.cost) ?? false`.
   - `PlaceTower` (`:189-192`): replace `EconomyService.Instance.Spend(...)` with
     `if (CrystalEconomy.Instance == null || !CrystalEconomy.Instance.TrySpend(_selectedTower.cost)) return;`
     so the spend is atomic and persisted (CrystalEconomy.TrySpend already
     no-ops + returns false when short, `CrystalEconomy.cs:77-100`).
   Use the null-conditional operator on all cross-service calls (CLAUDE.md §10).

2. **Reconcile the cost to ONE number.**
   `BuildMenu.Variants` Flame cost = 150 (`BuildMenu.cs:132`) and
   `TowerData.cost` = 150 (`TowerData.cs:26`) already match for the dev tower —
   confirm placement charges the SAME 150 the menu displays. (Today the menu's
   per-element costs are cosmetic because every element routes through the single
   `DevTower` asset in `OnConfirmBuild`, `BuildMenu.cs:589`; pin the charged value
   to the displayed `v.CrystalCost` until per-variant TowerData assets exist —
   pass the selected variant's cost into placement, or charge `v.CrystalCost`
   in `OnConfirmBuild` via `CrystalEconomy.TrySpend` before `StartPlacing`.)
   Pick one approach and document it inline. Recommended: charge `v.CrystalCost`
   in `OnConfirmBuild` after a `CrystalEconomy.CanAfford` re-check, and have
   `TowerPlacementSystem` NOT double-charge (it then only validates placement
   geometry/skill, not affordability). Whichever path is chosen, the cost must be
   deducted exactly once.

3. **EconomyService crystals reconcile to GameState (do not keep a divergent pool).**
   Either (preferred) repoint `EconomyService.Crystals` getter and crystal
   spend/grant to read/write `GameStateService.State.Resources.Crystals` so any
   remaining EconomyService consumer sees the same number, OR remove crystal
   handling from EconomyService entirely and leave it owning only Wood/Stone/Iron
   (which WO-134 addresses). Wave rewards already go to GameState via CrystalEconomy
   (`WaveManager.cs:851-852`) — do NOT change that path.

4. **No double-spend.** After the change, exactly one code path may deduct crystals
   for a placement. Add an inline comment marking the single authoritative spend site.

---

## Acceptance criteria

- [ ] Placing a tower deducts crystals from the SAME store the HUD/BuildMenu shows
      (GameState via CrystalEconomy) — verified by watching the HUD crystal counter
      drop by exactly the displayed cost on placement.
- [ ] The cost the BuildMenu displays for a tower equals the cost actually charged
      (one number, no Wood-only divergence).
- [ ] Wave-reward crystals (`CrystalEconomy.AddCrystals`) increase the SAME balance
      that placement later spends — earn → spend round-trips.
- [ ] Crystals are deducted exactly once per placement (no double-charge, no
      free placement).
- [ ] A placement is rejected (cursor invalid + no spend) when the player cannot
      afford it against the GameState balance.
- [ ] All cross-service calls use `?.` (CLAUDE.md §10); brace balance check passes
      on every `.cs` file edited.

## Files to edit

- `Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs` (CanPlace, PlaceTower)
- `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` (OnConfirmBuild spend, cost reconcile)
- `Assets/_Modules/Village/EconomyService.cs` (crystals → GameState reconcile OR drop crystals)

## Do NOT touch

- `CrystalEconomy.cs` spend/grant/persistence logic — it is the correct path; only
  CALL it. Do not duplicate its GameState round-trip.
- `WaveManager.AwardWaveCrystals` reward routing (`WaveManager.cs:817-853`) — already
  lands in GameState. (WO-134(b) changes the per-wave AMOUNT, not the destination.)
- Any `.unity` scene file. No hand-edits, no bakes. EconomyService and CrystalEconomy
  both self-bootstrap at runtime, so no scene rebake is required for this WO.
- Wood/Stone material handling (covered by WO-134(c)).

## Cross-dependencies

- **WO-127** (tower-manage UI desync) also edits `BuildMenu.cs`. WO-127 repoints the
  Upgrade screen at live `Tower` components and makes `Tower.Upgrade()` real;
  WO-131 touches `OnConfirmBuild`/`CanAfford`/the spend path. **Serialize these two
  on BuildMenu.cs — one branch at a time** (BuildMenu is a shared file). When WO-127
  wires the real upgrade action, its crystal spend MUST also route through
  `CrystalEconomy.TrySpend` for consistency with this WO.
- **WO-134(c)** (Wood/Stone fake costs) and this WO both touch EconomyService —
  coordinate: WO-131 owns crystals→GameState; WO-134(c) owns Wood/Stone deduction.
