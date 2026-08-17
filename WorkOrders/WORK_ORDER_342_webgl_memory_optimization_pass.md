<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-342 — WebGL: memory optimization + GC pressure reduction

**Status:** READY TO IMPLEMENT

**Depends on:** WO-196 (WebGL build working), WO-211 (unused assets removed)

**Lane:** 10 (Build/Deploy/Performance)

---

## Summary

WebGL builds have limited heap (default ~256MB). This WO profiles and reduces GC allocations from hot paths:
- Object pooling for projectiles / VFX spawns
- Reduce string allocations in update loops
- Cache frequently-called string concatenations

Targets **50% GC.Alloc reduction** in main gameplay loop.

---

## Files to edit

- `Assets/_Modules/Village/Projectiles/ProjectilePool.cs` (new file)
  - Object pool for arrows/spell projectiles (reuse instances vs. Instantiate)
  - Methods: Get(), Return()
- `Assets/_Modules/Village/VFX/VFXManager.cs`
  - Hook pooling: use ProjectilePool instead of Instantiate
- `Assets/_Modules/HUD/VillageHudController.cs`
  - Cache string values for stat display (e.g., "HP: {0}") instead of concatenating every frame
  - Use `OnGUI.Changed` event to update only when data changes
- `Assets/_Modules/Core/Audio/AudioService.cs`
  - Cache audio clip lookup dictionary to avoid Dict.TryGetValue allocations in hot path

---

## Acceptance criteria

- [ ] Projectile pool initializes 20 instances on scene load (configurable)
- [ ] Pool returns safely if all instances in use (Instantiate fallback)
- [ ] String caches reduce allocations by ≥40% in profiler
- [ ] GC.Alloc tracked in profiler (compare before/after)
- [ ] Brace balance check passes
- [ ] WebGL build still succeeds
- [ ] No memory leak on pool Return()

---

## What NOT to do

- Do NOT rewrite rendering pipelines
- Do NOT add new graphics features
- Do NOT touch the Asset Store packages

---

## Notes

Profile on a low-end device (e.g., iPhone SE or older Android). The bottleneck is usually update-loop string allocations and physics queries.
