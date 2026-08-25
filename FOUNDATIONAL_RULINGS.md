# Foundational rulings — the law that outlives the ticket that produced it

**Owner's rulings, 2026-08-24 onward.** Elevated deliberately above the tickets they came from,
because each one answers a *class* of future question. When a work order and this file disagree,
**this file wins** and the work order is wrong.

⚠ **Do not restate a count here.** This line used to say *"Three rulings"* and the file now carries
fourteen — the same one-fact-written-twice drift that produced the stale WO-number block and the
retired dependency table in `CLAUDE.md`. The sections below are the count.

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

⚠ **CORRECTED 2026-08-24 — I over-stated the exposure and it changed how I framed several
decisions.** Owner, verbatim: *"NOTHING IS LIVE, NOTHING IS AT SOLANA DAPP MONEY FACING"* ·
*"ONLY TEST TO ME."*

The app is **published/listed**, but **no player-facing money path is active**:
`MAINNET_SALES_ENABLED` is an env switch (`api/_lib/purchase-catalog.js:178`) and until it is true
**only the owner's own wallet can transact.** The 391 SKR settlement was **her own test purchase**.
⛔ **No player has ever spent money in this game.**

⭐ **What that changes: URGENCY, not the ruling.** SKU renames, price moves and schema changes carry
**no player-facing risk right now**, which makes this a cheap window to get them right rather than a
minefield. I had been arguing several decisions as if a live player base were exposed to them.

⭐ **What it does NOT change: the ruling itself.** These are design-integrity rules, and the reason to
hold them is that they are **far cheaper to keep than to retrofit.** Every one of them gets harder the
day sales switch on, and that day is the wrong time to discover the store and the progression system
share an economy.

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

  ⚠ **AND IT BINDS THE ANTI-CHEAT POSTURE — see WO-1128 and §6 below.** The moment leaderboard
  standing carries **material consequence**, client-authoritative combat outcomes stop being harmless
  and **combat outcome verification becomes required BEFORE those rewards launch**. This clause and
  §6 must be read together; changing either one changes the other.

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

### ⛔ NOTIFICATION COPY MUST MATCH THE BANKED-PRESSURE MODEL — it is not a style question

Away time **banks pressure**; it does not resolve combat. ⭐ **So while the player is away, NOTHING IS ATTACKING THEM.** A push notification saying *"Your town is under attack!"* is therefore **factually false**, and false urgency is the one thing this ruling cannot survive.

| ⛔ Never send | ✅ Honest, and better |
|---|---|
| "Your town is UNDER ATTACK" | "A siege is massing — it will be waiting when you return" |
| "You are LOSING resources" | "Pressure is building at the gates" |
| "Act now or lose your town" | "Your lookouts report movement" |

⚠ **AND THE HARD FENCE: a notification may NEVER be paired with a shield offer.** A push that manufactures alarm and then sells the cure is precisely the sell-the-cure-for-a-disease-we-added pattern this ruling exists to prevent — the same trade as tuning theft rates to move shields, arriving through a different door. ⛔ **If that pairing is ever proposed, the proposal is the tell.**

⭐ The honest version is also the better retention play: *"something is waiting for you"* is an invitation. *"You are being robbed right now"* is a punishment for having a life — and the player who opens the app to find nothing was actually lost learns that the notification lies. After that, none of them work.

⚠ **Scope note for the alert lane:** it may **observe** `SiegeScheduler`'s cadence and pending count. ⛔ It must never **spawn, partition, tune or resolve** — `WaveManager` is the single spawn authority, and `SiegeSpawnAuthorityRegression` fails the gate if a spawn call appears in `SiegeSession`.

---

## 4. VFX AUTHORITY IS SPLIT BY WHAT THE ACT ACTUALLY IS: repair · map · substitute.

> *"If a prefab literally says Fire Cast, mapping a fire ability to it isn't creative direction.
> It's plugging the toaster into the toaster outlet."* — owner, 2026-08-24

