# Overnight solo run — morning report (2026-05-25)

Ran autonomously under Standing Authority #35 after you went to bed. Everything below is **committed + pushed to origin/master** (repo in sync). Two architect agents + a WebGL build ran in parallel.

---

## ☀️ FIRST THING — sync + reimport
```powershell
cd C:\unitygames\defenders-of-the-realm-unity
# Unity must be CLOSED first.
powershell -ExecutionPolicy Bypass -File .\refresh-from-origin.ps1
```
That new script pulls all overnight commits, wipes the Library reimport caches (add `-Full` for a complete rebuild), and tells you to open Unity. After it, open **6000.4.8f1** → it reimports → the fixes below are live.

---

## ✅ FIXED + live after reimport (code/asset, build-verified, committed)
| Bug | Fix |
|---|---|
| HUD buttons all dead (Build, Q/W/E/R, wave-skip, "?") | `UIInputModuleFix.cs` — the EventSystem's UI actions pointed at a package-only `DefaultInputActions` that isn't in the build; now assigns default UI actions at runtime in every scene. **This was the cause of #1/#4/#5/#C at once.** |
| Pets move in straight lines | `Pet.cs` — eased accel/decel + arrival damp + per-pet speed variation |
| Walk through the Spire/Heart | `HeartController.cs` — runtime solid `CapsuleCollider` (builder had stripped its colliders) |
| Force field not transparent enough | `ForceFieldGate.mat` `_BaseAlpha 0.42→0.2` (only visible once WO-22 builds a real force-field sheet) |

Note on "abilities do nothing" / "no way to trigger wave": the wave loop **does** run (`[WaveManager] Loop armed — wave 1, 300s`); those were the dead HUD-click bug (now fixed) — and abilities also need an enemy to hit (see the playable-loop item).

## 🌐 WebGL — first build SUCCEEDED
Installed the WebGL module + ran the build: **`[WebGLBuild] SUCCEEDED — 310 MB in 35 min`** (`Builds/WebGL/index.html`). First-ever WebGL build works. Remaining (controlled, needs a browser): URP shader-stripping for no-magenta (§2.2 — grows Windows/Android builds, so it's a deliberate separate step), WebGL code paths (§2.3), and the browser smoke test. See `WORK_ORDER_09_webgl_build.RESULT.md`.

---

## 🎯 TOP PRIORITY — the playable loop (designed overnight, needs your re-bake)
You nailed it: **no exterior spawn world → no enemies attacking → no loop.** A creative agent diagnosed it and wrote the fix design:
- **`WORK_ORDER_27_enemy_spawn_world.md`** — the wave *code is healthy*; the world isn't: spawn points sit ~10–12 m outside the gates on stub roads, and the 300×300 exterior terrain is **cosmetic + non-navmesh**. Design: per-gate spawn ring **40 m** out + 16×16 walkable apron + **navmesh-baked 8×40 m corridor** to each gate → enemies spawn far out, march in, attack, breach → ATB. Changes to `ExteriorTerrainBuilder` + `VillageSceneBuilder.BuildApproaches` only.
- **`WORK_ORDER_26_larger_city.md`** — fixes "collide every few steps": enlarge wall ring 28×21→**42×33** (~84×66 m interior), right-size the building footprint colliders (they're oversized vs the meshes), re-space buildings to ≥6.5 m centers / 4 m streets. Coordinated with WO-27 (interior vs exterior, no conflict).

**Both require re-running the village/exterior scene builder** — I did NOT do this unattended (a bad re-bake at 3 a.m. with no one to catch it = a broken village you'd wake to). Each spec has exact old→new numbers + acceptance criteria, ready for you to apply + re-bake. This is the highest-value next step for a playable game.

## 📋 Full playtest triage → `BUGLOG_playtest_2026-05-24.md`
~17 bugs you reported, all captured + root-caused. Beyond the 4 fixed above, filed as WOs (with exact fixes; left for you because each needs a re-bake / GUI / design / browser):
- **WO-22** village wall+gate geometry (force-field-as-box #6, SE gate-wall gap #7, spawn-outside/in-wall #E/#F, NW rounded walls #G)
- **WO-23** dungeon interiors are placeholder primitives (#3 — the gitignored KayKit Dungeon pack isn't present locally; copy it in + re-bake)
- **WO-25** volume sliders dead (#A — `GameAudioMixer.mixer` is an empty stub; rebuild it in the Audio Mixer window)
- **WO-24** exterior world/zones architecture (#D — overlaps WO-27)
- **WO-18** hero static / no walk animation (Mixamo round-trip)
- **WO-21** ATB combat can't run (`BattleController._runtimeState` null in `ATBBattle.unity` — 1-field re-link; kept as an AC per your "don't auto-edit scenes")
- **Bug H** (no hero-life/pet-status bar): HUD renders fine; these are just *missing* HUD elements (design gap), not a break.

## Prioritized owner action list
1. `refresh-from-origin.ps1` → open Unity → confirm the 4 fixes (HUD clicks work, pets curve, can't walk through spire, force field fainter).
2. **WO-21** ATB re-link (30-sec: drag `ATBRuntimeState.asset` onto `BattleController._runtimeState` in `ATBBattle.unity`) → combat runs.
3. **WO-27 + WO-26** → re-bake → the playable loop + roomy village. *(Biggest win.)*
4. WO-22 (gates/walls), WO-25 (audio mixer), WO-23 (dungeon art), WO-18 (hero anim).
5. WebGL §2.2 + browser smoke; WO-24 exterior zones.

Nothing destructive was done; no curated scene was hand-edited or re-baked. All work is on `origin/master`.
