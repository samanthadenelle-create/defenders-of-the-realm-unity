---
name: run-defenders
description: Build, run, and drive the Defenders of the Realm / Echoes of Elarion Unity game. Use when asked to run, launch, build, smoke-test, headless-test, autopilot, fleet-test, screenshot, or verify the game / the Unity project / "defenders" / "eoa". Covers the batchmode compile+data gates, the Windows player build, and the headless AutoPilot fleet that actually drives the running game.
---

# Run Defenders of the Realm (Unity 6 game)

Unity 6 (URP) tower-defense + dungeon-crawler. It is **developed and driven HEADLESS**:
you don't open a window — you build the Windows player and drive it with the **AutoPilot
fleet** (`run-autopilot-fleet.ps1`), then **observe via captured JSON**, not pixels
(the fleet runs `-nographics`). The committed driver is the trio of repo-root scripts
(`build-windows.ps1`, `run-autopilot-fleet.ps1`, `run-unity-method.ps1`) plus the
**harvest** helper in this skill dir. All paths below are relative to the repo root — **that root
is machine-dependent** (`C:\eoa` on one box, `D:\eoa` on another), so never hardcode a drive letter.

**Golden rule: the Unity editor must be CLOSED for any batchmode command** (build/gate/fleet) —
it holds a project lock. Unity *Hub* running is fine. The fleet `.exe` needs NO Unity license.

