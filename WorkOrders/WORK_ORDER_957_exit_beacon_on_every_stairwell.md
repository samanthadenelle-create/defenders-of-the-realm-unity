# WORK ORDER 957 — EXIT beacon appears on EVERY stairwell in multi-floor dungeons

**Status:** READY TO IMPLEMENT (PARTIAL - RE-BAKE DONE 2026-08-14: all 7 dungeons re-composed, COMPOSE_ALL_OK 7/7, 13 pads now bake label='Leave' and every layout emits exitRoomId. The code half landed + gated 2026-08-10. REMAINING: exitRoomId is the 'entry' FALLBACK everywhere - WHERE the one true exit sits is still an owner design pick; the per-layout one-beacon regression is still unwritten; Assets/Resources/Dungeon/Exit/ still absent so a PLAYER build takes the primitive-arch fallback. See the 2026-08-14 note at the bottom)
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

## 1b. ✅ OWNER PINS RECEIVED (2026-08-10, verbatim: "keep the pads and use Leave for the word")

1. **Pads: KEEP** — leave-from-any-floor stays; the fix is presentation only.
2. **The word: `Leave`** — pads relabel from "Extract" to "Leave" (and "Extract (deep)" →
   "Leave (deep)" or plain "Leave" — keep the depth hint only if it reads clean); the TRUE exit
   keeps its distinct full-exit presentation.

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

---

## 2026-08-10 - PARTIAL LANDING of the combined 957 + 1007 + 1008 lane (CLI seat, gated)

**The CODE half landed in full and is gate-green. The BAKE has not been re-run, so none of it is on
screen yet.**

Landed in `Assets/_Modules/Dungeons/DungeonExitInteractable.cs` (+349/-88):
- **WO-1008** - `Beacon_Beam` is now TRANSLUCENT and capped to world y 2.9-4.0 (`:437`), under
  `RoomForgeCanon.WallHeight = 4`, so it can no longer punch through the floor above and read as
  "a green bar rising out of the descent hole". The point light + slow pulse are unchanged and still
  carry the cue from range. Colour is deliberately UNTOUCHED (owner's call, colourblind law): the
  distinction is SHAPE and POSITION.
- **WO-1007** - the true exit builds a KayKit Option-C monument arch (`wall_arched` +
  2x `pillar_decorated`, colliders stripped), resolving Resources first then the editor kit, and on
  failure Warns and falls back to the primitive arch (`:264`, `:293-324`). A lost exit is a softlock,
  so this path never returns nothing.
- **WO-957** - TWO presentations selected by `_isTrueExit` (`:216`, `:255`). TRUE = arch + beam +
  "EXIT". FALSE = a flat translucent `Pad_Marker` disc + a small `Pad_Label`, no light, no beam, no
  "EXIT" (`BuildLeavePad`, `:371`). `DungeonBaker` passes `false` for every per-floor pad and
  passes all four args explicitly, because reflection does NOT apply C# default args. **Owner pin 1
  honoured: the pads STAY.**
- Schema v2, additive: `DungeonComposeLayout.exitRoomId` designates the ONE true exit; unset falls back
  to the entry room (the pre-multi-floor behaviour), so v1 layouts parse and behave identically.

**Completion by the committer:** the lane's session expired having written `BuildLeavePad` against two
helper methods it never extracted - the tree did not compile (`error CS0103` on `ApplyDecorMaterial`
and `BuildWorldLabel`). Both were extracted from the existing inline blocks so the pad and the true
exit now share ONE material path and ONE world-label path - a second copy is how one of them ends up
opaque again. The pinned child names `Beacon_Beam` and `Beacon_Label` are preserved
(`DungeonRoomOwnershipRegression.cs:366` finds the beam by name). One behavioural delta, an
improvement: an unresolved shader now Warns instead of silently keeping the primitive material.

**Owner pin 2 ("the word is Leave") - DATA NOW LANDED:** every shipped content layout authored
`"label": "Extract"`, which overrides the code default, so the pin had not reached the screen. All 13
extract labels across `dg_bonecrypt` / `dg_ember_deep` / `dg_sunken_vault` are now `"Leave"`, in
BOTH dual copies, verified byte-identical and parsing. The two control fixtures (`dg_descent_probe`,
`dg_stair_rig`) were deliberately left alone - they are the quarantined WO-930 control group.

**REMAINING SCOPE - why this WO stays READY:**
1. **The dungeons have NOT been re-baked.** `_isTrueExit` is a `SerializeField` defaulting TRUE and
   the pads are BAKED objects, so every already-baked `Extract_*` still deserialises as a full
   arch+beacon and still says "Extract". **Nothing above is visible until a re-bake** - and per memory
   `dungeon-scene-shared-tree-corruption` that bake belongs in an ISOLATED WORKTREE, not this shared
   tree mid-wave. That is the next mechanical step and it is the owner's call to schedule.
2. **No layout authors `exitRoomId`.** The designation mechanism exists; every layout takes the
   `entry` fallback. Behaviour is correct-by-fallback, but WHERE the one true exit sits is a design
   pick, not something to invent.
3. **The WO-957 per-layout regression was not written** ("for each converted layout, exactly ONE exit
   beacon"). Nothing asserts `_isTrueExit`, `Pad_Marker`, or a beacon count per layout.
4. `Assets/Resources/Dungeon/Exit/` does not exist, so a PLAYER build always takes the primitive-arch
   fallback (with its Warn); the editor/bake path resolves from the gitignored kit. Registered as tracked
   debt in `HudUiRegression.MissingResourceBaseline` rather than hidden.

**Gate:** `Builds/gate-settle4.log` -> `COMPILE_GATE_OK` (zero `error CS`) ·
`Builds/regression-settle3.log` -> `REGRESSION_OK 143/143 suites`.

**Owner felt-verify (after the re-bake):** dark `dg_ember_deep` - the mid-floor pads read as quiet
discs saying "Leave", exactly one arch+beam, and the beam does not stand proud of the floor above.