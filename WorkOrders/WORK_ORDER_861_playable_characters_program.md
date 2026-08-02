# WORK ORDER 861 — Make Sylas + Thrain playable (simplified)

**Status:** READY (Phase 0 + Sylas) · Thrain re-tune + Cathedral need owner art/detail. Architect-reviewed 2026-08-02.
**Author:** UI/QA triage + architect review (read-only, §13) — Claude UI
**Lane:** Hero/Class — gate-flips + content tuning; NO new class enum. (Cathedral = a small World/Building sub-piece.)
**Origin:** owner 2026-08-02 — add **Sylas** + **Thrain** as playable; *"simplify the ask."* Clarified:
> "Thrain should be a mage (DPS class), glass cannon at close combat." · "Sylas: only different arrow types, maybe an
> offhand dagger." · "Thrain should be a staff and needs the Cathedral of Learning for leveling."

---

## Both heroes reuse EXISTING authored classes — no new class code
- **Sylas → the Ranger slot** (already authored: kit, tree, gear, `Ranger.controller`, `Portraits/Sylas.png`; he's
  already your raid companion).
- **Thrain → the Mage slot** — which the game **already names "Thrain"** (`SlugFor: Mage→"Thrain"`, `HeroCatalog.cs:126`).
  So there is NO rename and NO conflict; just un-gate the Mage and re-tune it (below).
Both = un-gate + tune/author, not a 7-site new-class thread.

## Phase 0 — Unblock the roster (once; small SYS change)
Only the Knight is playable today, hardcoded. Flip the single-class gates:
- `GameStateService.ChooseHero` stops forcing Knight (`~:668-679`, `ff.knightonly`).
- `HeroSelectController.IsPlayable` widens past `PlayableHero = Knight` (`:107,:727`).
- `VendorStockResolver.RosterClasses()` follows the playable set, not the flag (`:102-107`).
- **Per-class hot-swap loadout key:** `HeroLoadout.PrefsKey` is a single global `"dotr-loadout-knight-v1"`
  (`HeroLoadout.cs:39`) → make it per-class, else heroes share one W/E/R bar.
- Roster EditMode test: each playable class resolves body + controller + kit + tree + gear + portrait; no coercion to Knight.

## Phase 1 — Sylas the Ranger (LIGHT)
- Add Ranger to the playable set (Phase 0).
- **Weapons = arrow types + an offhand dagger** (owner): slim the ranger weapon list to a few **arrow-type variants**
  (the "weapon" is the arrow — e.g. plain / fire / poison / frost, each a `job:ranger` bow-ammo entry) + one
  **offhand dagger** (`job:ranger` off-hand). Trim/point the ranger `weapons.json` rows to this small set (fits the
  WO-860 "2-per-level, equippable-only" store thinning).
- Confirm a **live** `Resources/Heroes/Ranger.fbx` (today parked `.tripo-extracted`) + the bow-attach path. **Owner:
  confirm/supply the live Ranger body.**
- Smoke-test: select → spawn → cast (mana) → equip an arrow type / dagger → raid (run-defenders fleet).

## Phase 2 — Thrain the Mage: close-combat DPS glass cannon (tune the existing kit)
> **Hero differentiation (owner):** heroes differ by their **default combat-HUD skills (the W/E/R bar)** and their
> **skill trees** — both already data-driven (`abilities.json` per class, `hero-talents.json` `trees.<class>`). So
> "make Thrain feel different" = author his default skills + his tree, not new systems. Same lever differentiates Sylas.

- Add the Mage to the playable set (Phase 0) — card already reads "Thrain".
- **RANGED DPS glass cannon (owner-corrected):** he attacks from RANGE (keep the mage's ranged abilities — do NOT
  shorten to melee), with **high damage but low HP/defense**. The danger is being **engaged up close** — if an enemy
  closes on him he **dies fast, especially when out of mana**. Tune the `mage` block in `abilities.json` (damage up,
  keep ranged) + Mage base stats (HP/defense down). Fragile, high-output, position-dependent.
