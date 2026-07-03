# WO-543 — Rings, Amulets & Armor/Accessory VFX

**Status:** READY TO IMPLEMENT  
**Lane:** 4 — Store / Inventory / Gear (save-schema field: coordinate with Lane 3)  
**Size:** M  
**Mint date:** 2026-06-27  

---

## What & Why

Add rings and amulets as a third gear category — pure JSON stat modifiers, **no 3D mesh
attachment, no body mesh swap.** The only visual the player sees is:
1. The shop icon (2D PNG in the shop window — same as weapons/armor, WO-542 handles iconPath loading)
2. A VFX glow on the hero for armor rarity (new `ArmorVfxMap`, mirrors `WeaponVfxMap`)

This unlocks gear depth cheaply: 5 rings + 5 amulets = 10 new items from pure data.

Accessories are sold exclusively at **Sable Vey's Jeweler shop** (dialogue: `NPC_Jeweler.yarn`,
shop key `"jeweler"`). VendorStockContract for the jeweler should list all `accessories` entries.

---

## Data

Canonical file (already written, do not overwrite):
`Assets/Resources/Data/Canonical/accessories.json`

Schema per entry:

| Field | Type | Meaning |
|---|---|---|
| `id` | string | unique key |
| `name` | string | display name |
| `category` | `"ring"` \| `"amulet"` | accessory type |
| `slot` | `"ring"` \| `"amulet"` | equip slot key |
| `job` | string | `"any"` for all v1 accessories |
| `rarity` | string | common / uncommon / rare / epic / legendary |
| `damageMult` | float | **additive** bonus to hero's damage chain (0.08 = +8%). 0 or absent = no bonus. |
| `defense` | float | **additive** DR on top of armor (0.05 = +5%). 0 or absent = no bonus. |
| `hpBonus` | int | flat HP added to hero max HP. 0 or absent = no bonus. |
| `setId` | string | optional — links to the Aegis set (`"aegis"`) |
| `makersMark` | string | optional — used by ArmorVfxMap for tint |
| `iconPath` | string | `Resources.Load<Sprite>()` path — same pattern as WO-542 |
| `flavor` / `saga` | string | lore text |
| `req.level` | int | equip level gate |
| `buyWood/Iron/Food/Crystals` | int | shop prices |

---

## Part A — AccessoryDef + GearCatalog extension

**New file:** `Assets/_Modules/Village/Hero/AccessoryDef.cs`  
Mirror the pattern of `WeaponDef` / `ArmorDef` in `GearCatalog.cs`.

```csharp
[Serializable]
public sealed class AccessoryDef
{
    public string id;
    public string name;
    public string icon;
    public string category;   // "ring" | "amulet"
    public string slot;
    public string job;
    public string rarity;
    public float  damageMult; // additive bonus (0 = no bonus)
    public float  defense;    // additive DR bonus (0 = no bonus)
    public int    hpBonus;
    public string setId;
    public string makersMark;
    public string iconPath;
    public string flavor;
    public string saga;
    public GearReq req;
    public int buyWood; public int buyFood; public int buyIron; public int buyCrystals;
}
```

**`GearCatalog.cs`** — add:
- `AccessoryCatalogRoot` wrapper class (same as `WeaponCatalogRoot` / `ArmorCatalogRoot`)
- `public static IReadOnlyList<AccessoryDef> Accessories` property
- Load from `StreamingAssets/accessories.json` in `LoadAll()` (or on first access — match existing lazy-load pattern)

---

## Part B — Equip slots in EquipVM

`EquipVM.cs` currently declares `SlotMainhand` and `SlotChest`. Add:

```csharp
public const string SlotRing   = "ring";
public const string SlotAmulet = "amulet";
```

Add these two slots to `_equipSlots` in `BuildSlots()` (after chest slot). The slot
renders with the accessory's `iconPath` sprite (same WO-542 path) or the `icon` emoji
fallback. Compatible list for a ring slot = all `AccessoryDef` where `slot == "ring"` and
`req.level <= hero.level`. Same for amulet.

