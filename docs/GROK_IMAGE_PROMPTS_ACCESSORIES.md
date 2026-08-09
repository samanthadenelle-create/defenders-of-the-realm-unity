# Grok Image Prompts — Accessory Icons (rings + amulets)

WO-543 references 10 accessory icons that don't exist yet. Generate these in Grok, save each as a
**transparent-background PNG, 512×512**, with the EXACT filename, into:
`Assets/Resources/ItemIcons/`

Unity import is automatic (the ItemIcons importer + `ResolveItemSprite` pick them up by `iconPath`).
Until they land, the store shows the 💍/📿 glyph fallback — nothing is broken.

## Shared style (paste at the top of every prompt)
> Single fantasy RPG inventory icon, centered, 3/4 top-down view, painterly game-art style matching a
> dark "Obsidian" UI (deep bronze metal, rune accents), crisp silhouette, soft inner shadow, **fully
> transparent background (PNG alpha)**, no text, no border frame, no drop on a card — just the object.
> Square 512×512. Rarity glow color = **{GLOW}** (subtle rim/aura, not overpowering).

Rarity → glow color (matches the in-game rim-light VFX bands):
common = none/neutral grey · uncommon = warm white · rare = Oathweld blue · epic = violet · legendary = gold

---

## Rings
1. **`ring_iron.png`** — *Iron Band* (common, GLOW=neutral grey)
   > A plain dark-iron ring, no gem, no engraving, slightly hammer-marked — humble, functional.

2. **`ring_steadfast.png`** — *Steadfast Ring* (uncommon, GLOW=warm white)
   > A silver band set with a small chip of blue-grey Oathweld-treated iron; steady, dependable, faint warm sheen.

3. **`ring_embercoil.png`** — *Embercoil Ring* (rare, GLOW=Oathweld blue + ember)
   > A coiled metal ring that looks still-hot, faint orange ember light glowing between the coils, smith's-mark "Emberhand".

4. **`ring_heartward.png`** — *Heartward Seal* (epic, GLOW=violet)
   > A silver ring with a flush-cut crystal seal, violet aether light inside the stone, protective sigil engraved on the band.

5. **`ring_firstlight.png`** — *Ring of First Light* (legendary, GLOW=gold)
   > An ancient ornate ring, the central stone does not reflect light but emits a soft remembered golden glow, First-Light wonder, timeless and sacred.

## Amulets
6. **`amulet_travelers.png`** — *Traveler's Token* (common, GLOW=neutral grey)
   > A simple carved-wood road-luck charm on a leather cord, rustic, worn smooth from handling.

7. **`amulet_oathward.png`** — *Oathward Pendant* (uncommon, GLOW=warm white)
   > A round bronze Oathweld medallion on a cord, a sworn-binding ward rune stamped into the face, dependable.

8. **`amulet_lastpressing.png`** — *Last-Pressing Focus* (rare, GLOW=Oathweld blue)
   > A teardrop blue crystal held in delicate silver filigree, soft inner aether glow, elegant focus pendant.

9. **`amulet_elarion.png`** — *Elarion Amulet* (epic, GLOW=violet)
   > An ornate city-mark amulet of the Bright Centuries, old Heart-glyph rune on a violet-glowing gem, regal silver setting.

10. **`amulet_heartstone.png`** — *Heartstone Locket* (legendary, GLOW=gold)
    > A dark-iron hinged locket open to reveal a pulsing golden shard of the Heart inside, sacred, the shard radiates soft warm light, part of the Aegis set.

---

After generating: drop all 10 into `Assets/Resources/ItemIcons/`, reopen Unity (auto-imports as Sprite),
and the Jeweler (Sable Vey) shows real art instead of glyphs. No code or JSON change needed — the
`iconPath` pointers already match.
