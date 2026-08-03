> ⚠ **SUPERSEDED — Defend-the-Tower / PatriciaLight was REMOVED 2026-06-09; not a live system.** Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` (single-Knight overworld BattleArena; ATB flat/separate).

# Check-in Regression Suite (WO-329)

One suite, two teams, run on **every check-in**. It has four layers:

| Layer | Who runs it | Needs Unity? | Play mode? | What it catches |
|-------|-------------|--------------|-----------|-----------------|
| **Static gate** | UI / Cowork (Linux sandbox) **and** CLI | No | — | Brace balance, bridge reflection, `IDamageableStructure` using, asmdef boundaries, canonical JSON validity |
| **Regression suite** | CLI (Windows + Unity) | Yes | **No** (editor batchmode) | Compile, catalog parse + byte-equal, enemies/waves parse, expected catalog ids, prefab-path resolve, structure kit present, duplicate-landmines, canonical scenes open clean, WaveManager + WO-330 reward fields wired, LayoutValidator recipe math |
| **Unity tests** | CLI (Windows + Unity) | Yes | EditMode + PlayMode | EconomyService/AnimParams/GameState/catalog logic (EditMode); DEFEND→wave/hero/NRE end-to-end (PlayMode) |
| **Manual QA** | A human, before a player build | Play the game | Yes | T-pose / walk facing, HUD readability, build preview isolation, DTT grounding + defeat flow, gates/compass/trees, node interaction |

> ### ⚠ MARKER / SUITE CORRECTION (2026-08-02) — read before trusting anything below
> `checkin_gate.ps1` stage 3 used to invoke **`DeNelle.Editor.RegressionSuite.RunAll`**
> (the 22-case battery described in this file) and judge it by the bare literal
> `REGRESSION_OK` — which **three** classes emitted. So the real gate,
> **`DeNelle.Editor.DataRegression.RunAll`** (~90 registered oracle suites + ~26 inline
> catalog checks — what CLAUDE.md / START_HERE.md / every RESULT file mean by
> `REGRESSION_OK`), had **never run in the automated check-in path**. Also fixed the same
> day: this script did not PARSE at all under PowerShell 5.1 (`"$platform:"` is a hard
> parser error), so no stage of it had been running either.
>
> Now: markers are disjoint and the gate runs **both** suites and requires **both**.
>
> | Entry point | Marker | What it is |
> |---|---|---|
> | `DeNelle.Editor.DataRegression.RunAll` | `REGRESSION_OK <n>/<n> suites` | **THE** gate |
> | `DeNelle.Editor.RegressionSuite.RunAll` | `CHECKIN_SUITE_OK <p>/<n> cases` | the 22-case battery below |
> | `DeNelle.Editor.SessionRegression.RunAll` | `SESSION_GUARDS_OK 6/6 checks` | past-session guards |
>
> `RegressionMarkerRegression` `[regression-marker]` now enforces marker uniqueness,
> full registration of every `Run(out string)` oracle, and that every gate-script grep
> resolves to exactly one emitter.

**Why the headless Regression suite layer exists** (owner: *"so we don't patch one
hole by creating 3 more"*): it is the fast single-marker (`CHECKIN_SUITE_OK` /
`CHECKIN_SUITE_FAIL`) smoke battery that runs in plain editor batchmode — **no test
asmdef, no play mode, no NavMesh bake**. It proves the *static preconditions* of
the core loop (the catalog parses, the playable scene opens with its wiring, the
reward fields exist) so that a later PlayMode failure is a real gameplay bug, not
a missing field or an unparseable file. Entry point:
`DeNelle.Editor.RegressionSuite.RunAll` (menu: **Defenders/QA/Run Regression Suite**).

Files in this folder:

- `static_gate.py` / `static_gate.sh` — the no-Unity static gate.
- `checkin_gate.ps1` — the full Windows + Unity gate (calls the static gate first).
- `MANUAL_QA_CHECKLIST.md` — the non-headless visual/play pass.
- Unity tests live under `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`
  (assemblies `DeNelle.Tests.EditMode` / `DeNelle.Tests.PlayMode`).

---

## UI / Cowork team (Linux sandbox, NO Unity)

Run the static gate before handing work to CLI. From the repo root on the mount:

```bash
bash tools/regression/static_gate.sh
# or:
python3 tools/regression/static_gate.py
```

Brace-check only the files you touched (faster; other checks still run repo-wide):

```bash
python3 tools/regression/static_gate.py Assets/_Modules/Foo/Bar.cs Assets/_Modules/Foo/Baz.cs
```

Exit code `0` = clean, non-zero = at least one hard failure (printed with the
offending path). **A non-zero static gate blocks the hand-off** — fix it or flag
CLI before proceeding.

> Note: on the Linux mount, very large files can occasionally read *truncated*
> due to the known mount/Windows sync lag (CLAUDE.md §0). If the gate flags a
> brace mismatch on a file you did not touch, re-run on Windows (CLI) — the file
> is intact there. The gate's brace counter is comment/string-aware, so genuine
> code imbalance is the only thing it reports on a synced file.

## CLI team (Windows, WITH Unity)

Run the full gate before committing. **Close the Unity editor first** (batchmode
needs the project lock):

```powershell
# static + compile + EditMode + PlayMode
powershell -ExecutionPolicy Bypass -File .\tools\regression\checkin_gate.ps1

