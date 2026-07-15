# GROK-01 — Hovl VFX: Recommendations & Implementation Guidance

**Guidance doc #01** · 2026-07-14 · CLI-actionable · Source dossier: `docs/HOVL_STUDIO_SME.md` (full inventory, script-by-script, demo wiring, web research). This doc is the **ranked fix list + exact file:line changes** distilled for implementation. Branch `wip/village2-and-f8-tickets`.

## Verdict (why ours doesn't look like the demo)
Our Hovl integration is **architecturally sound** — the string-key catalog (`HovlVfxCatalog`, 30 keys), pooling, and driving the script-free `Projectile VFX loop/` prefabs with our own movers are all **vendor-endorsed v6 patterns**. Nothing needs re-architecting. The "not like the demo" verdict comes from **four concrete, fixable presentation deltas**, in priority order below. No magenta risk (all 245 mats resolve to the 10 `HS_*` URP Shader Graphs; the F8-49 magenta history belongs to the Lana/Spells legacy packs, NOT Hovl).

---

## Ranked fixes (felt impact per unit of work)

### 1. ⭐ Turn bloom ON outside the arena — THE headline (root cause) — ✅ IMPLEMENTED 2026-07-14 (pending owner felt-verify)
> **Applied:** `Assets/DefaultVolumeProfile.asset` Bloom **intensity 0 → 2**, **threshold 0.9 → 1.1** (matches the vendor VolumeURP profile; blooms the HDR particles, not the daylight scene). Value is a taste dial — owner tunes on device (SME range 1.5–2.5; demo runs 5). Data change (no compile gate); owner is the gate.

- **Evidence:** every Hovl demo scene (8/8) runs a URP Volume with **Bloom intensity 5, threshold 1.1** (`Assets/Hovl Studio/HSFiles/Settings/VolumeURP.asset`). Our global `Assets/DefaultVolumeProfile.asset` ships **Bloom active but intensity = 0 (effectively OFF)**. The only place we enable bloom is inside the arena: `BattleArena.cs:163-166` at intensity **1.4**. So every overworld cast, tower bolt, fountain aura, POI callout, structure burn renders with **zero glow** — the `HS_Blend_CG` materials emit HDR luminance *specifically* so bloom can halo them; at intensity 0 they read as thin flat sprites.
- **Fix:** raise `Assets/DefaultVolumeProfile.asset` Bloom intensity **0 → ~1.5–2.5**, threshold ~1.1 (demos run 5 = showcase-hot; owner felt-verifies the dial). OR add a global gameplay Volume mirroring `BattleArena.cs:907+` (`BuildArenaBloom`, constants :163-166) — that code is the in-repo template. Verify overworld/main cameras have `m_RenderPostProcessing: 1` and HDR on.
- **Impact:** one setting moves EVERY Hovl effect in the game toward the demo look, zero per-effect cost. **Owner felt-verifies (taste dial).**

### 2. Fix `ApplyStartColor` to hue-shift, not flat-fill
- **Evidence:** `VFXManager.Hovl.cs:344-351` writes ONE flat color into every child ParticleSystem's `startColor`, destroying authored gradients + per-sub-system balance (hot near-white cores, saturated halos). The vendor's own recolor (`HS_CameraHolder.Counter/OnGUI`, `HSFiles/Scripts/For demo scenes/`) caches each PS's HSV and **shifts HUE ONLY, preserving each system's saturation/value/alpha**. Ours turns effects into a uniform mush and kills the bright core bloom feeds on. Also LDR tints (`BlastColor`, `def.UnityColor`) multiply the HDR material color → can drag luminance below the bloom threshold, dimming twice.
- **Fix:** port the vendor algorithm — cache each child PS startColor HSV on first acquire, apply only the requested HUE, keep S/V/alpha (and keep HDR luminance ≥ authored). Callers keep passing the same `Color`; only the application changes (~20 lines).

