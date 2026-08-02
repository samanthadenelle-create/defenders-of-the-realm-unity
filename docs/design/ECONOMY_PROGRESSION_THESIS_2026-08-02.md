# Economy & Progression Thesis — owner statement, 2026-08-02

**Status:** OWNER DESIGN PILLAR. Not a proposal — this is the intent the economy is tuned against.
Captured verbatim during live felt-testing while tuning the Echo harvest bonus.

> *"the idea is after you have all them working your town should be safe and upgraded for enough
> natural protection you can do raids and use resources to attack others and then eventually
> create own bases"*

---

## 1. The four gates

```
  Echoes working   ->   town safe + upgraded   ->   raid / attack others   ->   build own bases
   (income opens)        (defense SINK)              (offense SINK)              (expansion SINK)
```

The Echo roster is the **income engine**. Completing it is meant to be the moment the town stops
being a survival problem and becomes a *base of operations*. Passive income then funds outward
play — raids, attacking others — and finally the player's own constructed bases.

## 2. What this settles about Echo tuning

The owner asked (same session) that the bonus be *"enough to make meaningful but not enough to
never need resources"*, and separately ruled the per-Echo readout down from **+55% to +5%**
(`echoes-balance.json`: base 0.02 + match 0.03; `perLevelBonus` 0.05 -> 0.01).

Measured aggregate at a FULL 6-Echo roster, all matched (`EchoBonusCalculator.AggregateHarvestMultiplier`,
`= count * (1 + specSum)`):

| | Echo Lv 1 | Echo Lv 8 (max) |
|---|---|---|
| before the ruling | 30.3x | 42.9x |
| **after** | **12.3x** | **14.8x** |

**THE THESIS SAYS 12–15x AT FULL ROSTER IS NOT A BUG — IT IS THE MILESTONE.** A self-sufficient
town is the *stated goal* of completing the roster. So the economy is healthy or unhealthy based
on the **SINK curve**, not the bonus:

> **Health rule: income growth must be matched by a sink unlocking at the same gate.**
> The failure mode the owner fears ("never need resources") arrives from the SINK side —
> resources piling with nothing worth buying — not from the bonus being large.

Two structural facts worth keeping in view while tuning:
- **Owning dominates levelling.** `count * (1 + ...)` means the 6th Echo multiplies everything;
  levelling all six 1 -> 8 only moves 12.3x -> 14.8x. The power spike is RECRUITMENT.
  Any pacing work should target *when the roster completes*, not the level curve.
- **Team composition currently dominates individual assignment.** Pair synergies (0.10 x3) +
  six-set (0.20) + hidden tri (0.25) = **0.75**, versus **0.30** for all six Echoes' individual
  contributions combined. That may be desirable (it rewards completing the set, which is exactly
  the milestone above) — flagged to the owner, not yet ruled.

## 3. The gap this exposes

| Gate | Sink | State |
|---|---|---|
| Town safe + upgraded | building upgrades, walls/towers, stockpile caps (WO-837) | EXISTS |
| Raid / attack others | troop training + raid loop | EXISTS (WO-774 polish + the WO-774.0 spectator ruling still open) |
| **Build own bases** | **the terminal, unbounded sink** | **DOES NOT EXIST** |

**"Create own bases" is not built.** It is the sink meant to absorb a mature economy indefinitely.
Until it exists, a full-roster player eventually accumulates with nothing left to buy — producing
the exact "never need resources" feeling, from the far end of the curve.

**Consequence for tuning:** do NOT nerf the Echo multiplier to compensate for a missing sink.
That would slow the early game without touching the endgame problem. Fix the sink.

## 4. Open (owner)

- Should the synergy/set block scale down with the per-Echo rescale, or is "completing the set is
  the real prize" the intent? (§2)
- Scope "create own bases": forward outposts? player-authored raid targets other players attack?
  a second town? This is the terminal sink and deserves its own program WO.
- Does the count spine (`count * (...)`, WO-709 "count-quadratic") stay as-is? It is the reason
  no percentage tuning can bring a full roster under 6x.
