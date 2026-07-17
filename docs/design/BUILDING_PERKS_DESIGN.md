# Building Perks -- Effect Mapping for the Owner's Upgrade Trees

Status: PROPOSAL / mapping doc (owner sign-off pending). Design + JSON draft only.
Do NOT commit the JSON until approved; the orchestrator applies it and mirrors
StreamingAssets.

This doc CONFORMS to the authoritative `docs/design/BUILDING_UPGRADE_TREES.md`
(owner-authored). It does not re-invent the trees -- it MAPS every tier upgrade and
capability there to a concrete engine effect, split **MAPS-TODAY** (a live
`GameModifiers` field, committable now) vs **NEEDS-NEW-SYSTEM** (must be built first).
Grounded in the actual code (cited file:line) per the CLAUDE.md SME-first rule.

---

## 1. SME ground truth -- the effects the engine can grant TODAY

A perk's `modifiers` object compiles into the one active `GameModifiers` via
`ModifierService.Apply` (`Assets/_Modules/Core/State/ModifierService.cs:124-139`),
which multiplies the mults and OR-s the flags. A perk can ONLY move a field that exists
on `GameModifiers` (`Assets/_Modules/Core/State/GameModifiers.cs`).

### Live numeric mults (verified consumers -- these DO something)

| JSON key                 | Effect                       | Proving consumer |
|--------------------------|------------------------------|------------------|
| `towerDamageMult`        | Tower damage                 | `DefenseTower.cs:234`, `ArcaneTower.cs:212`, `Tower.cs:210` |
| `towerRangeMult`         | Tower range                  | `DefenseTower.cs:236`, `ArcaneTower.cs:213`, `Tower.cs:191` |
| `troopDamageMult`        | Companion/troop damage (RAID)| `TroopDeployer.cs:74` |
| `troopHealthMult`        | Companion/troop health (RAID)| `TroopDeployer.cs:75` |
| `woodProductionMult`     | Wood yield per harvest tick  | `ResourceBuildingState.cs:102` via `ModifierService.ProductionMultFor("lumbermill")` (`:60`) |
| `foodProductionMult`     | Food yield per harvest tick  | `ProductionMultFor("windmill")` (`:61`) |
| `resourceEfficiencyMult` | Forge generic yield mult     | `ProductionMultFor("forge")` (`:62`) |
| `offlineBonusMult`       | Offline accrual bucket size  | folded `:133`; contract `GameModifiers.cs:46` |

Default of every mult = 1.0 (no-op). **These eight are the entire palette of
committable effects.** Note: `woodProductionMult` scales yield PER TICK, which is the
economic equivalent of "gather faster"; literal harvest-interval acceleration is
level-driven (`ResourceBuildingState.cs:111 CurrentHarvestInterval`), NOT modifier-driven,
so a "+50% gather" perk ships as a +50% YIELD mult today.

### Ability flags -- DECLARED but INERT (no consumer wired)

`arcaneOverload`, `battleForged`, `forgefire`, `eternalGrove`, `windsOfPlenty`
(`GameModifiers.cs:49-53`). Folded by `ModifierService` (`:134-138`) but a repo-wide
search finds NO gameplay consumer. The existing Tier-4 capstones that set these flags do
nothing yet. Any signature leaning on one is NEEDS-NEW-SYSTEM (needs its consumer built).

---

## 2. Resource pools -- all four EXIST (mapping only, no new pool needed)

`EconomyService` (`Assets/_Modules/Village/EconomyService.cs`) owns four spendable pools
plus Gold. The tree's four pools map cleanly onto them:

| Tree pool          | Engine pool  | Proof | Status |
|--------------------|--------------|-------|--------|
| Wood (Lumbermill)  | Wood         | `EconomyService.cs:121` | EXISTS |
| Food (Granary)     | Food         | `EconomyService.cs:129` (GameState.Resources.Food) | EXISTS |
| Metal (Smithy)     | **Iron**     | `EconomyService.cs:122`; `ResourceType.Iron` (`:372`) | EXISTS (named "Iron") |
| Essence/Aether (Forge) | **Crystals / AetherCrystal** | `EconomyService.cs:143`; `ResourceType.AetherCrystal` (`:381`) | EXISTS (named "Crystals") |

