<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 760 — Syndrath the Devourer: complete the licensed-dragon swap + fly-in→land→burn-towers→Tree behavior

**Status:** READY TO IMPLEMENT (owner-requested 2026-07-24; asset choice CONFIRMED by owner)
**Lane:** Combat/AI + Enemies + Resources asset swap (§9 — coordinate; touches DragonBoss + WaveManager + Resources/Enemies)
**Owner intent (verbatim):** the dragon "flies into town, lands, and uses fire attacks to burn towers. After all towers are destroyed then targets the tree of life."

---

## 0. Why this WO exists (the finding)

Canon (`KEY_FACTS.md` ~L198, `docs/SME/DRAGON_OPTIONS.md`) declares the dragon swap "✅ RESOLVED 2026-07-23, commercial-ship blocker cleared." **This is FALSE in code.** Code-verified 2026-07-24:
- The runtime still spawns the **OLD CC-BY-NC 3DHaupt dragon** (`Resources/Enemies/Boss_Dragon.prefab` → `Dragon.fbx`/`Dragon.controller`). The licensed model is imported but wired into **nothing** but its own demo scene.
- The uncommitted `DragonBoss.cs` / `EnemyFactory.cs` diffs are **comment-only** (headers repointed; zero behavior/wiring change).
- Current `DragonBoss` is **airborne + Heart-only**: never lands, never targets towers, deals all damage straight to the Heart from spawn. The desired fly→land→burn→Tree behavior is ~90% unbuilt.

So this WO clears the REAL ship-blocker (ship the licensed asset) AND builds the requested behavior.

## 1. Asset decision (owner-confirmed 2026-07-24)

- **USE:** `Assets/Dragon/` = Asset-Store product **71047 "Dragon Animated" (WDallgraphics)** — proven `licenseType: Store`. Rig has the needed clips: `takeoff / fly / glide / landing / walk / attack1-3 / bite / hit / die / die2`.
- **DELETE:** `Assets/RedDragon 1.2/` (+ its `.meta`) — no license artifact, stray. Owner confirmed delete.
- **git-rm the old CC-BY-NC files** once the new prefab is wired: `Resources/Enemies/Dragon.fbx`, `Dragon.controller`, `Materials/Dragon_Bump_Col2.*`, `Dragon_Nor_mirror2.jpg`, and the old `Resources/Enemies/Boss_Dragon.prefab`.

## 2. Phase A — Asset swap (editor tooling; do FIRST)

1. Build a game prefab from `Assets/Dragon/Prefab/Dragon.prefab` carrying a `DragonBoss` component + a hit collider, placed at **`Assets/Resources/Enemies/Boss_Dragon.prefab`** (same load path `WaveManager.SpawnApexBoss` uses at WaveManager.cs:1572), and/or assign it to the serialized `_apexBossPrefab` (WaveManager.cs:109).
2. Author an animator controller (new `DragonAnimatorSetup` editor script, mirror existing animator-factory pattern) that exposes the params `DragonBoss` drives (`Speed`, `Attack`, `Dead`) mapped to the licensed clips, PLUS new triggers/states for the behavior: `takeoff`, `fly`, `landing`, grounded `idle`, `attack1-3`. (The old controller only had Speed/Attack/Dead — the new states are required for land/takeoff.)
3. Prefer an editor-script builder run in batchmode (Unity closed) over hand-editing the `.prefab`/`.controller` YAML (serialization-safety, §3).
4. git-rm the old files (§1) and delete `Assets/RedDragon 1.2/`.

## 3. Phase B — Behavior: sequence-driven state machine (DragonBoss.cs)

Replace the current purely HP-gated, Heart-anchored orbit with a **sequence** matching the owner's words (HP phases may still modulate speed/aggression within a state, but the STATE progression is sequence-driven):

1. **Approaching (fly-in):** spawn off-map at altitude, drive `fly`, approach the town. (Replaces spawn-at-Heart+10-and-orbit.)
2. **Landing:** descend to a ground point near the base (sample ground height / NavMesh), play `landing`, settle to grounded.
3. **BurnTowers (grounded):** generalize the current Heart-only target to a `_currentTarget : IDamageableStructure`. Enumerate live towers via the established pattern — `FindObjectsByType<DefenseTower>(FindObjectsSortMode.None)` + `<ArcaneTower>`, filter `IsAlive` (DefenseTower.IsAlive = PlayerOwned && Hp>0 && !_broken). Pick nearest alive, aim a **fire-breath cone** at it, `ApplyContactDamage(amount)` + optionally `ApplyStatus(StatusEffect.Burn)` (CombatStatusTracker already exists). Subscribe to `DefenseTower.Destroyed` (event, DefenseTower.cs:110) to advance to the next tower. May reposition (short takeoff/hop) between targets.
4. **All-towers-destroyed gate → Tree:** when no `DefenseTower`/`ArcaneTower` `IsAlive`, `takeoff` (or turn) and set `_currentTarget = HeartController`, `HeartController.SetState(HeartState.Boss)`, resume the existing swoop/breath-on-Heart behavior as the FINALE (reuse the current TickSwoop/FireBreath, retargeted).
5. **Falling/death:** keep the existing spiral-death path.