⚠ **This SCOPES the standing rule; it does NOT retire it.** The standing rule — *the owner tags the
key, the CLI maps it verbatim, the CLI never picks* — is intact. What was wrong was reading it as
"no VFX moves without her," which parked tickets where nothing was being chosen at all.

| Act | Whose call |
|---|---|
| **REPAIR** — restoring a prefab to what it already had (a null material slot, a neutral default after the underlying defect is fixed) | ⭐ **the lead** |
| **MAP by an EXISTING SEMANTIC NAME** — a fire ability onto the prefab that says *Fire Cast* | ⭐ **the lead** |
| **SUBSTITUTE / creatively choose** — a *new* effect picked for a hook that no existing prefab names | ⛔ **the owner** |

**How to apply.** Ask: *does the library already name the answer, or am I choosing one?* If the label
answers it, proceed and **show her a capture**. If you would be picking, it is hers.

⛔ **The three untagged boss keys in WO-874 — `Boss_AttackImpact` / `Boss_PhaseTransition` /
`Boss_Telegraph` — stay HERS.** No prefab names itself the answer there, so mapping one is a choice.
They are the worked example of the right-hand column, not an exception to it.

⚠ **The owner is red/green colourblind.** This rule deliberately never asks her to choose between two
hues — only to accept or veto a named element mapping.

---

## 5. THE LEAD MAY BUMP THE SAVE SCHEMA — but only under FOUR conditions, ALL required together.

> *"engineering room without handing them a chainsaw next to the save files."* — owner, 2026-08-24

A schema bump is the moment old builds and new builds stop agreeing. The risk was never in **adding**
a field; it is in **reinterpreting one that already exists**, which is where a player's town changes
under them. So the authority splits on exactly that line.

### ✅ The lead may bump when ALL FOUR hold — this is a conjunction, not a menu

1. **Old saves deserialize successfully.**
2. **A missing field gets a safe default.**
3. **The migration has regression coverage.**
4. **Existing field semantics do not change.**

Any one of the four failing sends it to her. The repo already has the safe pattern: v36, v37 and v38
were all additive with a read-migration, and a pre-bump save simply reads the default.

### ⛔ Still HERS, always

**Rename · removal · reinterpretation · conversion · any destructive migration.** No exceptions, and
no "it's only a rename" — a rename of a live save key is the destructive case wearing a small word.

---

## 6. CLIENT-AUTHORITATIVE COMBAT IS ACCEPTABLE ONLY WHILE STANDINGS HAVE NO MATERIAL CONSEQUENCE.

> *"The moment standing gives anything economically meaningful, combat outcome verification becomes
> required BEFORE those rewards launch."* — owner, 2026-08-24

We verify the **clock** (offline accrual is server-reconciled; a forwards-clock claim is scaled down)
and stop there. Action outcomes stay client-authoritative — a locally edited save claiming a won
battle or a loot roll cannot be caught without simulating the game server-side. That is acceptable
**today** because the opponents are NPCs and a cheater takes nothing from anyone else.

⛔ **DO NOT record this trigger as "while the leaderboard is cosmetic."** The owner deliberately
hardened the wording, and her reason is the point: the soft version lets someone later argue *"well,
technically the leaderboard isn't competitive."*

### The trigger, in her terms — ANY of these flips it

Combat outcome verification becomes **required BEFORE launch** the moment leaderboard standing confers
any of: **crystals · currency · exclusive gear · progression advantage · paid-equivalent rewards ·
valuable seasonal prizes.**

⚠ **Reads together with §3's leaderboard clause** (a shield forfeits the season). §3 makes standing a
thing players trade against; §6 fences what standing is allowed to be worth until outcomes are
verified. **Change one, revisit the other in the same change.**

---

## 7. PAID VALUE OVERFLOWS THE CAP. EARNED VALUE DOES NOT.

