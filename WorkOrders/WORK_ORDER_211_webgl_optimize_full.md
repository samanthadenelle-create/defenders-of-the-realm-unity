**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-211: WebGL Build Full Optimization

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (reduce first-load hit from 186 MB to ~80–100 MB; major UX win)  
**Owner:** CLI  
**Depends On:** WO-196 (must complete first)  
**Blocks:** None  
**Can Run In Parallel:** None — WO-196 must finish first, then do this immediately  

---

## Problem

Current WebGL build (uncompressed): **186 MB** on itch.io. First-load is brutal for testers.

Goal: **~80–100 MB** (50–60% reduction) while keeping all gameplay intact.

---

## Phase 1: Remove Unused Assets (Guaranteed Safe, ~111 MB gain)

### 1a. Delete unused pet cosmetic
```
Assets/Resources/Cosmetics/Pets/pet-aether-twilight.fbx  (92 MB) — ZERO code references
```
✅ Safe. Not in any code, not loaded at runtime.

### 1b. Delete source FBX files (dev artifacts)
```
Assets/Resources/Heroes/Knight.fbx                       (14 MB)
Assets/Resources/Heroes/Mage.fbx                         (2.6 MB)
Assets/Resources/Heroes/Ranger.fbx                       (2.4 MB)
```
✅ Safe. Unity already extracted these into `.fbm` folders during import. The source files are redundant for builds.

**Rationale:** FBX sources are for re-importing if you need to tweak settings. Once `.fbm` (processed) folders exist, sources add zero value to the build.

---

## Phase 2: Texture Compression (Conditional, ~30–50 MB potential)

### 2a. Audit texture sizes
```bash
du -sh Assets/Resources/*/Textures Assets/Resources/Heroes/Textures
# Current state:
# - Resources/Textures: 85 MB
# - Resources/Heroes/Textures: 71 MB
# - Resources/Pets/Textures: ~20 MB (embedded in .fbm)
```

### 2b. Compress high-resolution textures
**For WebGL, recommend:**
- Downsample 4K → 2K where visually acceptable (hero/pet baked textures)
- Enable BC1/BC4 compression in Unity import settings (DXT5 for normals)
- Verify in WebGL player before committing

**Candidates (safe to test):**
- `Heroes/Textures/remesh_12_combined_Bake_Diffuse.png` — check if 2K downsampled is visually identical
- `Pets/Textures/Coyote_Mesh_Bake_*.png` — test downsampling
- `Enemies/Materials/Dragon_*.jpg` — compression-friendly format already

**Risk:** Texture downsampling may show quality loss on high-end displays. Test in WebGL player on a 1440p+ monitor before landing.

---

## Phase 3: Asset Pruning (Analysis Only, ~20–40 MB estimate)

### 3a. Inventory unused enemy models
Check if all KayKit skeleton variants are loaded at startup:
```
- Skeleton_Minion.fbx
- Skeleton_Rogue.fbx
- Skeleton_Warrior.fbx
- Skeleton_Mage.fbx (Caster in wave config?)
- Skeleton_Golem.fbx (Brute tier?)
- Necromancer.fbx
```

**Question for CLI:** Are all 6 enemy types spawned in the current village waves? If not, unused models can be moved to a `_Archive/` folder and excluded from builds.

### 3b. Check if all 3 pet species are loaded
Current: aether-sprite, flame-pup, ice-wolf (all referenced in code). Safe to keep.

---

## Execution Plan

### Step 1: Delete Phase 1 assets
```powershell
Remove-Item "Assets/Resources/Cosmetics/Pets/pet-aether-twilight.fbx"
Remove-Item "Assets/Resources/Cosmetics/Pets/pet-aether-twilight.fbx.meta"
Remove-Item "Assets/Resources/Heroes/Knight.fbx"
Remove-Item "Assets/Resources/Heroes/Knight.fbx.meta"
Remove-Item "Assets/Resources/Heroes/Mage.fbx"
Remove-Item "Assets/Resources/Heroes/Mage.fbx.meta"
Remove-Item "Assets/Resources/Heroes/Ranger.fbx"
Remove-Item "Assets/Resources/Heroes/Ranger.fbx.meta"
```

### Step 2: Rebuild WebGL
```powershell
Remove-Item -Recurse -Force Builds/WebGL
& .\build-webgl.ps1 -NoBrotli
```

Measure: `du -sh Builds/WebGL/` → should drop to ~75 MB uncompressed.

### Step 3 (Optional): Test texture compression
- Pick one hero texture (e.g., `remesh_12_combined_Bake_Diffuse.png`)
- Downsampling script or manual re-export at 2K
- Rebuild WebGL, test in browser
- If acceptable, apply to all hero/pet textures

### Step 4: Verify gameplay
- Run the game locally (Windows build): hero loads, pet loads, enemies spawn
- Run WebGL build in browser: check no missing-asset warnings in console
- Verify all 3 pets render correctly
- Verify all active enemy types spawn in waves

---

## Acceptance Criteria

- [ ] Phase 1 files deleted (pet cosmetic + source FBXes)
- [ ] WebGL rebuilt without Brotli
- [ ] New `Builds/WebGL/` size measured and logged
- [ ] **Target: ≤ 90 MB uncompressed** (from 186 MB)
- [ ] Zero console errors on game load (browser devtools F12)
- [ ] Hero + pet visuals match Windows build
- [ ] Wave 1–4 all spawn without missing-model warnings
- [ ] F1 dev portal works (confirms full load)
- [ ] Commit message logs: "WO-211: remove unused assets + unused source FBXes, reduce WebGL to X MB"

---

## Known Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Deleting FBX sources → can't re-import if needed later | Git has full history. If needed, restore via `git checkout HEAD~N -- Assets/Resources/Heroes/*.fbx` |
| Texture downsampling → visible quality loss | Test in browser on 1440p display. If unacceptable, revert that step. |
| Missing enemy model in waves → spawn fails silently | Check WaveManager enemy roster + test all waves in browser dev portal. |

---

## Post-Optimization

Once Phase 1 lands:
1. Re-upload new `Builds/WebGL/` to itch.io
2. Test with testers — report load time improvement
3. If Phase 2 (texture compression) is approved, create WO-212

---

## Why This Matters

- **itch.io first-load:** 186 MB → ~75 MB = 60% faster (saves ~2–3 min on 10 Mbps connection)
- **Mobile testers:** sub-100 MB = playable on limited bandwidth
- **Web distribution:** smaller zip = easier to share, fewer hosting concerns

---

**CLI: grab when ready. Estimate: 30–45 min (asset deletion + rebuild + testing).**

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
