# WO-1503: the hero melee trace named the HIERARCHY ROOT, not the target; the inline faction copy retired

**Status:** IMPLEMENTED - the reported defect DID NOT EXIST; the trace did. Severity corrected P0 -> **P2**.
**Silo:** `PlayerAttackController` + `CombatFactionRules` + the structure sweep suite.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1503 -> 1504 in the same edit). **RESCOPED the same day** after the
implementation lane disproved the premise at source.

## 1. EVIDENCE

### 1A. What was reported (P0, and WRONG)

Device log, 2026-09-06, eleven hits (7 x 65.6 and 4 x 82.0):

```
13:26:33.553 [Flow:Combat] hero MELEE hit 'CastleHubRoot' faction=Hostile dealtByHero=True amount=65.6
```

Read as: the hero is destroying its own castle during a town wave.

### 1B. What is actually true (proven at source by the lane)

- **`CastleHubRoot` carries only a `Transform`** - no collider, no `IDamageableStructure`. Nothing can hit it.
- Every hub enemy's `transform.root.name` IS `"CastleHubRoot"`, because `WaveManager`'s enemy root is a CHILD
  of it.
- The trace printed the **hierarchy root**, not the thing that took damage.

So the eleven lines were **correct kills of hostile enemies**, logged under a misleading name. There was no
faction misclassification and no self-damage. The `faction=Hostile` in the line was right all along.

**This is the shape of my own error worth recording:** a real measurement was used to support a conclusion it
did not support - `transform.root.name` is not the target's name, and I did not check which one the trace
printed before calling it a P0.

## 2. WHAT THE LANE SHIPPED

- `PlayerAttackController.cs:689` routed through `CombatFactionRules.MayAttack` - one faction authority on the
  melee path, which is the right end state regardless of the false alarm.
- The trace fixed to name **target + type + attacker faction**, so it can never again read as a hit on
  something that cannot be hit.
- Cases **H / I / J** added to the structure sweep.

## 3. WHAT NOT TO DO
- Do not "fix" `CastleHubRoot`'s faction. It has no collider and no damageable component; there is nothing to
  classify.
- Do not revert the trace change because the bug was not real. The unreadable trace is what cost the P0.

## 4. ACCEPTANCE (met)
- [x] Premise disproven at source, with the component list that proves it.
- [x] `PlayerAttackController.cs:689` goes through `CombatFactionRules.MayAttack`.
- [x] The trace names target + type + attacker faction.
- [x] Structure sweep cases H / I / J.

## 5. CARRIED FORWARD
The lane found **13 remaining inline faction comparisons** that bypass `CombatFactionRules`. Those are
**WO-1524**, not this ticket.
