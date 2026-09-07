# WO-1503: the hero's melee damages its OWN hub root during a town wave

**Status:** READY TO IMPLEMENT
**Silo:** `CombatFactionRules` (new on this branch) + the hub root's faction binding.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1503 -> 1504 in the same edit).

## 1. EVIDENCE

Device log, 2026-09-06, eleven hits (7 x 65.6 and 4 x 82.0):

```
13:26:33.553 [Flow:Combat] hero MELEE hit 'CastleHubRoot' faction=Hostile dealtByHero=True amount=65.6
```

immediately after:

```
[Flow:HUD] ... inVillage=True ... scene='Main_Castle_Overworld' -> Battle
```

`CombatFactionRules`, added on this branch, classifies the hub root as **Hostile**. So during a town wave the
player's own primary attack chews through the root of their own castle - the object the whole defence exists
to protect - and the trace records it plainly.

## 2. FIX SHAPE

- The hub root must answer **Friendly** through the ONE faction authority (`CombatFactionRules`). Fix the
  classification there, not by excluding the hub root at each damage call site.
- Regression: a hero strike aimed at the hub root REFUSES, with the faction rule as the reason.
- Audit the rest of the town's owned structures through the same rule while in it; if the hub root is
  misclassified, siblings may be too - state the result either way.

## 3. WHAT NOT TO DO
- Do not add a name check for `CastleHubRoot` in the melee path. A second authority on faction is how this
  happened.

## 4. ACCEPTANCE
- [ ] `CombatFactionRules` returns Friendly for the hub root; file:line in the RESULT.
- [ ] Zero `hero MELEE hit 'CastleHubRoot'` lines in a full town-wave session.
- [ ] Regression: hero strike on any player-owned structure refuses; RED proof stated.
- [ ] `REGRESSION_OK n/n` on a fresh log.