> *"I think if they're doing a purchase, we should not penalize them and we should allow that
> overflow with the caveat that none of their harvesters or rewards are gonna add to it until they've
> brought that under the threshold... if they're purchasing it, I don't want to hold onto the extra
> value and have to do more work. Let us do an override and allow it to overflow."*
> - owner, 2026-08-25

**The case:** a player buys a pack while a resource is at capacity. Under the earlier ruling the bank
paid what fit and discarded the rest, which is right for loot and wrong for something bought.

### The rule, split by SOURCE of the credit

| Source | Behaviour at cap |
|---|---|
| **PAID** - a purchase | **OVERFLOWS.** Credit the full purchased amount, above the cap. |
| **EARNED** - harvesters, rewards, raid loot, quest payouts | Adds **NOTHING** while that resource sits above its cap. Resumes when the player spends back under. |

**No overflow wallet, no escrow, no held value anywhere.** The owner's stated reason is that she does
not want value parked somewhere that then needs more machinery to manage. The overflow lives in the
ordinary balance, simply above the cap. An overflow store would be a second wallet with its own caps,
its own UI and its own bugs, bought to avoid a sentence.

### What this SUPERSEDES, and what it does not

It **narrows** `OWNER_RULINGS_OWED_2.md` ruling 5 (2026-08-24), which is a dated ledger and is NOT
rewritten - read it there, then read this. That ruling said a capped resource "pays what fits,
discards the overflow, and discloses exactly what was collected." **That still governs EARNED income
in full.** It no longer governs a PURCHASE.

**Crystals are unaffected either way** - they are UNCAPPED and always pay in full
(`TownBankCapacity.cs:238-242`, `:478-482`; pinned by `[no-crystal-cap]`). Do not implement a
crystal cap by implication.

**Never hardcode a resource-name list.** The capped test is `TownBankCapacity.IsCapped()`. A "stone"
written into a rule goes stale the day WO-1163 lands - that is why ruling 5 was recorded structurally.

### How to apply

Ask: *did the player PAY for this credit?* If yes, it lands in full. If no, it lands only up to the
cap, and above the cap it lands not at all. The player must be TOLD, in words, when they are above
capacity and earning nothing into that resource - a silent faucet that stopped is the
"I did the raid and got nothing" complaint wearing a new face. Never signal it by colour alone.

---

## 8. PRODUCTION PROMOTION IS AUTOMATED END TO END. (AMENDED 2026-08-25 - see the note.)

> ## STOP AMENDED THE SAME DAY IT WAS WRITTEN. Owner, 2026-08-25:
> *"That should all be handled programmatically from command center or via code."*
>
> **The original ruling below is SUPERSEDED, not deleted.** It said promotion was a manual owner act
> and the absence of a deploy script was the design. The owner has reversed it: the chain runs
> programmatically, promote included, with no human in the loop.
>
> ⭐ **WHAT REVERSED IT is worth recording.** The original held for one afternoon and the afternoon
> disproved it. In a single session the owner personally: pasted a migration file by hand, ran a
> parity check by hand, hunted an env var through a dashboard, read a deployment list to find a
> rollback id, and copied a URL between two commands. Every one of those is assembly, and every one
> is a place a step gets skipped. **A ruling that a human must do the deciding got implemented as a
> human doing the typing, and those are not the same thing.**
>
> ⚠ **What the amendment BUYS, beyond speed:** WO-1173's requirement (d) - run schema parity "after
> every production API deploy" - was declared unsatisfiable because no deploy script existed to hook.
> Codex was right at the time. **Automating the deploy makes that trigger REAL**, turning a discipline
> back into a gate.
>
> **Implementation: WO-1199.** Until it lands, promotion is still done by hand and the steps below
> still describe reality.

### The parts of the original that are FACTS, not policy - these survive the amendment

