# WORK ORDER 860 — Fix start loadout (sword+shield, not axe) + thin the weapon/armor store

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** Hero/Gear (Combat/economy data). `GearLoadout` + `GameStateService` (Part A) · `VendorStockResolver` + `vendors.json` (Part B).
**WO#:** minted from the proposed UI-seat reserved block (860–899) to avoid the main-line collisions (banner note 2026-08-02); owner to ratify the block.
**Origin:** owner felt-test 2026-08-02 — *"On start should be a sword and shield but on new I keep getting this axe I tried one time. Look at all the options in store for weapons and thin it out so there are only 2 options on each new level, isolate to only those, only show ones they can equip. Convey to return for new stock after leveling. Same with armor."*

---

## PART A — Start loadout: sword + shield, never a stale axe

### RCA (proven from code)
- **The axe is a stale PlayerPrefs equip that New Game never clears.** `GearLoadout.ApplyPersistedEquip`
  (`GearLoadout.cs:201-205`, via `Refresh()` `:171`) restores `PlayerPrefs["dotr-equip-weapon-knight"]` (written by
  `EquipWeaponById` `:551`) and OVERRIDES the auto-best pick. `WeaponFitsClass` only checks `job`, so any knight axe
  (`tripo_axe_a` "Reaver's Hatchet", or a `blink_axe1h_*`) passes. **`ResetToNewGame()` (`GameStateService.cs:802`)
  wipes ~40 GameState fields but never deletes the `dotr-equip-*` keys** — only tests/regression do. So a
  once-equipped axe returns on every new game, deterministically, forever.
- **Secondary:** even with the axe gone, the intended starter isn't honored — auto-best `GearCatalog.BestWeapon`
  (`GearCatalog.cs:232`) returns `knight_flameblade` (damageMult 1.2) over `knight_starter` "Squire's Blade" (1.0).
  And the shield seed `HeroBodySwapper.cs:629-631` (`EquipOffHandById("knight_shield_starter")`) is skipped when
  `usePackage` is true or an off-hand is persisted — so the shield can silently go missing too.

### Fix
1. **Clear the `dotr-equip-*` PlayerPrefs keys in `ResetToNewGame()`** (`GameStateService.cs:802`) — weapon + off-hand
   + armor, per class (the keys `EquipWeaponById`/off-hand/armor write). A new game must not inherit an old equip.
