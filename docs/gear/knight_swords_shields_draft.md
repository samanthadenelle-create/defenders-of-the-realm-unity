# Knight Swords & Shields — Creative Prep DRAFT

**Status:** DRAFT — author/creative prep only. NOT folded into live data. Do not gate/commit as live until the owner approves the names/stats and the shield-stat decision below.

**Author intent:** evocative names, a clean common→legendary rarity ladder, balanced `damageMult` + `reach` progression for swords, and a defensive stat for shields — matched to Elarion / medieval-fantasy / family-friendly canon. Ids are the canonical ones the store + VFX prep will reference.

---

## SME findings (read first — they shape the shield rows)

Verified against `Assets/_Modules/Village/Hero/GearCatalog.cs` (the live deserialize model) and `Assets/Resources/Data/Canonical/weapons.json` (the schema).

1. **Swords are fully modeled.** `WeaponDef` consumes `damageMult` (joins the hero damage chain) and `reach` (overrides the melee hitbox radius when > 0). The 4 existing knight swords (`knight_starter`/`knight_iron`/`knight_oath`/`knight_dawn`) already ladder common→epic at damageMult 1.0/1.25/1.6/2.1 and reach 2.8/3.4/4.0/4.6. We keep those 4 verbatim and add a 5th **legendary** rung.

2. **Shields have NO defensive stat field today.** `WeaponDef` declares: `id, name, icon, job, hand, category, damageType, damageMult, reach, req, setId, saga, flavor, makersMark, buyWood/Food/Iron/Crystals, prefabPath, iconPath, loadVia, capabilities`. There is **no `block`, `armor`, or `defense` field on `WeaponDef`.** The `defense` field exists only on `ArmorDef` (loaded from `armor.json`, a separate catalog). A shield placed in weapons.json is recognized purely as an **off-hand item** (`category:"shield"` → `IsOffHandItem`, seats in the off hand, blocks a 2H main-hand). It contributes **zero mechanical mitigation** right now.

   **→ DECISION NEEDED (flagged, not invented):** to make shields mechanically defensive we must **add a consumed `block` (or `armor`) field to `WeaponDef`** and wire it into the damage-intake chain (mirrors `ArmorDef.defense` = fractional incoming-damage reduction, e.g. `0.06` = 6%). I have authored a `block` value on every shield row below **as a proposed/forward field** and clearly marked it. **It is NOT consumed by the current build** — do not expect mitigation until a CLI WO adds the field + consumer. If the owner prefers, shields can instead ship purely cosmetic/off-hand for V1 and the `block` numbers stand as the balance target for when the field lands.

