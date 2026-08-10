# WORK ORDER 957 — EXIT beacon appears on EVERY stairwell in multi-floor dungeons

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 957 → 958 in the same edit)
**Silo:** Dungeons (exit beacon placement) — companions: WO-1007 (arch → icon), WO-1008 (beacon reads
as light); this WO is the PLACEMENT bug, those are the PRESENTATION tickets
**Origin:** owner F8 seq 2287, 2026-08-10 11:20, dg_ember_deep, verbatim: *"Seems to be an exit zone
at many places, feels like a bu[g]. I see on all stairs."* Corroborated by screenshot
`flag_20260810-150215_11.png` (seq 2286 frame): the green EXIT arrow standing on a mid-dungeon
DESCENT stairwell, floors above the actual exit.

## 1. ✅ MECHANISM FOUND AT SOURCE (2026-08-10, same session — supersedes the first hypothesis)

The "exit zones at many places" are the **PER-FLOOR EXTRACT PADS, and they are DELIBERATE**:
`DungeonExitInteractable.cs:82-93` — *"per-floor EXTRACT PADS are also DungeonExitInteractables"*,
baker-renamed `Extract_<id>` (`DungeonBaker.PlaceComposeExtracts`); default label authored as
`"Extract"` (`DungeonComposeLayout.cs:161`, one layout authors "Extract (deep)"). Leave-from-any-floor
is a design affordance — **the defect is PRESENTATION**: every pad wears the full green EXIT-arrow
beacon, so mid-dungeon floors read as "the way out" (owner F8 2287 + the seq-2286 screenshot).

**Plus the COPY defect (owner F8 2288, verbatim):** *"the word extreact [Extract] on screen is odd...
but its horrible."* "Extract" is shooter jargon, tonally wrong for Echoes of Elarion.

## 1b. Owner pins needed (both hers)

1. **Keep leave-from-any-floor pads?** (Recommended KEEP — a punishing no-exit crawl contradicts the
   cozy bar; the fix is presentation, not removal.)
2. **The word.** Candidates (ASCII, one word preferred): `Leave` · `Ascend` · `Return` · `Depart` —
   with the TRUE exit possibly distinct (e.g. pads say "Leave", the final exit says "Exit"). Her pick.

## 2. Fix shape (presentation-first, per §1)

- The FULL EXIT beacon (big green arrow) marks ONLY the layout's true exit; per-floor pads get a
  quieter, distinct affordance (small pad glyph + the owner's chosen word — word+shape, not a hue
  swap). Composes with WO-1007 (arch → icon) + WO-1008 (beacon reads as light).
- Relabel from the owner's word pick: the `label` default + the one authored "Extract (deep)" row
  (dual-copy layouts, version bump).

## 2. Fix shape

- ONE exit: the beacon spawns only at the layout's designated exit (the data should say which room —
  if no exit designation exists in the layout schema, add it additively with a version bump and
  default it to the entry room, matching pre-WO-930 behavior).
- Descent stairwells get NO exit marker (their affordance is WO-1008's territory — if stairs need
  their own "descend" cue, that is a separate presentation item; do not smuggle it).
- Regression: for each converted layout (dg_bonecrypt / dg_ember_deep / dg_sunken_vault /
  dg_stairwell_probe), exactly ONE exit beacon; a multi-stair layout never grows a second.
  ⚠ Do NOT touch the control-group graphs (`dg_stair_rig` / `dg_descent_probe` — DO NOT DELETE
  quarantine, DungeonMultiLevelRegression.cs:41-63).

## 3. What NOT to touch

WO-930's stair model/control group · the bake's PathComplete probe · WO-1007/1008 presentation scope.
