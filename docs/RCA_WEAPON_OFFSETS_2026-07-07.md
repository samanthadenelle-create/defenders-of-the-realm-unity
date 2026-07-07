# TRUE RCA — "why are the sword and shield still off?" (owner F8 2026-07-07 02:01)

> Owner mandate: DATA-proven RCA, find the band-aids to remove. Every claim below cites a
> captured line or a read code line. Companion: `docs/WEAPON_TRANSFORM_CENSUS_2026-07-07.md`
> (the full every-site transform census).

## VERDICT — two proven causes, neither is "the offsets didn't load"

**The offsets loaded and applied.** Player.log (her 02:00 session, dev build, FlowTrace live):
- `[Flow:Offset] loaded attachment offsets: 12 base + 4 dev-override -> 12 effective.`
- `[Flow:Offset] NUDGE 'sword_A' on geometry: +pos=(0.05,-0.01,-0.05) *rot=(117,-61,-111) *scale=0.46`
- `[Flow:Equip] off-hand seat: GEOMETRY-VERTICAL + saved DELTA pos=(0.09,-0.01,0.02) rot=(-180,-180,-70) scale=1`
(The earlier "silent log" read was a grep miss — the seat path traces as `[Flow:Equip]`/`[Flow:Offset]`.
Also ruled out by the same lines: the Paladin package-baked-body bypass — her hero is KnightV3,
17/17 renderers; and the id mapping works: equipped `tripo_shield_a` resolves mesh `shield_A`.)

### CAUSE 1 (what she saw in town) — the SHEATHE pose is a second orientation system that ignores every dialed offset
- Screenshot flag_00 (02:01): gear is SHEATHED — shield flat like a plank across the back,
  sword behind the shoulder. Log: `ResolveBackSocket ... sheathe anchor under 'CC_Base_Spine01'`.
- The Seating Editor tunes ONLY the drawn in-hand seat — `BeginSeatingEdit` literally calls
  `DrawForEditing` ("the seating editor tunes the IN-HAND (drawn) seat", EquipmentController ~2095).
- Out of combat, `ApplyHoldPose` (~1718-1766) re-parents to the back socket and applies
  hard-coded poses: derived `ComputeSheathRotation` for the sword (07-04 fix), and for the shield a
  hand-typed euler `_sheatheOffHandLocalEuler=(0,90,192)` + global 180° yaw — the same "magic euler"
  class the 07-04 comment itself calls out as the wrong pattern. Her offsets are composed into the
  DRAWN cache and never render in town. **Town is where she plays. This is the felt complaint.**

### CAUSE 2 (drawn-state WYSIWYG break) — the editor preview skips parent-scale compensation
- Runtime: `CompensateParentScale` sets gripRoot scale to `1/hand.lossyScale` — captured:
  `parent-scale compensate: parent='CC_Base_R_Hand' lossy=(1.666,1.666,1.666)` — then multiplies
  her `fo.scale`.
- Preview: `ApplySeatingPreview` sets `grt.localScale = Vector3.one * scale` — RAW (code ~2160).
- So the look she approved (preview, world ≈ 0.46×1.666 = 0.77) boots at world ≈ 0.46 — the sword
  reproduces **40% smaller than dialed, every boot, deterministically**.

## FIXES (landed overnight)
1. **Preview/runtime scale parity** — ApplySeatingPreview now composes the same compensate factor
   as the attach path (one shared source of truth).
2. **Saved value converted** — `sword_A` scale 0.46 → 0.766 (= 0.46 × the captured 1.666) in both
   repo mirrors AND the LocalLow dev overlay, so the look she APPROVED is what boots.
3. **Sheathe poses now owner-authorable through the SAME registry** — `ApplyHoldPose` consumes
   optional `<mesh>@sheathed` offset entries (fullOverride = absolute pose in the socket frame;
   nudge = on top of the built-in); the Seating Editor gains a Drawn/Sheathed mode so she can dial
   the back pose in-game exactly like the grip. No entry = old behavior (zero regression).

## BAND-AIDS FOUND (owner's remove-list — census-cited, decisions hers)
| # | Band-aid | Evidence | Recommendation |
|---|---|---|---|
| 1 | TWO orientation systems (drawn grip vs sheathe) selected by combat state | census conflict #1 | Fixed structurally by #3 above: sheathe = same registry |
| 2 | Grip rotation in FIVE layers (preset euler → MeleeGripNudge → ComputeMeleeGripRotation → offset rot → global +180 yaw) | census conflict #3/#4 | Collapse: bake WeaponGlobalYawDeg=180 into the derived grips + re-save offsets; retire preset eulers for offset-covered ids |
| 3 | `shield_A` fullOverride half-discards the 06-23 preset (euler dead, pos live) | AttachOffHandProp :1559-1570 | Zero the preset gripPos into the offset; delete preset euler |
| 4 | `scaleXyz` field written by the Forge, silently ignored by the weapon registry | JsonEntry :81-89 has no field | Remove from Forge output for weapon entries (it IS used by CastleMoatBuilder for bridge_south — different consumer) |
| 5 | Three offset files (repo, Resources mirror, LocalLow dev overlay that silently WINS) | registry :107/148-164 | Keep, but surface "DEV OVERLAY ACTIVE (n)" in the Seating Editor so a stale overlay can't silently mask repo values |
| 6 | Dead code: `SeatByHandle` (retired inference), `SetCombatActive` never called (an Update() poll owns combat state), `ff.weapongripinfer` branch | census L9 / conflict #8 | Delete SeatByHandle; either wire SetCombatActive from the battle events or delete it |
| 7 | `CompensateParentScale` fires on some branches, not others (shield fullOverride skips; sheathed shield uncompensated — capture 9403) | census conflict #7/#8 | One rule: always compensate, and offsets are authored in compensated space (the parity fix makes this WYSIWYG) |

_RCA compiled overnight 2026-07-07 from: owner F8 captures + screenshots (flag_00/01), Player.log
flow lines (dev build), the transform census (26-file read-only audit), and direct code reads.
No fix in this document was inferred — each traces to a cited line._