- **Mana = the survival lifeline, not just a resource (owner):** enough mana → he can defend/escape a close engage;
  empty mana → he dies when swarmed. So **mana conservation + recovery is the core mechanic** — include mana-recovery
  (regen/restore ability + tree nodes) and mana-conservation (reduced cost / efficiency nodes). Managing mana IS the
  skill ceiling and the survival skill.
- **Defensive protection spells (owner):** at least one **timed protection** spell — a shield / damage-reduction buff
  for X seconds (an `AbilityDef` with a buff duration, mana-costed) — his cooldown to survive a close engage while he
  repositions. Out of mana = no protection = death.
- **Weapon = a staff** (owner): make the Mage starter + tier a **staff** line (`job:mage`, staff type — several
  already exist: `mage_oak`/`mage_arcane`/`tripo_staff_*`); ensure the close-combat staff is the starter.
- Confirm a **live** `Resources/Heroes/Mage.fbx` + `Portraits/Thrain.png`. **Owner: confirm/supply the Mage body/portrait.**
- Smoke-test: select → spawn → ranged cast + mana-recovery + defensive buff → equip staff; verify he's fragile when a mob closes in (dies fast without mana/protection).

## Phase 3 — Cathedral (Thrain's spell/mana progression) — the building ALREADY EXISTS
The building is real: **"Cathedral of Magic"** (`structures-catalog.json` id **`arcane-tower`**, `npcModel:"Mage"`,
singleton) with **real upgrade tiers** (Awaken the Tower / Mana Flow / Arcane Resonance / Arcane Overload — the
"Arcane Tower Enhancements" / Upgrade+Skills panel). **Do NOT build a new building.** (Owner calls it "Cathedral of
Learning" — same building; `OWNER CONFIRM`: rename its `displayName` to "Cathedral of Learning" or keep "of Magic".)
- **Wire the Cathedral's upgrades/Skills to Thrain (owner):** *"just needs to add learnable spells or better magic /
  mana / health logic as skills."* So the building's perk/skill tiers grant the mage **learnable SPELLS** (unlock
  `abilities.json` mage abilities into his W/E/R bar) and/or **better magic/mana/health** (mana pool + regen +
  spell power + HP nodes). Reuse the existing building perk system (`BuildingUpgradeVM` perks + the Skills tab) —
  route those perks to the mage's abilities/stats rather than tower stats.
- This IS Thrain's leveling seam — no new leveling system; the Cathedral's existing Upgrade/Skills path becomes his
  spellbook + mana/health progression.

## Owner items
1. **Art:** live Ranger body (Sylas) + live Mage body + `Portraits/Thrain.png` (Thrain). (Sylas portrait exists.)
2. **Cathedral (`arcane-tower`, "Cathedral of Magic"):** already exists with real upgrades — confirm rename to
   "Cathedral of Learning" (or keep), and which of its upgrade/skill tiers unlock which mage SPELLS vs. which buff
   mana/health/spell-power.
3. **Sylas arrow types:** confirm the set (e.g. plain / fire / poison / frost) + the offhand dagger.

## Acceptance
- [ ] >1 hero selectable; a confirmed non-Knight stays that class. Roster test green.
- [ ] Sylas (Ranger) plays with arrow-type weapons + offhand dagger, casts using mana.
- [ ] Thrain (Mage) plays as a RANGED glass cannon (high damage, low HP, dies fast when engaged up close without
      mana), with mana-recovery + a timed defensive-protection spell, staff-wielding, mana-based.
- [ ] `CompileGate` + `DataRegression` green; `abilities.json`/`weapons.json` Resources+StreamingAssets byte-identical.

## Do NOT
- Do NOT add a new `HeroClass` enum member (both heroes reuse Ranger/Mage). Do NOT break the Knight path.
- Keep echoes-never-fight/hero-tag canon; ASCII-only TMP; colorblind law; data-file copies byte-identical.
