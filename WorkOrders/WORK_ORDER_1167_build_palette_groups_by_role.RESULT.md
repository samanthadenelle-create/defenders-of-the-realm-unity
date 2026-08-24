# WORK ORDER 1167 — RESULT

**Status:** IMPLEMENTED 2026-08-24 (CLI). Headless-verified; owner felt-verify + close owed (§13).
**Layout note:** rendered as WO-1172 **Option A** (inline vertical group dividers). The owner has the
three-option mockup page (WO-1172, artifact link in that WO) — headers are render-only, so a
different pick later is a View change; none of this data moves.

---

## What shipped

### Data (both canonical copies byte-equal, verified by the new oracle)
- **`structures-catalog.json` v32 → v33** — the six unroled Town rows filled (WO-1167 §4):
  `barracks`→`barracks`, `pet-house`→`echo_home`, `arcane-tower`→`arcane` (the §3 Civic group,
  verbatim), and the three LOCKED legacy rows get honest **unique** roles — `mill`→`gristmill`,
  `lumbermill`→`sawmill`, `mine_crystal`→`crystal_producer` — unique because `StructureRoles`
  refuses role collisions loudly, so they must not reuse `food_producer`/`wood_producer` (claimed by
  `collector_farm`/`collector_lumbermill`). A `_rolePassNote2026_08_24` records this, including that
  `crystal_producer` MOVES to `arcane-tower` by catalog edit when the Cathedral absorbs the verb
  (WO-1168 step 4).
- **`build-categories.json` v2 → v3** — the Town row authors `paletteGroups` exactly per §3:
  Producers / Storage / Trade / Civic, labels + role strings, ordered, display-only. A
  `_paletteGroupsNote` pins the four rules + the no-role-list-in-C# law.
- **`CatalogFallbackData.g.cs` regenerated** against v33 (`CATALOG_FALLBACK_GEN_OK`, rows=28
  version=33, both copies verified byte-identical by the generator).

### Code
- `BuildCategoryRegistry.cs` — `PaletteGroup` + parse of `paletteGroups`. ⛔ The hardcoded fallback
  **deliberately carries no groups** (a role list in C# is the WO-1161 drift shape); JSON-parse
  failure degrades to the flat pre-1167 strip.
- `BuildPaletteVM.cs` — `PaletteSectionVM` + `Sections` + the pure static
  **`GroupCards(cards, groups)`** projection (the regression drives the real shipped seam). Rules
  honoured: unlisted/unroled → trailing **Other**, never dropped; empty group renders nothing;
  order inside a section = the WO-963 sort untouched; duplicate role claim keeps the first and
  `FlowTrace.Warn`s. No role string appears in the code, by construction and by oracle.
- `BuildPaletteUI.cs` — grouped render inserts a `BuildSectionHeader` divider per non-empty
  section: 96px non-tappable obsidian plate, gold left rule, autosized gilt label (data-length
  labels shrink, never ellipsize). Colourblind-safe: text + shape + position carry the grouping.
  Band-coverage trace counts headers. Sections empty ⇒ the flat path, byte-for-byte behaviour for
  Defense / Castle Structures / legacy verbs.
- **New oracle `BuildPaletteGroupsRegression`** (`[palette-groups]`, registered in
  `DataRegression.RunAll`): authored-groups sanity, coverage (all six ids roled; every role at most
  one group), role-uniqueness across the catalog, **new-role→Other proven on the shipped
  projection**, **no role literal in the palette C#** (the registry is exempted only for tokens
  that are catalog IDS — its lockedIds mirror legitimately names ids), and dual-copy byte-equality
  of both edited files.

## Acceptance vs §5
- [x] Town renders headers in authored order; other verbs unchanged (flat path untouched)
- [x] Brand-new role → "Other" with no code change — pinned by `[newtype]` on the real projection
- [x] Locked ids stay filtered (filter runs before grouping); no empty group renders
- [x] Both JSON copies byte-equal; catalog changes = the six roles + version/note only
- [x] Oracle asserts total resolution + no hardcoded role list
- [x] `REGRESSION_OK 272/272 suites` (`Builds/wo1167-regression2.log`) · `COMPILE_GATE_OK`
      (`Builds/wo1167-gate.log`) · UI capture: see the evidence line below

## §4 name-collision pin
`lumbermill` **stays locked** (Town `lockedIds`), so the "Lumber Mill" display-name collision with
`collector_lumbermill` never renders side by side — the pin's "or keep it locked" arm is satisfied.
The rename ruling remains WO-1161/1163's.

## Evidence
| Gate | Log | Marker |
|---|---|---|
| Fallback codegen | `Builds/wo1167-fallback-gen2.log` | `CATALOG_FALLBACK_GEN_OK` (version=33) |
| Compile | `Builds/wo1167-gate.log` | `COMPILE_GATE_OK` |
| Data regression | `Builds/wo1167-regression2.log` | `REGRESSION_OK 272/272 suites` (incl. `[palette-groups]`) |
| UI capture | `Builds/wo1167-uicap.log` | `UI_CAPTURE_OK 89` — `Builds/ui-capture/BuildPaletteDock_open_2670x1200.png` OPENED AND READ: PRODUCERS divider → Lumber Mill/Farm/Iron Mine, STORAGE divider → Lumberyard/Foundry…, gold-ruled headers, cards + costs intact, tray opaque, quick-tabs standing |

First regression run (`wo1167-regression.log`) went red on the oracle's own over-broad lint —
`"jeweler"`/`"armorer"` in the registry fallback are catalog **ids**, not a role list — fixed by
exempting id-shaped tokens in that one file. 271/272 were green on the first pass.