2. **Set the explicit class STARTER on new game, not auto-best.** The Knight starts with `knight_starter`
   (Squire's Blade) + `knight_shield_starter` (Squire's Heater) — the intended sword+shield, applied on new game
   INSTEAD of `BestWeapon` (which picks Flameblade).
   - **Formalize a per-class STARTER LOADOUT data structure (review-mandated)** — a small dictionary
     `class → {mainHand, offHand}`, not an ad-hoc "if Knight then …". Seed Knight now; **this is the SHARED source of
     truth WO-861 reuses** for Ranger (Hunter's Shortbow + offhand dagger) and Mage (starter staff). Leaving it
     ad-hoc creates debt the moment the other two heroes land.
3. **Guarantee the shield seed fires** on a fresh Knight (not skipped by `usePackage`/persisted off-hand when the
   off-hand was just cleared in step 1). **Review-flagged failure mode:** the classic "I cleared the prefs but the
   seed is still skipped" — after the prefs clear, explicitly re-verify the `HeroBodySwapper.cs:629` seed path
   actually runs (its `usePackage` / `EquippedOffHand == null` guards must still evaluate true on a fresh Knight).

### Part A acceptance
- [ ] New Game (after having equipped an axe) starts the Knight with **Squire's Blade + Squire's Heater**, never the
      axe or Flameblade. Verified headless (equip an axe → ResetToNewGame → assert equipped weapon == knight_starter,
      off-hand == knight_shield_starter) — a `DataRegression`/EditMode case.
- [ ] `dotr-equip-*` PlayerPrefs cleared on ResetToNewGame (assert keys absent after reset).

---

## PART B — Thin the weapon/armor store to 2 per level, equippable-only, "come back for new stock"

### RCA (proven from code)
`VendorStockResolver.Resolve(context, job, level)` (`VendorStockResolver.cs:203`) is the single choke point every shop
binds. It has **NO count cap** — it emits the entire category catalog (roster-filtered), showing higher-level/
wrong-class items as **locked** rows rather than hiding them (`:232`). The bulk is **~66 `blink_*` placeholder
weapons** (mostly Lv1 axes/swords/shields), so a Lv1 Knight sees **~45+ equippable weapon rows** at the Forge.
Current gates: category (`vendors.json`), roster (`ff.knightonly`), `maxReqLevel` upper cap (currently 0/uncapped),
and an eligibility FLAG (`classOk && levelOk`) that only locks, never hides.

### Fix (all in `VendorStockResolver.Resolve` + `vendors.json` — one choke point, data-tunable)
1. **Only-equippable** — hide the locked rows: gate `result.Add` on `classOk && levelOk` (early-return otherwise,
   `:232/:247`). New `vendors.json` bool `"onlyEquippable": true` (opt-in per vendor, V1). → the player only sees gear
   they can actually equip now.
2. **Exclude the `blink_*` placeholders** from the shelf (they're the overload; `ff.blinkarmor` OFF already
   display-suppresses them in `PartyShopVM.cs:432` but the resolver still LISTS them — drop them in the resolver).
3. **Cap to 2 per level** — new `vendors.json` field `"perLevelCap": 2` (mirror the `MaxReqLevel` plumbing
   `:214`); after the eligible loop, bucket by `req.level` and keep the top 2. **Sort rule (review-flagged, document
   it):** primary = `damageMult` (weapons) / `defense` (armor), with a **stable secondary sort** (e.g. by `id`) so the
   pick is deterministic — raw `damageMult` alone can surface a thematically-odd weapon. A `starterPriority`/
   `shelfPriority` field is a later refinement; for V1, damageMult + stable tiebreak, documented. "Isolate to only
   those" = also drop items far below the hero's level so the shelf shows the CURRENT tier's 2 picks, not a history.
4. **"Come back after leveling for new stock"** — the per-vendor `emptyLine` already exists (`vendors.json:34,45` →
   `EmptyLineFor` `:147` → rendered `PartyShopPanelMvvm.cs:750`); reword it for the "nothing new until you level"
   case. For a footer UNDER a non-empty capped list, add a `"footerLine"` to `VendorDef` + a `FooterLine` on
   `PartyShopVM` (parallel to `EmptyLine`, `PartyShopVM.cs:338`) rendered by the panel. **Review note:** the
   footer-under-a-non-empty-list case is the one players see MOST after the cap (they have items, better ones unlock
   later) — verify the panel actually RENDERS `footerLine` there, not just the empty-state line.
5. **Same for armor** (armorer): identical cap/only-equippable/footer — the resolver's armor branch (`:237-251`) and
   the `armorer` vendor row. (Reminder from WO-840: the Armorer must be reachable; the armor category filter is
   already correct.)

### Part B acceptance
- [ ] At Lv1, the Forge shows **exactly 2 equippable weapons** (e.g. Squire's Blade + one alternative), no locked
      rows, no `blink_*` clutter; the Armorer shows 2 equippable armors.
- [ ] A "return after leveling for new stock" line shows when appropriate (empty or footer).
- [ ] The `perLevelCap`/`onlyEquippable`/`footerLine` are `vendors.json` data (tunable, no recompile to retune).
- [ ] `CompileGate` + `DataRegression` green; a resolver EditMode case asserts ≤2 equippable rows per level.

## Implementation order & WO-861 cross-check (review-mandated 2026-08-02)
1. **Part A FIRST** — pure correctness; it unblocks clean new-game testing for everything else (incl. WO-861).
2. **Write the regression BEFORE the fix (red→green):** the headless "equip an axe → ResetToNewGame → assert
   equipped == Squire's Blade + Heater, and `dotr-equip-*` keys absent" case must FAIL red on today's code, then go
   green after the fix. Make it a permanent DataRegression/EditMode test.
3. **Shared starter loadout** (Part A2) is the source of truth WO-861 reuses — implement it as the dictionary, not ad-hoc.
4. **Cross-check with WO-861 Phase 0** (once the roster un-gates): the thinned store must show only the SELECTED
   class's equippable gear. Add an acceptance case: **"With Sylas (Ranger) selected, the Forge shows only ranger
   arrows + the offhand dagger, ≤2 per level"** (and the Armorer only ranger-weight armor). The resolver already
   roster/job-filters, so this should hold once 861 Phase 0 lands — verify it does.

## Files to edit
- `Assets/_Modules/Core/State/GameStateService.cs` — `ResetToNewGame` clears `dotr-equip-*` (Part A1).
- `Assets/_Modules/Village/Hero/GearLoadout.cs` — explicit starter loadout on new game (Part A2).
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` — shield seed guarantee (Part A3).
- `Assets/_Modules/Village/Hero/VendorStockResolver.cs` — cap/only-equippable/blink-exclude (Part B1-3,5).
- `Assets/Resources/Data/Canonical/vendors.json` (+ StreamingAssets mirror, byte-identical) — `perLevelCap`,
  `onlyEquippable`, `footerLine`, reworded `emptyLine` (Part B).

## Do NOT
- Do NOT edit the weapon/armor catalogs to "fix" the overload — cap in the resolver; the catalogs stay (curated +
  blink placeholders live there for later).
- Do NOT change `WeaponFitsClass`/`ArmorFitsClass`/`MeetsReq` (the equip gate is correct; reuse it as the show-filter).
- Keep `vendors.json` Resources/StreamingAssets copies byte-identical.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `GearLoadout.cs:76; VendorStockResolver.cs:22-23,242` — starter + thinned shelf. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