- ⛔ **`.vercelignore` re-includes `/api`**, so **every** promotion from the repo root re-ships the
  serverless backend alongside the static payload. **There is no WebGL-only promotion.** Any plan of
  the form "ship the game build but hold `api/` back" is unimplementable as the tree stands, whether a
  human or a script does it.
- The rollback target must be captured **BEFORE** promoting. Recorded afterwards it names the thing
  being escaped.
- Judge every step by its **MARKER on a fresh log**, never an exit code. This repo's runners exit 0 on
  refusals and FAILs.

### What automation does NOT license

⛔ **Automating the act does not automate the JUDGEMENT.** The chain may promote without asking, but
it may not promote without PROVING - gates green, parity green, rollback captured, post-deploy checks
passed. An automated deploy that skips a gate is strictly worse than the manual process it replaced,
because nobody was watching.

⚠ And the reason the original rule existed has not evaporated: this is a **live store listing on a
money path**. The safety has to move from "a human is in the loop" to "the chain refuses" - it cannot
simply be dropped.

---

## 8-ORIGINAL (SUPERSEDED 2026-08-25, kept for its reasoning)

### PRODUCTION PROMOTION IS A MANUAL OWNER ACT. There is no deploy script, and that is the design.

> **Owner explicit, 2026-08-25.**

**The case:** WO-1173 wires a schema-parity gate to run *"after every production API deploy."* Codex
found, and the lead confirmed, that **no tracked script in this repo invokes `vercel --prod`.** The
WebGL script performs a PREVIEW and says *"never --prod"* in its own text. Production has only ever
been promoted **by hand** - repeatedly, on 2026-08-03, 08-04, 08-05 and again for the money-path
endpoints.

⭐ **RULED: that is deliberate and stays.** Promoting to production is an OWNER act, not an automated
one. The absence of a deploy script is **not a gap to be closed** by writing one.

**What follows from it:**
- ⛔ **Do NOT wire a production-deploy gate to the preview script.** That would label the wrong event
  as covered - a gate asserting something it never checks, which is strictly worse than no gate.
  Codex declined to do this and was right.
- WO-1173's device/store trigger surfaces (the morning chain, the detached APK build, Firebase
  distribution, and the `api/schema.sql` pre-push check) are the gate's real doors and are wired.
- The "after every production API deploy" trigger is satisfied by the **owner running parity by hand
  as part of promoting**, because the promotion is itself by hand. ⚠ That makes it a DISCIPLINE, not
  a gate, and this file is where that is written down so nobody later mistakes the absence of a script
  for an oversight.
- ⚠ **The corollary that bites:** `.vercelignore` re-includes `/api`, so **every `--prod` from the
  repo root re-ships the serverless backend alongside the static payload.** There is no
  WebGL-only promotion. Any plan of the form "ship the game build but hold `api/` back" is
  unimplementable as the tree stands.

---

## 9. THE SCHEMA-PARITY REPAIR MIGRATION IS AUTHORIZED against production.

> **Owner explicit, 2026-08-25.**

`api/migrations/20260824_0001_repair_schema_parity.sql` may be applied to the live Neon database.

    psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f api/migrations/20260824_0001_repair_schema_parity.sql
    node tools/schema-parity.mjs

**Audited before authorization** - zero `DROP`, `DELETE` or `TRUNCATE`; wrapped in `BEGIN`/`COMMIT`;
every statement additive (`CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`). It repairs
`dungeon_status`, `auth_sessions`, `purchase_quotes`, the entitlement audit columns and the widened
network CHECK.

⛔ **THE EXIT CODE IS NOT THE PROOF, AND THIS DATABASE HAS ALREADY TAUGHT US WHY.** `CREATE TABLE IF
NOT EXISTS` on a table that exists with the WRONG SHAPE reports success and changes nothing - the
`bug_reports` repair reported success three times while doing nothing. **The proof is
`SCHEMA_PARITY_OK` from `node tools/schema-parity.mjs` AFTER the run**, which is a shape query.
Same rule as every other gate here: judge by the marker, never the exit code.

