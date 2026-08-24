# Foundational rulings — the law that outlives the ticket that produced it

**Owner, 2026-08-24.** Three rulings elevated deliberately above the tickets they came from, because
each one answers a *class* of future question. When a work order and this file disagree, **this file
wins** and the work order is wrong.

⚠ **Why this file exists at all:** this repo's dominant failure mode is a fact recorded in a second
place going stale — a retired dependency table, a hardcoded repo root, a stale WO-number block, eight
hardcoded level ceilings. ⛔ **So do NOT restate these rulings inside individual tickets.** Cite this
file by name. A ticket that paraphrases one of these will drift from it, and the paraphrase will be
believed.

---

## 1. ⛔ PROGRESSION GATES CANNOT BE PURCHASED. Money accelerates the path; it never deletes it.

> *"Founder's Vow may accelerate the player toward gated permanent upgrades, but may not bypass their
> progression requirements."*

**The case that produced it:** the Founder's Vow proposed granting a builder/queue slot outright. That
slot is **Echo-gated** by the WO-911 Q6 ruling — *each Echo above 2 unlocks the RIGHT to buy, crystals
complete it._ A Vow granting the slot punches straight through the gate. Ruled: the Vow grants
**crystals toward** the slot.

**How to apply.** Before authoring any paid grant, ask: *does a non-paying player reach this by
playing?*
- **Yes, eventually** → a purchase may **shorten** the path. Legitimate.
- **No, this IS the gate** → ⛔ the purchase may not grant it. Sell the currency that completes it, or
  do not sell it.

⚠ **The tell is the copy.** If the marketing sentence is *"skip"*, *"unlock instantly"*, or *"no need
to"*, the rule is being broken. If it is *"sooner"*, *"faster"*, or *"toward"*, it is being kept.

⭐ We are **LIVE on the Solana dApp Store**. This is not a philosophical position — it is the thing a
player points at in a review.

---

## 2. PAID PERMANENCE SHOULD BE VISIBLE. If the kingdom took your money, the kingdom should remember.

> *"If you sell storage, patronage, Founder status, etc., the kingdom should visibly remember it."*

**The case:** Storehouse Deeds were ruled a **percentage multiplier** rather than a fourth physical
container — correct engineering, because a new container touches placement, `BaseLayout` and the
singleton rules on a live save schema. ⚠ But a multiplier is **invisible**, and a permanent purchase
that cannot be seen does not feel permanent.

⭐ **The resolution is to separate MECHANICS from VISUALS**, and it generalises:
- **Mechanic** — the invisible, save-safe change (a multiplier, a cap, a rate).
- **Visual** — cosmetic evolution of what is **already placed**: upgraded props, extra crates and
  carts, reinforced doors, banners, a larger yard.

⛔ **No new placeable object is required, and that is the point** — the estate visibly grows while
placement, `BaseLayout` and the singleton rules are never touched.

### ⛔ And the Heart of Elarion is not a sponsor surface

> *"That protects your most important world object from becoming a NASCAR hood covered in sponsor
> names."*

The $500 Patron Monument stands **NEAR** the Heart. It does **not** alter it. ⚠ Applies to every
future paid or prestige cosmetic: **the Heart is world canon, not inventory.** The village centre
(0,0,0) is the one object no purchase may write on.

---

## 3. OFFLINE LOSS CREATES REPAIR, NEVER IRREVERSIBLE PUNISHMENT.

> *"...without making somebody log back in Tuesday morning and discover that Saturday's $40 purchase
> was eaten by goblins."*

**The case:** roaming troops may attack an offline town — they must, or the 48-hour shield protects
nothing and should not be sold.

**A gate falling costs:** the gate is damaged · defensive capacity drops until repaired · the player
pays wood/stone/iron · the repair takes time · **possibly** a small, **bounded** theft of **stored
basic resources**.

⛔ **NEVER, while offline or otherwise:** destroyed premium items · lost cosmetics · lost crystals ·
permanent building deletion · a troop wipe.

### ⚠ The line inside this ruling, and it is thin

Offline theft plus a shield sold to prevent it is, structurally, **selling the cure for a disease we
added**. It is legitimate here for one reason only: **theft exists so raids have stakes, and the
shield is a convenience for players who travel.**

⛔ **Theft rates may NEVER be tuned upward to move shield sales.** If that trade is ever proposed —
*"raise the steal a little, shield conversion is soft"* — **that proposal is the tell that the line
has been crossed**, and the answer is no. Write the reason down when it happens; the next person will
not remember why it was obvious.

