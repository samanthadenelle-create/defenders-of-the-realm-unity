# WO-1565 RESULT - authored descriptions; the unauthored case now FAILS the catalog gate

**Status:** IMPLEMENTED - 2026-09-06, uncommitted, awaiting gate. Edit-only lane (no Unity, no git).
**Option chosen (sec.4):** (a) - all unauthored rows drafted against the existing voice, derived from each
row's own `repo`/behaviour, for the owner to review in one pass.
**Touched:** `structures-catalog.json` x2 (+9/-0 each), `StructureCardVM.cs` (+12/-9),
`BuildEconomyRegression.cs` (+40/-8 mine), `DATA_CLASS_MAP.md` (+1/-1), this WO (+1/-1).

## 1. Copy authored - 9 rows, both canonical copies, ASCII, one sentence each, <= 48 chars

`tower_ground_archer` "Quick, short-range arrows; ground foes only." (range 14 / fireRate 2.5 / canHitAir false)
`tower_ballista` "Slow, heavy bolts that also strike air." (dmg 30 / fireRate 0.5 / canHitAir true)
`tower_catapult` "Lobs stones at long range; ground foes only." (range 28, canHitAir false, **no splash field**)
`tower_arcane_spire` "Casts Aether bolts at ground and air foes." (element Aether / projectileStyle spell)
`wall_wood` "Cheap timber barrier that slows attackers." `wall_stone` "Sturdy stone barrier that holds attackers."
`gate_stone` "Opens for you; blocks enemies until it falls." (`Gate.cs:12-16` + `GateProximityOpener.cs:2`)
`healing_caravan` "Mends the Heart between waves." (`HealingFountain.cs:3,14-15` - it heals the HEART, out of
battle only; the first draft said "nearby allies over time" and was wrong)
`deco_torch` "Decorative torch light for your walls."
**ONE sentence is deliberate:** `ManageScreenVM.FirstClause` (`:1907-1913`) truncates at the first period,
so a second sentence never reaches the Manage card - the ticket's own evidence screen.

## 2. Code

- `StructureCardVM.cs:459` `DescriptionFor` - the type-level `switch` is DELETED; it traces
  `desc-unauthored-<id>` and returns `string.Empty` (what the `e == null` branch already returned).
  Consumers handle empty: `BuildPaletteUI.cs:1311`, `BuildStructureInfoPanel.cs:288`. Seam anchors the gate
  brackets on are intact (`:459` .. `FootprintFor` at `:490`).
- `BuildEconomyRegression.cs:235` `CheckStructureDescriptions` - description now REQUIRED on every buildable
  row (was Resource/Collector only); rows with no `visualPrefabPath` are exempt and logged by id
  (`repair_default` only - `DataRegression.cs:2791-2793` skips them for the same reason). Adds an ASCII-only
  check (no em-dash: the shipping font drops it) and a source pin that FAILS if fallback prose returns.

## 3. LF proof - byte-mode patch (memory `canonical-json-edits-binary-only-verify-newlines`)

File is **CRLF, no BOM**. Python bytes-in/bytes-out; StreamingAssets written by `copyfile` of the patched
Resources copy, never patched twice. (First patch was reverted with `git checkout --` and re-run once after
the FirstClause finding - LF before read 1613 again.)

```
LF before=1613  after=1622  added=9   (1613 + 9 authored rows)
bytes=104539  sha256=f057b75276e5e4503c9db503d3dbc172dd3c562f4db8e85038b50a78d2bc9d0c  identical=True
json.load OK: Resources 28 entries, StreamingAssets 28 entries
still unauthored: ['repair_default']  (exempt, no visualPrefabPath)
before: both copies 103966 bytes, sha256 c5d3a46a..., already identical and clean at HEAD
```

## 4. Contradictions found

1. **Sky Ballista was ALREADY authored** (`tower_siege_tower`, anti-air copy). It was 3 of 4 towers plus the
   Arcane Spire - not all four. Row untouched.
2. **The 48-char cap cannot be widened:** that row measures **119** chars and sec.6 forbids editing it. Cap
   stays scoped to Resource/Collector; the 9 new rows are all <= 45.
3. **The Catapult carries no splash/AoE field** - "at groups" would have asserted a mechanic the data lacks.
4. **A second fallback survives in a forbidden file:** `ManageScreenVM.cs:1344` and `:1581` still do
   `if (IsNullOrWhiteSpace(description)) description = "A village structure.";`. Harmless (the gate blocks
   unauthored rows) but wants its own ticket.
5. **`BuildEconomyRegression.cs` already held ~110 lines of UNCOMMITTED WO-1480 wall-tier work** from another
   lane (hunks at `:130`, `:1454`) before this lane opened it. Left untouched - do not commit blind.
6. WO says "~24 build rows"; the catalog holds **28**, 10 unauthored on arrival.

## 5. Registration (DataRegression.cs NOT edited)

`Assets/Editor/Regression/DataRegression.cs:426` - `BuildEconomyRegression.Run(out var buildEconReason)`.
Already wired; no new registration. Markers `BUILDECON_OK` / `BUILDECON_FAIL`; the check is invoked at
`BuildEconomyRegression.cs:81`.

## 6. OWED

1. **`CatalogFallbackGenerator.Generate` MUST run FIRST** - `CatalogFallbackData.g.cs` embeds catalog bytes +
   sha256, so the fallback-freshness gate is RED until regenerated:
   `powershell -NoProfile -File .\run-unity-method.ps1 -Method DeNelle.Editor.CatalogFallbackGenerator.Generate -LogName catalog-fallback-gen.log -ExpectMarker CATALOG_FALLBACK_GEN_OK`
2. `COMPILE_GATE_OK`, then `REGRESSION_OK <n>/<n>` incl. `BUILDECON_OK` - fresh logs, judged by marker.
3. `UI_CAPTURE_OK` - open the palette + detail-card PNGs, read the Catapult and Sky Ballista lines.
4. Canon: `docs/reference/DATA_CLASS_MAP.md:671` updated here; **`docs/reference/STRINGS_AUDIT.md:705`
   (row N-49) still lists the four deleted fallback strings** - flag or fix in the gate commit (sec.15).
5. Owner felt-verify + close (PO closes), including a review pass on the 9 drafted lines.

Braces: StructureCardVM.cs 62/62, BuildEconomyRegression.cs 574/574. NUL bytes: 0 in both.
