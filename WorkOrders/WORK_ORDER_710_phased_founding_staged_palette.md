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

## THE LAW (owner, 2026-07-13 — the rule every case below instantiates)
**"As it becomes viable, we add it to the visible ones."** A building enters the palette at
the moment the game can honestly answer *why would I build this?* — need creates the option,
never a list. Every future building gets its viability trigger AT MINT TIME (a signal id in
its WO) instead of a bespoke ruling later.

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
   - **SHOPS (Forge/Armorer/Arcane Tower/Store)** reveal AFTER the teaching wave is
     won — Grok phase 4, "upgrades feel like rewards for surviving, not chores at the start."
   - **JEWELER is discovery-gated (owner, 2026-07-13): it unlocks when the player FINDS their
     first gem-stone** — the loot asks the question, the building answers it. Signal = first
     gem/crystal-class pickup (`item.acquired:<gem-class>` one-shot adapter); until then the
     Jeweler card stays locked with the carrot "Found a strange stone? Someone could set it."
     (Replaces the old static unlock-gate lockedIds entry for jeweler as the PLAYER-FACING
     gate; the catalog row is untouched.)
   - **BARRACKS is milestone-gated the same way (owner, 2026-07-13): it reveals when
     AUTOMATED DEFENDING unlocks** — the troop-training building appears exactly when troops
     become a concept (the army/auto-defense feature arriving, `ff.barracks` flipping ON /
     the ArmyStorage-deploy loop going live). Until then it never shows; carrot on unlock:
     "Your walls can fight without you now - train who mans them."
   - **ARENA/COLOSSEUM the same (owner, 2026-07-13):** the colosseum structure + arena entry
     reveal only when the arena feature itself unlocks (`ff.arena`/`ff.colosseum` flipping ON
     — the WO-703 default-OFF gates become the milestone: feature lands, building appears).
   Post-Onboarded: everything visible forever — EXCEPT the milestone-gated trio: Jeweler
   (first-stone discovery), Barracks (automated defending), Arena/Colosseum (arena feature).
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