⛔ **The five `tmp/neon-repair-*.sql` files are NOT authoritative** and must not be run. They are
untracked operational scratch from an earlier incident. The tracked migration above is the one.

⚠ **Applying it is the OWNER's action.** `DATABASE_URL` is redacted for every agent seat including the
lead, so no seat here can run it, verify it, or confirm it landed.

---

## 10. EXPOSURE IS NOT WHAT THE REPO SAYS IT IS. Ask before you price the risk.

> **Owner, 2026-08-25 (and 2026-08-24 before it):** the published dApp Store listing does not carry
> this work. **The only person exposed is her, testing her own build.**

STOP **THIS IS THE SECOND TIME THE LEAD OVERSTATED EXPOSURE IN TWO DAYS, AND THE SECOND TIME IT
CHANGED THE ADVICE GIVEN.** Section 1 already records the first: *"I over-stated the exposure and it
changed how I framed several decisions."* The lead read that section on the morning of 2026-08-25 and
made the same error that afternoon - asserting that "every real player who has opened the store has
seen Price unavailable" when no player has a build containing the store at all.

### Why the mistake is structurally easy to make here

This repo's canon states, loudly and correctly, that **the game is PUBLISHED on the Solana dApp
Store.** It is true. But "published" says nothing about WHICH BUILD is published, and the listing can
be - and is - many builds behind the tree. A seat that reads "live game" and infers "live players are
exposed to today's commit" has made an inference the canon never supported.

**Three facts that look like one, and are not:**
1. The app is LISTED on the dApp Store.
2. The listing carries a build that predates most of this work.
3. `MAINNET_SALES_ENABLED` is unset, so only the owner's wallet can transact at all.

Each is separately true. Only all three together tell you the exposure, and the exposure is: **the
owner, on builds she installs herself.**

### How to apply

!! **Before pricing a decision on risk to players, ASK what the published listing actually contains.**
Do not derive it from the fact of publication, and do not derive it from HEAD. The lead cannot see the
store listing from the repo, and nothing in the tree records which build is live.

STOP **The failure mode is not caution - it is MISDIRECTED caution.** Overstating exposure produces
advice that sequences work around a danger that is not there, and it costs the owner the freedom to
test. Both times, the corrected answer was *less* restrictive: flip the switch, deploy the build, try
it - there is nobody to hurt.

* **The inverse is equally binding.** The day the listing IS updated, this section stops applying and
nobody will announce that. Re-ask; never cache the answer.

---

## 11. ONE QUEST ILLUSTRATION PER QUESTLINE. Not per quest, and not one generic slab.

> **Owner, 2026-08-25**, answering a direct question from the CLI lead: a shared illustration per
> chapter / questline, **reused across every quest inside it.**

The two options she was NOT choosing are both worth naming, because both are the obvious default a
seat would reach for:
- ⛔ **one image per quest** — the art bill then scales with quest count, and quest count is the
  thing this project adds most cheaply. A content axis that is free in JSON must not be expensive in
  art.
- ⛔ **one generic slab for all quests** — cheap, and it makes every questline read as the same
  errand.

⭐ **The consequence to design against:** the parchment slab's art slot is keyed to the
**QUESTLINE**, not to the quest. So the UI seat designs **one reusable slot** — a single image
region that a questline supplies and every quest inside it inherits — rather than a unique-image
pipeline with a per-quest asset key.

**How to apply.** When a quest surface needs art, ask what OWNS the image. The answer is the
questline. A quest that wants its own picture is asking for a new questline, or it is asking for
nothing.

---

### ⭐ AMENDED 2026-08-25 — the image is GENERIC ACTION CONTEXT, and that settles the count

