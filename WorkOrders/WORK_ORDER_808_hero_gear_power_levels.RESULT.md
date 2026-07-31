# WO-808 RESULT — Hero gear power levels (Option A, instance reforge)

**Implemented:** 2026-07-30 evening (CLI). **Gates:** COMPILE_GATE_OK + REGRESSION_OK incl.
the new `[gear-levels]` oracle (fresh logs verified 20:27).

## What shipped (CLI scope, all 6 items)

1. **Per-instance level persisted** — `GameState.GearLevels` (Dictionary<string,int>, gearId → level).
   **NO schema bump**: additive default-on-read on the committed v35, the exact `troopLevels`
   precedent (nullable wire field appended at END of PersistedState; dehydrate/hydrate/New-Game
   wired in GameStateService; no SaveMigrator change). Old saves load with all gear at baseline.
2. **`gear-levels.json`** dual-copy (Resources authority + byte-identical StreamingAssets mirror).
   5 rarity bands × max level 5. `statMult[0]` always 1.0 (L1 == authored exactly); rarer bands
   climb less per level but cost more (a levelled common never trivializes a legendary).
   Soft placeholder numbers — owner retunes values, structure stays.
3. **Improve verb** — `GearProgression.Improve(id, rarity)`: instant V1 (no Obsidian channel, per
   WO default-lean), charges **ResourceLedger** (the single GameState wallet — never the
   EconomyService in-session pool), honest refusals ("Already at max level." / "Need more Wood,
   Iron."), persists, refreshes every live GearLoadout, raises `GearProgression.Changed`.
4. **Pure resolver** — `GearStatResolver.EffectiveDamageMult/EffectiveDefense(def, level)` applied
   at the SINGLE combat choke point (`GearLoadout.ApplyStats` — the two lines every damage/
   mitigation consumer reads through `WeaponMult`/`ArmorDefense`). Defense multiplies THEN clamps
   0..0.9 (never approaches immunity). Melee, abilities, companions, hero mitigation all inherit.
5. **UI shows Lv N** (functional floor — the WO-798-family design pack re-skins later):
   - Forge/Armorer (PartyShop, the live vendor path): **Improve button** (x 0.04–0.28 action band,
     kit BuildObsidianButton; visible only on owned gear, luminance-dimmed per the colorblind law
     when maxed/unaffordable, label "Improve Lv N" / "Max Level"); gilt `[Lv N]` chip on owned
     rows; specs pane shows the live leveled Damage/Defense **and** an "Improve Lv N -> N+1 (+X)"
     preview line.
   - Inventory: top-right `Lv N` chip on tiles (badge, not a Button — never inflates to the 112px
     touch floor); detail stats read leveled power.
   - Equipment panel: the grant line reads "Lv N  +X% dmg ..." (leveled numbers).
   - MVVM law kept: ALL level/state math in VMs (GameStateService/GearCatalog never touched from
     Views); `ItemVM` gained a `Level` field (trailing optional ctor arg — zero caller churn).
6. **Oracles** — `GearProgressionTests` (9 EditMode: clamp, strict cost monotonicity, L1==baseline
   identity, strictly-stronger levels, defense safety clamp, unknown-rarity no-op, dict round-trip)
   + `GearLevelsRegression` in the batch gate (dual-copy identity, curve integrity, full rarity
   coverage vs weapons.json/armor.json — no shipped item can be silently ladder-less).

## Deliberate scope notes

- ShopPanel (legacy, `FeatureFlags.PartyShop`-OFF path) did NOT get Improve — dead path in live
  config; parity rides the WO-798 design implementation if wanted.
- InventorySidebar did NOT get a second CTA — the strip is sub-112px; a second button trips the
  MinTouch inflate-collision (UI map finding). Improve lives at the Forge/Armorer per the WO
  fantasy; the design pack decides any inventory-side entry point.
- Reach does not scale with level (separate touch point, owner call).
- No timers, no premium, no B/C models (owner lock).

## Owner acceptance remaining (PO closes, not CLI)

- [ ] Felt: improve an equipped weapon at the Forge → hits harder in the hub.
- [ ] Felt on Seeker.
- [ ] Retune gear-levels.json numbers if the curve feels off (data-only change).