### The shield is FIXED-DURATION, and the use case defines it

> *"for shield we limit to a fixed duration"* · *"designed as I'm out for X time but am close to
> getting what I need saved"*

⭐ **That framing is what keeps ruling 3 on the right side of its own line.** The product is not
"immunity"; it is **"I am away for a known stretch, and I am close to banking something I do not want
to lose."** Time-boxed protection for a player who is travelling - not a permanent safety net.

Design consequences that follow from the use case, not from monetization:

- **Fixed duration, stated up front.** The player buys a KNOWN window, not a subscription to safety.
- ⭐ **CHEAP FIRST, PAINFUL AFTER** (owner, 2026-08-24: *"either that or make the cost cheap then as
  added painful"*). **This supersedes the hard non-stacking rule I first proposed**, and it is better:
  a hard refusal punishes the legitimate case - someone genuinely away two weeks hits a wall - while
  an escalating price lets them cover it and makes permanent immunity progressively unaffordable.
  The traveller pays little; the player buying immunity pays steeply more each time. **It self-limits
  without ever telling a paying player "no."**
- ⭐ **NO HARD CEILING. The owner overruled me and was right** (2026-08-24: *"if they really want a
  super long shield lol"* · *"if someone wants to drop real money it can fund months of development
  or maybe a UI staff"*).

  I argued for an absolute cap on protected time. ⛔ **The argument was wrong, and the reason it was
  wrong is worth keeping: THE ATTACKERS ARE NPCs, NOT OTHER PLAYERS.** A player who buys permanent
  immunity takes **nothing from anyone else** - no ranking, no resource, no queue position. That is a
  **difficulty setting they paid for**, not pay-to-win, and it is categorically different from ruling
  1's progression gate, where the purchase would skip content the player is meant to earn. A shield
  skips a threat they are meant to *endure*, and enduring it is optional by design.

- ⭐ **A SHIELD REMOVES YOU FROM THE LEADERBOARD** (owner, 2026-08-24). This is the answer to the
  competitive exception I raised, and it is better than the cap it replaces: **immunity OR standing,
  never both.** Nothing is capped, nothing is policed - **the player chooses, and the choice enforces
  itself.** A whale may buy any shield they like; what they cannot buy is a ranked position while
  protected from the risk everyone else is ranked under.

  ⭐ **RULED: the exclusion is FOR THAT SEASON, and A SEASON IS 30 DAYS** (owner, 2026-08-24). This
  supersedes the symmetric down-time rule I proposed, and it is simpler in the way that matters: it is
  **explainable in one sentence at the point of sale** - *"using a shield ends your leaderboard run
  for this season."* My version required a player to reason about elapsed uptime to know where they
  stood, and a rule you cannot state on the button is a rule players discover by being burned.

  ⚠ It is also **unexploitable by construction**: there is no window to shield through, because any
  shield at all costs the whole season. ⛔ **Any shield, any duration, forfeits the full 30 days** -
  a one-hour shield and a 30-day shield cost the same standing, which is what removes the arithmetic.

  ⛔ **This must be stated in the shield's own purchase copy, before the buy.** A player who discovers
  after paying that they left the leaderboard has been surprised by a term, and that is a refund
  request and a review, not a design detail.

  ⚠ The remaining risk is **retention, not fairness**: a player can buy their way out of the loop that
  keeps them playing, and then churn. That is worth **watching in the data**, not designing around -
  and it is the player's call to make.
- ⛔ **THE CURVE IS NOT A CONVERSION KNOB.** This ruling already fences theft rates against being
  tuned to move shield sales. An escalating price is **the second knob on the same product**, and it
  is exactly where that pressure will reappear - as *"soften the curve, conversion is soft."* ⚠ Same
  answer, same reason: the proposal is the tell. Fenced now, while it costs nothing to say.
- ⛔ **The shield DROPS when the player returns and acts.** It protects the absence, not the player.
  A shield still up while its owner is online and raiding is a different product, and a worse one.
- **It protects the in-progress accumulation** - the thing they were close to saving - which means
  what it must actually stop is the **bounded resource theft**, not the gate damage. Gate damage is
  repairable by design; the stolen stockpile is the loss that stings.

---

## Where these came from

Ten rulings on 2026-08-24 (`OWNER_RULINGS_OWED.md`). Seven answered a ticket. **These three answered a
class**, so they were pulled up here where the next ticket can find them.
