# WORK ORDER 735 — Troop Visual Placeholders — RESULT

**Status:** DONE (data-only; no C# touched)
**Date:** 2026-07-16
**Silo:** Art integration / Resources (troops.json visuals — iconId + model only)
**Scope kept:** troops.json `iconId` / `model` / `modelYaw` in BOTH canonical copies. No factory fork, no .cs edit, no scene edit, no `ff.barracks` change, no gate/build/commit.

---

## Summary

All 7 roster troops now (a) load a **non-capsule** body and (b) show a **role-appropriate icon** in the Train UI list + detail socket + deploy tray. Achieved with **existing owned assets only** — no new FBX/prefab/sprite authored (out of scope per WO).

- **Capsule fallbacks after this WO: 0** (was 2 — outrider + battlemage hit the tinted-capsule fallback because their `model` keys pointed at stub assets with no loadable mesh).
- Icons resolve through the existing pipeline `TroopTrainingPanel.TroopIcon -> RpgUiCatalog.Get("icons", iconId)` -> `Resources/RpgUi/icons/<name>.png` (added in WO-732 schema / WO-733 UI; no code change needed here).

---

## SME findings (verified on disk, not assumed)

### Model loadability (`Resources.Load<GameObject>("Heroes/<model>")`)

| Model key | Asset on disk | Loads as GameObject? |
|-----------|---------------|----------------------|
| `SC_Footman` | `Assets/Resources/Heroes/SC_Footman.prefab` | YES |
| `SC_Archer` | `Assets/Resources/Heroes/SC_Archer.prefab` | YES |
| `Knight` | `Assets/Resources/Heroes/Knight.fbx` (+ .controller, KnightPackage.prefab) | YES (hero rig; already used by shieldguard/legionnaire) |
| `Ranger` | ONLY `Ranger.controller` + `Ranger.fbx.tripo-extracted` (a stub file, **no `Ranger.fbx`**) | **NO** -> tinted capsule |
| `Mage` | ONLY `Mage.controller` + `Mage.fbx.tripo-extracted` (a stub file, **no `Mage.fbx`**) | **NO** -> tinted capsule |
| `Cleric` | ONLY `Cleric.controller` + `Cleric.fbx.tripo-extracted` (stub) | NO (not used by roster) |

Confirms the WO-732 RESULT note: Ranger/Mage are `.controller` + `.tripo-extracted` stubs with no loadable mesh. `VisualFactory.Skin` returns null on those -> `TroopFactory` falls back to a blue tinted capsule (functional but not the felt goal).

### Icon pipeline (`iconId`)

`iconId` is NOT a HudIcons path. It is a **sprite name key** into `RpgUiCatalog.RoleIcons` = `Resources/RpgUi/icons/`. Sprites present on disk (verified): `icon_sword`, `icon_shield`, `icon_combat`, `icon_energy_sword`, `icon_compass`, `icon_tree`, `icon_heart`, `icon_settings`, `icon_inventory`, `icon_quest`, `icon_talk`. (The HudIcons/Ranger, /Wizard, /Knight ability jpgs are a *different* catalog and would resolve null through TroopIcon, so they were not used.) A null/unmatched iconId still degrades gracefully: melee -> `icon_sword`, ranged -> `icon_combat`, then an ASCII letter glyph.

---

## Changes applied (both copies, byte-identical)

### Icons (all 7 set to an owned, role-appropriate `Resources/RpgUi/icons` sprite)

| Troop id | Role | iconId | Rationale |
|----------|------|--------|-----------|
| troop-footman | melee | `icon_sword` | generic front-line blade |
| troop-archer | ranged | `icon_combat` | dual-weapon = ranged DPS read (matches existing ranged default) |
| troop-spearman | melee | `icon_sword` | reach melee; no spear glyph in owned set |
| troop-shieldguard | melee (tank) | `icon_shield` | distinct tank read |
| troop-outrider | melee (fast flank) | `icon_compass` | scout/recon/hunt metaphor, differentiates the flanker |
| troop-battlemage | ranged (caster) | `icon_energy_sword` | glowing/energy = magic read; only "arcane-ish" glyph owned |
| troop-echo-legionnaire | melee (elite) | `icon_tree` | Elarion crest (Legion of Elarion), distinct elite read |

### Models (removed both remaining capsule fallbacks)

| Troop id | Before | After | modelYaw | Why |
|----------|--------|-------|----------|-----|
| troop-outrider | `Ranger` (no mesh -> capsule) | `SC_Archer` | -90 -> **0** | lightest owned loadable body = "fast/light silhouette" per WO table; SC_ pack faces +Z so yaw 0 |
| troop-battlemage | `Mage` (no mesh -> capsule) | `SC_Archer` | -90 -> **0** | closest owned loadable ranged/light body for a fragile caster; yaw 0 for SC_ pack |

Untouched (already loadable + correct): footman=SC_Footman, archer=SC_Archer, spearman=SC_Footman (yaw 0); shieldguard=Knight, echo-legionnaire=Knight (yaw -90, Tripo/AccuRIG faces +X). Footman/Archer/Knight quality unchanged.

---

## Acceptance check (WO §Acceptance)

- [x] All 7 types spawn without capsule fallback (0 capsules — outrider/battlemage moved to loadable SC_Archer).
- [x] Footman/Archer unchanged vs pre-roster.
- [x] Facing correct: SC_ bodies yaw 0 (face +Z / move dir); Knight bodies yaw -90 (Tripo +X correction) unchanged.
- [x] No scene hand-edits; no UXML; no C# touched (so CompileGate N/A — nothing to compile).
- [x] Dual-copy byte-identical.

---

## Verification

- Dual-copy md5 (both files): `2e2cd1e157974d3ef746c039e1350d6e` — **MATCH**.
- JSON validity: both parse; 7 troops each.
- ASCII-only: all added values are ASCII.
- Files touched: `Assets/Resources/Data/Canonical/troops.json`, `Assets/StreamingAssets/Data/Canonical/troops.json`. **No `.cs` edited** -> brace/NUL gate not applicable (0 code files touched).
- Did NOT gate/build/commit (per instruction). Did NOT flip `ff.barracks`. Did NOT touch TroopTrainingPanel / TroopDialogueCommands / TroopUnlock / repair / camera / aura / store / dungeon / roster-734.

---

## Real-art TODO (for a later art WO — JSON `model`/`iconId` swap only, no code change)

| Troop id | Placeholder model | Placeholder icon | Owner art TODO |
|----------|-------------------|------------------|----------------|
| troop-footman | SC_Footman (final-ish OK) | icon_sword | optional bespoke portrait |
| troop-archer | SC_Archer (final-ish OK) | icon_combat | optional bow-specific icon |
| troop-spearman | SC_Footman | icon_sword | **needs spear prop / distinct body**; optional cooler-steel tint |
| troop-shieldguard | Knight | icon_shield | optional darker-armor tint; distinct tank silhouette |
| troop-outrider | **SC_Archer (stand-in)** | icon_compass | **needs real Ranger mesh** (`Resources/Heroes/Ranger` is a `.tripo-extracted` stub — no `Ranger.fbx`); wants a fast/light body |
| troop-battlemage | **SC_Archer (stand-in)** | icon_energy_sword | **needs real Mage mesh** (`Resources/Heroes/Mage` is a `.tripo-extracted` stub — no `Mage.fbx`) + a proper arcane/staff icon (no magic glyph in owned RpgUi/icons set) |
| troop-echo-legionnaire | Knight | icon_tree | optional gold/tree accent; elite scale mult |

### Honest gaps (per CLAUDE.md §12 — recommend, don't invent)

- **No owned bow icon and no owned magic/staff icon** in `Resources/RpgUi/icons`. Archer/battlemage use the nearest owned glyphs (`icon_combat` / `icon_energy_sword`) as placeholders. If the owner wants literal bow/spellbook icons, that is a new-asset task (the HudIcons ability jpgs in Ranger/Wizard folders exist but are wired to a different catalog and would need either a code path change or a mirror into `Resources/RpgUi/icons`).
- **Ranger + Mage FBX meshes are genuinely absent** — the `.tripo-extracted` entries are stubs, not loadable models. Real silhouettes for outrider (fast/light) and battlemage (caster) require the owner to source/import actual FBX art; until then SC_Archer is the honest loadable stand-in.
- Spearman shares the SC_Footman body (no spear); acceptable day-one per the program table, flagged for the art pass.
