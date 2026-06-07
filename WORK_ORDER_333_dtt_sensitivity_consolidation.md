# WORK ORDER 333 — DTT movement/aim sensitivity: consolidate ("group those")

**Status: ❌ DUPLICATE — CLOSED. Superseded by WO-332 (DTT aim sensitivity).** Kept (not deleted) to
preserve the tree; the `_aimSensitivity` + clamp + "group the scattered sensitivity" work lives in WO-332.

**[original spec below — see WO-332]**

**Status (orig): SPEC — diagnose then group.** **Lane:** 3 (Combat Feel) / DTT. **Origin:** owner playtest
2026-06-07 — "in DTT there's something about movement sensitivity adjusting in a story; try to group those."

## Problem
DTT (PatriciaLight) movement/aim **sensitivity is adjusted in scattered places** — including during
**story/sequence beats** — so it drifts/feels inconsistent and there's no single dial. The owner wants
those grouped.

## Goal
ONE source of truth for DTT input sensitivity (aim turn rate, look/strafe sensitivity, aim-assist radius,
any reticle speed), so it's consistent across normal play AND story beats, and tunable in one spot.

## Plan
1. **Diagnose (read-only first):** grep DTT input/aim/strafe code for every place sensitivity is set or
   changed — `PatriciaLightController` (TickHeroStrafe / aim), `TowerAimSystem` (`_assistRadiusPx`, reticle
   speed), `LeanTouchAimDriver`, and any story/cinematic/Yarn sequence that mutates sensitivity mid-DTT.
   List each site (file:line) + what it sets.
2. **Group:** introduce a single sensitivity config (a serialized struct / constants block on the DTT
   controller or a small `DttInputSettings`) and route all the scattered sites to read from it. Story beats
   that need a temporary change push/pop a value through that one config (not their own ad-hoc field), so it
   always restores cleanly.
3. (Optional later) expose it in the settings/pause menu so the player can tune aim sensitivity.

## Notes
- FELT — owner playtests after. Pairs with the DTT cluster (317/318/320, just fixed) and the WO-318 aim work.
- Local WO; next free 334.
