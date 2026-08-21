<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 134 — Input / HUD / reward consistency cluster

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Priority:** P1 — combat reads as broken on contact; rewards are misleading
**Date:** 2026-05-30
**Source:** docs/QA_player_sanity_pass_2026-05-30.md (P1-D, P1-H, P1-E)
**Lane:** HUD + Combat/Economy (code only — no scene files)

Three independent sub-fixes; each can ship on its own branch.

---

## (a) Ability hotkey labels disagree across HUD / input / data

**Symptom:** The four ability badges read **Q / W / E / R**, but only the number row
**1 / 2 / 3 / 4** actually casts. A player pressing Q/W/E/R gets nothing; combat feels
broken on first contact. (The Help menu and abilities.json add a THIRD name — "F" —
for slot 2.)

**Root cause (verified):**
- HUD badges hard-coded `SlotKeys = { "Q", "W", "E", "R" }`
  (`Assets/_Modules/HUD/VillageHudController.cs:132`; rendered at `:774`).
- Input maps the four slots to the **number row**:
  `digit1Key..digit4Key` (new Input System) / `Alpha1..Alpha4` (legacy)
  (`Assets/_Modules/Village/Hero/HeroAbilityInput.cs:48-51, 63-66`). The file header
  notes W is deliberately reserved for WASD movement
  (`HeroAbilityInput.cs:4-6`), and `VillageSceneBuilder` confirms 1-4 was chosen over
  Q-W-E-R for exactly that reason (`Assets/Editor/VillageSceneBuilder.cs:3472-3473`).
- `abilities.json` labels slot 2 `"key": "F"` for every class
  (`Assets/StreamingAssets/Data/Canonical/abilities.json:23, 68, 78`) — yet F is the
  **interact** key per the Help menu (`Assets/_Modules/HUD/HelpMenu.cs:270`:
  "1/2/3/4 + face buttons: cast Q/W/E/R • F: interact").
  So one slot has three names (HUD "W", input "2", data "F") and "F" also collides
  with interact.

**Fix (precise):** Pick ONE canonical scheme and make HUD badge + input map +
`abilities.json.key` + HelpMenu agree.
- **Recommended:** show the badges that actually work — `SlotKeys = { "1", "2", "3", "4" }`
  at `VillageHudController.cs:132`. Then set `abilities.json` `"key"` to `"1"/"2"/"3"/"4"`
  for the matching slots (remove the "F" on slot 2 for all three classes:
  `abilities.json:23, 68, 78`), and fix `HelpMenu.cs:270` so the control hint reads
  "1/2/3/4: cast" without the misleading "Q/W/E/R" / "F: cast" wording (keep
  "F: interact" only if F is still the interact key).
- (Alternative, more work: rebind input to Q/E/R/F in `HeroAbilityInput` — but W stays
  reserved for movement, and F collides with interact, so the number row is cleaner.)

**Acceptance:** the badge shown == the key that casts == `abilities.json.key` == the
Help-menu hint, for all four slots; no slot labels a key that does something else
(no "F" that is also interact).

**Files:** `VillageHudController.cs` (`:132`), `abilities.json`, `HelpMenu.cs` (`:270`).

---

## (b) Non-boss waves award 0 crystals, but the clear banner shows current balance as if a reward

**Symptom:** Clearing a normal wave shows "+N ◆" on the banner, but most waves pay
**nothing** — the "+N" is just the player's **current balance**, not what they earned.

**Root cause (verified):**
- `WaveManager.AwardWaveCrystals` only credits on a boss interval AND a chance roll,
  plus an optional event bonus; ordinary waves grant **0**
  (`Assets/_Modules/Village/Waves/WaveManager.cs:817-853`). It is called from
  `CompleteWave` (`WaveManager.cs:798`), which then fires `OnWaveCleared` (`:800`).
- The banner value is the live GameState balance, not a delta:
  `WaveFeedbackDirector.OnWaveCleared` passes `CurrentCrystals()` (=
  `GameState.Resources.Crystals`) into `ShowWaveClearBanner`
  (`Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs:98, 118-124`). So it reads
  like a reward even when nothing was earned.
- Note the banner semantics are also muddled: `IVillageHud.ShowWaveClearBanner` is
  `(int waveId, int enemiesDefeated, string flavour)` — the value being passed is
  crystals, not enemies defeated.

**Fix (precise):**
1. Give **every cleared wave a real, deterministic base reward** in
   `AwardWaveCrystals` (`WaveManager.cs:817-853`) — e.g. a base crystal grant per
   wave (tune freely; keep the boss-interval bonus + event bonus on top). Route it
   through `CrystalEconomy.AddCrystals` exactly as today (`WaveManager.cs:851-852`) so
   it persists to GameState (consistent with WO-131).
