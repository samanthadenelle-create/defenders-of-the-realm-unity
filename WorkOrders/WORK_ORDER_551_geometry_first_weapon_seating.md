# WORK ORDER 551 — Geometry-First Weapon Seating (offset = nudge, not replacement)

**Status:** READY TO IMPLEMENT (runtime model implemented in this WO; Forge-preview slice specced as follow-up)
**Silo:** Combat/AI + Hero attach (code only — `Assets/_Modules/Village/Hero/*`, `Assets/OffsetForge/offsets.json`)
**Owner action required:** slot this WO number into the master backlog (`MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`) and felt-verify in-game; re-calibrate `sword_A` only if the geometric seat reads off (see Owner Decisions).

---

## Owner principle (north star)

> "Every sword has a similar pattern — wide on Y, narrow on X and Z — so you can
> deterministically rotate it so the hilt is forward and seat it in the hand. **Trust the
> geometry.**" Seating is GEOMETRY-FIRST and automatic; the manual Offset Forge is the
> EXCEPTION (only for weapons that break the pattern), NOT the default.

## The bug this corrects

Commit `b773176d` (already on the base branch, in `EquipmentController.AttachLoadedProp`)
made any NATIVE prefab that HAS an `offsets.json` entry **BYPASS** the geometric path
(`NormalizeInto` + `SeatByHandle`) and use the Offset Forge value as a full **REPLACEMENT**
of the grip frame (`seatNativeAuthored`). That is backwards vs the owner principle: it made
the manual offset the AUTHORITY instead of geometry. It also caused "handle still wrong"
because the authored value was dialed against the RAW pivot, not the trued+seated runtime
frame, so it could never reproduce in-game.

---

## The model (geometry-default + offset-as-nudge)

1. **Geometry is the default for ALL conforming melee.** Always `NormalizeInto` (true the
   prop: longest axis → +Y, narrowest → +X, bounds-centred, scaled to held length) then, for
   melee, `SeatByHandle` (find the crossguard width-spike, seat the grip at the handle, blade
   → +Y). This seats every standard sword correctly with **NO per-weapon authoring**.
2. **Offset Forge becomes a CALIBRATION NUDGE applied ON TOP of the geometric result** —
   `true → seat-by-handle → (optional) nudge`. The nudge is RELATIVE to the trued+seated
   runtime frame: COMPOSE the rotation onto the geometric grip (`_baseGripRot *= Euler(rot)`),
   ADD the position (`gripPos + pos`), MULTIPLY the scale. **An all-zero entry == pure
   geometry** (no-op).
3. **Exception (opt-in, per-entry, native-only): `"fullOverride": true`** on an entry makes a
   genuinely non-conforming prop skip geometry and reproduce the Forge raw-pivot frame exactly
   (the legacy replacement behaviour). **Default is nudge-on-top**, so geometry always runs
   unless an entry explicitly opts out.
4. **Forge previews in the trued frame** (so the authored nudge reproduces in-game) — *specced
   as a follow-up slice below; deferred from runtime to avoid coupling the generic tool to
   game geometry. Runtime model is fully implemented now.*

RANGED (bow/shield) stay on their existing simpler paths — shields go through
`AttachOffHandProp` (which does not consult the registry at all), bows through their own
`NormalizeInto` + preset euler. This WO touches ONLY the main-hand melee path.

---

## Files to edit

- `Assets/_Modules/Village/Hero/EquipmentController.cs` — `AttachLoadedProp`:
  - Remove `seatNativeAuthored` (the b773176d bypass). Native melee now runs geometry.
  - Seat decision: `(vis.native && !meleeSeat) || fullOverride` → `SeatNative`; else
    `NormalizeInto` (+ `SeatByHandle` for melee). Restores pre-b773176d geometry for swords.
  - Base rotation: `fullOverride` → `Euler(rot)` (replacement); else melee →
    `ComputeMeleeGripRotation`; else preset euler.
  - Offset apply → **nudge**: `localPosition = gripPos + pos`,
    `_baseGripRot *= Euler(rot)`, `localScale *= scale`. `fullOverride` branch keeps the raw
    replacement (pos + `one*scale`).
  - FlowTrace instrumentation: trued? seated grip-shift localY? nudge applied? (per §12).
