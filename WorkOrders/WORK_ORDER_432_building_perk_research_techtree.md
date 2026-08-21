**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 432 — Building Perk/Research Layer + Tech-Tree Gate

**Status: READY TO IMPLEMENT** (owner-designed 2026-06-20; WO# provisional — confirm vs
`CLI_LANES_WO_NUMBERS.md`; 431 is held for the MVVM gate, so this took 432).
**Lane:** 6 Economy/Progression (+ 4 UI/HUD for the panel section). **Depends on:** WO-430 (built).
**Canon:** memory `building-upgrade-tier-perk-techtree`. **North star:** extend the existing data-driven
ladder; presentation stays MVVM; do NOT greenfield.

## Owner design — faithful WC3 adaptation (2026-06-20)
The owner pasted the full WC3 progression model and asked us to mimic it. Three pillars:
1. **Tech tiers** (WC3 Town Hall→Keep→Castle) → a global **Village/Stronghold Tier** at the Heart of
   Elarion. Gates which research LEVELS are buyable.
2. **Hero progression** (levels/attributes/ultimate) → **ALREADY BUILT** (`HeroTalentPanel`). Out of scope.
3. **Numerical & ability research at specialized buildings** (WC3 Blacksmith Lvl 1/2/3 damage/armor; caster
   Adept→Master + ability unlocks) → **the pillar this WO builds.**

Owner refinements — **BUILD TO THESE**:
- **BUTTON-DRIVEN** research UI: each upgrade is a button (Lvl 1/2/3) with its Gold cost, WC3-blacksmith style.
- **Damage/armor upgrades MIMIC WC3**: incremental NUMERICAL levels (e.g. +damage / +armor per level), per
  type where it fits. These are the "Numerical Stats" pillar — straightforward stat buttons.
- **"creative owns that"**: the AI/creative DESIGNS the ability unlocks + the Tier-3 signature capstones
  (the "Utility & Spell Training" / ability pillar). Latitude granted — pick what best incentivizes.
- **The faithful-WC3 gate**: research buttons show Lvl 1/2/3, but **Lvl 2 requires Village Tier 2 and Lvl 3
  requires Village Tier 3** — exactly WC3's "need a Keep for 2nd-tier upgrades, a Castle for 3rd."

**Building → WC3 research-building mapping:** Forge = weapon/damage, Armorer = armor, Arcane Tower =
caster/spell, Lumbermill = economy, Windmill = food/harvest. (Two upgrade FLAVORS per building: cheap
incremental NUMERICAL levels [Gold, WC3-style] + creative-owned ABILITY unlocks; Tier-3 signature = one
ability capstone.)

## What already exists (be the SME — do NOT rebuild)
- `Assets/_Modules/Core/State/BuildingTierCatalog.cs` + `Data/Canonical/building-tiers.json` — the WO-430
  ladder. `BuildingTierDef` = { tier, name, costWood/Food/Crystal, **modifiers: GameModifiers** }.
- `BuildingUpgradeService.TryUpgrade(id, tier)` — spends build resources, advances `GameState.BuildingTiers`.
- `ModifierService` — compiles `GameState.BuildingTiers` → active `GameModifiers` (TierOf, Changed event).
- `BuildingUpgradeVM` (MVVM) — exposes the tier ladder; `BuildingUpgradePanelMvvm` renders it. Economy
  exposes **`Coins` (= Gold)** already (`BuildingUpgradeVM.Coins`, `CostString` prints "Gold").
- Buildings: `arcane-tower, armorer, forge, lumbermill, windmill`.

## Scope (the delta to build)

### 1. Data — extend `building-tiers.json` + `BuildingTierDef`
Add two fields to `BuildingTierDef` (Core):
- `[JsonProperty("requiresVillageTier")] public int RequiresVillageTier;` — tech-gate (0 = no gate).
- `[JsonProperty("perks")] public List<BuildingPerkDef> Perks = new();`
New `BuildingPerkDef` (Core): `{ id, name, goldCost (int, = Coins), modifiers: GameModifiers, isSignature (bool) }`.
Author perks in JSON per building per tier. **Tier 3 of each building gets exactly one `isSignature` perk**
(the capstone). Creative may design the signature effects (see §5 suggestions) — encode as `GameModifiers`
or, where a modifier field doesn't exist, add a minimal flag to `GameModifiers` + honor it where the
building's behavior reads modifiers (do NOT add bespoke per-building code paths in the panel).

### 2. Tech-gate — `BuildingUpgradeService.TryUpgrade`
Before charging, gate: if `tierDef.RequiresVillageTier > 0 && VillageTier.Current < RequiresVillageTier`
→ refuse, return a typed reason ("Requires <town-center> Tier N"). **Town-center anchor (CONFIRM W/ OWNER):**
add a global **Village/Stronghold Tier** in `GameState` (`int VillageTier`), upgraded at the **Heart of
Elarion** (Keep is removed, §7). Provide `VillageTier.Current` + a `HeartController`-driven upgrade action.
If the owner prefers a dedicated building, swap the anchor — keep the gate read behind one accessor.

