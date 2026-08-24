# WO-1183 — Season buy-in with a winner's pot

**Status:** SPEC — ⛔ **NOT READY, and deliberately so.** This needs legal review and one structural
decision before any code. **Silo:** Monetization/competitive.
**Origin:** owner, 2026-08-24: *"if we get enough traction players buy in for a season and winners
take the pot."* ⭐ **Explicitly END-STATE** — owner: *"im talking end state after we have a real foothold."* This is
**not a build order and must not gate current work.**

⚠ **Its value TODAY is knowing what NOT to build.** Keeping the store economy and any future
competitive ladder **separate from the outset costs nothing now** and avoids rewriting
`FOUNDATIONAL_RULINGS.md` §1 later, once every affected decision is already shipped and load-bearing.
That is the whole reason to write this ticket years early.

---

## ⛔ THE LOAD-BEARING WARNING: this reclassifies every monetization ruling we already made

Today's rulings are safe **because the leaderboard is cosmetic**. Nobody competes for value, so:
- "Money accelerates the path, never skips the gate" (`FOUNDATIONAL_RULINGS.md` §1) is about *feel*.
- "A shield forfeits the season" is a fair trade — you give up a badge.
- Storage Deeds, the Founder's Vow crystals, Patronage tiers — all benign.

⚠ **Attach a cash pot to standing and every one of those becomes a way to buy money.** Faster
progression → higher rank → larger payout. The Founder's Vow stops being a supporter's badge and
becomes an investment with a return. ⛔ **The store and the ladder cannot share an economy once the
ladder pays.**

⭐ **This is the single most important thing to understand before building it**, and it is invisible
until the pot exists — at which point every prior decision is already shipped and load-bearing.

## ⛔ Legal — this is a regulated category, not a feature flag

**Paid entry + prize determined by outcome** is regulated almost everywhere:
- In much of the **US**, *consideration + prize + chance* = an illegal lottery. Skill-contest
  carve-outs exist but are **state-by-state**, and several states prohibit **paid-entry skill
  contests** outright.
- **Crypto payouts add money-transmission exposure** on top of the contest question, and we settle in
  SPL tokens on a live storefront.
- ⚠ The **Solana dApp Store's own terms** must be checked before this ships — a listing violation
  costs the app, not just the feature.

⛔ **Do not build this on an engineering opinion, mine included.** It needs a real review. Everything
below assumes that review comes back workable.

## ⭐⭐ 2026-08-24 - THE OWNER'S ACTUAL DESIGN, and it is better than what I assumed

Owner, in three parts:
> *"once we have the arena actually up and working where people can engage with each other and we
> have enough of a player base, it then could be considered as they can enter the arena."*
> *"all of that is based on the premise that they're not using the castle. It'll all have to be their
> own hand designs that they've built and paid for."*
> *"at that point, now we're talking about fully authored bases formed by the very people that are
> fighting to be the best."*

⭐ **This is a DESIGN COMPETITION, not a wealth ladder** - and it dissolves most of my objection. The
arena base is **authored by the competitor**, separate from the persistent castle, and the thing being
judged is **the base they built**. The base *is* the play.

⚠ **Three preconditions the owner set, in order** - none skippable:
1. The **arena actually works** and players engage each other.
2. There is **enough of a player base** for a ladder to mean anything.
3. **Only then** is paid entry *considered*.

⛔ **The persistent castle is explicitly NOT the arena base.** That is the single most important
sentence in this ticket, because it is what keeps the store out of the ladder.

### ⛔ AND HERE IS THE ONE DISTINCTION THAT DECIDES EVERYTHING - it hides inside "built and paid for"

**Base DESIGN is skill. Base CONTENTS are spend.**

- ⭐ **Fixed, EQUAL palette** - every entrant gets the same pieces and only the *arrangement* differs.
  Money buys **nothing**. The pot rewards pure design skill, `FOUNDATIONAL_RULINGS.md` §1 survives
  untouched, and the competition is defensible to a player, a reviewer, or a regulator.
