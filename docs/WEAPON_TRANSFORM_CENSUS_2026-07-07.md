# WEAPON & SHIELD TRANSFORM CENSUS — 2026-07-07 (owner-requested "whole stack" audit)

> Owner directive: "I want to know EVERYTHING that touches the rotation. There have been
> band-aids on band-aids. Everything that touches placement from creation to spawn in town."
> Read-only census, verified from code with file:line citations. Companion to the offsets
> consumer-chain RCA (in flight) and the instrumented headless proof (to follow).

## HEADLINE FINDINGS

**1. Two orientation systems select by combat state.** The elaborate hand-grip composition
(SeatNative → MeleeGripNudge → offsets.json nudge → global +180° yaw) only RENDERS when the
weapon is DRAWN. Out of combat, `ApplyHoldPose` re-parents both props to a back socket with a
COMPLETELY SEPARATE orientation path (`ComputeSheathRotation` for the sword,
`_sheatheOffHandLocalEuler + 180Y` for the shield) that ignores every dialed offset.

**2. Grip rotation is implemented in FIVE stacked places** (+ a sixth for the sheathe view):
preset `gripEuler` → `MeleeGripNudge`/`_swordGripEuler(-25,90,0)` → `ComputeMeleeGripRotation`
→ offsets.json `rot` → `WeaponGlobalYawDeg=180` (added 07-05, ON TOP of values dialed before it
existed — the sheathe shield euler `192` is literally `12 + 180` split across two layers).

**3. offsets.json carries a `scaleXyz` field the runtime silently ignores**
(`AttachmentOffsetRegistry.JsonEntry` has only uniform `scale`). Editor writes it; runtime
cannot read it.

**4. Three offset files, one concern:** repo `Assets/OffsetForge/offsets.json` → mirrored to
`Resources/OffsetForge/offsets` (ships) → overridden per-id at runtime by
`offsets-dev.json` in persistentDataPath (Seating Editor saves; OVERLAY WINS silently).

**5. `shield_A` fullOverride half-discards the 2026-06-23 authored preset:** preset
`gripEuler(-58,16,-90)` is overwritten by the override euler, but preset `gripPos(-0.05)` still
adds in. Half the old dial is dead, half is live.

**6. Scale has four owners** (`ProportionalHeldLength`, `SeatNative`/`NormalizeInto`
scale-by-longest, `CompensateParentScale`, fullOverride `one*fo.scale`) and the compensate step
fires inconsistently: main hand always, shield only on some branches — and NOT when the shield
re-parents to the back socket (the "sheathed shield oversized" capture 9403 admitted this).

**7. Dead code still present:** `SeatByHandle` (retired inference), `SetCombatActive` (the
designed combat signal — never called; an `Update()` poll auto-mirror owns the state instead),
`ff.weapongripinfer` branch (default OFF).

**Rule-outs (owner hunt list):** `HudPostureReset` touches HUD only. `GearVisualApplier`
primitive path gated off. No import-time postprocessor rotates weapon meshes (import layer only
sets isReadable).

## FULL LAYER MAP (execution order, file:line)

- **L0 import:** `WeaponPropReadablePostprocessor.cs:44-57` (isReadable only, RC3a 07-04).
- **L1 presets:** `EquipmentController.cs:110-166` (Sword/Shield gripPos/Euler/heldLength),
  `:389-434` (rig axes + per-family nudges WO-435), `:335-355` (sheathe pose fields),
  `:430-434` (`WeaponGlobalYawDeg=180` + `ApplyGlobalWeaponYaw`, owner 07-05).
- **L2 id→visual:** `:186-214` (`knight_starter → Native(Sword("sword_A"))`), `:2316-2382`.
- **L3 attach bone:** `RigAttachmentRegistry` (WO-510) → avatar fallback
  (`EquipmentController.cs:647/657` main, `:1409/1419` off-hand). The one clean layer.
- **L4 main-hand seat:** `AttachLoadedProp :798-991` — offset key = vis.mesh `:824-838`;
  `trustNativePivot :842-843` (sword_A = TRUE); `ProportionalHeldLength :844/2229-2233`;
  `SeatNative :853/2444-2455` vs `WeaponBoundsOrient.NormalizeInto` (`WeaponBoundsOrient.cs:31-145`)
  + `SeatHiltLowerHalf :1204-1244`; parent `:872`; `CompensateParentScale :873/1782-1802`;
  base rot `:907-920`; offset nudge/override `:934-960`; global yaw `:962`;
  render-verify/rollback `:978-980`; → `ApplyHoldPose :988-990`.
- **L5 off-hand seat:** `AttachOffHandProp :1538-1645` — fullOverride branch `:1559-1570`
  (preset euler DISCARDED, `_offHandParentCompensate=false`); global yaw `:1612`.
- **L6 carry state:** `ApplyHoldPose :1718-1766`, driven by `Update()` auto-mirror `:1682-1710`
  (polls battle/wave; `SetCombatActive :1665` dead). Sheathe path `:1804-1851`
  (`ResolveBackSocket`, `ComputeSheathRotation`).
- **L7 body swap:** `ReseatForBody :523-533` (from `HeroArmorVisual.cs:372,876`) → full re-attach.
- **L8 Seating Editor (dev):** `SeatingEditorOverlay` → `BeginSeatingEdit :2064`,
  `ApplySeatingPreview :2109-2170`, `SaveSeating :2178-2198` → `offsets-dev.json` overlay
  (`AttachmentOffsetRegistry :107/211`); editor bake via `OffsetForgeMirrorSync` (Resources copy
  is what ships — `ReadBaseJson :148-164`).

## ONE-OWNER-PER-CONCERN TARGET (the collapse proposal, pending owner sign-off)

| Concern | Single owner should be | Today scattered across |
|---|---|---|
| Mesh orientation | WeaponBoundsOrient (geometry) | + SeatNative bypass + offset euler + global yaw |
| Grip point | SeatHiltLowerHalf / centre-grip | + presets + offset pos + dead SeatByHandle |
| Grip rotation | ComputeMeleeGripRotation | + preset euler + MeleeGripNudge + offset rot + global yaw |
| Per-asset calibration | AttachmentOffsetRegistry (ONE file) | 3 files + presets + hardcoded yaw |
| Scale | ProportionalHeldLength × one normalize | 4 owners, inconsistent compensate |
| Carry state | ApplyHoldPose w/ ONE orientation source | own parallel sheathe-orientation system |
| Combat signal | SetCombatActive (event-driven) | Update() poll; setter dead |

_Compiled from the 2026-07-07 overnight census agent (read-only, code-verified). The
instrumented headless proof of the owner's specific "offsets not applying" symptom follows in
the RCA doc._
