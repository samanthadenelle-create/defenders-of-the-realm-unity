# WORK ORDER 710 — Phased founding: staged palette reveal (the "chunked tutorial")

**Status: READY TO IMPLEMENT** (owner + Grok brainstorm 2026-07-13 evening; owner chased it:
"what happened to chunking the tutorial" — minted from banner 710, bumped to 711 same edit).
**Lane:** BuildMode/Tutorial. **Depends:** WO-702 (founding beats, SHIPPED) · the 2026-07-13
economy rulings (free first builds, zero seed, level-1 produces — SHIPPED) · WO-709 (echo
multiplier, seam SHIPPED).

## What already shipped of the brainstorm (do NOT rebuild)
- Beat order Production-before-Storage-before-Defense = WO-702's founding chain (Hollow ->
  farm/lumbermill free-build -> stores lesson on the Lumberyard -> defense -> teaching wave).
- Town tab as the first-open/tutorial default; Defenses default post-Onboarded; Walls tab
  flagged off (`ff.wallstab`, settlement building/WO-708) — landed 2026-07-13 evening.
- "Free model": first build of each id FREE (v32 flags) replaced the seed — the phased feel's
  economic half.

## What THIS WO ships — progressive palette disclosure
1. **Category reveal rides the founding beats** (data-driven off the SAME SeenTutorials
   `tutorial_v2:<step>` keys — no new state). The owner-confirmed order — "start with a few,
   then lead into GATHERING, then into STORAGE":
   - **Beat 1-2 (greet/hollow):** the Town tray shows ONLY the Echo Hollow card ("a few").
   - **GATHERING:** the producers band reveals next — Farm + Lumbermill (income starts,
     level-1 produces).
   - **STORAGE:** the containers band (Lumberyard/Foundry/Silo) reveals at the
     founding_stores beat — taught by the surplus, not by a list.
   - **DEFENSES tab** appears at founding_defense.
   - **SHOPS (Forge/Armorer/Arcane Tower/Jeweler/Store)** reveal AFTER the teaching wave is
     won — Grok phase 4, "upgrades feel like rewards for surviving, not chores at the start."
   Post-Onboarded: everything visible forever.
2. **SELECTABLE = the current chunk ONLY (owner, verbatim: "if we group the tutorial into
   chunks only those options should be under the build - at least as selectable").** During
   the founding, a card outside the active chunk is NEVER armable: it renders as a
   dashed/locked plate with the carrot text (the workforce-panel grammar: "Wakes with your
   first harvest" / "Survive the first wave") and refuses the tap with the standard toast.
   Word carries meaning, never color alone. Whether out-of-chunk cards are visible-locked or
   fully hidden is the implementer's layout call per band — but selectable is BINDING.
3. **Surplus-triggered storage line (the brainstorm's best beat):** when banked wood crosses a
   soft threshold (~150) during the free-build beat, Sylas volunteers the storage line early
   ("We're making more than we can carry - time for proper storage") — a contextual one-shot
   row (`ctx_` pattern) gated on a new `resource.threshold:wood:150` signal (one adapter read
   at the EconomyService grant choke point).
4. **Upgrades-as-reward:** the building UPGRADE panel affordance (not the buildings) stays
   quiet until the teaching wave is won (Onboarded) — first upgrade prompt lands as part of
   the victory beat ("now make them stronger").

## Beat-order reconciliation (implementer note)
The SHIPPED WO-702 chain runs greet(10) -> hollow(20) -> stores/Lumberyard(30) -> free-build
town(35) -> echo(37) -> defense(40) -> wave(45). The owner-confirmed chunk order (few ->
GATHERING -> STORAGE) means the guided-placement sequence becomes: hollow -> a GATHERING beat
(guided Farm + Lumbermill placement, income visibly starts) -> THEN the stores lesson +
containers. Implementation = a small tutorial-steps.json reorder/split (data-only: move
founding_stores after a new founding_gathering row; the strategic-warning copy stays verbatim
on the stores beat). Keep both JSON copies byte-equal; the founding fleet probe's beat
sequence updates to match.

## Gates
- [ ] Fleet founding probe extended: palette card counts per beat (greet=1 card, town=full
      Town band, stores=+containers, defense=tab appears); skip path reveals everything.
- [ ] No re-lock on reload mid-founding (SeenTutorials keys are the truth); migrated saves see
      everything (Onboarded=true).
- [ ] COMPILE_GATE_OK + DataRegression baseline + owner felt-pass of the reveal cadence.

## What NOT to touch
Tab taxonomy (Town/Defenses ruled) · the free-build flags · founding beat ORDER (WO-702) ·
lockedIds semantics in build-categories.json (the reveal is a runtime layer on top).

*Cross-refs:* Grok brainstorm paste 2026-07-13 · WO-702 · WO-707 (economy rulings section) ·
WO-709 · `ui-blink-template-master-frame-formula` (locked-plate grammar).
