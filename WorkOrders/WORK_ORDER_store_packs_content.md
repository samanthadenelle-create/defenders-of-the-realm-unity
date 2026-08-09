# WORK ORDER — Store Packs Content (Starter Shelf)

**Status:** DONE - data only (reconciled 2026-08-09 from the tree - `Assets/Resources/Data/Canonical/packs.json` carries the eight starter bundles as packs 6-13 and names this file as their source; the remaining art assets are content the file itself flags as pending, not engineering work)

**Status:** CONTENT DELIVERED (data authored) — art assets PENDING
**Type:** Content (data only — no `.cs`)
**Authored:** 2026-06-28
**Source:** `docs/audits/AUDIT_monetization_2026-06-28.md` (PackStore ~70% built; needs a real launch shelf)
**Silo:** Monetization/Backend (isolated lane, §9)

---

## 1. What this delivers

Eight new themed **starter store packs** authored as DATA into the catalog the runtime
already loads, plus the matching cosmetic catalog entries so **no granted cosmetic is
dangling**. This fills out the PackStore shelf (previously only the 5 ladder packs) with
a compelling, varied launch set: seasonal cosmetic bundles, an onboarding value pack, a
harvest-economy pack, a wardrobe pack, a flair pack, and a convenience pack.

**No code was written.** This rides entirely on the existing `PackCatalog` / `CosmeticCatalog`
loaders and the `PackDef` schema (`Assets/_Modules/Wallet/PackCatalog.cs`).

### Files written (all data)
- `Assets/Resources/Data/Canonical/packs.json` — runtime copy (Resources, WebGL-safe)
- `Assets/StreamingAssets/Data/Canonical/packs.json` — source/edit copy (kept identical)
- `Assets/Resources/Data/Canonical/cosmetics.json` — added 23 pack-granted cosmetic rows
- `Assets/StreamingAssets/Data/Canonical/cosmetics.json` — kept identical

### Validation done
- Both JSON files parse.
- `tier` unique across all 13 packs (lookup key for `PackCatalog.FindByTier`).
- `sku` unique across all 13 packs (entitlement key).
- Every new-bundle `contents.cosmetics[]` SKU resolves to a `cosmetics.json` `id` (zero dangling).
- Resources copy == StreamingAssets copy for both files.

---

## 2. Schema used (verbatim from `PackCatalog.cs` — not invented)

`PackCatalogData`: `version` int, `currencyDisclaimer` string, `packs` PackDef[].
`PackDef`: `sku` (kebab, unique), `tier` (int, unique), `name`, `tagline`, `theme` (doc),
`founderOnly` (bool), `pricing {usd,usdc,sol,skr}`, `contents {cosmetics[], economy{glimmer,
crystals,food,coins}, convenience[{kind,count,description}]}`, `packExclusiveCosmetic`.

- `tier` is a **unique lookup key**, NOT the price band. The 8 bundles take tiers **6–13**;
  they price-anchor to the existing ladder points ($4.99 / $9.99 / $19.99).
- `convenience.kind` ∈ `instant-build | instant-repair | harvest-auto-collect | xp-weekend` only.
- Pack-granted cosmetics live in `cosmetics.json` with `unlockMethod: "achievement"` +
  `glimmerCost: 0` — the existing semantic closest to "owned via entitlement, equippable,
  not glimmer-buyable" (no new enum value invented; see §5 follow-up note).

`version` bumped 1 → 2 in packs.json to mark the expanded catalog.

---

## 3. The eight packs

| Tier | SKU | Name | Price (USD ref) | Theme | Pack-exclusive cosmetic |
|---|---|---|---|---|---|
| 6 | `frostfall-bundle` | Frostfall Bundle | $9.99 | Winter seasonal — knight + pet + palette | Frostfall Crest |
| 7 | `embergrove-bundle` | Embergrove Bundle | $9.99 | Autumn seasonal — mage + pet + palette | Embergrove Crest |
| 8 | `bloomtide-bundle` | Spring Awakening | $4.99 | Spring seasonal — ranger + banners + palette | Bloomtide Crest |
| 9 | `starters-hand` | Starter's Hand | $4.99 | Onboarding value — economy-heavy + early convenience | Tender's First Token |
| 10 | `echo-patron-pack` | Echo Patron Pack | $19.99 | Harvest-loop economy headstart + auto-collect | Echo Patron Sigil |
| 11 | `hero-wardrobe-pack` | Hero Wardrobe Pack | $9.99 | Cosmetic-forward Knight wardrobe (Dressable) | Wardrobe: Heartwood Surcoat |
| 12 | `realm-defender-bundle` | Realm Defender Bundle | $9.99 | Visual weapon/shield flair (no combat power) + banner | Realm Defender Crest |
| 13 | `builders-cache` | Builder's Cache | $19.99 | Convenience-forward — big build/repair + xp-weekend | Builder's Cache Sigil |