## 4. Phase C — Fire-breath VFX (folds in WO-757)

Wire the sustained fire-breath cone from a mouth socket. WO-757 (`WORK_ORDER_757_dragon_breath_particle_pack.md`, SPEC) specs a `Boss_FireBreath` multi-layer cone via `PP_FlameThrower` (Unity ParticlePack), aimed with a chin/mouth socket + offset + `LookRotation`, timed damage, quality tiers. Add `VFXType.Boss_FireBreath` (+ impact) to the VFXCatalog and drive via `VFXManager` (the ONE pool). Reuse existing fire types where they fit (`Impact_ExplosionFire`, `Boss_AttackImpact`). Follow the WO-758 authoring mental-model for the prefab look.

## 5. Files in play
- `Assets/_Modules/Village/Enemies/DragonBoss.cs` (the bulk — target abstraction, new phases, land/takeoff/burn loop, retarget)
- `Assets/_Modules/Village/Waves/WaveManager.cs` (fly-in spawn point/entrance; `_apexBossPrefab` wiring)
- `Assets/_Modules/Village/Vfx/VFXType.cs` + VFXCatalog (Boss_FireBreath, WO-757)
- `Assets/Resources/Enemies/` (new Boss_Dragon prefab; git-rm old model)
- New `Assets/Editor/DragonAnimatorSetup.cs` (or similar) — animator controller builder
- `HeartController` — only the existing `SetState(HeartState.Boss)` hook (no change needed)
- `EnemyFactory.cs` — the `boss-dragon` model-key resolve (currently comment-only diff) reconcile if that secondary NavMesh path is kept

## 6. Acceptance criteria
- [ ] The APEX boss that spawns is the **licensed** `Assets/Dragon` model — the old CC-BY-NC `Dragon.fbx`/`Boss_Dragon.prefab` are git-rm'd and no runtime path loads them. `Assets/RedDragon 1.2/` deleted.
- [ ] On an apex-boss wave, the dragon FLIES IN, LANDS, fire-attacks towers, and only after ALL towers are destroyed does it target the Heart.
- [ ] Tower damage routes through `IDamageableStructure.ApplyContactDamage`; "all towers destroyed" derived from `IsAlive`/`Destroyed`, not a hard-coded count.
- [ ] Fire-breath VFX routes through `VFXManager` (ONE pool — no raw Instantiate, no second VFX stack).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` green; no new `[ui-mvvm]`/brace/NUL failures.
- [ ] Canon updated same-breath: `KEY_FACTS.md` L198 + `DRAGON_OPTIONS.md` corrected from "RESOLVED" to reflect the actual swap landing; fresh `CANON_GROUND_TRUTH` note.

## 7. What NOT to touch / landmines
- Do NOT build a second VFX pool (the two-VFX-stack scar) — everything via `VFXManager`.
- Do NOT hand-edit `.prefab`/`.controller`/`.unity` YAML — use an editor-script builder in batchmode (§3, editor closed).
- ASCII-only any TMP strings; never meaning by color alone.
- `.cs` via Edit/Write only (§0 mount hazard). Keep `DragonBoss.ApplyStatus` no-op for the dragon RECEIVING burn (it applies burn to towers, doesn't take it) unless design says otherwise.
- Verification: headless proves compile + no-throw + FlowTrace fired only (`-nographics` = no particles); the LOOK + feel is owner felt-test / F8. Run the headless UI/VFX pass where applicable; do not claim the visual on faith.

## 8. Verification plan
1. Editor-script asset build (batchmode) → confirm `Boss_Dragon.prefab` loads the licensed mesh.
2. `CompileGate.Run` → `COMPILE_GATE_OK`.
3. `DataRegression.RunAll` → `REGRESSION_OK` (no new reds).
4. AutoPilot fleet apex-boss path (note: overworld/boss coverage is fleet-capped — expect partial) + a human/F8 felt-test of the full fly→land→burn→Tree sequence.
5. Owner felt-verify + CLOSE. Push HELD until owner authorizes.