### 3. Perk purchase — new `BuildingPerkService`
`TryResearch(string buildingId, string perkId)`:
- find the perk in the catalog; verify its tier ≤ the building's current tier (gate: can't research a
  perk from a tier you haven't reached) and not already owned.
- spend `goldCost` via `IEconomy` (Coins). Refuse if unaffordable.
- record owned perk in `GameState` (new `List<string> OwnedBuildingPerks` keyed `buildingId:perkId`,
  additive SaveSchema — coordinate one-at-a-time per §5).
- apply via `ModifierService` (recompile so the perk's `GameModifiers` join the active set). Persist.

### 4. VM + View (MVVM — presentation never reads game state)
- `BuildingUpgradeVM`: add `IReadOnlyList<PerkVM> Perks` (only perks whose tier ≤ CurrentTier; each:
  id, name, goldCostString, Owned, Affordable, IsSignature) + a `PrereqLockReason` string per locked tier.
  Add command `Research(perkId)`. Raise `Changed` after a buy.
- `BuildingUpgradePanelMvvm`: render a **Research / Perks** section under the tier ladder — a Gold-cost
  button per perk (owned = checked/disabled; unaffordable = dimmed; signature = gilt-highlighted). Show
  the tier prereq-lock reason on gated tiers ("Locked — needs <center> Tier 2").

## Icon convention (so the owner's HUD icons auto-bind — NO drag-drop, per canon)
**OWNER'S CHOSEN PATH (2026-06-20): `Resources/HudItems/BuildingUpgrades/` — all PNG, Sprite Mode = Single.**
The building-upgrade/research icons resolve via `Resources.Load<Sprite>("HudItems/BuildingUpgrades/" + id)`
(NOT the legacy `HudIcons/` path — that stays for the resource counters `hud_wood/iron/food/crystal`).
Author each `BuildingPerkDef` with an `iconId` (defaults to the perk id); the View resolves
`HudItems/BuildingUpgrades/<iconId>`. Names the owner authors against (file = `<id>.png`, Sprite, Single):
- Tier rows: `tier-1`, `tier-2`, `tier-3`. Building icons: `arcane-tower`, `armorer`, `forge`,
  `lumbermill`, `windmill`.
- Research perks: `<building>-<perk>` e.g. `forge-damage-1/2/3`, `armorer-armor-1/2/3`,
  `arcane-spell-1/2/3`; signatures: `forge-masterwork`, `arcane-overload`, etc.
- Missing icon = `Debug.LogWarning` + a neutral placeholder (never an error / never blocks the button).
- **Implementer note:** the existing `BuildingUpgradePanel.ResourceIcon` hard-codes `HudIcons/`; the WO-432
  research/tier icon resolver must read `HudItems/BuildingUpgrades/`. Keep both — don't repoint the legacy one.

## Acceptance criteria
- [ ] `building-tiers.json` carries `requiresVillageTier` + `perks[]`; Tier 3 of each of the 5 buildings has
      exactly one `isSignature` perk.
- [ ] Upgrading to a gated tier is REFUSED with a clear reason until the town-center tier is met.
- [ ] Researching a perk spends Gold (Coins), persists, and its `GameModifiers` take effect (verify via
      `ModifierService` recompile — a headless test asserts the active modifier changed).
- [ ] Owned perks survive save/reload (SaveSchema additive).
- [ ] Panel shows the perk section + lock reasons; MVVM seam intact (View reads only the VM).
- [ ] EditMode test: tier-gate refuses below prereq; perk buy spends Coins + applies modifier; reload keeps perks.

## What NOT to touch
- The existing tier-ladder cost/advance math (`BuildingUpgradeService.TryUpgrade` charging) — only ADD the
  prereq gate ahead of it.
- The MVVM seam — no game-state reads in the View.
- Resource buildings' legacy level curve (`ResourceBuildingState`) — out of scope.
- Do NOT hand-edit scenes.

## §5 — Signature perk ideas (creative; owner may override)
- **arcane-tower** → *Arcane Overload*: shots chain to a 2nd target (or periodic nova).
- **forge** → *Masterwork*: chance to craft one tier higher for free.
- **armorer** → *Tempered Plate*: party gains a small flat damage-reduction aura.
- **lumbermill** → *Heartwood*: periodic burst of bonus Wood + cheaper wall upgrades.
- **windmill** → *Bountiful Harvest*: passive Food trickle even off-shift / boosts pet harvest yield.

## Open decision for owner (one)
**Tech-gate anchor:** confirm the prerequisite is a global **Village/Stronghold Tier upgraded at the Heart
of Elarion** (recommended, since the Keep was removed), vs. a dedicated new town-center building. The spec
reads the gate behind one accessor so the anchor is swappable.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
