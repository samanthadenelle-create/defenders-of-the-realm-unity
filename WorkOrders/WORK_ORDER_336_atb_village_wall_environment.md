<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 336 — ATB Battle Arena: Village Wall Environment (Immersive Background)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** 2 (Combat/AI) — parallel-safe (ATBBattle scene; Village.unity NOT touched)
**Priority:** MEDIUM — atmosphere/polish; battle arena is currently a black void
**Theme:** Village walls / torch-lit gate — approved by owner 2026-06-07
**Dependency:** Do WO-335 first (remove capsule) so the scene is clean before adding art

---

## Goal

Replace the bare gray floor + black void of ATBBattle with a night-time village gate
environment, reinforcing the "defending Elarion" narrative. The battle is triggered
when the Heart reaches ≤30% HP, so the setting should feel desperate and siege-like.

---

## Environment Vision

```
          [night sky — stars, moon]
    [distant fires on rampart tops]
  ┌──────────────────────────────────┐
  │  stone wall  [GATE ARCH]  stone  │  ← background plane ~30 units behind fighters
  │  [torch]               [torch]   │
  └──────────────────────────────────┘
  ═══════════════════════════════════════  ← cobblestone/dirt ground plane
        ELARA          SKELETON
```

---

## Implementation Approach

**Strongly prefer a builder script** over manually placing objects in the Unity editor,
so the environment can be rebuilt if the scene is ever wiped.

Create `Assets/_Modules/BattleATB/ATBBattleEnvironmentBuilder.cs`:
- An `[ExecuteInEditMode]` Editor helper (or a menu item under
  `Defenders > Battle > Build ATB Environment`)
- Instantiates all prefabs at hardcoded positions
- Idempotent — clears any existing `ATBEnvironment` root before rebuilding

---

## Scene Elements

### 1. Skybox
```csharp
// Assign a dark night-sky material to RenderSettings.skybox
// Use the existing project skybox if one is set; otherwise use:
// Material: "SkyboxNight" — if it doesn't exist, create a simple
//   Skybox/6Sided material with a very dark navy (#06111E) solid colour
//   and a subtle star particle system (prefab: "StarfieldParticles" or similar)
```

### 2. Ground Plane
Replace the existing gray platform with a proper ground:
- Scale: ~40×40 units centred at (0,0,0)
- Material: cobblestone or stone tile from `Assets/polyperfect/…`
  - Check `docs/polyperfect-asset-catalog.md` for stone/ground materials
  - If none found: a flat dark-gray Lit URP material (`#2a2a35`) is fine as fallback
- Name in hierarchy: `ATBEnv_Ground`

### 3. Stone Wall + Gate Arch (background)
Position: Z ≈ +14 (behind fighters), centred at X=0
- **Preferred**: Polyperfect `_M` wall / gate prefab
  - Catalog first — look for "wall", "gate", "castle", "medieval" in
    `docs/polyperfect-asset-catalog.md`
  - Use `_M/Prefabs_M/<Category>_M/` quality tier prefabs only
- **Fallback**: Simple quads (3 planes: left wall section, arch/gate centre,
  right wall section) with a dark stone Lit URP material
- Scale large enough to fill the camera frame behind both characters
  (~12 units wide, ~6 units tall)
- Name in hierarchy: `ATBEnv_WallRoot`

### 4. Torches (×2, flanking the gate)
Position: left X ≈ −3.5, right X ≈ +3.5; Y at wall base + ~1.5 units
- **Preferred**: Polyperfect torch prefab — check catalog
- **Fallback**: Small cylinder (handle) + cone (flame), both Lit URP material
- Each torch needs a **PointLight** child:
  ```
  Range: 4      Intensity: 2.5
  Colour: #FF8C00 (warm amber)
  Shadows: None (perf)
  ```
- Add a **simple fire particle system** (use any existing fire VFX from the project,
  e.g. from `Assets/_Modules/VFX/` — check for "fire", "torch", "flame" prefabs)
- Name in hierarchy: `ATBEnv_Torch_L` / `ATBEnv_Torch_R`

### 5. Distant Wall Details (optional, time-permitting)
- 2–4 small fire/glowing dots at the top edge of the wall art to suggest
  defenders with torches on the ramparts
- Can be small PointLights or tiny emissive quads — very low cost

### 6. Ambient Lighting Adjustment
```csharp
// Shift ambient light slightly warmer to match torch firelight
RenderSettings.ambientLight = new Color(0.08f, 0.06f, 0.04f);
// Or use the existing Lighting settings panel — don't break existing look too much
```

---

## Camera
Do NOT move the battle camera — characters must stay in frame.
Check that wall geometry doesn't clip through the characters from the camera angle.
If it does, push Z further back (+16, +18) until clear.

---

## Polyperfect Notes
- Always use `_M` quality tier prefabs
- Log `Debug.LogWarning` (not error) if any prefab is null — pack may not be imported
- Check `docs/polyperfect-asset-catalog.md` before naming any prefab path

---

## Files to Create / Edit

```
Assets/_Modules/BattleATB/ATBBattleEnvironmentBuilder.cs   ← NEW (editor helper)
Assets/_Modules/BattleATB/ATBBattleEnvironmentBuilder.cs.meta ← NEW
ATBBattle.unity                                            ← EDIT (NOT Village.unity)
```

Do NOT touch:
- Village.unity
- WaveManager, HeartController, TowerSwapService
- ATBCombatManager battle logic

---

## Acceptance Criteria

- [ ] ATBBattle game view shows a night-time stone wall + gate behind fighters
- [ ] Two torches visible with warm orange point lights
- [ ] Ground is cobblestone/stone textured (not plain gray)
- [ ] Sky is dark (not the default Unity blue)
- [ ] Elara and Skeleton still readable against the background (no blending/occlusion)
- [ ] Frame rate does not drop below existing baseline (no expensive shaders or too many
      lights added without shadows disabled)
- [ ] `ATBBattleEnvironmentBuilder` runs without errors and is idempotent (rebuild = clean)
- [ ] No regression to any ATB combat logic

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `ATBBackgroundController.cs unreferenced in scene` — backdrop art unwired. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
