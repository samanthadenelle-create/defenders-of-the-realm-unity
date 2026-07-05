# ⚠ WORK ORDER 330 — DTT Hero Cyan Silhouette Fix — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09.

**Status:** CLOSED — SUPERSEDED (system removed 2026-06-09)  
**Lane:** 2 (Combat/AI) — code-only, parallel-safe  
**Scene:** PatriciaLight_TD  
**Priority:** HIGH — visually broken; hero is invisible as a gameplay character  
**Screenshot evidence:** docs/screenshots/dtt_bugs.png (wave 3/5 — hero renders as solid bright-cyan silhouette, no texture or mesh detail visible)

---

## Problem

The player-controlled hero in Defend the Tower renders as a solid bright-cyan silhouette
the size and shape of a humanoid figure. No URP Lit shading, no albedo texture, no detail —
just a flat unlit cyan glow covering the entire mesh.

Observed alongside: white floating circles (likely uninitialized VFX particles), and the
tower + enemies render correctly with proper URP materials.

Root cause candidates (check in order):
1. `HeroBodySwapper` assigned a placeholder/debug material (cyan = missing material fallback in URP)
2. The hero prefab in the DTT scene is referencing a `_M` prefab that has an unlit or magenta-turned-cyan
   material from an earlier URP migration pass
3. A `SkinnedMeshRenderer` on the hero has its material cleared to `Default-Material` which
   renders cyan under certain URP settings
4. `StoryCompanion` or `HeroBodySwapper` is applying a tint override (same root as WO-310 green
   tint on companion) — check `SetTint()` calls and any `materialPropertyBlock` writes

---

## Acceptance Criteria

- [ ] Hero in PatriciaLight_TD renders with correct URP/Lit material matching the hero character art
      (same visual as the hero in the Village scene)
- [ ] No cyan silhouette visible at any wave or hero state
- [ ] Material fix is applied at the prefab level (not a scene override), so it persists on rebuild
- [ ] No regression to WO-310 companion color or WO-286 hero rig

---

## Files to Investigate

```
Assets/_Modules/BattleATB/PatriciaLight_TD/           ← DTT scene root
Assets/_Modules/Village/Hero/HeroBodySwapper.cs        ← material assignment
Assets/_Modules/Narrative/StoryCompanion.cs            ← tint logic (WO-310 root)
Assets/polyperfect/Low Poly Ultimate Pack/_M/          ← correct _M prefabs
```

## What NOT to Touch

- Village.unity scene file
- WaveManager, TowerSwapService, any monetization code