### 3. Soft-stop projectiles on impact (trails finish, don't pop)
- **Evidence:** on arrival every caller does `handle.Stop()` → `ReturnHovlToPool` → `StopAllParticles` = `Stop(true, StopEmittingAndClear)` + immediate `SetActive(false)` (`VFXManager.Hovl.cs:301-322`, `VFXManager.cs:841-845`). The whole projectile — including the trail stretched behind it — vanishes in one frame. `HS_ProjectileMover` instead stops emission and lets live trail particles finish their lifetime.
- **Fix:** give `VFXHandle.Stop()` (or a `StopSoft()` used by the projectile callers at `RangedAttackVFX.cs:178-181`, `HeroAbilities.cs:1424`, `ArcaneTower.cs:392+`) a graceful path: `Stop(true, StopEmitting)` **without Clear**, then `ReturnHovlAfterDelay(go, key, ~0.6s)` — **which already exists** at `VFXManager.Hovl.cs:325-329`, it's just not what `Stop()` calls.

### 4. Orient impacts to the surface normal
- **Evidence:** demo hits orient via `FromToRotation(Vector3.up, contact.normal)`; our impacts mostly spawn `Quaternion.identity` (`Enemy.cs:1595`, `HeroAbilities.cs:1373`, `TowerCombat.cs:567`). Ground hits ≈ correct; wall/steep hits look pasted-flat.
- **Fix:** pass a normal-derived rotation where a hit normal (or wall-vs-ground flag) is known. Ground-only hits can stay identity. Low priority.

---

## Secondary polish (know-about)
- **Projectile point-light:** demo `Projectiles with logic/` carry a real-time Light (on in flight, off on impact); our script-free `Projectile VFX loop/` twins have **no Light** (verified). Optional: add a small pooled Light to the follower path (`HovlVfxFollower.cs`), gated behind a quality tier — vendor warns light is the expensive part on mobile.
- **Prewarm loop auras:** `IsLoop` rows spawned into an already-active state (Heal_Aura, Aegis_Shield, Taunt_Aura, Poi_NodeAura) ramp from empty each acquire (we `Clear()+Play()`). Enable Prewarm on the main PS at pool-build (`CreateHovlInstance`, `VFXManager.Hovl.cs:278+`).
- **AOE sound ownership:** AOE prefabs carry `HS_EffectSound` (Start-driven, NOT pool-aware) — first play sounds, pooled replays silent. Either strip/disable + route through `CoreServices.Audio`, or `PlayOneShot` on re-enable. Pick one owner.
- **Beam skill (future):** the 3D Lasers prefabs are **dead without `Hovl_Laser`** — when we ship a beam, keep the script (assign `HitEffect`, call `DisablePrepare()` before pool return, swap the raycast for our targeting).
- **Distortion (future):** `HS_Distortion`/`HS_BlendDistort` (4 mats) need **Opaque Texture ON** in `Assets/Settings/DeNelle-URP.asset` or they render flat.

## Explicitly FINE as-is — do NOT churn
The string-key catalog + generator pipeline; pooling (vendor's own demos pool identically); using `Projectile VFX loop/` script-free prefabs with our movers (vendor-endorsed v6.0.3 separation, same as Infinity PBR's Projectile Factory); shader/material state (URP Shader Graph everywhere, no magenta, no Support Package needed); `transform.localScale` scaling (PS scalingMode = Hierarchy); skipping `ProofUrpParticleShaders` for Hovl (`VFXManager.Hovl.cs:288-290`).

## Suggested delivery order
1. **Bloom** (#1) — global profile change; owner felt-verify (biggest win, smallest change).
2. **Hue-shift tint** (#2) — ~20 lines in `VFXManager.Hovl.cs`.
3. **Soft-stop** (#3) — reroute `Stop()` to the existing `ReturnHovlAfterDelay`.
4. **Impact orient** (#4) — where normals are known.
5. Secondary polish as capacity allows; beam/distortion only when those effects ship.

## Verification
Bloom + tint + soft-stop are **felt/visual** — owner is the gate (screenshot fleet / on-device). Where a headless check helps: assert `DefaultVolumeProfile` Bloom intensity > 0 in a settings regression; the VFX pool caps + FlowTrace (`VFXManager.cs:139-142`, no-key/no-prefab/cap-hit self-report) already instrument spawn health.

---
*Cross-ref: `docs/HOVL_STUDIO_SME.md` (full dossier), `docs/vfx/HovlStudio_Inventory.md`, `docs/vfx/SkillTree_VFX_Mapping.md`.*
