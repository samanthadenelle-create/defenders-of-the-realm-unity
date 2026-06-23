# WORK ORDER 435 — Weapon grip/handle orientation on the held hero weapon (LOGGED BUG)

**Status: FIXED (2026-06-18)** — melee weapons (incl. native Blink/Tripo swords) now ALWAYS run the
derived NormalizeInto -> SeatByHandle -> rig-hand-axis grip path; native pivot is no longer trusted for
melee (it was wrong -> hilt floated/blade clipped). Equip flow instrumented with deep FlowTrace ("Equip").
Was: **LOGGED — fix later** (owner: "agree but log bug for later", 2026-06-17).
**Severity:** P2 felt/polish (cosmetic; equip flow itself works). **Lane:** Combat/Art (orient).

## Symptom
The equipped weapon shown in the hero's hand reads mis-gripped — the handle isn't seated in
the palm / the weapon's orientation looks off. Surfaced repeatedly in F8 playtest captures:
"weapon placement feels off" (06-17 00:05), "weapon still same could be our weapons" (04:30),
**"Handle grip" / "handle issue"** (06-17 19:02, `flag_04.png`, MainCastle_Hall).

Note: this became visible *because* WO-434's MVVM inventory now lets the player equip freely and
the equip→`GearLoadout`→world-visual path fires correctly — so this is an ORIENT bug, not an
equip/inventory bug.

## Likely area (§4 — derive transforms from geometry + name, do NOT hand-type Eulers)
- `WeaponOrientHelper` (the generalized weapon/armor orient — apply at equip, adjustable via DevOrient).
- `HeroBowAttachment.NormalizeInto` (the proven bow grip precedent: grip constants + NormalizeInto).
- `GearVisualApplier` (melee attach: RightHand bone, per-archetype localPos/Euler/scale — currently
  TUNED CONSTANTS for sword/staff/mace; the grip offset there is the prime suspect).
- `EquipmentController` (KayKit real-mesh attach path).
- Canon: `docs/WEAPON_ARMOR_ORIENT_LOGIC.md` (read before touching — orientation is DERIVED from
  mesh bounds + asset name, never guessed; a `manual=true` correction is canon and never overwritten).

## When picked up
- Diagnose which path is rendering the held weapon (primitive `GearVisualApplier` cubes vs
  `EquipmentController` KayKit mesh) before adjusting — the grip offset lives in whichever is active.
- Fix per §4 (derive grip from bounds+name; if a manual nudge is needed, mark it `manual=true`).
- Verify against the held-weapon for each class/weapon archetype (sword/staff/mace/bow).

## RCA (read-only agent, 2026-06-17 — root cause confirmed, NOT fixed)

**Active render path = `EquipmentController`** (NOT `GearVisualApplier` — its primitive-cube path is
gated off, `EnablePrimitiveGear=false`). Flow: `GearLoadout.OnGearChanged` → `EquipmentController
.HandleGearChanged` → `EquipBestForHero` → `Equip(weaponId)` → attach KayKit mesh to `RightHand`.

**Root cause (a §4 violation):** melee grip transforms are **hand-typed constants**, applied
asset-agnostically, and **inconsistent** across archetypes — instead of derived from mesh bounds+name:
- Sword `gripPos=(0,0.02,0)` (EquipmentController.cs:88-93) + rig-derived `ComputeSwordGripRotation`
  (633-655) + a serialized `_swordGripEuler=(-25,0,0)` nudge (252). Also runs `NormalizeInto`+`SeatByHandle`.
- Staff `gripPos=(0,0.05,0)` (112-117) and Mace `gripPos=(0,0.02,0)` (106-111): **identity `gripEuler`,
  NO rig-derived rotation, NO `SeatByHandle`** — so orientation depends entirely on `NormalizeInto`'s
  output; if the FBX's mesh-local longest axis ≠ visual "up", it sits wrong. This is the prime smell.
- The same Y-offset is applied to every instance of a type, ignoring each FBX's own handle pivot.
- **Bow is the correct precedent:** `HeroBowAttachment.NormalizeInto` (HeroBowAttachment.cs:166-196)
  derives grip-at-origin from bounds → identity grip euler works. Melee never generalized it.
- Tooling: `WeaponOrientHelper`/DevOrient/Orientation-Inspector are NOT implemented yet (future per
  the canon doc); `_swordGripEuler` is a session-only inspector hack, not a persisted `manual=true`.

**Recommended fix (Option A, preferred — not implemented):** generalize the bow's `NormalizeInto`
(bounds-derived, longest→Y, narrowest→X, grip centred at origin) to ALL melee; drop the hand-typed
`gripPos`/`gripEuler` constants; apply a rig-hand-axis rotation only AFTER the mesh is bounds-oriented.
Fallback interim: data-drive per-asset `visualGrip`/`visualEuler` (weapons.json) so it's tunable
without recompile. Either way honor §4 (derive; mark any manual nudge `manual=true`, never overwritten).

Key cites: EquipmentController.cs:88-144 (presets), :327-389 (attach+seat), :633-655 (sword rot),
:772-798 (NormalizeInto), :405-493 (SeatByHandle); HeroBowAttachment.cs:166-196 (correct precedent).

*Cross-ref:* `docs/WEAPON_ARMOR_ORIENT_LOGIC.md`, `ARCHITECTURE_PRINCIPLES.md §4`, WO-434 (the
inventory work that surfaced it). Mirror to the Notion "Work Orders" board.
