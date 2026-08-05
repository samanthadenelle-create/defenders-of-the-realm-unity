# WORK ORDER 870 — Tower fire VFX: route the "horrible" tower fire to the good Unity fire prefabs

**Status:** READY TO IMPLEMENT
**Author:** UI/QA triage (read-only, §13) — Claude UI
**Lane:** VFX (§9). **WO#:** UI-seat block; **870**=this.
**Origin:** owner 2026-08-04 — *"the fire from the towers is horrible"* · *"I want the vfx to work well"* ·
*"we have amazing prefab fire from unity fire prefabs."*

---

## 1. The ask
The defensive towers' **fire VFX** (muzzle / projectile / impact on the arcane + defense towers) looks bad. Replace it
with the **existing high-quality Unity fire prefabs** the owner has on hand — do NOT author new VFX, do NOT keep the
current one.

## 2. How (same VFX law as the portal, WO-869)
- **Use EXISTING prefabs — author none.** The owner has good fire prefabs (a Unity fire pack, e.g. the same
  Ultimate-VFX-tier assets). CLI locates the current tower-fire hook (the tower muzzle/projectile/impact key on
  `ArcaneTower.cs` / `DefenseTower.cs` / `Tower.cs` / the projectile) and the owner-tagged replacement fire prefab.
- **Owner tags the key; CLI maps VERBATIM** (memory `vfx-map-owner-tags-no-creative-pick`) — no creative pick or
  substitution; hold any un-tagged hook for the owner to tag.
- **Route through `VFXManager`** (pooled, quality-gated) — never instantiate the particle prefab directly.
- **WO-753 one-owner teardown** — a destroyed tower / expired projectile tears down its effect; no orphaned particles.
- Keep it performant: pooled, quality-gated, no per-frame allocation (many towers can fire at once).

## 3. General VFX-quality note (owner: "I want the vfx to work well")
This is the standing rule for the whole VFX pass (portal WO-869, tower fire, any effect): **reuse the shipped
packs (Mirza Beig Ultimate VFX + the fire prefabs), route through `VFXManager`, owner-tags-the-key/CLI-maps-verbatim,
WO-753 teardown.** Never greenfield a particle system when a good prefab already ships.

## 4. Acceptance
- [ ] The tower fire on-device reads as the good fire prefab (owner confirms the look) — not the current "horrible" one.
- [ ] Routed via `VFXManager` (pooled/quality-gated); destroyed tower/projectile tears down the effect (WO-753) — no
      orphaned particles.
- [ ] No new VFX authored; the prefab is an existing pack asset the owner tagged.
- [ ] `CompileGate` green; verified on-device (VFX — headless can't judge it).

## 5. Do NOT
- Do NOT author a new particle system. Do NOT instantiate prefabs outside `VFXManager`. Do NOT creative-pick the fire —
  use the owner-tagged prefab, mapped verbatim.