- ⚠ **Drawn from the player's FUNDED progression** - better towers because they bought further up the
  tree. ⛔ Then the pot is **a return on spend laundered through a layout screen**, and it is the
  thing §1 exists to prevent, wearing a design-competition costume.

**RECOMMEND the equal palette.** It is also the better *game*: Clash's war-base metagame is
interesting precisely because the constraint is shared, and a layout beats a bigger wallet.

⚠ **"Paid for" needs one word of clarification from the owner** - paid in *in-game resources* (fine
if the palette is equal) or in *real money* (⛔ then the palette is not equal and the pot rewards
spend).

## The earlier framing (superseded in part by the above)

### The structural decision: rank on what money CANNOT buy

If the season ranks the town the player **funded**, the pot is a **rebate on spending** and the
best-funded player wins by construction — which is both the legal worst case (it looks like a return
on stake) and the product worst case (nobody else enters twice).

**RECOMMENDED: a separate seasonal mode with NORMALIZED starting conditions.** Everyone begins the
season equal; the ladder measures play, not purchase history. Then:
- The store keeps selling convenience, permanence and cosmetics — **none of which touch the ladder**.
- ⭐ `FOUNDATIONAL_RULINGS.md` §1 survives **unchanged**, because there is no path from spend to rank.
- The shield/leaderboard trade (§3) survives too, and gets simpler: the seasonal mode has no offline
  town to shield.

⚠ **The alternative — ranking the persistent town — requires abandoning or rewriting §1**, and should
only be considered with that stated out loud.

## Open questions before this can leave SPEC

1. **Legal review outcome**, per jurisdiction, plus the dApp Store terms.
2. ⭐ **LARGELY ANSWERED 2026-08-24 - the paid season is OPT-IN** (owner: *"and only if they opt in
   for it"*). That is close to deciding the structural question by itself: **an opt-in track is a
   separate mode by definition.** Players who never opt in play the game exactly as today, touch no
   pot, forfeit nothing, and are ranked by nothing.

   ⭐ Opt-in also **fixes the legal posture in the right direction**: entry becomes an affirmative,
   informed act rather than a condition of playing, which is the distinction most contest rules turn
   on. And it removes the worst product outcome - a paying player being outranked by a bigger spender
   they never agreed to compete against.

   ⚠ **Still to rule:** whether the opted-in season starts from **normalized conditions** or from the
   player's **funded town**. Opt-in makes it *consensual*; it does not by itself make it *fair*. ⛔ If
   it ranks the funded town, `FOUNDATIONAL_RULINGS.md` §1 must be rewritten in the same change - a
   consensual pay-to-win ladder is still a pay-to-win ladder.
3. **Where does the pot sit between entry and payout?** ⚠ Holding player funds is custody, and custody
   is its own regulatory question. The Squads 2-of-3 treasury is not automatically an answer.
4. ⭐ **RULED 2026-08-24: under the minimum, entries are REFUNDED.** Not rolled over, not topped up
   from the treasury. ⚠ Topping up would mean **we** are staking the pot, which is a materially
   different legal posture than holding player entries — and a rollover leaves us holding funds across
   a boundary, which is custody. Refunding is the cleanest of the three and the easiest to explain.
   ⛔ The minimum must be **published before entries open**, or the refund rule is unverifiable by the
   player.
5. **Season length** — 30 days is already ruled for the cosmetic ladder
   (`FOUNDATIONAL_RULINGS.md` §3). Does a paid season inherit it?

## Acceptance (provisional — do not implement)

- [ ] Legal review complete and recorded, with jurisdictions named
- [ ] The structural decision is ruled and `FOUNDATIONAL_RULINGS.md` is updated to match — ⛔ if the
      persistent town ranks, §1 must be rewritten in the same change, not left contradicting
- [ ] No path exists from any purchasable item to seasonal standing — asserted by a regression, the
      way `battle_monthly.json`'s zero-combat-power gate already asserts its own separation
- [ ] Entry, pot custody and payout are auditable end to end
