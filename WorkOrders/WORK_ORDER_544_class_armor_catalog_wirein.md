<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-03
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-03) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-544 — Class-Specific Armor Catalog Wire-In

**Status:** READY TO IMPLEMENT  
**Lane:** 4 — Store / Inventory / Gear  
**Size:** S  
**Mint date:** 2026-06-27  
**Depends on:** WO-542 (ShopPanel icon rendering — iconPath loading already specced)

---

## What & Why

The armor catalog was expanded from 5 to 20 entries (v2) this session. 14 class-specific
shop icons are now live in `Assets/Resources/ItemIcons/`. The code needs three small changes
to wire everything in cleanly:

1. `ArmorDef` is missing the `perk` field — JSON has it, C# drops it on deserialize.
2. `Assets/StreamingAssets/Data/Canonical/armor.json` is still v1 (5 entries). The game
   reads Resources-first (`CanonicalJson.Read`), so it already gets the v2 file — but
   StreamingAssets is the fallback and must be kept in sync.
3. The shop `aStats` line only shows defense + hp. Perk text should surface in the shop
   detail panel so players know what class bonus they're buying.
4. 25 dead `blink_armor_*` images still live in `ItemIcons/` — dead weight, delete them.

---

## Data — already complete, do not overwrite

**`Assets/Resources/Data/Canonical/armor.json`** — v2, 20 entries  
5 universal + 5 knight + 5 ranger + 5 mage. Fully authored (names, lore, stats, pricing,
makers' marks, perk text). **Read-only for this WO.**

### Full 20-entry roster for reference

| id | name | job | rarity | defense | hpBonus | perk |
|---|---|---|---|---|---|---|
| `armor_cloth` | Wanderer's Cloth | any | common | 0.04 | 10 | — |
| `armor_leather` | Tanned Leather | any | uncommon | 0.08 | 25 | — |
| `armor_chain` | Chainmail Vest | any | rare | 0.14 | 45 | — |
| `armor_plate` | Elarion Plate | any | epic | 0.20 | 75 | — |
| `aegis_plate` | Aegis of Elarion | any | legendary | 0.28 | 100 | Aegis set |
| `armor_knight_common` | Ironward Plate | knight | common | 0.06 | 20 | Reduces incoming melee damage by a small additional amount while standing still. |
| `armor_knight_uncommon` | Bastion Plate | knight | uncommon | 0.12 | 40 | Extended stun on Shield Bash when HP is above 75%. |
| `armor_knight_rare` | Vigil Plate | knight | rare | 0.18 | 60 | Gain a brief damage reduction buff after landing a charged attack. |
| `armor_knight_epic` | Emberbrand Plate | knight | epic | 0.25 | 85 | Charge attacks release an ember burst on impact, dealing bonus fire-adjacent damage. |
| `armor_knight_legendary` | Oathplate of Elarion | knight | legendary | 0.35 | 120 | A portion of damage taken is reflected to attackers. Oathweld runes pulse brighter as HP falls. |
| `armor_ranger_common` | Scout's Leather | ranger | common | 0.05 | 12 | Slightly increased movement speed when no enemies are within close range. |
| `armor_ranger_uncommon` | Shadowhide Vest | ranger | uncommon | 0.10 | 28 | Extends Snare Arrow root duration. |
| `armor_ranger_rare` | Heartwood Warden | ranger | rare | 0.16 | 50 | Increases critical chance on ranged attacks. |
| `armor_ranger_epic` | Windstrider Coat | ranger | epic | 0.22 | 70 | Dodge chance increases briefly after firing Suppressing Volley. |
| `armor_ranger_legendary` | Leafcloak of Elarion | ranger | legendary | 0.30 | 95 | Aim charges faster. Nature-adjacent damage bonus on all ranged attacks. Glows green near the Heart. |
| `armor_mage_common` | Apprentice Robes | mage | common | 0.03 | 8 | Minor increase to mana regeneration rate. |
| `armor_mage_uncommon` | Aetherweave Mantle | mage | uncommon | 0.08 | 22 | Reduces Frost Nova cooldown. |
| `armor_mage_rare` | Arcane Sigil Vestments | mage | rare | 0.13 | 40 | Increases all spell damage. Runes glow brighter with each successive cast. |
| `armor_mage_epic` | Starwoven Robe | mage | epic | 0.20 | 65 | Meteor Strike pulls nearby enemies toward the point of impact. |
| `armor_mage_legendary` | Aethercloak of Elarion | mage | legendary | 0.28 | 90 | Reduces all spell mana costs. Chance for spell echo on cast. Trails blue aether light when moving. |

### Icons — already in place

All 14 class-specific PNGs are live in `Assets/Resources/ItemIcons/`:
`armor_knight_common.png` through `armor_mage_legendary.png`  
(Except `armor_mage_common.png` — pending one more Grok image. Shop will emoji-fallback until it lands.)

---

## Part A — Add `perk` field to `ArmorDef`

**File:** `Assets/_Modules/Village/Hero/GearCatalog.cs`

After `public float hpBonus;` (line ~151), add:

```csharp
/// <summary>WO-544: Passive class bonus descriptor. Data-only v1 — surfaced in shop
/// detail text. Gameplay effect wired in a future WO per class ability pass.</summary>
public string perk;
```

No other changes to `ArmorDef`. Newtonsoft will populate it from JSON; null = no perk
(the 5 universal armors omit the field and that's fine).

---

## Part B — Sync StreamingAssets armor.json

**File:** `Assets/StreamingAssets/Data/Canonical/armor.json`

Replace with the current contents of `Assets/Resources/Data/Canonical/armor.json` (v2, 20 entries).

The game reads Resources-first, so this is a sync-only step — not urgent, but keeps the
fallback path correct and avoids confusion on Android/WebGL builds.

---

## Part C — Surface `perk` in shop detail stat line

**File:** `Assets/_Modules/Village/Hero/ShopVM.cs`

In the armor row-building loop (currently around line 337), the `aStats` string is:

```csharp
string aStats = "+" + aDefPct + "% def" + (a.hpBonus > 0f ? "   +" + Fmt1(a.hpBonus) + " hp" : "") + DeltaVsEquipped(a);
```

Change to:

```csharp
string aStats = "+" + aDefPct + "% def"
    + (a.hpBonus > 0f ? "   +" + Fmt1(a.hpBonus) + " hp" : "")
    + DeltaVsEquipped(a)
    + (!string.IsNullOrEmpty(a.perk) ? "\n" + a.perk : "");
```

This appends the perk line below the stat numbers in the shop detail panel. The `\n`
matches the existing multiline format used in ability descriptions. If `perk` is null/empty
(universal armors) nothing is added — zero regression.

**FlowTrace:** add `FlowTrace.Step("Shop", $"armor detail built: {a.id} perk={a.perk ?? "none"}")` 
adjacent to the existing armor-row FlowTrace (if present) or as a new Step. Guard nothing — 
the null-check on `a.perk` is already in the string above.

---

## Part D — Delete dead blink_armor_* images

**Folder:** `Assets/Resources/ItemIcons/`

Delete these 25 files + their `.meta` pairs (50 total):
`blink_armor_basic1.png` through `blink_armor_basic9.png` (9 files)  
`blink_armor_bear.png`, `blink_armor_beasthunter.png`, `blink_armor_bird.png`,
`blink_armor_boar.png`, `blink_armor_centurion.png`, `blink_armor_demonhunter.png`,
`blink_armor_dragonhunter.png`, `blink_armor_dragonic.png`, `blink_armor_engineer.png`,
`blink_armor_hydra.png`, `blink_armor_landwarrior.png`, `blink_armor_lionguard.png`,
`blink_armor_minotaur.png`, `blink_armor_minotaur.png`, `blink_armor_pantherknight.png`,
`blink_armor_savage.png` (16 files)

These are all Blink-rig assets (rig removed 2026-06-09). Nothing in the live codebase
references them. Safe to delete.

---

## Do NOT Touch

- `Assets/Resources/Data/Canonical/armor.json` — fully authored, hands off
- `Assets/Resources/Data/Canonical/weapons.json` — separate system, wrong lane
- `Village.unity` — never
- Any UXML — WebGL incompatible
- `VillageSceneBuilder.cs` — wrong lane

---

## Acceptance Criteria

- [ ] `GearCatalog.AllArmors()` returns 20 entries (headless DataRegression assert)
- [ ] `ArmorDef.perk` deserializes correctly — e.g. `FindArmor("armor_knight_epic").perk` == the Emberbrand perk string
- [ ] Shop detail panel for `armor_knight_epic` shows "Charge attacks release an ember burst…" below the stat line
- [ ] Universal armors (`armor_cloth` etc.) show NO perk line — no null exception, no extra newline
- [ ] StreamingAssets `armor.json` matches Resources version (20 entries, `version: 2`)
- [ ] `blink_armor_*` images gone from ItemIcons — no broken references (grep `blink_armor_` in all `.cs` and `.json` to confirm nothing references them)
- [ ] Brace balance on every `.cs` touched

---

## Verification

**Headless:** `ArmorCatalogTest` — assert `GearCatalog.AllArmors().Count == 20`; assert
`GearCatalog.FindArmor("armor_ranger_legendary").perk != null`; assert universal armor
perk is null or empty (no regression).

**Felt (PO verifies):** Open armorer shop → tap Emberbrand Plate → detail shows ember
perk text. Tap Wanderer's Cloth → no perk line, no broken layout.