So NO new resource pool is required -- "Metal" = the Iron axis, "Essence/Aether" = the
Crystals axis. Gold (Coins, `:157`) is the perk RESEARCH currency (`BuildingPerkService`
spends `perk.GoldCost`).

**One schema gap to flag:** `BuildingTierDef` only has `costWood`/`costFood`/`costCrystal`
(`BuildingTierCatalog.cs:55-57`) -- there is **no `costIron` (Metal) field**. Tier-upgrade
costs cannot charge Metal today. If Smithy tiers should cost Metal, add `costIron` to
`BuildingTierDef`. (Perks themselves cost Gold only, so the phase-1 JSON below is
unaffected.)

---

## 3. Building-id reconciliation (open question -- recommend + flag)

The live perk system (`building-tiers.json`) has SIX ids: `arcane-tower`, `armorer`,
`barracks`, `forge`, `lumbermill`, `windmill`. The owner's canon has FOUR buildings.
Recommended mapping (orchestrator/owner to confirm before authoring ids):

| Tree building        | Pool    | Recommended existing id | Note |
|----------------------|---------|-------------------------|------|
| Lumbermill           | Wood    | `lumbermill`            | direct match |
| Granary              | Food    | `windmill`              | reframe displayName Windmill -> Granary |
| Smithy               | Metal   | `armorer` (Blacksmith)  | the military building; rename -> Smithy |
| Forge (Magic)        | Essence | `arcane-tower`          | the magic building; the new "Forge" fantasy |

Left over: the existing `forge` (resource-efficiency) and `barracks` (troops) ids fall
OUTSIDE the new four-building canon. Decision needed: retire, merge into the four, or keep
as bonus tabs. `barracks`' army-cap/train fantasy is largely absorbed by **Granary** in
the new canon (companion count). I author the phase-1 JSON below under the recommended ids;
if the owner prefers new ids (`granary`, `smithy`), it is a find/replace on the drafts.

---

## 4. The four trees, mapped effect-by-effect

Legend: **MAPS-TODAY** (live field, in phase-1 JSON) / **NEEDS-NEW** (system to build,
phase 2). T0 = base tier (opens the tab), T1/T2 = the two research tiers.

### 4.1 Lumbermill -- Wood / Construction  (id `lumbermill`)

| Tier | Capability (from canon)                    | Mapping |
|------|--------------------------------------------|---------|
| T0   | Slow wood income; basic walls              | baseline `woodProductionMult` 1.0 -- MAPS (no-op base) |
| T1   | +50% wood gathering speed                  | **MAPS-TODAY** `woodProductionMult` 1.50 (as yield) |
| T1   | Stronger basic towers (wooden barricades)  | **MAPS-TODAY** `towerDamageMult` (+dmg); NEW tower TYPE = NEEDS-NEW |
| T1   | Faster outpost repairs                     | **NEEDS-NEW** `repairSpeedMult` -> WallRepairController (cost-based today, no speed axis) |
| T2   | Auto-gather from distant nodes             | **NEEDS-NEW** `autoCollect` flag + distant-node harvest service (collectors accrue but need a manual `CollectAll` tap, `ResourceCollectorService.cs:17`) |
| T2   | Reinforced outposts reduce wave damage     | **NEEDS-NEW** `structureArmorMult` -> wall/outpost toughness (level-stepped in catalog, no modifier) |
| T2   | Wood -> temporary defense buffs            | **NEEDS-NEW** spend-resource active-buff system |

### 4.2 Granary -- Food / Population & Sustain  (id `windmill`)

