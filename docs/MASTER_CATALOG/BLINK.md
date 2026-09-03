# MASTER CATALOG — Blink (what remains true, 2026-08-02)

> **REWRITTEN 2026-08-02**, verified from the tree (FeatureFlags.cs, HeroBodySwapper.cs, RpgUiCatalog.cs,
> `Assets/Resources/RpgUi/` listing, grep for runtime `Assets/Blink` references), branch
> `wip/village2-and-f8-tickets`. The exhaustive 2026-06-27 edition (full directory census, Obsidian
> sprite inventory, Grok reconciliation) is superseded; this file keeps only what is still load-bearing.
> Deep dossier: `docs/SME/BLINK_SME.md` (2026-07-11 — paths there are repo-root-relative; the repo root itself is machine-dependent, never hardcode a drive letter).

---

## Census, re-counted at source 2026-09-02 (what is actually IN the warehouse)

- **777 `.prefab` files** under `Assets/Blink` — characters, armour, weapons and UI.
- ⛔ **ZERO VFX.** Nothing under `Assets/Blink` matches `*vfx*` in any case. **Blink is not a VFX
  source and never has been** — the VFX packs are Hovl, Unity ParticlePack, Lana Studio and the
  (gitignored) Spells Pack; see `resources-art.md` §6. A ticket that says "use the Blink VFX" is
  describing something that does not exist.
- Four top-level entries: `Art/`, `StylizedArmorBundle2/`, `UltimateBundle/` (+ the folder root).
  ⚠ **Two of those four bundles are a `README.txt` and nothing else** —
  `Assets/Blink/StylizedArmorBundle2/README.txt` and `Assets/Blink/UltimateBundle/README.txt` are
  pointers to Asset Store bundles that were never claimed/downloaded. So the on-disk footprint
  overstates what is usable: **check for a README before planning work against a Blink bundle.**
  (Three further READMEs sit deeper under `Art/`: `Characters/LowPoly/`,
  `Characters/Stylized/Humans/`, `Weapons/LowPoly/MegaWeaponPack1/`.)

---

## The four truths

1. **Blink is an ART warehouse, and today its live role is the UI RE-SKIN KIT + gear-data source.**
   The pack (Blink Studios RPG Art ULTIMATE bundle, ~9.5k files, only 7 demo/editor `.cs`) sits at
   `Assets/Blink/` — present on this machine, **gitignored in its entirety** (absent on fresh clone/CI).
   Its Obsidian UI sprite theme is now the game-wide UI standard via
   `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING, owner-ratified 2026-06-28) and the Obsidian
   conformance program (WO-714+, `[ui-obsidian]` ratchet).

2. **Runtime NEVER loads from `Assets/Blink`.** The only `Assets/Blink` strings in `Assets/_Modules`
   are three comments (verified 2026-08-02). The runtime path is the committed mirror:
   **`Resources/RpgUi/<role>/<canonical-name>`** via `RpgUiCatalog.Get(role, name)`
   (`Assets/_Modules/Core/UI/RpgUiCatalog.cs`) consumed by `ElarionUiKit` (sprite-first, procedural
   fallback, 9-slice applied by the importer). Editor tool `Defenders > Art > Import Blink UI Pack`
   (`Assets/Editor/BlinkUiImporter.cs`) mirrors Obsidian sprites in. The mirror has grown far past the
   old "proof slice": **~22 role folders** (panel, button, icons, slot, bars, hud, abilities, badge,
   classslot, crown, currency, decoration, element, emblem, font, frame, potion, prefabs, silhouette,
   spellicons, …) plus two SpriteAtlases (`RpgUiAtlas_Simple/_Sliced.spriteatlas`).
   **Never UXML/UI-Toolkit** — code-built panels + Blink sprites only (`docs/BLINK_UI.md`).

3. **The Blink BODY RIG is JUNKED** (owner pivot 2026-06-22, `docs/COMBAT_PIVOT_NORTHSTAR.md`).
   - `ff.blinkarmor` default **OFF** (`FeatureFlags.cs:52`) — `HeroArmorVisual` armored-body swap inert;
     flipping it back invites the `ShareBaseSkeleton FAILED` bone-map spam that got it junked.
   - The shipping hero (Knight "Grom") **skips the Blink base body entirely**
     (`HeroBodySwapper.cs:73-82`): `ff.knightv3` ON (default) loads `Resources/Heroes/KnightV3.fbx`
     (CC/AccuRIG humanoid) with `Knight.controller`; the Tripo Knight is the fallback. The Blink
     `hero/base/HumanMale` Addressable path survives only for non-Knight classes, which
     `ff.knightonly` (default ON) makes unreachable.
   - `ff.blinkchrome` default **OFF** (`FeatureFlags.cs:111`) — a residual "hide our chrome" toggle;
     the Obsidian look ships through the RpgUi mirror + kit regardless, so this flag is legacy polish, not the skin gate.

4. **Blink gear still feeds the item catalogs — as curated DATA, through committed copies.**
   Blink weapon/armor ids appear in the canonical data (`Assets/Resources/Data/Canonical/weapons.json`,
   `armor.json`, etc.); per WO-747 ("Option A", 2026-07-18) the Gear Caster curates the full library
   (owner included ~65 Blink weapons in `GearCurationPicks.json`) and `GearCurationExporter` writes the
   Resources projection the runtime loads (Resources-first, WebGL-safe — `GearCatalog.cs:485`).
   Editor-side, `BlinkAddressableMarker` + `BlinkGearSource` (`Assets/Editor/Catalog/`) still generate
   from the pack when present; consumers are guarded for the pack-absent case.

## Where to look

| Concern | File |
|---|---|
| UI law (template canon) | `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING) |
| Re-skin mechanics | `docs/BLINK_UI.md` (2026-06-17, still accurate) · `Assets/Editor/BlinkUiImporter.cs` |
| Runtime sprite access | `Assets/_Modules/Core/UI/RpgUiCatalog.cs` · `Assets/_Modules/Core/UI/ElarionUiKit.cs` |
| Flags | `Assets/_Modules/Core/FeatureFlags.cs` (`blinkarmor:52`, `blinkchrome:111`) |
| Hero body reality | `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` (Knight skip `:73-82`) |
| Gear generation | `Assets/Editor/Catalog/BlinkAddressableMarker.cs` · `GearCatalogGenerator.cs` |
| Pack dossier | `docs/SME/BLINK_SME.md` (publisher id 49855; path drift noted) |
| Stale banner | `docs/BLINK_NOTES.md` (⚠ STALE — pre-pivot hero framing; warehouse facts still true) |

## Standing risks

- **Gitignored pack:** any regeneration (importer re-run, gear regen, Addressables content build) needs
  the pack on disk; a fresh clone has only the committed `Resources/RpgUi` + data projections. All
  consumers warn-and-skip when absent.
- **Do not re-open the armor path** without an owner ruling — junked is a pivot decision, not a bug.
- The 2026-06-27 edition's Obsidian sprite inventory + BlinkUiImporter border tables were accurate then
  but the mirror has since been extended heavily (WO-714/720/722 arc); re-derive from
  `BlinkUiImporter.cs` + `Resources/RpgUi/` rather than trusting old counts.
