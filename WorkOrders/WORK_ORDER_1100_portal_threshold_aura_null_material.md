# WORK ORDER 1100 — Dungeon-portal threshold aura renders with a NULL material (MagentaProbe M2)

**Status:** READY - ⭐ **the owner ruling LANDED 2026-08-24** (`FOUNDATIONAL_RULINGS.md` §4): restoring a prefab's own missing material is a REPAIR and is the lead's call. *(Prior line said:)* BLOCKED - owner ruling open. The normalizer + `[vfx-null-slot]` suite landed (`bb9844a97`) and the original theory was DISPROVED, but a ruling is outstanding. *(Bucket corrected 2026-08-24: led with IMPLEMENTED while saying OWNER RULING OPEN - the WO-1181 class.)*
>  PRIOR: **Status:** IMPLEMENTED 2026-08-16 - theory DISPROVED (MagentaGuard false-positives on authored-disabled renderers); normalizer + [vfx-null-slot] suite landed (commit `bb9844a97`); OWNER RULING OPEN on 5 genuine null-slot ParticlePack prefabs - see RESULT
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1100 -> 1101 in the same edit; ⚠ this is the FIRST
mint of the new CLI block (the main line jumped 1000→1100 over the UI seat's 1000–1099 — C-1 went
live; owner ratification of the block ranges pending).
**Lane:** VFX assets / portal presentation. Disjoint from WO-946 (withhold policy) and WO-1025 (tree).
**Provenance:** owner F8 **seq 2404–2415** (12 identical captures, 2026-08-16 06:53, editor session,
`Main_Castle_Overworld`):

> `[Flow:MagentaProbe] FAIL cause=DungeonWorldPortalSpawner.BuildPortal
> obj='[DungeonWorldPortals]/DungeonWorldPortal_HealersCottage/[Hovl_Portal_Threshold_Aura]'
> slot=0 material='NULL' shader='NULL' supported=n/a class=M2`
> (stack: `MagentaGuard.Probe:689` <- `SweepGameObject:491` <- `DungeonWorldPortalSpawner.BuildPortal:447`)

## 1. What the captured data proves

Every portal build in the session probed a renderer under the threshold-aura child whose slot-0
material is **NULL** (not a broken shader — `shader='NULL'`, class M2). The WO-869 belt-and-braces
recovery sweep at `DungeonWorldPortalSpawner.cs:443-447` fires and cannot help: a NULL slot is not a
broken-shader repaint. 12 captures = one defect probed once per portal.

## 2. Known adjacent facts (read before theorising)

- The aura is attached by `AttachThresholdAura` (`DungeonWorldPortalSpawner.cs:796`), key
  `ThresholdAuraKey = "Portal_Threshold_Aura"` (`:730`) — the spawned object's name
  `[Hovl_Portal_Threshold_Aura]` says the key resolves to a **Hovl-derived prefab**.
- **This is the VFX self-containment class** (the `Casting_Fire` precedent): `CopyAsset` mirrors a
  prefab but never its materials; the 08-06 mirror pass fixed 27 prefabs under `Resources/VFX` and
  `VFX_ART_MIRROR_OK` guards THAT root — a portal prefab living elsewhere (or added later by
  `d7e2e4eae` "real pack vortex" / the `0e4690036` portal-material metas) escapes the gate.

## 3. Plan

1. **Triage (read-only):** resolve which prefab `Portal_Threshold_Aura` maps to (VFX catalog row),
   open it, list its material GUIDs, and check each against disk. Name the missing one.
2. **Fix at the owning layer:** restore/mirror the missing material into tracked space (dedupe into
   `Resources/VFX/_Shared/` per the mirror convention) — or, if the material never existed, this is a
   VFX-key retag which is the OWNER's tag to make (stop and surface).
3. **Widen the gate:** `VFX_ART_MIRROR_OK`'s scan scope must include wherever this prefab lives, so
   the next escaped prefab fails the gate instead of the owner's morning.
4. Headless verify: the MagentaProbe line absent across a portal-spawning session.

## 4. What NOT to touch

- `MagentaGuard` itself — it is the net that caught this (§12: never strip).
- `VfxManualPicks` / owner VFX tags — no creative substitutions.

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 3: **UNBLOCKED. Restoring a prefab's own missing material is a REPAIR.**

The VFX authority split is now **canon in `FOUNDATIONAL_RULINGS.md` §4** — read it there; ⛔ not
restated here, per that file's no-paraphrase rule.

Five ParticlePack prefabs have a **null material slot**. Putting back what the prefab already had is a
repair, which sits in the **lead's** column — proceed, and send her a capture.

**Status → READY.**