**Save schema (coordinate with Lane 3):**  
`SaveSchema.cs` needs two new equipped-item fields: `equippedRingId` and `equippedAmuletId`
(strings, empty = nothing equipped). Bump save version. Lane 3 owns the version bump —
route this as a dependent task on Lane 3's current sprint.

---

## Part C — Stat application on equip

`GearLoadout.cs` (or wherever `EquippedWeapon` / `EquippedArmor` is resolved):  
When an accessory is equipped, its bonuses **stack additively** on top of weapon and armor:

```
// Damage chain: base × talent × level × timing × weaponDamageMult × (1 + accessory.damageMult)
// Defense:      armorDef.defense + accessory.defense  (cap at 0.70 to prevent immune)
// Max HP:       base + armorDef.hpBonus + accessory.hpBonus
```

If both ring AND amulet are equipped, both apply (additive stacking).

---

## Part D — ArmorVfxMap (new file)

**`Assets/_Modules/Village/Hero/ArmorVfxMap.cs`**

Mirror `WeaponVfxMap.cs` exactly in structure — pure static resolver, no MonoBehaviour,
DataRegression-pinned.

Drives a **rim-light glow on the hero mesh** (not a swing trail). Applied via
`MaterialPropertyBlock` on the hero's `SkinnedMeshRenderer` at equip time.

| Rarity | Rim color | Rim intensity |
|---|---|---|
| common | none | 0 (off) |
| uncommon | warm white | 0.15 |
| rare | Oathweld cool-blue | 0.30 |
| epic | violet | 0.45 |
| legendary | gold | 0.70 + slow Lana VFX `Burst_rings` particle on hero |

**makersMark tint** (same blend pattern as `WeaponVfxMap.ThemeTintStrength = 0.18`):
- Oathweld → cooler blue rim
- Emberhand → warm orange tint
- Last-Pressing → amber-gold
- Heartwood → green

**Applies to both armor and accessories** — resolve from whichever equipped item has the
highest rarity (weapon trail is separate; rim-light is the armor/accessory channel).
Call `ArmorVfxMap.Resolve(equippedArmor, equippedRing, equippedAmulet)` — returns the
dominant rarity profile.

**Instrument:** `FlowTrace.Step("ArmorVfx", ...)` on resolve + on apply. Guard
`SkinnedMeshRenderer` null (hero may not be in scene).

---

## Part E — VendorStockContract for Jeweler

`VendorStockContract` for shop key `"jeweler"` should list all 10 accessory IDs.
Check if a jeweler contract already exists; extend it rather than creating a new one.

---

## Do NOT Touch

- `weapons.json` / `armor.json` — cleaned this session, leave as-is
- `Village.unity` — never
- Any UXML — WebGL incompatible
- `VillageSceneBuilder.cs` — wrong lane

---

## Acceptance Criteria

- [ ] `GearCatalog.Accessories` returns 10 items loaded from `accessories.json`
- [ ] Ring and amulet slots appear in EquipVM below the chest slot
- [ ] Equipping a ring/amulet applies its bonuses (verified by `EquipVMTests`)
- [ ] `ArmorVfxMap.Resolve()` returns distinct profiles per rarity (DataRegression pins gold == `GoldColor`)  
- [ ] Hero renderer shows rim-light change on armor rarity change (FlowTrace captured, no null exception)
- [ ] Jeweler shop lists all 10 accessories
- [ ] Save round-trip: equip ring → save → load → ring still equipped (`equippedRingId` persists)
- [ ] Brace-balance on every `.cs` touched

---

## Verification

**Headless:** DataRegression — `AccessoryCatalogTest`: loads `accessories.json`, asserts
10 items, asserts `damageMult` cap (< 0.20 for non-legendary), asserts `defense` cap (< 0.15
for non-legendary), asserts every item has `iconPath`. `ArmorVfxMap` regression: distinct
color per rarity band, gold == `GoldColor`, widths escalate.

**Felt (PO verifies):** equip the Heartstone Locket → hero has visible gold rim-light →
swap to Wanderer's Cloth → rim disappears. Shop at jeweler → 10 items in list.
