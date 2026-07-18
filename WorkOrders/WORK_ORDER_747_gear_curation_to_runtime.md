# WORK ORDER 747 — Gear curation -> runtime (Option A: curated catalog actually ships)

**Status:** IN IMPLEMENTATION (architect-ruled 2026-07-18, owner chose Option A). **Implemented by: Claude (CLI).**
**Owner (PO):** Sam — chose Option A; owner action = curate armor in the Gear Caster; felt-verify the shipped gear.
**Priority:** P1 — a live bug: the owner's curated gear + the hero class-default armor never load in-game.
**Lane:** Gear catalog (single silo). Source: `GAP_AUDIT_2026-07-18.md` (DATAWEB armor/weapons hold) + architect review.

---

## The bug (verified from code)

- The **Gear Caster** (`Assets/Editor/GearCasterWindow.cs`) curates against the FULL **StreamingAssets** gear
  library (`weapons.json` 434 / `armor.json` 30, blink-rich) and writes owner picks to
  `Assets/Editor/GearCurationPicks.json`. Currently **65 weapons `included:true` (all blink), 0 armor picks.**
- The **runtime** (`GearCatalog.cs` via `CanonicalJson`) loads **Resources-first** (WebGL-safe): Resources
  `weapons.json` = 34 / `armor.json` = 20, **blink-free**. StreamingAssets is fallback only.
- **Nothing at runtime reads `GearCurationPicks.json`** (editor-only). So the 65 curated blink weapons never
  appear in shop/loot/default/equip, and the blink-armor class defaults (`HeroBodySwapper.cs:939-942`) +
  the save seed (`SaveIntegrityRegression.cs:54`) silently no-op (id not in the winning catalog).
- The **DATAWEB red** frames this as a byte "drift" — the wrong invariant; it can never be green while
  curation means anything, and a byte-sync either floods loot with 369 un-curated blink weapons (Res<-SA)
  or keeps curation disconnected.

## Option A (chosen) — curation compiles to Resources; gate on curation, not bytes

1. **NEW `Assets/Editor/Catalog/GearCurationExporter.cs`** — menu `Defenders/Gear/Export Curated Catalog -> Resources`
   + static `Export()` batch entry. Reads StreamingAssets library + `GearCurationPicks.json`; writes
   `Resources/Data/Canonical/weapons.json` + `armor.json` = the curated subset (weapons = `picks.included`
   ∪ code-referenced default weapon ids; armor = armor picks ∪ referenced default armor ids incl. the
   blink_armor defaults + real universal/class-common ids). Preserves every row field (Newtonsoft JObject),
   version parity, `"_generated"` marker. §12 Guard/FlowTrace.
2. **`DataWebRegression` made curation-aware** for weapons/armor ONLY: assert Resources id-set == the curated
   projection AND every Resources id exists in the StreamingAssets library. Marker `GEAR_CURATION_OK` /
   `GEAR_CURATION_FAIL`. Absent picks file -> WARN + skip. All other catalogs' byte check unchanged.
3. **Runtime unchanged** — Resources-first / WebGL-safe stays; Resources becomes a *generated artifact*
   (do-not-hand-edit).
4. Fix stale `HeroBodySwapper.cs:936` comment (claims ids match Resources armor.json — false pre-export).

## CLI post-implementation (not the agent)
- Run `GearCurationExporter.Export` headless, **review the diff** (what enters/leaves the runtime weapon set —
  34 real -> 65 curated blink; confirm no orphaned default id), then gate (`CompileGate` + `DataRegression`
  incl. `GEAR_CURATION_OK`) + commit + push.

## Owner action
- **Curate armor in the Gear Caster** (0 armor picks today) so armor ships intentionally, not only via the
  exporter's default-id safety net.

## Do NOT
- Change `GearCatalog` load order, `GearCasterWindow`, or `GearCatalogGenerator`; no other catalog's gate;
  no `.unity` edits; ASCII-only; brace gate.

## Rejected options
- **B** (runtime -> StreamingAssets full library): breaks WebGL (StreamingAssets File IO throws in-browser)
  + makes curation meaningless (ships all 434). **C** (naive byte-sync): floods loot with un-curated blink,
  downgrades armor v2->v1, curation still disconnected.
