# docs/SME/ — Asset-Pack SME Dossiers (READ-FIRST INDEX)

**Purpose (owner directive 2026-07-12):** every custom/third-party pack in this project has a
detailed SME dossier — implementation, logic, and how to use it **as the author intended** —
so no agent ever guesses at a pack again. **Before touching ANY third-party asset, read its
dossier here.** If a dossier is missing or stale, that is a gap to fix in the same breath (§15
canon maintenance).

**Product identities + installed versions:** [ASSET_STORE_LEDGER_2026-07-12.md](ASSET_STORE_LEDGER_2026-07-12.md)
— the authoritative purchase list (publisher, product, version, changelog highlights). Start there
when you need to know exactly WHAT a folder is.

## Where to look — dossier router

| You are touching… | Read this dossier | Covers |
|---|---|---|
| Any Hovl effect / VFXManager.PlayKey / HovlVfxCatalog | [HOVL_STUDIO_SME.md](../HOVL_STUDIO_SME.md) | RPG VFX Bundle v6.0.4 — HS_* scripts, demo wiring, shader state, our PlayKey gaps |
| Knight/hero animation clips, combos, blocks, parries | [SWORD_SHIELD_MOCAP_SME.md](SWORD_SHIELD_MOCAP_SME.md) | S&S Moves 45-clip kit + hero-motion + magical-moves siblings; intended moveset vs our usage; morning pick recommendations |
| KayKit models, Fantasy Weapons Bits, Adventurers | [KAYKIT_SME.md](KAYKIT_SME.md) | inventory (previously UNCATALOGUED), rig story, weapon-map consumers |
| polyperfect / Quaternius world props, buildings, people | [POLYPERFECT_QUATERNIUS_SME.md](POLYPERFECT_QUATERNIUS_SME.md) | v10 Landmarks/Empire content, _M tier rule, URP fix menus, generic animation system |
| Blink anything — Obsidian UI, weapons, icons, armor, orcs, textures | [BLINK_SME.md](BLINK_SME.md) | the 20+ Blink products incl. OBSIDIAN UI (our UI canon), 400 Weapons, 500 Spell Icons, Stylized Orcs Bundle, armor sets, texture bundles; the junked-armor DO-NOT-REUSE list |
| Non-Hovl VFX — Mirza Beig, Zakhan Spells Pack, Lana Studio, VfxParade | [VFX_PACKS_SME.md](VFX_PACKS_SME.md) | per-pack shader/magenta audit (F8-49 class), the Spells Pack separate-URP-package install story, script logic |
| Character/creature models — Black Dragon, Supercyan, Models/People, Models/Pet | [CHARACTER_PACKS_SME.md](CHARACTER_PACKS_SME.md) | rig compatibility per model, Tripo vs store-bought, Supercyan v3 Built-in-shader conversion need |
| Audio — music, SFX, SfxId mapping | [AUDIO_SME.md](AUDIO_SME.md) | leohpaz RPG Essentials SFX + all Assets/Audio content, AudioService/SfxClipLibrary wiring, silent-gap table |

## House rules for dossiers
1. **One dossier per pack family**, filed here (except the Hovl dossier, at docs/HOVL_STUDIO_SME.md by owner ask).
2. Every dossier carries: inventory → our consumers (file:line) → intended usage (from the pack's own docs/demos) → web research (cited URLs) → opportunities/gaps → a one-page executive summary.
3. **No color-only descriptions** anywhere (the owner is red/green colorblind).
4. Update the dossier **in the same commit** as any change to how we consume its pack (§15).
5. Purchased-but-uninstalled or removed products (e.g. Yarn Spinner, removed per WO-455) are tracked in the ledger, not given dossiers.

*Status 2026-07-12 overnight: ledger + S&S dossier complete; Hovl, KayKit, polyperfect/Quaternius, Blink, VFX-misc, character-packs, and audio dossiers being authored by the overnight research fleet — this index links their target filenames.*
