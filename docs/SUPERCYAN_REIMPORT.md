# Supercyan Character Pack — Re-import (fresh clone / CI)

The **Supercyan "Character Pack: Fantasy"** is kept **local-only** (gitignored at
`/Assets/Supercyan/`, same policy as polyperfect / KayKit / Tripo — keeps the repo
lean). It supplies the **troop + hero test bodies** and **~310 Humanoid animations**.
This doc is how to restore it on a fresh clone or a new machine.

## Why it's gitignored
- It's a paid Asset-Store pack (license is per-seat; we don't redistribute it in git).
- It's bulky (8 characters, 310 anims, textures, demo scene).
- Only the **specific variants we use** are committed, in a tracked `Resources/` path:
  `Assets/Resources/Heroes/SC_Footman.prefab` + `SC_Archer.prefab` (variants of the
  Supercyan Knight / Archer). These **reference** the pack by GUID, so the pack must be
  present for them to resolve (else troops fall back to tinted capsules).

## Re-import steps (fresh clone)
1. **Import the pack** from the Unity Asset Store into the project:
   *Character Pack: Fantasy* (Supercyan / Virtual Frontiers). It lands at
   `Assets/Supercyan/`.
2. **Fix URP magenta.** Supercyan ships **Built-in-shader** materials (and a
   Built-in-only surface `SupercyanShader`) that render **magenta in URP**. Convert them:
   - Menu: **Defenders → Art → Fix Supercyan URP Materials**, or
   - Batchmode: `-executeMethod DeNelle.Editor.SupercyanUrpMaterialFix.Run`
   This swaps all 50 `Assets/Supercyan/Materials` to **URP/Lit** (matte), carrying
   `_MainTex → _BaseMap` and `_Color → _BaseColor`. Idempotent (re-run safe).
   *(Equivalent to the readme's Render Pipeline Converter → Material Upgrade, done
   deterministically so it works headless.)*
3. Done — `SC_Footman` / `SC_Archer` now resolve and render correctly.

## Notes (from the pack docs — see `Assets/Supercyan/*.pdf`)
- All models are **mecanim Humanoid** (~2200–3050 tris, 28-bone rig).
- **Compatibility with humanoids *outside* Supercyan packs is "not guaranteed"** — so
  Supercyan-body + Supercyan-anims is the safe combo; retargeting the 310 clips onto our
  AccuRIG/CC_Base heroes is a polish-stage thing to **validate**, not assume.
- We drive movement via **NavMeshAgent** (`TroopController` / `HeroLocomotion`) — use the
  pack's **meshes + anim clips + controllers-as-reference**, but do **not** attach its
  behaviour/movement scripts (they fight our NavMesh locomotion).
- Plan: Supercyan = **test layer**; swap final models (Humanoid-mapped) at polish.

## CI caveat
Because the pack is gitignored, **cloud CI cannot build characters** until the pack is
present in the CI workspace (same limitation as the other local packs). If we move to
cloud CI, the pack must be provisioned there (licensed seat) or committed.