> **Owner, 2026-08-25, on the 24 per-quest briefs:** *"that's good since it's generic — just context
> of the action the quest grants."*

⭐ **This dissolves the apparent conflict between this section and `docs/QUEST_IMAGE_BRIEFS.md`.**
That document briefs an image for EVERY quest; this section rules ONE ILLUSTRATION PER QUESTLINE.
Both are right, because the brief describes **what kind of action the quest is about** — a smith at
an anvil, a shepherd on a hillside — not a specific dramatised moment.

Consequences, and they are the useful part:

- ⛔ **A duplicate brief is NOT a defect.** Six near-duplicate clusters exist in that document (the
  largest being wolf-bonding, flame-pup and aether-sprite, because eight authored bond quests fold
  onto the three companion species that ship). ⛔ Do NOT "fix" them by inventing distinctions the
  quests do not have.
- ⭐ **The art bill is bounded by DISTINCT ACTION TYPES, not by quest count.** That is why the count
  never runs away, and it is the real reason this section holds.
- ⛔ **Do not brief an image by naming a specific character** unless the quest genuinely centres on
  them. Types — an old smith, a soldier on a wall — travel across a questline; a named face does not.
- ⚠ A quest whose action is genuinely unlike anything else earns its own image. The rule is one per
  questline as the DEFAULT, not a cap.

### ⛔ SUPERSEDED IN PLACE 2026-08-25 — THE SLOT SHOWS THE REWARD, NOT AN ILLUSTRATION

> **Owner, 2026-08-25:** *"instead of that, why not use that image place to highlight the rewards of
> completing it — isn't that really what denotes what quest you want to take?"*

⭐ **She is right, and it retires the art requirement rather than re-scoping it.** A player chooses a
quest by what it PAYS and whether the trip is worth it. Flavour art is the least decisive information
on the panel, and it was being given the largest element on it.

**What this ruling does to the two above it:** sections 11's per-questline count and its
generic-action-context amendment are **MOOT for the quest board**. ⛔ Do not commission quest
illustrations. `docs/QUEST_IMAGE_BRIEFS.md` keeps its value as a quest INVENTORY (24 quests, givers,
outcomes) — ⛔ but its IMAGE lines are no longer a work order for anyone.

**What the slot shows instead:** the reward, at size, as **icon plus quantity** — the same grammar
section 13 rules for costs. ⭐ This is the same fix, not a second one: the WO-1195 spec already routes
every cost through ONE formatter, and this becomes its showcase surface.

⚠ **THE EDGE CASE THIS RULING CREATES, and it is now a real defect rather than a footnote.**
Quest 3 (`forgemasters_act1`, "Honest Steel") has **no authored reward at all** — no crystals, food,
magic, item or keystone; its only payoff is unlocking act 2. Under an illustration that was a data
note. Under a REWARD slab it is **an empty hero element on the screen where the player decides.**

⛔ The slot may never render blank. Either author a reward, or render what the quest actually grants
— ⭐ *"Unlocks Act 2"* is a legitimate reward and arguably a better one than 90 food. Any quest whose
payoff is progression must SAY so.

⛔ **AND XP BELONGS IN THAT SLOT — EXCEPT QUESTS DO NOT GRANT ANY.**

> **Owner, 2026-08-25:** *"don't forget EXP amounts can drive those too."*

She is right that XP drives quest choice, and verifying it at source exposed a gap larger than the
panel:

- **XP is a REAL system.** `Assets/_Modules/Core/Progression/IXpEarner.cs:35` declares `AddXp`, heroes
  level, and the dev tools grant it (`DevPanelController.cs:1155`).
- ⛔ **Quests award NONE.** Across all **63 stages** of `quests.json` the `reward` object carries
  exactly `crystals`, `food`, `magic`, `grantItemId`, `grantsKeystone`. There is **no `xp` field**, and
  the only callers of `AddXp` in the tree are DevTools and `AdminOverlay`. ⛔ **No quest path touches
  hero XP at all.**