| Tier | Capability                                  | Mapping |
|------|---------------------------------------------|---------|
| T0   | Basic food; small tower health-regen aura   | food baseline MAPS; tower-regen aura = **NEEDS-NEW** (no tower-HP/regen modifier) |
| T1   | +max companion count                        | **NEEDS-NEW** `armyCapBonus` (int) -> `ArmyStorage.MaxArmySize` (const 10, `ArmyStorage.cs:42,48`; comment says "expandable later via a barracks tier"). Cheap, high value. |
| T1   | "Bounty": boosts gather yield for one run   | permanent version = **MAPS-TODAY** `foodProductionMult`/`woodProductionMult`; timed one-run buff = **NEEDS-NEW** (buff timer) |
| T2   | Passive food income even OFFLINE            | bucket-size = **MAPS-TODAY** `offlineBonusMult`; true offline GENERATION = **NEEDS-NEW** (offline accrual service) |
| T2   | High-tier support towers (life totems)      | **NEEDS-NEW** new tower type + heal aura |
| T2   | Hero "Feast" (heal towers + companions)      | **NEEDS-NEW** hero ability system |

### 4.3 Smithy -- Metal / Military  (id `armorer`, pool Iron)

| Tier | Capability                                  | Mapping |
|------|---------------------------------------------|---------|
| T0   | Basic metal for simple tower dmg/armor       | tower dmg = **MAPS-TODAY** `towerDamageMult`; tower ARMOR/hp = **NEEDS-NEW** (no tower-HP modifier) |
| T1   | Tower weapon tiers (Iron -> Steel)          | numeric buff = **MAPS-TODAY** `towerDamageMult`; discrete tier UNLOCK + model = **NEEDS-NEW** |
| T1   | Heroes get better gear for gathering runs   | companion proxy = **MAPS-TODAY** `troopDamageMult`/`troopHealthMult`; real hero GEAR = **NEEDS-NEW** (hero equipment; HeroTalent tree is separate) |
| T2   | Legendary upgrades (magic dmg, chain lightning) | **NEEDS-NEW** new tower mechanics |
| T2   | Epic towers                                 | **NEEDS-NEW** new tower type |
| T2   | Permanent hero equipment slots              | **NEEDS-NEW** equip-slot system |
| T2   | Salvage enemy drops into metal              | **NEEDS-NEW** salvage/drop system |
| T2   | (numeric war payoff)                        | **MAPS-TODAY** `troopDamageMult` + `towerDamageMult` stack |

### 4.4 Forge -- Magic / Essence (Aether)  (id `arcane-tower`, pool Crystals)

| Tier | Capability                                  | Mapping |
|------|---------------------------------------------|---------|
| T0   | Essence -> basic spell runes for towers      | numeric tower buff = **MAPS-TODAY** `towerDamageMult`; rune-conversion system = **NEEDS-NEW** |
| T1   | Elemental tower upgrades                     | numeric = **MAPS-TODAY** `towerDamageMult` + `towerRangeMult`; elemental TYPING = **NEEDS-NEW** (catalog has `element` but no modifier axis) |
| T1   | Companion spells (firestorm during defense) | **NEEDS-NEW** companion ability system |
| T2   | Master magic (global cooldown reductions)   | **NEEDS-NEW** cooldown system |
| T2   | Hero ultimates                              | **NEEDS-NEW** hero ability system |
| T2   | "Realm Echo" (temp super-towers / wave-clear)| **NEEDS-NEW** -- closest inert hook is the `arcaneOverload` flag (wire it) |
| T2   | (numeric magic payoff)                      | **MAPS-TODAY** `towerDamageMult` + `towerRangeMult` stack |

---

## 5. Ships-now vs Phase-2 (the greenlight split)

### Ships now (phase 1) -- pure `GameModifiers` mults, committable JSON in section 6
- Lumbermill: +wood yield ("gather speed"), stronger basic towers (+tower dmg).
- Granary: +food yield, bigger offline bucket, permanent Bounty (+yield).
- Smithy: tower weapon-tier NUMBERS (+tower dmg), hero/companion gear NUMBERS (+troop dmg/hp).
- Forge: elemental tower NUMBERS (+tower dmg/range).

### Phase 2 -- NEEDS-NEW-SYSTEM (owner greenlight; scope each as its own WO)

