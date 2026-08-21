**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 246 — Replace KayKit Placeholder NPCs with Purchased Character Pack
**Status: READY TO IMPLEMENT**
**WO:** 246 | **Lane:** VILLAGE (serial — requires rebake)
**Closes:** DEF-91

---
## What to do

Replace all KayKit low-poly placeholder meshes on `AmbientNPC` GameObjects with the purchased CGTrader character pack at `Assets/Models/People`.

## Character pack contents (confirmed on disk)

| NPC role | Model file | Animation |
|---|---|---|
| Blacksmith | `Blacksmith/SKM_Blacksmith.fbx` | `AS_Blacksmith_Forging.fbx` |
| Merchant | `Merchant/SKM_Merchant.fbx` | `AS_Merchant_Talking2.fbx` |
| Villager (female) | `PeasantMevika/SKM_PeasantMevika.fbx` | `AS_Peasant_Idle.fbx` |
| Guard | `Soldier/SKM_Soldier.fbx` | `AS_Soldier_Idle.fbx` |

## Implementation

In `VillageSceneBuilder.cs` (or the script that places AmbientNPC GameObjects):
1. Find all GameObjects with `AmbientNPC` component
2. Replace the `SkinnedMeshRenderer` mesh with the appropriate character pack FBX
3. Wire the matching animation clip to an `Animator` component
4. Run `Defenders > Art > Fix Polyperfect URP Materials` after placement
5. Rebake the village scene

**Scale:** All character models should be normalised to 1.8m height. Check `import settings → scale factor`.

## Acceptance criteria
- [ ] All KayKit capsule/placeholder NPCs replaced with purchased character pack meshes
- [ ] Each NPC plays its idle animation loop in Play mode
- [ ] No pink/magenta materials on any NPC mesh in WebGL
- [ ] NPC scale is (1,1,1) — no oversized models
- [ ] NPC bounding boxes do not clip into the player model or camera
- [ ] Scene rebaked — NPC placements are stable after rebake
- [ ] Brace balance check passed

## What NOT to touch
- `Village.unity` — do not hand-edit; all changes via VillageSceneBuilder
- ATB, WaveManager, EnemyBrain scripts

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
