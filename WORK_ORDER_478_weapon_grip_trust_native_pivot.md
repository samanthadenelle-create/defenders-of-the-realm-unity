# WORK_ORDER_478 — Weapon grip: trust the native prefab pivot (supersedes stale WO-435)

**Status: READY TO IMPLEMENT** (instrument first, then fix; held until editor closed) · F8 ticket #5 "weapon grip."
**Type:** EXISTING (WO-435 marked FIXED 06-18 but NOT fixed; its RCA is stale vs current code) · **Silo:** Hero/Equipment.

## Root cause (RCA agent, code+asset proven)
Default knight weapon = `knight_starter` (weapons.json:64, no prefabPath → Resources branch) → IdMap `sword_A` →
`Assets/Resources/Heroes/Props/Weapons/sword_A.prefab` = the Blink `Sword1h_01` mesh, **authored grip-at-origin,
identity transform** (a genuine NATIVE prop). BUT `EquipmentController.AttachLoadedProp` (:663-683, the "BUG 2 FIX"
at :671-672) **discards the native pivot for ALL melee** and reverse-engineers a grip via `NormalizeInto +
SeatByHandle`:
- `SeatByHandle` (:833-921) bins vertices for a crossguard "width spike" ≥1.6× median (:862). A stylized Blink
  sword has no pronounced flare → spike test fails → falls to the **16%-bounds fallback** (:885-904) that GUESSES
  blade-vs-pommel by end-width (:892-894); a wrong guess flips the blade 180° (:914-920) or grips mid-hilt.
- **Build-killer:** `CollectWidthProfile` skips meshes where `!sharedMesh.isReadable` (:937,:953). If the Blink mesh
  isn't Read/Write enabled, EVERY bin is empty → the 16% guess ALWAYS runs. The correct native pivot was already
  thrown away at :671. → "a wrong constant replaced by a wrong heuristic."

WO-435's RCA cites line numbers/values that no longer exist (pre-generalization code) — **reopen/supersede it.**

## Fix (instrument FIRST per §12 — no weapon-transform diagnostic exists today)
1. **Instrument** (the missing proof tool): in `AttachLoadedProp` after seating, `FlowTrace.Step("Equip", ...)` dump
   `prop.localPosition`/`localRotation.euler` (mesh vs gripRoot) + `gripRoot.localPosition`/`_baseGripRot.euler`
   (gripRoot vs hand bone); in `SeatByHandle` log `clearSpike`, `spikeBin`, `median`, branch taken (spike vs 16%),
   blade-flip. Headless-equip `knight_starter`, capture the seated offset → pinpoint.
2. **Fix:** when `vis.native && IsMelee(vis.kind)`, route through **`SeatNative`** (:1508-1523, trust grip-at-origin
   + scale only) instead of `NormalizeInto + SeatByHandle`. Gate the inference path on **`!vis.native`** (its original
   intent before BUG 2 inverted it) — keep inference only for non-native Tripo/raw FBX (sword_D/F/G, staff_*).
   Apply only the small per-archetype nudge (`_swordGripEuler`) if the dump shows it's still needed.
3. **Belt-and-braces:** enable Read/Write on the Blink weapon FBX importers so the inference path (when it runs) has vertices.
4. **Verify gap:** `VerifyWeaponRendersNow` (:735-774) checks visibility NOT orientation — a mis-gripped weapon passes.
   Consider an orientation assert.

## Acceptance
knight_starter (+ other native Blink weapons) seat hilt-in-palm, blade pointing out, no 180° flip — proven by the
captured transform dump (seated offset ~native pivot), not by eye alone. Non-native weapons unaffected.

## NOT touch
The non-native inference path itself (only gate it off for native); unrelated equip/loadout logic. Mark WO-435 superseded.