2. **Pass the actual delta**, not the running total, into the banner. Have
   `AwardWaveCrystals` return (or expose) the crystals granted this wave, and have
   `WaveFeedbackDirector.OnWaveCleared` show THAT delta instead of `CurrentCrystals()`
   (`WaveFeedbackDirector.cs:98`).
3. Resolve the banner-signature mismatch: either pass the reward delta in a way that
   reads as crystals, or pass `enemiesDefeated` per the `IVillageHud` contract and
   surface the reward separately. Document the chosen meaning inline.

**Acceptance:** every cleared wave grants a non-zero, deterministic base reward that
lands in the GameState/CrystalEconomy balance; the clear banner shows the **amount
earned this wave** (delta), never the running balance; banner argument meaning is
documented and matches `IVillageHud`.

**Files:** `WaveManager.cs` (`:817-853`), `WaveFeedbackDirector.cs` (`:90-124`).

---

## (c) Tower material costs (Wood/Stone) are faked and never deducted

**Symptom:** Build cards show "Wood 20 ✓ / Stone 5 ✓" with reassuring checkmarks,
implying a material economy — but wood/stone are never tracked or spent. Pure UI
theatre.

**Root cause (verified):**
- `BuildMenu.GetMaterialCount` is a stub returning hard-coded `wood → 20`, `stone → 5`
  (`Assets/_Modules/Village/Buildings/UI/BuildMenu.cs:697-705`), and `CanAfford(v)`
  checks against those constants (`BuildMenu.cs:688-693`).
- The placement spend (`TowerPlacementSystem.PlaceTower`) only ever touches the
  crystal/Wood overload of EconomyService (`TowerPlacementSystem.cs:189-192`) — it
  never deducts Stone, and after WO-131 the crystal path moves to CrystalEconomy.
- EconomyService DOES track real Wood/Stone/Iron pools (`EconomyService.cs:96-98,
  103-106`, with `TrySpend(ResourceCost)` at `:152-161`) — they just aren't wired to
  the build flow.

**Fix (precise):** Pick ONE:
- **(c1, preferred if the resource pillar is live)** Wire material counts to the real
  pools: `GetMaterialCount` (`BuildMenu.cs:697-705`) reads `EconomyService.Instance.Wood/Stone`,
  and on placement deduct the variant's Wood/Stone via
  `EconomyService.Instance.TrySpend(new ResourceCost(wood: v.Wood, stone: v.Stone))`
  (atomic; `EconomyService.cs:152-161`). Coordinate with WO-131 so crystals route
  through CrystalEconomy/GameState while Wood/Stone stay in EconomyService (or are
  also reconciled to GameState.Resources if WO-124's resource HUD expects that).
- **(c2, if the resource economy is NOT player-facing yet)** **Hide the material rows**
  in the build card so the UI stops promising a system that does nothing — remove the
  Wood/Stone `BuildCostRow`s and the material checks in `CanAfford` until materials
  are real. Lower-risk for a demo.

**Acceptance:** either materials are really deducted on placement and the card shows
live counts (a placement is blocked when short on Wood/Stone), OR the material rows
are removed so nothing claims a non-functional economy. No hard-coded `20/5` left
masquerading as inventory.

**Files:** `BuildMenu.cs` (`:688-705`), and (c1 only) `TowerPlacementSystem.cs` /
`EconomyService.cs`.

---

## Global acceptance criteria (all three sub-fixes)

- [ ] `?.` on all cross-module service calls (CLAUDE.md §10).
- [ ] No new `System.Reflection` in bridge scripts.
- [ ] Brace balance check passes on every `.cs` file edited.
- [ ] No `.unity` scene files hand-edited; no bakes fired by UI. None of these three
      sub-fixes requires a scene rebake (HUD, WaveManager, BuildMenu, EconomyService
      all self-bootstrap or are pure code).

## Do NOT touch

- `CrystalEconomy.cs` persistence logic — call it, don't duplicate (see WO-131).
- The wave-loop control flow / breach handling (`WaveManager.TriggerBreach` etc.) —
  out of scope (QA P1-I is a separate design WO).
- The tower-upgrade screen — that is WO-127.

## Cross-dependencies

- **WO-131 (economy unification)** — (b) and (c) both touch the economy and/or
  `BuildMenu.cs` / `EconomyService.cs`. Land WO-131 first (it sets crystals as the
  GameState source of truth); then (b) keeps its reward on that path and (c) handles
  Wood/Stone only. **Serialize edits to `BuildMenu.cs` and `EconomyService.cs`** with
  WO-131 (and WO-127, which also edits BuildMenu) — one branch per shared file.
- Sub-fix (a) is fully isolated (HUD/data/Help text) — can ship anytime.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `BuildMenu.cs:719-724, WaveManager.cs:100-357` — b+c shipped; a deliberately reversed. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