⭐ **So the reward that most drives quest selection in an RPG is the one reward quests do not give.**
That is a game-design gap, not a UI gap — the reward slab merely makes it visible. ⚠ It is also
strictly larger than the Quest 3 case above: that is one quest with nothing to show; this is EVERY
quest missing the line the player most wants to see.

⛔ **This is the owner's call and is NOT ruled here.** Two directions: add `xp` to the quest reward
schema and author values (a data + service change — ⚠ check whether hero XP is save-persisted before
treating it as cosmetic), or accept that quests pay in resources and progression only, and design the
slab around what actually exists. ⛔ Do NOT design a slab with an XP line that renders zero for every
quest — that is worse than omitting it.

⭐ Consequence worth noting: the nine quests with no authored GIVER stop mattering under this design.
The giver was only ever needed to caption a picture.

## 12. A SHELF SHOWS PRICES; ELIGIBILITY IS CHECKED AT THE TILL.

> **Owner, 2026-08-25:** the store's display-price path serves everyone — **no wallet, no session.**
> The wallet is requested **only when the player commits to buy.**

**Both halves are the ruling, and the second half is the one that gets dropped.**

### Half one — browsing is public
The display-price path is a public price list. Opening the store must not mint a backend session,
must not ask for a signature, and must not require a connected wallet. An unauthenticated list
returns the **PUBLIC LADDER** — what anyone could buy — not a per-wallet filtered set.

### Half two — ⛔ LOOSENING THE LIST MUST NOT LOOSEN WHAT CAN BE SOLD
Per-wallet eligibility — `walletAllowed`, `MAINNET_SALES_ENABLED`, the canary's stricter gate —
**stays exactly where it is**, and is enforced at the **BINDING quote** and at **`/verify`**. The
list is a display concern. The sale is an authorization concern. They were coupled, and the ruling
decouples them **in one direction only**.

⚠ **This ruling does NOT authorise guest purchases.** `PurchaseGate.WalletRequiredAboveUsd = 4.99`
is **not** being implemented as a guest checkout — the owner chose **browse-only**. A guest who taps
buy is **asked to connect**. A seat that reads half one and infers "so guests can buy the cheap
packs" has invented a second ruling the owner did not make.

**How to apply.** Before adding an auth requirement to any store path, ask: *does this path bind or
charge anything?*
- **No** → it is shelf display. It serves everyone.
- **Yes** → it is the till. It authenticates, and it authenticates **as late as possible**.

---

## 13. A COST IS WRITTEN AS AN ICON AND A QUANTITY, NEVER A LETTER.

> **Owner, 2026-08-25:** *"Ok currently in the build scren it shows things like 130I 400W 10C I want
> the chip then the quantity in place of where it exists in build screen. that was the ask here."*

**The ruling.** Wherever a build cost is displayed, it renders as the resource **CHIP/ICON followed
by the quantity**, in the place the existing cost string already occupies. The letter-suffix form -
`130I`, `400W`, `10C` - is **RETIRED**. This is not a relayout: the cost string keeps its position,
its slot and its neighbours; only its composition changes.

### Why the letter form is worse than it looks

The build screen's cost line is the player's **first read of affordability** - can I afford this,
right now, before I tap. A single letter is a poor carrier for that read: `I` for iron and `C` for
crystal are language-bound (they are English initials and nothing else), and they are easy to confuse
with each other and with the digits beside them at a glance, at the smallest text size in the game.
This is the same failure **WO-1195** already names - a resource is named by its ICON, never by a
letter - appearing on the one screen where the player actually commits resources. The icon carries the
identity; the number carries the amount; neither has to be read as language.

### Scope - ⚠ read this before touching any HUD code

⚠ **This ruling is about the BUILD-SCREEN COST STRINGS.** That is the surface the owner named and the
surface it binds.

