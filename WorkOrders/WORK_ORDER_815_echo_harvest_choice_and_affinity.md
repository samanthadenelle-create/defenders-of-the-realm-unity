# WORK ORDER 815 — Echo harvest resource CHOICE + per-Echo resource AFFINITY

**Status: SUPERSEDED by WO-830** (echo harvest affinity + synergy, owner-approved 2026-08-01). Retained as origin ruling only.
**Origin (owner, verbatim):** "there should be options for which resource the echos harvest
and each can have an affinty with a certain type to promote using them for that +5% food maybe"
**Lane:** Echoes / Harvest (builds on WO-784 echo-lane consumers + WO-811 gather-or-repair)
**Related canon:** memory `echo-lane-design-rulings` (claim-loop first; teaching conversation
per unlock), `echo-is-essence-of-guarded-person` (card copy law).

## The ruling

1. **Choice:** an Echo assigned to Harvest can be pointed at a RESOURCE (wood / food / iron /
   crystals — whatever the harvest lane already yields), not locked to one stream.
2. **Affinity:** each Echo carries an authored affinity for ONE resource type; harvesting its
   affine resource earns a bonus (~+5%, retunable). Promotes matching the person to the work —
   soft optimization, never a hard gate (any Echo can work any resource).

## Design sketch (for the spec pass)

- Data: `echo-roster` entries gain `affinity: "food"` (authored per soul — fits their story:
  e.g. the miller's essence loves the fields). Bonus constant in data, not code (+0.05 start).
- State: per-echo `assignedResource` beside the existing lane assignment (GameState echo block;
  additive default-on-read — absent = today's behavior).
- Rate: `EchoService.RatePerSecond` (or the WO-784 Core contract seam — do NOT bypass it again)
  multiplies by `1 + bonus` when assignedResource == affinity.
- UI: the Echo card / workforce panel gains a resource picker row (kit-built; affinity resource
  marked with a text chip "Affinity +5%" — never colour-only) + card copy hint per
  `echo-is-essence-of-guarded-person`.
- Teaching: the unlock conversation mentions the soul's affinity (memory: a teaching
  conversation at every Echo unlock).

## Acceptance (sketch)

- [ ] Assign an Echo to each resource type; rates change accordingly (headless data check).
- [ ] Affine assignment yields exactly the authored bonus; oracle pins the multiplier.
- [ ] Old saves load with no assignment = today's rates (no migration needed).
- [ ] Screenshot: picker row + affinity chip on the card at both capture resolutions.

## Do NOT

- Rebuild the lane system (WO-784 owns the consumer wiring; this rides its seam).
- Hard-gate resources behind affinity (owner: promote, not require).
