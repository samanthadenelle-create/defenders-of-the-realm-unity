# WORK ORDER 946 — POI node auras + Tree of Life VFX: retire the strong yellow, go subtle

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 945 → 947 in the same edit, together with WO-945)
**Silo:** VFX (aura prefabs/params) — art-tuning lane, no gameplay logic
**Type:** owner LOOK RULING (creative direction is hers; implementation maps it verbatim)
**Origin:** owner F8 seq 2252, 2026-08-10 10:17, scene Main_Castle_Overworld, verbatim:
*"remove the yelllow from the nodes and the tree of Life (its a vfx) but we want something subtle,
not so strong"*

---

## 1. The ruling

The yellow aura on the resource/POI nodes AND on the Tree of Life (Heart) is too strong. Replace with
something SUBTLE — lower intensity/saturation presence, not a louder different color. "Remove the
yellow" + "subtle, not so strong" are the constraints; the specific replacement look is dialed until
the owner felt-passes it.

## 2. Known anchors (locate-level, verify at implementation)

- The POI aura VFX family: `Poi_NodeAura`, `Poi_Landmark` (named in the 08-06 loop-cap captures) —
  find their live prefab homes under `Assets/Resources/VFX/` (the WO-905/08-06 mirror moved shared art
  to `Assets/Resources/VFX/_Shared/`).
- Tree of Life aura: the hub ambient VFX (`ff.hubambientvfx` gate; tree aura + tower glow per canon §7).
- ⚠ `VFXType` serialises by ORDINAL (append-only) and `Build()` drops builder-only rows on regenerate
  (both carried-forward canon traps) — tune the ART/params, do not reorder or hand-add catalog rows.
- ⚠ Any prefab touched must stay self-contained (VFX_ART_MIRROR rule — no reference into gitignored
  packs; the mirror regression will catch it).

## 3. Acceptance

1. Yellow reads as gone/subtle on the nodes and the Tree of Life in a DEVICE screencap or UI/scene
   capture — this class needs EYES, not markers (canon 08-09: headless gates cannot see look).
2. No new pack references (VFX_ART_MIRROR_OK stays green); COMPILE_GATE_OK + REGRESSION_OK unchanged.
3. Owner felt-verify + CLOSE (a look ruling only she can pass).

## 4. What NOT to touch

- VFX pool/reclaim logic (the ONESHOT 40/40 saturation is a SEPARATE open item — do not bundle).
- The VFXType enum order; the vfx catalog generator's derivation rules (08-06 rulings).