⛔ **The ambient, posture-driven HUD resource dock is OUT OF SCOPE and UNCHANGED.** Verified at source
(`Assets/_Modules/HUD/Kit/HudKitController.cs`, `BuildResourceChips`; landed 2026-07-03 in
`925464df7`, no feature flag), the shipped behaviour is posture-dependent and stays exactly as it is:
- `calm(town)` - widget `resourceChips`, ships **COLLAPSED**; the rail chip reads "Resources".
- `calm(explore)` - widget `resourceChipsCollapsed`, **GOLD only**; a tap gives a **6-second timed
  reveal**, not persisted.
- `build` and all three `hostile` postures - **no resource readout at all**.
- The right-rail arbiter permits at most **ONE** expanded section.

⛔ **An earlier draft of this section said "ALL FIVE RESOURCES ARE PERMANENTLY ON SCREEN" and forbade
collapsing or context-gating any resource anywhere. THAT DRAFT WAS WRONG AND IS RETIRED.** It came
from a mis-framed question - the lead offered the owner "all five always visible" vs "core three plus
tap" for the ambient dock, she picked "all five", and the answer was written up as a HUD layout
mandate. She was never talking about the dock. As written, that draft forbade shipped behaviour the
owner never asked to change, and any seat obeying it would have torn out the posture logic to satisfy
a ruling that does not exist.

⭐ **Recording the correction matters more than the correction.** The failure was not a typo in a
ruling; it was **asking the owner to choose a layout without first reading what shipped.** A choice
offered between two options that do not describe the live build cannot produce a usable answer, and
the answer it does produce will be recorded as law. Read the surface, then ask.

### ⛔ STILL UNRULED - the build palette card's image

Open and **NOT decided**. The owner floated: *"what if we remove the image and put just test and the
costs? easier less work"*. The lead's counter is that the image is what makes a card recognisable
without reading, and proposed a thumbnail-plus-text card instead. ⛔ **Neither position is recorded
here as decided.** This is pending a capture of the actual card, and a seat that implements either
one is inventing a ruling.

---

## 14. WO-1163 (stone replaces food) RUNS NOW. The lead's concern becomes the SEQUENCING DISCIPLINE.

> **Owner, 2026-08-25**, overruling the lead. The lead recommended **holding** WO-1163 until the
> store work settled; the owner chose **now**. The decision stands.

⛔ **The concern is preserved here rather than erased, because it is accurate and it still governs
HOW the work is done.** The lead's objection was:
- the change is **save-schema-adjacent**, on a **live build with an activated pay path**; and
- it moves **three mirrored files that go red together**.

**The ruling converts that objection from a reason to WAIT into a reason to be PRECISE:**

1. Follow **the ticket's own section 7 sequencing exactly.** It is not advisory ordering.
2. Treat the mirror law as a **SINGLE ATOMIC CHANGE** — the server's `USD_ANCHORS`, **both**
   canonical `packs.json` copies, and the quote test's hardcoded resource-key list move together, in
   one change, or the build is red. ⚠ Landing any subset is the failure mode the concern was about.

⛔ **WO-1163 itself is not edited by this ruling.** It is already READY and correctly specced; this
section is about **timing only**. Do not re-spec it, and do not add a hold banner to it.

---

## Where these came from

Ten rulings on 2026-08-24 (`OWNER_RULINGS_OWED.md`). Seven answered a ticket. **Three answered a
class** (§§1-3), so they were pulled up here where the next ticket can find them.

Eleven more on 2026-08-24 (`OWNER_RULINGS_OWED_2.md`). **Three of those answered a class** and were
elevated here at the owner's own request: **§4** (VFX authority, from rulings on WO-875 / WO-1100 /
WO-874), **§5** (save-schema bumps, from WO-1154 §5 — *"it governs a class"*), and **§6** (anti-cheat
posture, from WO-1128, which also binds §3's leaderboard clause).