- `Assets/_Modules/Village/Hero/AttachmentOffsetRegistry.cs` — add `bool fullOverride` to
  `AttachmentOffset` + the JSON mirror (`JsonEntry.fullOverride`); default false (missing key
  → false, so existing files are unaffected).
- `Assets/OffsetForge/offsets.json` — zero `sword_A`'s rotation to `(0,0,0)` (it was authored
  in the old replacement convention; under nudge-convention a conforming sword needs a ZERO
  nudge). All-zero == pure geometry.

## What NOT to touch

- The off-hand / shield path (`EquipOffHand`, `BeginAddressableOffHand`, `AttachOffHandProp`)
  — shields keep their proven seat and do not consult the registry.
- Bow seating (`Bow(...)` preset, `HeroBowAttachment`) — RANGED stays as-is.
- `NormalizeInto`, `SeatByHandle`, `ComputeMeleeGripRotation` internals — reuse, do not rewrite.
- `sword_G` / `sword_D` / `sword_F` (raw fbx, no offset entry) — already pure geometry; must
  not regress.
- No `.unity` scene edits; no `System.Reflection`; LogWarning never error on missing assets.

## Acceptance criteria

- [ ] A native sword WITH an offset entry (e.g. `knight_starter` → `sword_A`) runs
      `NormalizeInto` + `SeatByHandle` (FlowTrace shows `seat: GEOMETRY ...` + `trued+seated:
      grip-shift localY=...`), NOT the bypass.
- [ ] With `sword_A` zeroed, the starter sword seats by pure geometry (hilt in palm, blade
      forward) with no authored rotation.
- [ ] A non-zero nudge entry composes ON TOP of geometry (FlowTrace `NUDGE ... on geometry`),
      and an all-zero entry logs `pure geometry (no nudge)`.
- [ ] `"fullOverride": true` (native) reproduces the legacy raw-pivot frame (escape hatch).
- [ ] `sword_G` and other no-offset swords are unchanged (pure geometry, no regression).
- [ ] Shields/bows unchanged.
- [ ] Brace balance OK on both `.cs`; `offsets.json` parses.

---

## Owner decisions flagged

- **`sword_A` rotation zeroed.** The stored `(-94,14,-100)` was a replacement-convention value
  (raw pivot). Under the new nudge convention applying it on top of geometry would double-rotate
  the already-correct sword, so it is zeroed to "pure geometry" (the conforming-sword
  expectation). If the geometric seat reads slightly off in felt-test, dial a SMALL nudge in the
  updated Forge (once the preview-in-trued-frame slice lands) — not a full replacement.
- **`shield_A` / `Knight` / `crystals` entries left as-is** — not consumed by the main-hand melee
  path (shield uses the off-hand path; Knight/crystals are hero/prop alignments authored in the
  Forge), so the convention change does not affect them.

---

## Follow-up slice (specced, NOT done here) — Forge previews the trued frame

**Why deferred:** `OffsetForgeWindow` (`OffsetForge.Editor`) is deliberately generic with NO
game references; `ApplyOffsetToInstance` sets `localRotation/Position/Scale` on the RAW model
instance. To preview in the SAME frame the runtime produces, the editor must run
`NormalizeInto` + `SeatByHandle` on the preview instance before applying the nudge — but those
live private in `EquipmentController` (`DeNelle.Village`). Replicating them in the tool, or
referencing the game assembly from the generic tool, is a real refactor and risks the
serialization-sensitive grip code.

**Recommended approach:** extract `NormalizeInto` + `SeatByHandle` (+ the width-profile helpers)
into a shared pure-static `WeaponGeometry` utility (candidate home: `OffsetForge.Runtime` so the
generic tool can reference it, or `DeNelle.Core` with the tool taking a ref). Have BOTH
`EquipmentController.AttachLoadedProp` and `OffsetForgeWindow` call it. Then the Forge:
1. instantiates the model, runs `WeaponGeometry.TrueAndSeat(instance)` (trued + grip-seated),
2. applies the authored nudge ON TOP (compose rotation, add pos, mul scale),
3. (optionally) parents under the existing R_Hand context with `ComputeMeleeGripRotation`'s
   basis so the owner dials against the real hand frame.
This makes the authored value reproduce 1:1 in-game (root cause of "handle still wrong").
Mint as the next WO slice; keep `fullOverride` entries previewing the raw pivot.
