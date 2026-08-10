# WORK ORDER 35 — Knight Hero Coloring Wrong

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: hero rig/art re-owned by the 2026-06-22 combat/hero pivot, COMBAT_PIVOT_NORTHSTAR)
**Date:** 2026-05-26
**Author:** Bug triage — playtest screenshot
**Priority:** High — Knight renders dark/grungy with red splatters; reads as a
              villain or damaged unit, not a player hero

---

## Problem

Screenshot shows the Knight hero with:
- Very dark grey / near-black armor
- Bright red splatters across the chest and shield
- Overall "battle-damaged / undead warrior" read

Owner direction: "coloring of knight is wrong."

The Knight should read as a **clean, heroic steel knight** — the dark splatter
texture is the wrong Tripo export variant being applied.

---

## Root Cause

`HeroBodySwapper` applies textures via two sequential methods:

```csharp
ApplyExtractedTexture(body, cls);  // loads Resources/Textures/Knight.png
ApplyClassTint(body, cls);         // fallback tint — SKIPPED if texture loaded
```

`ApplyExtractedTexture` loads `Resources/Textures/Knight.png`. This file was
extracted via Tripo's "Send To Unity" flow **but it is the wrong colorway** —
it appears to be a battle-damage / dark variant rather than a clean hero armor
base color.

Meanwhile the FBX itself ships with an embedded texture in:
```
Assets/Resources/Heroes/Knight.fbm/knight_basecolor.JPEG
```

The FBM texture is the Tripo model's canonical base color and may be the
correct clean-armor look. `RetargetMaterialsToUrp` does attempt to pull the
embedded texture from the FBX material, but `ApplyExtractedTexture` **overwrites
it** with the incorrect PNG afterward.

---

## Fix

### Option A — Prefer FBM embedded texture (recommended immediate fix)

In `HeroBodySwapper.ApplyExtractedTexture`:

```csharp
// BEFORE — only tries the manually-exported flat PNG:
string texPath = cls switch
{
    HeroClass.Knight => "Textures/Knight",
    ...
};

// AFTER — try FBM embedded texture first; fall back to flat PNG:
string texPath = cls switch
{
    HeroClass.Knight => "Heroes/Knight.fbm/knight_basecolor",   // FBM embedded
    HeroClass.Ranger => "Heroes/Ranger.fbm/ranger_basecolor",   // FBM embedded (if exists)
    _ => null,
};
// If FBM load fails, fall through to the manually-exported PNG:
if (!string.IsNullOrEmpty(texPath))
{
    var tex = Resources.Load<Texture2D>(texPath);
    if (tex == null) texPath = cls switch    // FBM path failed — try flat PNG
    {
        HeroClass.Knight => "Textures/Knight",
        HeroClass.Ranger => "Textures/Ranger",
        _ => null,
    };
    ...
}
```

### Option B — Disable extracted-texture override for Knight; use tint fallback

If both the FBM texture and `Textures/Knight.png` produce the dark result (i.e.
the Tripo model was exported in this dark colorway), remove the Knight entry
from `ApplyExtractedTexture` entirely and let `ApplyClassTint` handle it:

```csharp
// ApplyExtractedTexture — remove Knight case:
string texPath = cls switch
{
    // HeroClass.Knight removed — use tint fallback (steel grey reads hero-clean)
    HeroClass.Ranger => "Textures/Ranger",
    _ => null,
};
```

`ApplyClassTint` would then apply `new Color(0.78f, 0.80f, 0.86f)` (steel
silver) — a clean, readable hero color with no splatters.

### Option C — Owner re-export from Tripo (preferred long-term)

Request a new Tripo "Send To Unity" export with a **clean armor colorway**
variant. Replace `Resources/Textures/Knight.png` with the new export.
No code change required — `ApplyExtractedTexture` already loads this path.

---

## Recommended Approach

**Now (unblock)**: Implement Option B (remove Knight from extracted-texture
path, use steel tint) so the Knight reads as a heroic silver-armored character.
**Week 7**: Owner triggers a new Tripo export with the correct Knight colorway
and drops it at `Resources/Textures/Knight.png` — Option C auto-applies.

---

## Additional Fix — Material Smoothness

`RetargetMaterialsToUrp` sets `_Smoothness = 0.15f` (nearly matte) for all
Tripo materials. A knight in armor should read as slightly polished metal:

```csharp
// BEFORE:
if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", 0.15f);
if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic",   0f);

// AFTER — apply class-specific PBR values:
float smoothness = cls == HeroClass.Knight ? 0.55f : 0.15f;
float metallic   = cls == HeroClass.Knight ? 0.45f : 0.0f;
if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", smoothness);
if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic",   metallic);
```

This requires passing `cls` into `RetargetMaterialsToUrp` (currently stateless).
Change signature to `private static void RetargetMaterialsToUrp(GameObject body, HeroClass cls)`.

---

## Files to Edit

- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs`
  - `ApplyExtractedTexture()` — try FBM path first OR remove Knight entry (Option A/B)
  - `RetargetMaterialsToUrp()` — accept `HeroClass cls`; apply class PBR values
  - `Start()` — pass `cls` to `RetargetMaterialsToUrp(body, cls)`

---

## Acceptance Criteria

- [ ] Knight hero renders with clean steel-grey armor (no dark splatter texture)
- [ ] Knight visually reads as a player hero, not a villain or damaged unit
- [ ] Knight shield and weapon are visible, appropriately colored
- [ ] Mage and Ranger visuals are unaffected
- [ ] No scene re-bake required — `HeroBodySwapper` runs at runtime
