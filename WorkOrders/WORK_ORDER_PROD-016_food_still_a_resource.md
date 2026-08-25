# PROD-016 — Food is still a world node and an Echo job (pre food→stone)

**Status:** READY — **duplicate-of-record for WO-1163; ⛔ do NOT assign this ticket separately.** It is not independently implementable: every surface below lives in WO-1163's economy/data change, so handing this out puts a second seat in the same files. It exists only so the food→stone observations stop being re-reported. **Closes when WO-1163 lands and the owner verifies.** **Silo:** Economy.
*(Board note 2026-08-24: bucket unchanged (Ready); the banner now says "do not assign" so no seat takes it as parallel work. Also corrected in §"surfaces" below — `silo`'s display name is **"Stoneyard"** (one word), per the later authoritative ruling in WO-1163 §6.1; this file carried the superseded two-word "Stone Yard" from §4b. A duplicate-of-record that preserves a superseded fact is worse than no duplicate.)*
**Reported:** owner, 2026-08-24 — *"food nodes exist"* · *"you can assign echo to harvest food"* · *"food is still option instead of stone"*.

## Not a defect — unimplemented work, filed so it stops being re-reported

The food→stone pivot is **specced (WO-1163) and not built**. Everything observed is the correct pre-pivot behaviour, including "Gathering Food +2%" (which matches `baseContributionPerEcho = 0.02` exactly, with no match bonus because Corvin favours Gold — the balance data rendering honestly).

## ⚠ The surfaces that must move TOGETHER when it lands

Filed here because the **count** is the reason this is not a quick change:

1. Echo harvest picker (the Food option, and its "NEEDS: Farm" gate)
2. World harvest nodes
3. `collector_farm` → **Quarry**, `silo` → **Stoneyard** (one word — WO-1163 §6.1, 2026-08-24, supersedes the two-word "Stone Yard"; display names only — ⛔ **ids are frozen save keys**)
4. Echo affinity copy (Corvin "Favors: Gold", Aldwin food)
5. ⛔ **Pack copy — and this one is the money path.** `impulse-food-medium` is **LIVE ON THE SHELF**, and its copy is grain fiction: *"Basket of Grain… the Folk eat tonight"*, *"Grain Cart"*, *"Harvest Wagon… the season's yield"*. At rename it becomes **a card that sells grain and delivers stone**. WO-1165 §7 is explicit: rename the copy in the SAME change.
6. Building costs authored in food — `building-tiers.json` carries `costFood` values in the thousands

⚠ WO-1165 §7 also notes the value proposition genuinely **weakens**: food's real sink was troops (~122k, re-spent every raid), and that migrates to gold. Stone inherits only one-time L2 building tiers. Post-rename those SKUs are the weakest on the shelf unless a siege/rebuild drain lands first.

## Acceptance

- [ ] Owner schedules WO-1163; this ticket closes with it