### Per-pack contents

**Frostfall Bundle** (tier 6, $9.99) — winter. Cosmetics: `cosmetic.frostfall-bundle.hero-outfit`
(Frostfall Knight Regalia, knight), `.pet-skin` (Glacierborn Companion, ice-wolf),
`.building-palette` (Frozen Hearth Palette), `.exclusive` (Frostfall Crest). Economy:
glimmer 150 / crystals 1200 / food 400 / coins 800. Convenience: instant-build ×3.

**Embergrove Bundle** (tier 7, $9.99) — autumn. Cosmetics: `.hero-outfit` (Embergrove Mage
Vestments, mage), `.pet-skin` (Emberkin Companion, flame-pup), `.building-palette` (Embergrove
Autumn Palette), `.exclusive` (Embergrove Crest). Economy: 150/1200/400/800. Convenience:
instant-build ×3.

**Spring Awakening** (`bloomtide-bundle`, tier 8, $4.99) — spring. Cosmetics: `.hero-outfit`
(Bloomtide Ranger Garb, ranger), `.permanent-banner` (Bloomtide Banners), `.building-palette`
(First-Thaw Green Palette), `.exclusive` (Bloomtide Crest). Economy: 75/600/200/400.
Convenience: instant-build ×2.

**Starter's Hand** (tier 9, $4.99) — onboarding "best first buy". Cosmetics: `.exclusive`
(Tender's First Token — the one welcome cosmetic). Economy: glimmer 50 / crystals 900 /
food 300 / coins 600 (economy-weighted). Convenience: instant-build ×3, instant-repair ×2.

**Echo Patron Pack** (tier 10, $19.99) — harvest loop. Cosmetics: `.building-palette` (Workshop
Patron Palette), `.exclusive` (Echo Patron Sigil). Economy: glimmer 300 / crystals 4000 /
food 2000 / coins 2500. Convenience: harvest-auto-collect ×3, instant-build ×5.

**Hero Wardrobe Pack** (tier 11, $9.99) — cosmetic-forward, ties to Wardrobe/Dressable. Cosmetics:
`.hero-outfit-a` (Warden Plate), `.hero-outfit-b` (Vigil Cloak), `.hero-outfit-c` (Gilded
Pauldrons), `.exclusive` (Heartwood Surcoat) — all knight. Economy: glimmer 200 / crystals 800 /
food 200 / coins 500. Convenience: instant-build ×1.

**Realm Defender Bundle** (tier 12, $9.99) — flair only, zero combat power (the covenant).
Cosmetics: `.weapon-flair` (Heartsteel Blade Flair), `.shield-flair` (Aegis of Elarion Shield
Flair), `.permanent-banner` (Defender's Banner), `.exclusive` (Realm Defender Crest). Economy:
glimmer 150 / crystals 1000 / food 300 / coins 700. Convenience: instant-build ×2.

**Builder's Cache** (tier 13, $19.99) — convenience-forward (exercises the token tray). Cosmetics:
`.building-palette` (Master Builder Palette), `.exclusive` (Builder's Cache Sigil). Economy:
glimmer 250 / crystals 3500 / food 1000 / coins 2000. Convenience: instant-build ×15,
instant-repair ×15, xp-weekend ×2.

---

## 4. Art still needed (placeholders shipped)

Every new cosmetic row carries a `previewColor` swatch placeholder so the store card renders
now. Real art is required before these read as finished. Grouped by role:

### Hero outfits (skinned-mesh / material variants on the Tripo rig)
- `cosmetic.frostfall-bundle.hero-outfit` — Frostfall Knight Regalia (pale-frost plate). Swatch `#7fb4d4`.
- `cosmetic.embergrove-bundle.hero-outfit` — Embergrove Mage Vestments (autumn-leaf robes). `#c8632a`.
- `cosmetic.bloomtide-bundle.hero-outfit` — Bloomtide Ranger Garb (first-thaw green oilskin). `#7bc467`.
- `cosmetic.hero-wardrobe-pack.hero-outfit-a` — Warden Plate (heavy). `#9aa3ad`.
- `cosmetic.hero-wardrobe-pack.hero-outfit-b` — Vigil Cloak (light plate + cloak). `#5d6b7a`.
- `cosmetic.hero-wardrobe-pack.hero-outfit-c` — Gilded Pauldrons (parade). `#d4b65a`.
- `cosmetic.hero-wardrobe-pack.exclusive` — Heartwood Surcoat (world-tree embroidery). `#7fae6b`.

### Pet skins (mesh/material on existing pet rigs)
- `cosmetic.frostfall-bundle.pet-skin` — Glacierborn Companion (ice-wolf, pale frost). `#bfe0f3`.
- `cosmetic.embergrove-bundle.pet-skin` — Emberkin Companion (flame-pup, banked-forge). `#e76a2f`.

### Weapon / shield flair (cosmetic overlay on hero loadout — NO stat change)
- `cosmetic.realm-defender-bundle.weapon-flair` — Heartsteel Blade Flair (green-steel finish + faint glow). `#6fae8e`.
- `cosmetic.realm-defender-bundle.shield-flair` — Aegis of Elarion Shield Flair (world-tree crest facing). `#c2a24a`.

### Building palettes (recolor sets across Heart/Workshop/Tower/Farm)
- `cosmetic.frostfall-bundle.building-palette` — Frozen Hearth Palette. `#a8d4ff`.
- `cosmetic.embergrove-bundle.building-palette` — Embergrove Autumn Palette. `#c47a3a`.
- `cosmetic.bloomtide-bundle.building-palette` — First-Thaw Green Palette. `#8ed07a`.
- `cosmetic.echo-patron-pack.building-palette` — Workshop Patron Palette (brass/amber). `#c79a3e`.
- `cosmetic.builders-cache.building-palette` — Master Builder Palette (stone/timber). `#a89070`.

### Banners (permanent in-village)
- `cosmetic.bloomtide-bundle.permanent-banner` — Bloomtide Banners. `#62b08e`.
- `cosmetic.realm-defender-bundle.permanent-banner` — Defender's Banner. `#9c5b4a`.

### Pack-exclusive emblems/crests (store badge + owned marker)
- `cosmetic.frostfall-bundle.exclusive` — Frostfall Crest. `#6fa8cf`.
- `cosmetic.embergrove-bundle.exclusive` — Embergrove Crest. `#b8601f`.
- `cosmetic.bloomtide-bundle.exclusive` — Bloomtide Crest. `#57a37f`.
- `cosmetic.starters-hand.exclusive` — Tender's First Token. `#d9c27a`.
- `cosmetic.echo-patron-pack.exclusive` — Echo Patron Sigil. `#b88a2e`.
- `cosmetic.realm-defender-bundle.exclusive` — Realm Defender Crest. `#8a4f40`.
- `cosmetic.builders-cache.exclusive` — Builder's Cache Sigil. `#977f5f`.

Plus each pack wants **one store-card hero image** (the bundle splash). Eight card images total.

---

## 5. Known gaps / follow-ups for CLI (NOT done here — out of content scope)

These are runtime-code items from the audit; flagged so they are not lost. Each needs a `.cs`
change and is for CLI, not this content WO:

1. **Original 5 packs still have dangling cosmetics.** `cosmetic.hearth-spark.exclusive`,
   `cosmetic.lanternlight.*`, `cosmetic.folks-thanks.*`, `cosmetic.patron-of-elarion.*`,
   `cosmetic.founders-vow.*` are granted by packs 1–5 but absent from `cosmetics.json`. Their
   display names are canon (monetization-v2-spec) and were not invented here. Add them in a
   follow-up once the canon strings are confirmed, mirroring the §4 placeholder pattern.
2. **`glimmer` and convenience tokens are granted-on-paper only.** Per the audit, runtime
   applies `crystals/food/coins` but NOT `glimmer`, and convenience items are counted/shown
   but not yet redeemable (no token tray). Builder's Cache and Echo Patron Pack assume that
   tray exists — they will under-deliver until it ships.
3. **No live store entry point.** `MarketplaceInteractor.OpenStore()` is not called by anything
   (proximity/F open path removed). The shelf cannot be reached in-scene until that is rewired.
4. **`unlockMethod` for pack-granted cosmetics.** Reused `"achievement"` (glimmerCost 0) so the
   shop treats them as owned-via-entitlement / not glimmer-buyable. If a dedicated `"pack"`
   unlock method is later desired, it needs a `CosmeticDef` + shop-filter code change; the data
   here will migrate cleanly (just swap the `unlockMethod` string).
5. **Real wallet provider.** Everything runs `StubWalletProvider` (Solana SDK not installed) —
   purchases of these packs settle on the devnet stub only. Unchanged by this WO.

---

## 6. Acceptance criteria

- [x] 8 new packs in both `packs.json` copies, identical, valid JSON.
- [x] Unique `tier` (6–13) and `sku` across all 13 packs.
- [x] Every new pack's `contents.cosmetics[]` SKU exists as a `cosmetics.json` `id`.
- [x] Both `cosmetics.json` copies identical, valid JSON, no junk/blank rows.
- [x] No `.cs` edited.
- [x] Convenience kinds restricted to the four allowed values.
- [ ] (Art) 23 cosmetic assets + 8 store-card splashes produced (placeholders shipped).
- [ ] (CLI follow-ups §5) addressed separately.
