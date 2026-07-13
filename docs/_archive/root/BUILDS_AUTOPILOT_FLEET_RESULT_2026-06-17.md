# Autopilot Fleet — Functional Bug Sweep (2026-06-17)

**Run:** 12 headless player instances (`run-autopilot-fleet.ps1 -Count 12`), distinct seeds, `-nographics`
(logic/flow/crash coverage; visuals not resolved headless). Build = HEAD `86c4896b`. Ranked by distinct-run
reproduction. Raw: `Builds/autopilot-tickets.md` / `.json`.

## ✅ Base loop PASSED functionally (validation — all 12 runs)
Boot→gameplay · ResolveHero · WalkToEachGate (3/4) · **OpenEachVendor (0 contract violations, 0 empty)** ·
**AssertEconomyDeduct (buy works, inv 0→1)** · **AssertEquip (mage_starter equipped)** · **OpenEachHUDPanel (5/5)**.
→ The shop / equip / inventory MVVM work + economy + HUD panels are functionally solid headless.

## 🐞 Ranked bugs + RCA verdicts
| # | Bug (repro) | RCA verdict | Status |
|---|---|---|---|
| 1 | Wave won't trigger — `TriggerWave` timeout (11/12) | **Ambiguous** — autopilot calls `WaveManager.ForceSpawnNextWaveNow()` whose `BeginLoop().Forget()` is async; phase stays Idle if `WaveDataLoader` (waves.json/enemies.json) fails to load OR an async race. Could be real data-load failure OR probe race. | **NEEDS DIAGNOSIS** (add a load-complete log; check waves/enemies json resolve in MainCastle_Hall) |
| 2/3 | Can't exit castle / world-gate seam doesn't fire (9/12) | **Real:** `SceneTransitionTrigger.requireConfirm = true` (needs F-key); autopilot never presses F. Builder (`CastleHubBuilder` WireOuterWorldConnection ~1067 / EnsureExitSeamAtRecipeGate ~1789 / WireOutpostConnectors ~1173) never sets it false. NOTE: a *player* CAN press F, so this is partly an autopilot limit + a design choice (auto-cross vs F-prompt). | **OWNER DESIGN CALL** (auto-cross the hub→OuterWorld seam? set requireConfirm=false) |
| 4 | Navmesh can't path to gate (7/12) | **Real:** castle navmesh fragmented — 3/4 gates unreachable (hero stops ~35m short), documented in `SEAM_RCA_2026-06-13.md`. Bake defect, not a collider. | **NEEDS BAKE FIX** (diagnose via `CastleGateNavVerify`, re-bake) |
| 5 | Yarn "No node" recurs (2/12) | **Real + FIXED (partial):** d3e84609 missed `<<ShowTrainingUI>>` (Barracks) + `<<OpenRumorBoard>>` (Inn). | **Barracks FIXED** (`324409e4`); **Inn = owner call** (command followed by Brom's narration) |
| — | TMPro NRE GenerateTextMesh (1/12) | low-repro; bad/empty text string somewhere | watch |

## Patched this pass
- **Yarn no-node (Barracks)** — `<<stop>>` after both `<<ShowTrainingUI>>` (`324409e4`). Memory updated:
  void/Action commands need `<<stop>>` too; the headless fleet is how to catch these.

## Needs your call / heavier work (held — not blind-patched)
1. **Wave trigger** — diagnose real-vs-probe (a load-complete log + check `WaveDataLoader` in MainCastle_Hall). If real, it's a base-loop combat blocker.
2. **Hub→OuterWorld exit** — design: auto-cross the seam (`requireConfirm=false`) vs keep the F-prompt? (Players can press F; the bot can't.)
3. **Castle navmesh bake** — 3/4 gates unreachable; needs a bake-layer fix (bigger op).
4. **Inn `<<OpenRumorBoard>>`** — `<<stop>>` would silence Brom's rumor narration; restructure or accept.

## Note
Fleet is `-nographics` → it finds **logic/flow/crash** bugs, not visual ones (1008 render-artifact records filtered).
Re-run after the above land to confirm the ranked list shrinks.

*Cross-ref:* `Builds/autopilot-tickets.md`, the 3 RCA agent reports (this session), WO-437/438, `GRANT_DEMO_VALIDATION.md`.
