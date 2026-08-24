# WORK ORDER 1167 — RESULT

**Status:** IMPLEMENTED 2026-08-24 (CLI). Headless-verified; owner felt-verify + close owed (§13).
**Layout note:** OWNER PICKED WO-1172 **OPTION B** (2026-08-24) — segmented filter chips. The
first-shipped Option A divider render was swapped the same day, View-only, proving the seam: no
data or VM change. Current render: a 112px CHIP BAND (chips are CONTROLS, so the MinTouchPx floor
binds) replaces the 44px crystals line inside the unchanged 303px dock (tray 259 → 191 — raising
the dock instead would overflow the right-edge column's 42.4px spare on the Seeker); crystals
read-out folds into the band's right end. Chips = "All" + one per non-empty section + Other when
occupied, all captioned label + live count; **All is the default, always** (nothing hides behind a
tap by default; verb change resets to All); active chip = gilt underline (position/shape tell).
First chip capture caught every chip overprinted at one spot — AddImage's stretched anchors fight
the HorizontalLayoutGroup; rebuilt on the card recipe (point anchors + explicit width), re-captured
clean.

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
| UI capture (A) | `Builds/wo1167-uicap.log` | `UI_CAPTURE_OK 89` — divider render verified in the 2670×1200 PNG (historical; superseded by B) |
| Compile (B) | `Builds/wo1172b-gate2.log` | `COMPILE_GATE_OK` |
| Data regression (B) | `Builds/wo1172b-regression2.log` | `REGRESSION_OK 272/272 suites` |
| UI capture (B) | `Builds/wo1172b-uicap2.log` | `UI_CAPTURE_OK 89` — `BuildPaletteDock_open_2670x1200.png` OPENED AND READ: All (13) active + underlined, Producers (3) / Storage (3) / Trade (4) / Civic (3), counts sum to the card total, crystals right, cards + costs intact, quick-tabs standing |

First regression run (`wo1167-regression.log`) went red on the oracle's own over-broad lint —
`"jeweler"`/`"armorer"` in the registry fallback are catalog **ids**, not a role list — fixed by
exempting id-shaped tokens in that one file. 271/272 were green on the first pass.
