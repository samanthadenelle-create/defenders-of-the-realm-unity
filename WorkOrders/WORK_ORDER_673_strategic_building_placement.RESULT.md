# WO-673 RESULT — Strategic building placement (player-defined map) (DONE; flag since removed)

**Committed:** the 07-11/12 arc (save v30 `strategicPlacementMigrated`, placement/move/rotate,
Build → Town/Defenses/Walls taxonomy, 45° stepped rotation, all 7 creative-review decisions).
Owner-ratified pivot doc: `docs/WO673_ARCHITECTURE_REVIEW.md` + `WO673_CREATIVE_REVIEW.md`;
memory `player-defined-map-pivot`. RESULT written retroactively 2026-07-13 during the sync handoff.

- Shipped behind `ff.strategicplacement` (default OFF for the felt-pass cohort) with the v30
  one-shot migration converting auto-placed structures to movable records (marker-latched).
- Touch placement chain device-confirmed on the 07-12 preview (Armed → tap → PlaceConfirm →
  Place() → under-construction).
- **Superseded state 2026-07-13 (WO-695, ex-682):** the flag is REMOVED — strategic placement is
  locked ON, new game = blank template + grace-default Forge record (FTUE guard). WO-673's
  "default OFF until felt-pass" and "new game inherits the pre-laid town" language is bannered
  in the spec. Follow-ups live in WO-674 (walls) / WO-696 (repair-context verb).