## Prerequisites
- Unity **6000.4.8f1** installed via Unity Hub (`C:\Program Files\Unity\Hub\Editor\6000.4.8f1\`).
- Windows + PowerShell (batchmode) and Git Bash + `python3` (for `harvest.sh`).
- Gitignored art packs (`Assets/polyperfect`, `Assets/Models/KayKit`, …) are absent on a fresh
  clone — the game still builds/runs (committed `Resources/` art survives); world looks "black".

## Run — AGENT PATH (the drive loop)

The canonical loop is **gate → build → drive → observe**. Every command here was run this
session and produced the marker shown.

**1. Compile gate** (authoritative "does it compile" — brace + leak + NUL scan):
```bash
powershell -ExecutionPolicy Bypass -File ./run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate.log
# -> prints  COMPILE_GATE_OK :: scripts compiled clean
```

**2. Data/logic gate** (headless "real object in -> assert -> one marker"; catalogs, save, equip):
```bash
powershell -ExecutionPolicy Bypass -File ./run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log
# -> prints  REGRESSION_OK <n>/<n> suites   (or REGRESSION_FAIL: <n> failure(s) ...)
```

The marker carries its suite COUNT on the same line on purpose. Grep the SHAPE
(`REGRESSION_OK \d+/\d+ suites`), never the bare token — until 2026-08-02 three
different classes emitted a bare `REGRESSION_OK` and the check-in gate was judging
the 22-case legacy battery while every RESULT file read it as this ~90-suite one.
Sibling markers, all disjoint now:
| Entry point | Marker |
|---|---|
| `DeNelle.Editor.DataRegression.RunAll` (**THE** gate) | `REGRESSION_OK <n>/<n> suites` |
| `DeNelle.Editor.RegressionSuite.RunAll` (22-case battery) | `CHECKIN_SUITE_OK <p>/<n> cases` |
| `DeNelle.Editor.SessionRegression.RunAll` | `SESSION_GUARDS_OK 6/6 checks` |
`DeNelle.Editor.Regression.RegressionMarkerRegression` ([regression-marker], registered
in the data gate) keeps those disjoint and fails if a new oracle is written but never
registered, or a gate script greps a marker nobody emits.

**3. Build the Windows player** (ALWAYS wipe `Builds/Windows` first — stale exe-stub = level3 crash):
```bash
powershell -ExecutionPolicy Bypass -Command "Remove-Item -Recurse -Force 'Builds\Windows' -ErrorAction SilentlyContinue; .\build-windows.ps1"
# -> prints  [build] SUCCESS -> <repoRoot>\Builds\Windows\DefendersOfTheRealm.exe
```

**4. Drive it — launch the headless AutoPilot fleet** (N player instances, distinct seeds,
each drives boot -> vendors -> economy -> equip -> HUD -> wave -> scene-cross and asserts
oracles; writes per-run break-logs + a ranked ticket file). Run in the background; it takes
~`TimeoutMin` minutes:
```bash
powershell -ExecutionPolicy Bypass -File ./run-autopilot-fleet.ps1 -Count 12 -SeedStart 1000 -TimeoutMin 15
# launches 12 instances; on exit writes Builds/autopilot-tickets.md + .json
```

**5. Observe — harvest the run** (the OBSERVE step; this is your "screenshot"):
```bash
bash .claude/skills/run-defenders/harvest.sh
# prints: per-run talk-route verdict, high-signal counts (talk violations / dialogue
# No-node / softlocks), NEW real errors (render artifacts + guard-handled magenta filtered),
# and the ranked ticket file path.
```
Raw artifacts live under `%LOCALAPPDATA%Low\DeNelle\Defenders of the Realm\autopilot-runs\<n>\`:
`break-log.jsonl` (error-level lines + F8 flags), `autopilot-summary.json` (per-phase pass/fail
+ details), `break_*.png` (screenshots — **blank under -nographics**). Ranked, deduped tickets:
`Builds/autopilot-tickets.md`.

## Run — HUMAN PATH (real visuals)
The fleet is `-nographics` (no pixels). For actual visuals, launch the built player directly:
```bash
powershell -ExecutionPolicy Bypass -Command "& 'Builds\Windows\DefendersOfTheRealm.exe'"
# a window opens (Title -> HeroSelect -> PetSelect -> MainCastle_Hall). F8 = capture+flag. Close to quit.
```
Useless on a headless box; this is the only path that renders. Boot a single scene with
`& 'Builds\Windows\DefendersOfTheRealm.exe' -bootScene MainCastle_Hall`.

## Gotchas (battle scars — verified this session)
- **`-nographics` = NO pixels.** Fleet `break_*.png` are blank; observe behaviour via
  `break-log.jsonl` + `autopilot-summary.json`, never screenshots. Render bugs (magenta) and
  UITK panels (dialogue) **cannot** be reproduced headless — they need the human path / F8.
- **break-log captures ERROR-LEVEL ONLY.** `FlowTrace.Step`/`Warn` (Debug.Log/LogWarning) do
  **not** land in `break-log.jsonl` — only `FlowTrace.Fail`/exceptions/softlocks/F8 flags. To
  assert a non-error signal headless, make the oracle emit `FlowTrace.Fail` on violation (that's
  how `AssertVendorTalkRoute` works). `Player.log` holds Step lines but is **overwritten per
  fleet instance** → unreliable for fleets.
- **License "505 / LICENSE ERROR" line is transient.** Judge success by the marker
  (`COMPILE_GATE_OK` / `REGRESSION_OK <n>/<n> suites` / `[build] SUCCESS`), not the wrapper exit line. Re-run if
  a batchmode call reports a license error at *shutdown* but produced its marker. Do NOT kill processes.
- **Editor lock.** If `tasklist | grep Unity.exe` shows a process, a build/gate is running or the
  editor is open — defer; don't collide. (Unity *Hub* is fine.)
- **Fleet wipes stale run logs at launch** (clean aggregation slate) — harvest BEFORE relaunching,
  or you lose the prior run's break-logs.
- **Coverage is HUB-capped.** The fleet exercises MainCastle_Hall + a warp to Village2; the open-world
  outpost/combat/walk loop is blocked (WO-453) → "no outpost realized — skipped" every run is EXPECTED.
- **Video/shader-pass errors in the log are -nographics artifacts** (`VideoDecode`, "custom render path
  shader needs ≥1 passes") — the emitter filters them from tickets; `harvest.sh` excludes them. Not bugs.

## Troubleshooting
| Symptom | Fix |
|---|---|
| `compileErrors=True` + `CS####` in log tail | real compile error — read the tail, fix the named file, re-run the gate |
| Batchmode reports a license error but no marker | transient; re-run the same command (it succeeded for us on retry). Don't kill procs. |
| `Player exe not found` from the fleet | run step 3 (build) first; confirm `Builds/Windows/DefendersOfTheRealm.exe` exists |
| Player build crashes with `level3 corrupted` | you skipped the `Remove-Item Builds\Windows` wipe — incremental builds keep a stale exe stub |
| `harvest.sh` finds no runs | the fleet hasn't completed (or wiped on relaunch); check `autopilot-runs/` for `*/break-log.jsonl` |

## F8 Live-Triage — persistent daemon (no manual re-arm)

While the owner felt-tests, every F8 flag / error / softlock must land on the CLI without the owner
saying "rearm" or "watch". Use the **inbox daemon** (not the one-shot `f8-watch.sh`).

**1. Start once** (idempotent; survives the whole play session):
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\run-defenders\f8-watch-start.ps1
```

**2. Agent session** — `.cursor/rules/f8-auto-triage.mdc` (alwaysApply) requires:
- Background `f8-watch-poll.ps1` with notify on `F8 INBOX PING` (mid-session wake)
- Every turn: `f8-check-inbox.ps1` first; if `NEW_CAPTURE`, read `logs/f8-inbox/LATEST_CAPTURE.md` before any code-read
- After triage: `f8-ack.ps1` + re-launch poll

**3. Stop daemon** (end of day): `f8-watch-stop.ps1`

| Script | Role |
|--------|------|
| `f8-watch-daemon.ps1` | Persistent watcher → inbox + `PING.json` |
| `f8-watch-poll.ps1` | Agent background poller; exits on un-acked capture |
| `f8-check-inbox.ps1` | Sync poll (`NEW_CAPTURE` / exit 1) |
| `f8-ack.ps1` | Ack after triage |

Legacy: `f8-watch.sh` (bash, exits on first fire, needs manual re-arm).

## Reference
Full operating SOP + the latest run ledger: `OVERNIGHT_AUTOPILOT_LOG.md`. Build/gate/bake cycle
table: `docs/HANDOVER.md` §4. Instrumentation method (`FlowTrace`/`Guard`/break-log):
`docs/INSTRUMENTATION_STANDARD.md`.