# also produce a Windows player build
powershell -ExecutionPolicy Bypass -File .\tools\regression\checkin_gate.ps1 -Build

# skip PlayMode (faster iteration; not for the final pre-merge run)
powershell -ExecutionPolicy Bypass -File .\tools\regression\checkin_gate.ps1 -SkipPlayMode
```

Stages run in order and short-circuit on a hard prerequisite failure:

1. **Static gate** (`static_gate.py`) — must pass or nothing else runs.
2. **Compile gate** (`DeNelle.Editor.CompileGate.Run` via `run-unity-method.ps1`)
   — must print `COMPILE_GATE_OK` or the rest is skipped.
3. **Data regression — THE gate** (`DeNelle.Editor.DataRegression.RunAll` via
   `run-unity-method.ps1`) — must print `REGRESSION_OK <n>/<n> suites`. ~90 registered
   oracle suites + ~26 inline catalog checks. Judged on the SHAPED marker, not the token.
4. **Check-in battery** (`DeNelle.Editor.RegressionSuite.RunAll` via `run-unity-method.ps1`)
   — must print `CHECKIN_SUITE_OK`. Headless smoke cases; no test asmdef / play mode.
   Not redundant with 3: only this stage opens Village2, runs the behavioural NavMesh
   castle-gate query, and lints for per-frame fork-bombs / Yarn `command:`.
5. **EditMode tests** (`-runTests -testPlatform EditMode`).
6. **PlayMode tests** (`-runTests -testPlatform PlayMode`).
7. **Build** (`build-windows.ps1`) — only with `-Build`, only if the above are green.

It prints a summary table and returns a single exit code: `0` = PASS,
`1` = at least one FAIL. Results XML lands in `Builds/tests-EditMode.xml` /
`Builds/tests-PlayMode.xml`; logs in `Builds/*.log`.

---

## What the tests cover

**EditMode** (`Assets/Tests/EditMode/`):
- `EconomyServiceTests` — Wood/Iron `Grant` / `TrySpend` / `CanAfford`, atomic
  failed-spend, `OnChanged`, and the `TerritoryMultiplier` ramp.
- `AnimParamsTests` — every cached hash matches `StringToHash`, the `Dead` bool
  latch + `DeathDir` are canonical.
- `GameStateRoundtripTests` — `SaveSchema.SaveFile` JSON roundtrip of scalar /
  enum / struct / collection fields (the persisted wire format).
- `CanonicalCatalogTests` — weapons/armor/crafting/consumable JSON parse with a
  non-empty top-level array.

**PlayMode** (`Assets/Tests/PlayMode/`):
- `VillageSmokeTests` — Village scene loads; hero spawns non-null with an
  Animator; `WaveManager.ForceBeginNextWave()` leaves `Idle`; no
  `NullReferenceException` during a short headless run.

---

## What blocks a merge

A change **must not merge** if any of these are red:

- Static gate exits non-zero (UI or CLI).
- CompileGate does not print `COMPILE_GATE_OK`.
- DataRegression does not print `REGRESSION_OK <n>/<n> suites` (see the `REGRESSION_FAIL` rows).
- RegressionSuite does not print `CHECKIN_SUITE_OK` (see the per-case `[FAIL]` rows).
- Any EditMode or PlayMode test fails.
- (For a player-facing build) any **Manual QA** Section 1-6 item is FAIL.

> **Known canonical-scene mismatch (flag for CLI):** the PlayMode
> `VillageSmokeTests` loads scene name `"Village"`, but the canonical playable
> scene is **Village2** (project memory: *Village.unity is abandoned*). The
> headless `RegressionSuite` correctly targets `Assets/Scenes/Village2.unity`.
> Before the PlayMode layer is meaningful, point `VillageSmokeTests.SceneName` at
> the canonical scene **and** ensure it is in Build Settings (LoadSceneAsync
> resolves by name).

Green static gate + green full gate + a clean Manual QA pass = safe to merge /
ship.

> The Unity test C# files are marked `// SCAFFOLD — CLI must build-verify under
> Unity Test Framework`. UI authors them; **CLI is the authority** that they
> compile and run green under the Unity Test Framework on Windows.
