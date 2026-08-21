<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 765 — Capture hand-placed layout → Default Town seed

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** BuildMode / Onboarding / Editor tooling. Scope: **SMALL-MODERATE** (record spine + replay already exist; net-new = a capture command + a 3-line founding change).
**Owner intent (verbatim):** *"if on the next build I place all the buildings (all with the correct heights), can you do an offset from the script and save that as the prefab instance to use for default town setup?"*

---

## 0. The answer + the key design steer

YES — and capture it as a **layout RECORD (ids + offsets), NOT a frozen prefab**. A prefab instance would bake the building scales at capture time and BREAK the single-`YHeightVariable` tuning (WO-764). A record replayed through `StructureFactory` keeps heights live-normalized, so the captured Default Town still re-scales when the base variable changes. Owner praised that elegance — this preserves it.

**Why it's cheap:** the record type, the live→record primitive, the replay, and a capture precedent all already exist (RCA 2026-07-24, code-verified). Capturing stores WHERE (cell/yaw/level); HOW-TALL stays driven by `YHeightVariable` at replay.

## 1. Existing seams (reuse — do NOT greenfield)

- **Record type:** `PlacedStructureData` (`Assets\_Modules\Core\State\PlacedStructureData.cs:37-88`) — `[Serializable] struct`: `itemId, cellX, cellZ, yawSteps, level, yawOffset, worldY, wallMounted`. Serialized as `SaveSchema.baseLayout` (`SaveSchema.cs:313`), lives in `GameState.BaseLayout` (`GameState.cs:269`), persisted to PlayerPrefs `dotr-save`.
- **Live→record primitive:** `PlacedStructure.ToSaveData()` (`Assets\_Modules\Village\BuildMode\PlacedStructure.cs:64-66`) — snapshots a live building into a `PlacedStructureData`. Every placed AND replayed building carries a `PlacedStructure` (id+cell+yaw+level+worldY+wallMounted).
- **The walk:** `FindObjectsByType<PlacedStructure>()` (pattern already at `BuildModeController.cs:1561`).
- **The replay:** `BaseLayoutLoader.Rebuild → Spawn` (`BaseLayoutLoader.cs:202-388`) — consumes `List<PlacedStructureData>`, rebuilds each via `StructureFactory.Create(entry, pose, Root)` (`:287`) → **applies the WO-764 height model at spawn**. NO load-side change needed.
- **Capture precedent:** `CastleOffsetCapture` (`Assets\Editor\CastleOffsetCapture.cs:28-67`) — a `[MenuItem("Defenders/Castle/…")]` that walks live children, reads TRS, writes a JSON recipe. Same loop shape; mirror it but write `PlacedStructureData` JSON via Newtonsoft (the SaveSchema type).

## 2. Current Default Town seed (what we replace)

Default Town today reads **no saved layout** — `FoundingChoiceController.OnDefaultTown()` (`:204-241`) just sets `StrategicPlacementMigrated = false`, and on next hub load `StrategicPlacementMigration.RunIfNeeded` (`StrategicPlacementMigration.cs:229-294`) regenerates records from a **hardcoded name table** (`BakedRows :85-95`, `StationRows :110-114`) against the live baked ring (`CastleHubBuilder`). So positions are locked to the baked scene, not author-controlled. This WO gives the owner direct visual authorship instead.

## 3. The build

1. **Capture command** (editor menu + optional in-build dev button): walk `FindObjectsByType<PlacedStructure>()` in the home hub, call `ToSaveData()` on each, collect `List<PlacedStructureData>`, serialize to **`Assets/Resources/Data/default-town-layout.json`** (Newtonsoft, same type SaveSchema uses). Mirror `CastleOffsetCapture`'s write pattern. Log count + ids captured.
2. **Founding change** (~3 lines): `OnDefaultTown()` — instead of clearing the migration marker, LOAD `default-town-layout.json` → assign to `svc.State.BaseLayout`, set `StrategicPlacementMigrated = true` (so the baked ring stands down), `Save()`. Replay is then pure `BaseLayoutLoader`.
3. **Height caveat:** the record has no `heightMul` — height is NOT stored per record (good: it's applied at replay by `StructureFactory` from `YHeightVariable × mult`). `worldY` is SEAT elevation (0 = ground; non-zero = wall-top), captured as-is. So a captured town inherits the live height model automatically. (Only if the owner wants per-building height OVERRIDES baked into the seed would we add `heightMul` + a schema bump — default NO.)

## 4. Acceptance criteria
- [ ] A capture command writes `default-town-layout.json` = a `PlacedStructureData[]` of every hand-placed building (id, cell, yaw, level, yawOffset, worldY, wallMounted).
- [ ] `OnDefaultTown()` loads that seed into `State.BaseLayout` (marker set true; baked-ring regen bypassed).
- [ ] A new player picking Default Town gets the captured arrangement, rebuilt via `BaseLayoutLoader` → `StructureFactory` (heights live-normalized by `YHeightVariable`).
- [ ] Changing `YHeightVariable` re-scales the captured Default Town (heights NOT frozen).
- [ ] No duplicate-village regression (the old `SeedBaseLayoutIfFirstEntry` double-copy bug — `BuildModeController.cs:2796-2805` — stays disabled; capture is explicit, not auto).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`; a regression asserts capture→serialize→BaseLayoutLoader round-trips a known placement set.

## 5. Notes
- Sequence: land WO-764 (height model) → owner places the town in a build at correct heights → run capture → Default Town seeded. WO-765 depends on WO-764.
- Data source: read-only RCA 2026-07-24 (all file:line cited), per §12.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
