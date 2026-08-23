# WORK ORDER 1067 — Certify every gear visual before sale

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: IMPLEMENTED FOUNDATION — OWNER CAPTURE APPROVAL PENDING
**Parent:** WO-1063 · **Silo:** gear art, offsets, captures and gates

## Baseline

The audit found 96 weapon rows; 76 authored icons (all present), 20 fallback/generic images, 66
Addressable prefab rows (all keys registered), and 18 rows without `prefabPath`. Registration does not
prove scale, pivot, grip, forward axis, tip direction or sheathe pose. All 24 armor rows have present
authored icons; armor is 2D image plus applied stats here.

## Readiness state

- `Ready`: item-specific icon/model, preview, held and sheathed poses approved.
- `IconOnly`: inventory-safe; 3D preview/equip held.
- `NeedsGrip`: model resolves; held pose fails.
- `NeedsSheathe`: held passes; sheathe fails.
- `MissingVisual`: no honest item-specific visual.
- `Held`: deliberately unavailable.

Only `Ready` weapons may resolve into the live Forge. Never treat a cube/generic sword as success.
Armor needs correct icon identity and functioning applied stats, not 3D attachment.

## Evidence

Create a fixed-format contact sheet for every compatible rig: list icon, large preview, held front,
held side, attack/contact frame, sheathed front and sheathed back/side. Record bounds, grip rule,
offset, scale, tip sign and failure reason. Armor gets a normalized 2D card sheet and stat proof.

Held and sheathed transforms are distinct. Geometry inference may propose but never visually certify.
Staff/bow/shield/one-hand/two-hand remain distinct archetypes. Missing assets fail loudly and become
`Held`, not a player-facing substitute.

## Gates

- Every `Ready`/Forge row resolves item-specific icon and model.
- No ready row uses primitive/generic fallback.
- Every readiness result links capture evidence.
- All armor icons resolve and match ids.
- Full runtime catalog covered, including failures.
- Fresh-clone and Android Addressables parity green.
- Owner visually approves each `Ready` weapon and representative device transitions.

## Do not

- Do not call static gates visual approval.
- Do not use a universal offset for unlike imports.
- Do not expose failures to satisfy shelf count.
- Do not rotate a hero rig to repair one weapon.

## Orientation evidence ruling (2026-08-22)

AABB dimensions are explicitly inadmissible as orientation proof: opposite rotations can be
bounds-identical. `GearVisualGeometryAudit` compares actual vertex spread at opposing longitudinal
end bands, carrying forward the taper method proven in `JewelerPitchSolver`. The result is evidence,
not certification; only the owner-reviewed contact sheet may author `visualReadiness: "ready"`.
