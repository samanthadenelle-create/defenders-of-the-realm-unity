# WO-1429: an out-of-mana Mage has NO attack at all and can only run

**Status:** READY TO IMPLEMENT - minted 2026-09-06 (CLI) from the owner's playtest
**Silo:** Hero combat (DeNelle.Village.Hero) - the primary-attack resolution seam
**Owner ruling (2026-09-06, verbatim):** *"if you play as the mage once you expel all of your MP you have no ability to
attack in anyway all you can do is run until you regain your mana. I think when mana is gone, it should automatically
flip to attack with the staff just a manual very low attack while it recharges back to at least 50% then it switches
back to magic."*

---

## 1. The defect

A class with no affordable action is a class that cannot play. The Mage at 0 mana can only run away, which is not a
difficulty spike - it is a **dead state with no verb**, the same species as every other defect found on 2026-09-06:
a capability that exists (the melee sweep) which the player cannot reach.

## 2. Root cause, read at source

`HeroAbilities` resolves the primary attack down ONE of two paths (`HeroAbilities.cs:463-500`):
- the class's basic is RANGED - derived, never a per-class table, from the authored def's effect shape plus
  `RangedPrimaryReachFactor = 2f` (`:479`) - so the primary CASTS the locked Q def; or
- it is not, and the primary is the class-agnostic **melee sweep**.

`TryGetRangedPrimary` (`:497+`) is *"the SINGLE decision seam: PlayerAttackController fires through it and
HeroTargetIndicator gates auto-acquire on it, so the input and the targeting can never disagree"*.

**The Knight takes the melee path. The Mage's basic is a spell, so it takes the ranged path - and that path has no
fallback when the cast cannot be PAID FOR.** The sweep is already implemented and already class-agnostic; the Mage
simply never reaches it. Nothing here needs inventing.

⚠ **VERIFY BEFORE EDITING** (this WO asserts the mechanism from a read of the resolution seam, not from a capture):
instrument or headless-run an out-of-mana Mage and confirm the primary input produces NOTHING today. CLAUDE.md section 12 -
static reading LOCATES a cause, it never CONCLUDES one. If the input in fact produces a failed-cast with a message, the
fix is a different one and this WO should be re-scoped rather than implemented as written.

## 3. The fix

**When the class basic is ranged but currently unaffordable, fall through to the melee sweep** - the path the Knight
already uses. The hero always has a verb.

### 3.1 Hysteresis is REQUIRED, and the owner has set the numbers
Switch to the staff at **0 mana**; return to magic at **>= 50%**. Without a gap the hero would flip back to spellcasting
on the first point of regenerated mana and immediately fail again - a flicker between two weapons, several times a
second, at exactly the moment the player is under pressure. The two thresholds must be **separate authored values**, not
one comparison.

### 3.2 The staff attack is DELIBERATELY WEAK
Owner: *"just a manual very low attack"*. It is a floor that keeps the player acting, not a competitive option - it must
never be the optimal Mage rotation. It is the EXISTING class-agnostic sweep; do NOT author a second melee system.

### 3.3 Derive, never hardcode the class
⛔ The existing seam is explicitly derived and its own comment forbids a per-class table: *"DERIVED, NEVER A PER-CLASS
TABLE - that is the same hand-authored-vs-derived defect class as IsLoop, Hidden, and the town that laid itself on its
side"*. **Do not add `if (class == Wizard)`.** The rule is "the resolved primary cannot be paid for", which is true for
any class whose basic has a cost, and it generalises for free.

### 3.4 The player must be able to SEE it
The bar face and any targeting affordance must show which primary is live. A silent weapon swap is the same
tell-the-player-nothing failure as the rest of that day's findings. One word on the face is enough.

## 4. Scope
**In:** the primary-attack resolution seam, the two thresholds, and the face/telegraph that names the live primary.
**Out:** mana regeneration rate, staff damage tuning, the Q ability's own cost, and any other class's kit - all balance,
and **the owner rules on balance**. Bring numbers back rather than choosing them.

## 5. Regression - `PrimaryFallbackRegression`, marker `PRIMARY_FALLBACK_OK` / `_FAIL <case>`
Each case with a one-line REVERT RECIPE; the CLI proves RED then GREEN.
1. `[no-verbless-hero]` **the case that matters**: for every playable class, at every mana value from 0 to max, the
   resolved primary is non-null. RED: restore the unconditional ranged path.
2. `[staff-at-zero]` a ranged-basic class at 0 mana resolves the MELEE sweep.
3. `[magic-at-half]` the same hero at >= 50% resolves the SPELL again.
4. `[hysteresis-gap]` the two thresholds are distinct values, and rising mana from 0 does not re-arm the spell before
   the upper one. RED: make both read the same constant - the flicker returns.
5. `[no-per-class-table]` source: the fallback contains no class-name comparison. RED: add one.
6. `[knight-unaffected]` a melee-basic class resolves exactly as it does today at every mana value.

## 6. Acceptance
- [ ] Brace + NUL on every `.cs`; `COMPILE_GATE_OK`; `REGRESSION_OK n/n` with the new suite green and all six RED
      proofs recorded.
- [ ] **Captured proof of the defect BEFORE the fix** (section 2) and of the behaviour after - a headless run or an F8
      trace showing the primary at 0 mana.
- [ ] Owner felt-test: play the Mage, empty the bar, and confirm you can still fight and that the swap is visible.

## 7. RULED: the staff swing is FREE

**Owner ruling 2026-09-06, verbatim: *"No swing Staff should have no cost only casting magic should."***

The fallback costs **nothing**. Only casting magic spends mana.

**This is what makes section 3's guarantee actually hold, and it is not merely a balance preference.** A fallback with
a cost - stamina, a cooldown, anything - can itself become unavailable, and the hero is back in the dead state this WO
exists to remove. **Free is the only cost that guarantees "the hero always has a verb" is true at every instant.**
Encode it that way: the sweep must not consult any resource pool.

Add to section 5 as case 7:
`[fallback-is-free]` the melee-sweep path spends no resource - no mana, no stamina, no charge - at any mana value.
RED: give the sweep any cost and case 1 `[no-verbless-hero]` fails with it, which is the point.

## 8. Still open for the owner
1. Staff damage - a fraction of the spell, a flat floor, or scaled off the hero's level?
2. Should the same fallback apply to any OTHER class whose basic has a cost, or is the Mage the only one today?
