# Armor Image Naming Guide — Where to Save Your Grok Images

**Drop all images into:** `Assets/Resources/ItemIcons/`  
**Format:** PNG with transparent background, 512×512  
**Filename must match exactly** (no caps, no spaces, include `.png`)

---

## Knight Armor — 5 images

| Save as | What it looks like | In-game name |
|---|---|---|
| `armor_knight_common.png` | Rusty/worn iron plate with simple helmet — basic soldier gear, no embellishment | Ironward Plate |
| `armor_knight_uncommon.png` | Dark plate with chainmail visible underneath, layered shoulderguards, slightly cracked | Bastion Plate |
| `armor_knight_rare.png` | Black/ember plate with lava-glow seams — fire knight style (Emberhand forge) | Vigil Plate |
| `armor_knight_epic.png` | Dark navy ornate plate with gold decorative etching — regal but battle-ready | Emberbrand Plate |
| `armor_knight_legendary.png` | Black plate with glowing gold Oathweld runes — legendary, runes pulse, war-worn but unbroken | Oathplate of Elarion |

---

## Ranger Armor — 5 images

| Save as | What it looks like | In-game name |
|---|---|---|
| `armor_ranger_common.png` | Green leather with hood and cross-strap harness — light scout gear | Scout's Leather |
| `armor_ranger_uncommon.png` | Brown leather with arrows on back, buckled straps — practical, well-maintained | Shadowhide Vest |
| `armor_ranger_rare.png` | Dark leather with green leaf overlays and belt — Heartwood-grown material | Heartwood Warden |
| `armor_ranger_epic.png` | Green glowing leafy nature armor with plate elements — nature magic infused | Windstrider Coat |
| `armor_ranger_legendary.png` | (If you have a 5th ranger image — full living-leaf cloak, glows faintly green, leaves seem to move) | Leafcloak of Elarion |

> **Note:** If you only have 4 ranger images, the legendary can reuse the epic with a brighter green tint, or generate a new one using the prompt below.

**Leafcloak legendary Grok prompt:**
```
[BASE STYLE BLOCK]
Item: A full-length ranger's cloak made of overlapping living leaves — deep forest green, 
each leaf edged with faint bioluminescent light. The leaves are not sewn on; they grow 
from the garment itself. Worn leather chest beneath, but the cloak dominates. 
The fabric shimmers slightly as if breathing. Heartwood grove craft.
```

---

## Mage Armor — 5 images

| Save as | What it looks like | In-game name |
|---|---|---|
| `armor_mage_common.png` | Simple gray or brown apprentice robes — plain, no trim, worn at the hem | Apprentice Robes |
| `armor_mage_uncommon.png` | Purple hooded coat with rune trim — serious mage's traveling mantle | Aetherweave Mantle |
| `armor_mage_rare.png` | Dark coat with silver swirl/sigil patterns — orange rune-pressed accents | Arcane Sigil Vestments |
| `armor_mage_epic.png` | Navy celestial robe with gold stars/moons — constellation patterns, deep blue glow | Starwoven Robe |
| `armor_mage_legendary.png` | Purple/black void robe with flowing dark energy, or deep blue aether-constellation legendary robe | Aethercloak of Elarion |

> **Note:** If you generated two strong legendary candidates (purple void robe + deep blue constellation robe), use the **darker one** for `armor_mage_legendary.png` — it reads as older and more powerful. Save the other as `armor_mage_epic.png` if you want it.

---

## Universal Armor (if you need them)

These 5 already have prompts in `docs/GROK_IMAGE_PROMPTS_GEAR.md`. 
If not yet generated:

| Save as | What it looks like |
|---|---|
| `armor_cloth.png` | Simple worn linen tunic, gray-brown, frayed rope belt |
| `armor_leather.png` | Dark brown leather vest, iron buckles, practical |
| `armor_chain.png` | Silver chainmail shirt, tight rings catching light |
| `armor_plate.png` | Polished silver/gold chest plate, Bright Centuries ornate |
| `aegis_plate.png` | Dark iron legendary chest plate, Oathweld runes glowing gold/blue |

---

## Rings & Amulets

Already documented in `docs/GROK_IMAGE_PROMPTS_GEAR.md`:

| Save as | Item |
|---|---|
| `ring_iron.png` | Plain dark iron band |
| `ring_steadfast.png` | Silver ring, small blue sapphire |
| `ring_embercoil.png` | Copper coil ring, amber ember glow |
| `ring_heartward.png` | Wide silver seal, white glowing crystal |
| `ring_firstlight.png` | Ancient gold ring, self-luminous gemstone |
| `amulet_travelers.png` | Rough stone pendant on leather cord |
| `amulet_oathward.png` | Silver medallion, Oathweld rune inscription |
| `amulet_lastpressing.png` | Teardrop crystal in silver filigree cage |
| `amulet_elarion.png` | Large circular amulet, concentric rune rings |
| `amulet_heartstone.png` | Dark iron locket, glowing Heart-shard inside |

---

## Quick Checklist

Once you have all images, drop them in `Assets/Resources/ItemIcons/` and tell CLI — WO-542 (ShopPanel icon loading) needs the files in place to display them.

- [ ] `armor_knight_common.png`
- [ ] `armor_knight_uncommon.png`
- [ ] `armor_knight_rare.png`
- [ ] `armor_knight_epic.png`
- [ ] `armor_knight_legendary.png`
- [ ] `armor_ranger_common.png`
- [ ] `armor_ranger_uncommon.png`
- [ ] `armor_ranger_rare.png`
- [ ] `armor_ranger_epic.png`
- [ ] `armor_ranger_legendary.png`
- [ ] `armor_mage_common.png`
- [ ] `armor_mage_uncommon.png`
- [ ] `armor_mage_rare.png`
- [ ] `armor_mage_epic.png`
- [ ] `armor_mage_legendary.png`
- [ ] `armor_cloth.png`
- [ ] `armor_leather.png`
- [ ] `armor_chain.png`
- [ ] `armor_plate.png`
- [ ] `aegis_plate.png`
- [ ] `ring_iron.png`
- [ ] `ring_steadfast.png`
- [ ] `ring_embercoil.png`
- [ ] `ring_heartward.png`
- [ ] `ring_firstlight.png`
- [ ] `amulet_travelers.png`
- [ ] `amulet_oathward.png`
- [ ] `amulet_lastpressing.png`
- [ ] `amulet_elarion.png`
- [ ] `amulet_heartstone.png`
