# WORK ORDER 20 — RESULT

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** Implemented. The HUD now receives a live Heart-HP + crystal-balance push at runtime. Build clean. Runtime visual confirm (bar drops on damage, counter tracks spend/earn) is the remaining eyes-on tick (build-side gate).
**Editor:** Unity 6000.4.8f1

---

## 1. Problem (statically confirmed in WO-07 / WO-10)

`VillageHudController.SetHeartHp(current,max)` and `SetCrystals(amount)` existed but had **no runtime caller** in normal gameplay (`SetHeartHp` only from the DevPanel; `SetCrystals` from nothing). So the Heart HP bar ("Elarion") stayed at its UXML default `100/100` and never dropped when the Heart/gates took damage, and the crystal counter never reflected `GameState.Resources.Crystals`. This is the same missing-bridge class WO-07 fixed for mana/cooldowns; `SetWave` was the only readout with a working push (`WaveHudBridge`).

## 2. Fix

**New `Assets/_Modules/Village/Heart/HeartHudBridge.cs`** — a per-frame bridge modelled on `HeroAbilitiesHudBridge`:
- Discovers the HUD by component-type name (`VillageHudController`) and invokes `SetHeartHp` / `SetCrystals` by **reflection** — `DeNelle.Village` can't reference `DeNelle.HUD` (the asmdef-isolation seam the other bridges use).
- Each frame pushes `SetHeartHp(heart.Hp, 100f)` (Heart HP is a 0-100 scale — `HeartController.SetHp` clamps to 100) and `SetCrystals(GameStateService.Instance.State.Resources.Crystals)` (null-guarded).
- `HeartController` is found directly (`FindAnyObjectByType` — same asmdef).

**`Assets/_Modules/Village/VillageController.cs`** — `Start()` now also calls `EnsureHeartHudBridge()`, which attaches the bridge at runtime (idempotent, `[DisallowMultipleComponent]`). Runtime-attached for the same reason as the WO-08 gate openers: the HUD/Heart are baked by the edit-time `VillageSceneBuilder`, which the curated-scene rule forbids re-running.

No gameplay-balance values changed; additive only (respects the WO hard rules).

## 3. Verification

- ✅ Headless build after the change: `[DesktopBuild] SUCCEEDED — 559 MB`, 0 compile errors, 0 warnings in the edited files → compiles and ships.
- ✅ Static correctness: the bridge mirrors the proven WO-07 reflection-push pattern; method signatures match `VillageHudController.SetHeartHp(float,float)` / `SetCrystals(int)`; crystal read path matches the DevPanel's `state.Resources.Crystals`.

## 4. Remaining (build-side gate)

The runtime *visual* confirmation — Heart HP bar drops when a gate is breached / the Heart is hit, and the crystal counter updates on spend/earn — needs Editor playmode or an in-Village build run (the Title→HeroSelect→PetSelect→Village flow isn't headlessly drivable). The fix is verified by static correctness + clean build.

- **Owner ~1-minute confirm:** Village playmode → use the DevPanel to damage the Heart (or let a wave breach a gate) and watch the top-left "Elarion" bar drop; grant crystals via DevPanel and watch the counter rise. This also closes the matching WO-10 smoke-test rows (Heart HP decreases; crystal counter).

## 5. Notes

- Closes the WO-07 §6 / WO-10 systemic finding. With this, all four dynamic HUD readouts have a runtime push: Wave (`WaveHudBridge`), Mana + ability cooldowns (`HeroAbilitiesHudBridge`, WO-07), Heart HP + Crystals (`HeartHudBridge`, this WO).