Ordered by value-per-effort (owner's own examples first, cheapest first):

| New system                    | Field / hook                        | Powers | Effort |
|-------------------------------|-------------------------------------|--------|--------|
| Companion/army cap            | `armyCapBonus` (int) -> `ArmyStorage.MaxArmySize` | Granary T1 (+companions) | XS -- one field + one line |
| Auto-gather / passive collect | `autoCollect` flag + ticking service (wire `eternalGrove`/`windsOfPlenty`) | Lumbermill T2, Granary T2 offline | S |
| Offline income generation     | offline accrual service (beyond `offlineBonusMult` bucket) | Granary T2 passive food | S |
| Tower toughness / armor       | `towerArmorMult` / `structureArmorMult` | Smithy T0 armor, Lumbermill T2 reinforced outposts | S |
| Repair speed                  | `repairSpeedMult` -> WallRepairController | Lumbermill T1 faster repairs | S |
| Wave burst super-tower        | wire `arcaneOverload` flag to a wave hook | Forge T2 Realm Echo, Overload | S |
| Timed run buffs               | buff-timer system | Granary "Bounty", wood->defense buffs | M |
| Tower weapon-tier UNLOCKS     | discrete tier gate + model swap | Smithy T1 Iron->Steel | M |
| Hero equipment + slots        | hero gear/equip system | Smithy T1/T2 hero gear | L |
| Salvage drops -> metal        | drop + salvage loop | Smithy T2 | M |
| Elemental typing              | element damage axis (catalog `element` unused by combat) | Forge T1 | M |
| Companion spells / hero ults / Feast | ability system | Granary Feast, Forge spells/ults | L |
| Per-tier building MODEL SWAPS | visual tier swap (StructureTierVisual exists for placed structures) | all buildings visual progression | M |

**Do first (owner's named levers, both cheap):** `armyCapBonus` (Granary companions) and
`autoCollect` (Lumbermill auto-gather). Both are exactly the WC3 fantasies the owner
called out and both are small.

---

## 6. Phase-1 JSON draft (MAPS-TODAY perks only)

Matches the authored `BuildingPerkDef` schema (id/name/effect/goldCost/iconId/
isSignature/modifiers). Drop each `perks` array into the matching tier of
`Assets/Resources/Data/Canonical/building-tiers.json`, then MIRROR byte-equal to
`Assets/StreamingAssets/Data/Canonical/building-tiers.json`. Gold costs follow the
existing curve (T1 ~300, T2 ~600-800, T2-signature ~1200-1600). `iconId` reuses existing
sprites; owner can reskin. NEEDS-NEW perks are intentionally EXCLUDED (section 5 table).
Ids are proposed under the section-3 recommended building ids.

```jsonc
// === LUMBERMILL (Wood) — id "lumbermill" ===
// T1 (Sawmill) perks:
[
  { "id": "lumber-sawmill-yield", "name": "Sawmill Blades", "effect": "Wood yield +50%", "goldCost": 350, "iconId": "Lumber_Mill_T1_Improved_Logging", "isSignature": false, "modifiers": { "woodProductionMult": 1.50 } },
  { "id": "lumber-barricades", "name": "Wooden Barricades", "effect": "Tower damage +6%", "goldCost": 400, "iconId": "Lumber_Mill_T1_Construction_Aid", "isSignature": false, "modifiers": { "towerDamageMult": 1.06 } }
]
// T2 (Ancient Sawmill) perks:
[
  { "id": "lumber-deep-reserves", "name": "Deep Reserves", "effect": "Wood +20%, offline bucket +15%", "goldCost": 1200, "iconId": "Lumber_Mill_T1_Construction_Aid", "isSignature": true, "modifiers": { "woodProductionMult": 1.20, "offlineBonusMult": 1.15 } }
]

// === GRANARY (Food) — id "windmill" ===
// T1 (Harvest Granary) perks:
[
  { "id": "granary-bounty", "name": "Bounty Harvest", "effect": "Food yield +30%", "goldCost": 350, "iconId": "Lumber_Mill_T1_Improved_Logging", "isSignature": false, "modifiers": { "foodProductionMult": 1.30 } },
  { "id": "granary-tower-victuals", "name": "Field Rations", "effect": "Companion health +6%", "goldCost": 400, "iconId": "Lumber_Mill_T1_Efficient_Processing", "isSignature": false, "modifiers": { "troopHealthMult": 1.06 } }
]
// T2 (Eternal Granary) perks:
[
  { "id": "granary-preserves", "name": "Eternal Preserves", "effect": "Food +20%, offline bucket +15%", "goldCost": 1200, "iconId": "Lumber_Mill_T1_Construction_Aid", "isSignature": true, "modifiers": { "foodProductionMult": 1.20, "offlineBonusMult": 1.15 } }
]

// === SMITHY (Metal/Iron) — id "armorer" ===
// T1 (Armory Smithy) perks:
[
  { "id": "smithy-steel-edges", "name": "Steel Edges", "effect": "Tower damage +8%", "goldCost": 400, "iconId": "Blacksmith_T1_Sharpened_Edges", "isSignature": false, "modifiers": { "towerDamageMult": 1.08 } },
  { "id": "smithy-hero-gear", "name": "Wayfarer's Kit", "effect": "Companion damage +8%", "goldCost": 450, "iconId": "Blacksmith_T1_Reinforced_Plating", "isSignature": false, "modifiers": { "troopDamageMult": 1.08 } }
]
// T2 (Rune-Forged Smithy) perks:
[
  { "id": "smithy-runeforged", "name": "Rune-Forged Arms", "effect": "Tower damage +12%, companion damage +12%", "goldCost": 1600, "iconId": "Blacksmith_T1_Sturdy_Shields", "isSignature": true, "modifiers": { "towerDamageMult": 1.12, "troopDamageMult": 1.12 } }
]

// === FORGE (Essence/Aether) — id "arcane-tower" ===
// T1 (Arcane Forge) perks:
[
  { "id": "forge-elemental-bolts", "name": "Elemental Infusion", "effect": "Tower damage +8%", "goldCost": 400, "iconId": "Arcane_Tower_T1_Arcane_Basics", "isSignature": false, "modifiers": { "towerDamageMult": 1.08 } },
  { "id": "forge-focusing-lens", "name": "Focusing Runes", "effect": "Tower range +8%", "goldCost": 450, "iconId": "Arcane_Tower_T1_Warding_Runes", "isSignature": false, "modifiers": { "towerRangeMult": 1.08 } }
]
// T2 (Elarion Forge) perks:
[
  { "id": "forge-elarion-mastery", "name": "Elarion Mastery", "effect": "Tower damage +14%, range +10%", "goldCost": 1600, "iconId": "Arcane_Tower_T1_Warding_Runes", "isSignature": true, "modifiers": { "towerDamageMult": 1.14, "towerRangeMult": 1.10 } }
]
```

Each `perks` array goes on the corresponding tier object (T1 -> the tier-2 def, T2 -> the
tier-3 def, under the recommended-id mapping). The existing Tier-4 flag-setting tiers stay
untouched. When a phase-2 system lands (e.g. `autoCollect`), swap the interim signature
for the qualitative capstone (e.g. Lumbermill's Deep Reserves -> a true "Ancient Sawmill:
auto-gather" perk).

---

## 7. Summary for the owner

- **All four resource pools already exist** (Wood, Food, Metal=Iron, Essence=Crystals) --
  no new pool needed, just naming. One gap: tier upgrades can't cost Metal yet (add
  `costIron` to the tier schema if wanted).
- **Ships now (phase 1):** every quantitative lever in your trees maps to a live modifier
  -- +gather yield, +tower damage/range, +companion damage/health, bigger offline bucket.
  JSON drafted in section 6 (four buildings, two research tiers each, one signature each).
- **Phase 2 (needs new systems):** the qualitative WC3 capstones -- companion cap,
  auto-gather, offline income, tower armor, weapon-tier unlocks, hero equipment/salvage,
  spells/ultimates/Feast/Realm Echo, and per-tier model swaps. Prioritized in section 5;
  **`armyCapBonus` (companions) and `autoCollect` (auto-gather) are the two cheap,
  highest-value builds to greenlight first.**