3. **Pricing note (informational):** `GearCatalog.GetBuyCost` now charges **GOLD** via `GearAppraisal.Appraise` (tier + stats + maker's-mark premium); the legacy `buyWood/buyIron/buyCrystals` fields are **retained on the def but no longer drive shop cost**. They still belong in the rows for consistency with every existing entry and for any resource-craft path, so I ladder them ~2.5×/tier exactly like the existing swords (crystals gate rare+).

---

## SWORDS (knight, main-hand 1h)

| id | name | rarity | damageMult | reach | wood | iron | crystals | makersMark |
|---|---|---|---|---|---|---|---|---|
| `knight_starter` | Squire's Blade | common | 1.00 | 2.8 | 20 | 40 | – | – |
| `knight_iron` | Iron Longsword | uncommon | 1.25 | 3.4 | 40 | 150 | – | – |
| `knight_oath` | Oathkeeper | rare | 1.60 | 4.0 | 80 | 350 | 10 | Emberhand |
| `knight_dawn` | Dawnbreaker | epic | 2.10 | 4.6 | 150 | 700 | 40 | Emberhand |
| `knight_vigil` | **Vigil's Edge** *(NEW)* | legendary | 2.45 | 5.0 | 300 | 1500 | 120 | Emberhand |

*First 4 rows are the EXISTING live entries (shown for the ladder; do not duplicate them when folding in — only `knight_vigil` is new). The legendary rung is tuned just under the existing legendary `aegis_emberbrand` (2.4 mult / 4.8 reach) as a buyable, non-set legendary so the set sword stays the apex.*

### Sword JSON rows (paste-ready)

Existing 4 are already present in weapons.json — **fold in only the new `knight_vigil` row:**

```json
    {
      "id": "knight_vigil",
      "name": "Vigil's Edge",
      "icon": "⚔️",
      "job": "knight",
      "category": "sword",
      "hand": "1h",
      "damageType": "melee",
      "rarity": "legendary",
      "damageMult": 2.45,
      "reach": 5.0,
      "req": {
        "level": 12
      },
      "buyWood": 300,
      "buyIron": 1500,
      "buyCrystals": 120,
      "makersMark": "Emberhand",
      "flavor": "Carried by the night-watch of Elarion who never let the Heart's light gutter. Its edge is said to find the dark before the eye does."
    }
```

---

## SHIELDS (knight, off-hand — `category:"shield"`)

`block` = **PROPOSED forward field** (fractional incoming-damage reduction, mirrors `ArmorDef.defense`). **NOT consumed by the current build** — see SME finding #2. Numbers ladder ~+5% per tier.

| id | name | rarity | block *(proposed)* | wood | iron | crystals | makersMark |
|---|---|---|---|---|---|---|---|
| `knight_shield_starter` | Squire's Heater | common | 0.05 | 30 | 30 | – | – |
| `knight_shield_iron` | **Ironward Kite** *(NEW)* | uncommon | 0.10 | 50 | 150 | – | – |
| `knight_shield_oath` | **Oathbound Bulwark** *(NEW)* | rare | 0.16 | 100 | 350 | 10 | Oathweld |
| `knight_shield_dawn` | **Dawnguard Aegis** *(NEW)* | epic | 0.22 | 180 | 700 | 40 | Oathweld |
| `knight_shield_vigil` | **Heartwall** *(NEW)* | legendary | 0.28 | 350 | 1500 | 120 | Oathweld |

*`knight_shield_starter` is the EXISTING live entry. Note: the live starter row currently has no `block` field; if/when `block` is added the starter should be backfilled to `0.05` for the ladder to read correctly. Maker's-mark **Oathweld** (the defensive/binding forge, per existing canon — it marks the Aegis cleric weapon and reads as "bound/warding") fits shields better than the blade-forge Emberhand.*

### Shield JSON rows (paste-ready)

Fold in the 4 new shields. **The `block` line is the proposed forward field** — keep it (it documents the balance target) but know it is inert until a CLI WO adds `WeaponDef.block` + a consumer; the rows are valid JSON and load fine as off-hand items without it.

```json
    {
      "id": "knight_shield_iron",
      "name": "Ironward Kite",
      "icon": "🛡️",
      "job": "knight",
      "category": "shield",
      "hand": "1h",
      "rarity": "uncommon",
      "block": 0.10,
      "req": {
        "level": 3
      },
      "buyWood": 50,
      "buyIron": 150,
      "flavor": "A full kite of banded iron — heavier than a squire's heater, and it shows in every blow it eats."
    },
    {
      "id": "knight_shield_oath",
      "name": "Oathbound Bulwark",
      "icon": "🛡️",
      "job": "knight",
      "category": "shield",
      "hand": "1h",
      "rarity": "rare",
      "block": 0.16,
      "req": {
        "level": 6
      },
      "buyWood": 100,
      "buyIron": 350,
      "buyCrystals": 10,
      "makersMark": "Oathweld",
      "flavor": "Oathweld-bound at the rim so the boards hold as one. Knights swore on it before they swore on the blade."
    },
    {
      "id": "knight_shield_dawn",
      "name": "Dawnguard Aegis",
      "icon": "🛡️",
      "job": "knight",
      "category": "shield",
      "hand": "1h",
      "rarity": "epic",
      "block": 0.22,
      "req": {
        "level": 10
      },
      "buyWood": 180,
      "buyIron": 700,
      "buyCrystals": 40,
      "makersMark": "Oathweld",
      "flavor": "Faced with dawn-pale steel that throws back the morning. Behind it, a line does not break."
    },
    {
      "id": "knight_shield_vigil",
      "name": "Heartwall",
      "icon": "🛡️",
      "job": "knight",
      "category": "shield",
      "hand": "1h",
      "rarity": "legendary",
      "block": 0.28,
      "req": {
        "level": 12
      },
      "buyWood": 350,
      "buyIron": 1500,
      "buyCrystals": 120,
      "makersMark": "Oathweld",
      "flavor": "They say a stave of it was cut close to the Heart of Elarion; what it guards, the dark does not reach."
    }
```

---

## The three weapons.json mirrors to fold into (later, by CLI)

The canonical authoring trio (the `Builds/...` copies are generated by the player build and should NOT be hand-edited):

1. `Assets/Resources/Data/Canonical/weapons.json` (primary — `CanonicalJson.Read` hits Resources first)
2. `Assets/StreamingAssets/Data/Canonical/weapons.json`
3. `Assets/Data/Canonical/weapons.json`

(Generated, do not hand-edit: `Builds\Windows\...\StreamingAssets\Data\Canonical\weapons.json` and `Builds\WebGL\StreamingAssets\Data\Canonical\weapons.json` — they regenerate on build.)

---

## Open decisions for the owner / CLI WO

1. **Shield `block` field:** approve adding `float block` to `WeaponDef` (default 0) + wiring it into the damage-intake chain (fractional reduction like `ArmorDef.defense`), and backfill `knight_shield_starter` to `0.05`. Until then shields are off-hand-cosmetic only and the `block` numbers are inert balance targets.
2. **Level reqs:** new legendary rungs gated at level 12 (one tier past the existing epics at 10). Adjust if the cap/curve differs.
3. **Names:** Vigil's Edge / Ironward Kite / Oathbound Bulwark / Dawnguard Aegis / Heartwall — all family-friendly, Elarion-toned, no "Avalon".
