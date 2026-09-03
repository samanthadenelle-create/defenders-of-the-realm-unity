# WORK ORDER 1288 - Make the first session feel alive

**Status:** FIXED — implemented 2026-08-31; evidence in this file's Result section: `STARTER_SETTLEMENT_PROOF_OK` (`Builds/starter-settlement-proof-r4.log`), `Builds/starter-settlement-proof.png`, `COMPILE_GATE_OK` and a fresh post-proof `REGRESSION_OK`. Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02: the line carried no canonical marker and read as Unlabeled.)* *(Prior line:)* Status: DONE
Source: `new-player-onboarding-work-order.md`, owner direction 2026-08-31

## Product decision

The recommended first-time path opens on an established, editable settlement.
`Start from Scratch (Blank Canvas)` remains visible as the deliberate secondary path.
This supersedes the temporary 2026-08-23 default-off ruling, whose reason was stale
building identities; the current catalog and WO-1250 identity corrections are now the authority.

## Architecture

Do not create or maintain another Unity scene. Use two idempotent passes:

1. Existing `StrategicPlacementMigration` loads and adopts the proven baked storefront town.
2. `StarterSettlementCompletion` adds missing current-economy structures and defenses through
   `BaseLayoutLoader.Spawn`, making every addition movable, upgradeable, damageable, and persisted.

The second pass is gated by `SeenTutorials[founding.default_town_selected]` and seals itself with
`SeenTutorials[founding.starter_settlement_v1]`. It never touches existing or scratch-path saves.

## Starter completion template

- Crafting Station
- Iron Mine
- Lumberyard
- Foundry
- Stoneyard
- Four Archer Towers, one covering each cardinal gate

The established scene already supplies the Weaponsmith, Lumber Mill, Quarry, Echo Hollow,
Armorer, Cathedral of Magic, Store, and Jeweler ring. Barracks remains governed by its existing
post-founding unlock/adoption rule instead of bypassing progression.

Preferred cells are authored, but the service checks the real footprint occupancy and searches
outward up to six cells before refusing. A refusal is fail-loud and reported in telemetry.

## First 2-5 minute sequence

1. Select `ENTER ELARION (Recommended)` after hero/login setup.
2. Arrive in a populated town with useful buildings and four visible defensive anchors.
3. Echo guide identifies the Heart/gate situation and leads the player to one understandable action.
4. Player starts the small tutorial attack; the inherited towers visibly contribute.
5. Win feedback, resource payout, and the next gentle Build nudge land immediately.

The blank-canvas path retains the existing build-first tutorial and all creative freedom.

## Measurement

- `founding_path_selected { path, recommended }`
- `starter_settlement_ready { added, existing, failed, total }`
- Existing `tutorial_started`, `tutorial_completed`, `wave_completed`, and `session_start`
  provide the action funnel/session-duration joins.
- Compare first meaningful action completion, tutorial completion, median first-session duration,
  and D1 return by `founding_path_selected.path` cohort.

## Acceptance

- Recommended founding is default-on and visually primary.
- Scratch start remains available and is never modified by the completion service.
- Default founding produces the established ring, five completion buildings, and four Archer Towers.
- Reruns/reloads add no duplicates.
- Missing catalog rows or seats fail loudly and appear in `starter_settlement_ready.failed`.
- Compile and registered regression are green.
- PC fresh-save-equivalent proof passes with five completion buildings and exactly four Archer
  Towers. The central spawn/navigation lane remains unobstructed and defenses occupy the four
  perimeter approaches. The proof uses an isolated throwaway state and restores the player's
  original save before exit.

## Result evidence

- `Builds/starter-settlement-proof-r4.log` — `STARTER_SETTLEMENT_PROOF_OK`, layout assertions,
  synchronous 1920x1080 camera capture, and `STARTER_SETTLEMENT_PROOF_SAVE_RESTORED`.
- `Builds/starter-settlement-proof.png` — rendered PC world proof.
- `Builds/data-regression-starter-settlement-postproof.log` — fresh post-proof `REGRESSION_OK`.
- `Builds/compilegate-starter-settlement.log` — `COMPILE_GATE_OK`.
