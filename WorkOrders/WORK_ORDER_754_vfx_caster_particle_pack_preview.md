> ⚠ **NUMBER COLLISION — this document does not own WO-754; `WORK_ORDER_754_rewarded_ads_monetization.md` does.**
> Referred to hereafter as **WO-754-B (VFX caster particle-pack preview)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-754 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK ORDER 754 — VFX Caster: view Particle Pack multi-layer VFX (fix)

**Status:** IMPLEMENTED (CLI 2026-07-23) — owner felt-verify in Editor  
**Classification:** Editor tooling / VFX (player-felt via owner eyes only)  
**Silo:** Editor  
**Depends on:** WO-758 mental model · WO-757 breath art path  
**Companion:** `Defenders > Animation > VFX Caster`  

---

## Problem

VFX Caster only listed **Hovl Studio** + catalog keys. Owner’s validated fire
(`FlameThrower` under Unity **Particle Pack**, multi-layer) never appeared, so the
booth could not audition the WO-757/758 recipe. Preview also needed explicit
multi-layer Simulate + depth for soft particles.

## Design (match WO-757/758 + Room Forge style)

| Principle | Application |
|-----------|-------------|
| Prefab = recipe | Preview whole hierarchy; never flatten layers |
| Pack-aware library | Hovl + ParticlePack + Spells (TEXT labels) |
| Multi-layer | List layer names; Simulate roots `withChildren:true` |
| Soft particles | Preview camera `depthTextureMode = Depth` |
| Colorblind | Pack/status TEXT only |

## Delivered

File: `Assets/Editor/VfxCasterWindow.cs`

- Pack toggles: Hovl / ParticlePack / Spells (EditorPrefs)  
- Scan `Assets/UnityTechnologies/ParticlePack` + Spells Pack  
- Labels: `[ParticlePack] FlameThrower  [uncatalogued]`  
- Root PS detection fixed (ancestor walk, not self-in-parent)  
- Stop PlayOnAwake → `Simulate(t, withChildren:true, restart:true)`  
- Auto-fit scale/camera for huge pack effects  
- Dig-in: pack, root count, **Layers (do not flatten)** list  
- Tip: search FlameThrower for multi-layer fire  

## Acceptance (PO)

- [ ] Open **Defenders > Animation > VFX Caster** after recompile  
- [ ] Toggle **ParticlePack** ON, Rescan  
- [ ] Search **FlameThrower** → select → preview animates flame + embers + smoke  
- [ ] Shader audit shows materials (flag [BROKEN] if pink)  
- [ ] Hovl catalogued keys still list and play  

## Do NOT

- Flatten FlameThrower into one ParticleSystem  
- Wire Syndrath breath in this WO (that is WO-757)  
- Commit gitignored pack binaries  

## RESULT

`WorkOrders/WORK_ORDER_754_vfx_caster_particle_pack_preview.RESULT.md`
