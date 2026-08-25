# Rulings owed — batch 2, 2026-08-24

> # ✅ ALL ELEVEN ANSWERED 2026-08-24 — this document is CLOSED.
> Every ruling below has been **recorded in its own ticket** and each ticket's `**Status:**` line
> updated. ⭐ **Three answered a CLASS and were elevated into `FOUNDATIONAL_RULINGS.md`** — §4 (VFX
> authority: repair · map · substitute), §5 (save-schema bumps, four conditions), §6 (anti-cheat:
> client-authoritative combat only while standings carry no material consequence, cross-referenced
> with §3's leaderboard clause). ⛔ **Two were SENT BACK, not approved:** ruling 5 (WO-978 — the
> crystal example contradicts WO-1165; ticket → BLOCKED) and ruling 10 (WO-1159 §5 — modified into a
> two-state WARN/BLOCK policy). ⚠ Per `FOUNDATIONAL_RULINGS.md`'s own no-paraphrase rule, the tickets
> **cite** §§4-6 rather than restating them.
>
> | # | Ticket | Outcome | New status |
> |---|---|---|---|
> | 1 | PROD-012 | APPROVED — internet IS required, all three | READY |
> | 2 | WO-1160 | APPROVED — ONE deployment, four post-deploy requirements | BLOCKED (evidence owed) |
> | 3 | WO-875 / WO-1100 / WO-874 | APPROVED → `FOUNDATIONAL_RULINGS.md` §4 | READY / READY / BLOCKED (3 boss keys stay hers) |
> | 4 | WO-1154 | APPROVED with four conditions → §5 | BLOCKED (faucet measurement only) |
> | 5 | WO-978 | ⛔ SENT BACK — crystals stay UNCAPPED | BLOCKED |
> | 6 | WO-1128 | APPROVED, trigger HARDENED → §6 | READY |
> | 7 | WO-1169 §5 Q4 | APPROVED — promo-code authoring only | SPEC (scoping closed) |
> | 8 | WO-823 Phase E | APPROVED — **3 of 10** | READY |
> | 9 | WO-1060 | APPROVED — NO waivers | READY |
> | 10 | WO-1159 §5 | ⛔ MODIFIED — two-state WARN/BLOCK | READY |
> | 11 | WO-814 | APPROVED — per-rarity, weapons first | READY |
> | — | WO-970 (bottom) | APPROVED — reset staff grip to (0,0,0), capture to veto | READY |
> | — | WO-831 (bottom) | ⛔ NOT a ruling — stays an art task | unchanged |

**Recommendation first on every one, same as last time.** Fourteen candidates went in; **five were
already answered and are listed at the bottom with the evidence**, and two were engineering calls the
lead has taken rather than hand you. ⭐ **Eleven survive**, ordered by how much they unblock.

⚠ **`FOUNDATIONAL_RULINGS.md` is canon and binds this whole document.** Where an option below would
break §1 (progression gates cannot be purchased), §2 (paid permanence should be visible; the Heart is
not a sponsor surface) or §3 (offline loss creates repair, never irreversible punishment), it is
flagged — and the rule would have to be **amended in the same change**, never quietly contradicted.

⛔ **No number below is invented.** Where a number is owed, the shape is proposed and the number is
yours.

---

## ✅ ANSWERED 2026-08-24 — 1. Does the game require an internet connection? (PROD-012 §4)

> **RULED: YES, all three.** Declare it on the listing · honest setup screen with Retry, exact copy *"An internet connection is required to finish setting up Elarion."* · ⛔ NO offline asset floor. Recorded in PROD-012 → READY.

> ⭐ **RECOMMEND: (1) YES, declare it. (2) YES, an honest screen with a retry. (3) NO offline floor.**

**Frees: PROD-012 outright, and unparks PROD-010's first-run surface.**

Moving buildings and enemies to the CDN **deleted `Assets/Resources/Structures` and
`Assets/Resources/Enemies`**, so the fallback chain has no second tier. A first run with no connection
is a town of **invisible buildings** — loud in our logs, **completely silent to the player**. Bundles
cache, so this is a **first-run-per-build** requirement, not a per-launch one.

1. **Declare "internet required" on the dApp Store listing.** We are currently making the opposite
   claim by omission, and it is the cheapest of the three to keep true.
2. **An honest screen beats an empty town.** *"You need a connection to finish setting up"* + retry.
   ⭐ This is the one that actually protects a new player, and it is small.
3. ⛔ **No minimal offline art floor.** It is the only one of the three that means duplicating content,
   and duplication carries the PROD-010 hazard — already-installed APKs adopt the new remote catalog,
   so a local path that does not exist in shipped builds gives *existing* players invisible buildings.
   Paying that to make a disconnected first run show *something* is a bad trade.

⭐ **Clash of Clans requires a connection and says so.** Nobody has ever churned over that sentence.

## ✅ ANSWERED 2026-08-24 — 2. May the lead promote `api/` to production? (WO-1160)

> **RULED: yes, ONE deployment only** — ⛔ *"This does not create standing production-deploy authority."* Four post-deploy requirements set (quote health · session health · purchase-quote smoke test · **capture the deployed commit hash**). Recorded in WO-1160; status stays BLOCKED until the evidence is posted.

> ⭐ **RECOMMEND: yes — a one-time approval for this deploy, not a standing one.**

**Frees: WO-1160 (P0), and it is the only thing between the go-live build and a working sale. It also
lets WO-1158, WO-1157 and WO-1159 finally be felt-tested.**

Two endpoints the money path needs — `POST /api/purchases/quote` and `POST /api/auth/session` — **return
404 in production**. They exist only in preview. Consequences, both proven by probe:

- Every Night Market card reads **"Price unavailable."** That is the fail-closed path working as
  designed — the client refuses to invent a price rather than charge a made-up one.
- ⚠ **Invisible:** the session endpoint is 404 too, so the wallet still prompts **three times**. The
  one-prompt fix is silently inert in production and would have read as *"WO-1157 failed."*

⛔ **No code change is involved.** The block is purely that `START_HERE` §4 makes web promotion yours:
*"Web deploys stay preview-only … NEVER `--prod` — promotion is the owner's."* I am not asking you to
retire that rule — a per-deploy word keeps it intact.

⚠ One honest note: the Vercel CLI is not installed on this machine, so the first promotion carries an
install step.

## ✅ ANSWERED 2026-08-24 — 3. VFX authority

> **RULED: split it — REPAIR → lead · MAP by existing semantic name → lead · SUBSTITUTE → owner.** Elevated to `FOUNDATIONAL_RULINGS.md` §4 at her request. ⛔ WO-874's three boss keys stay hers. WO-875 + WO-1100 → READY.

> ⭐ **RECOMMEND: split it — REPAIR and MAP are the lead's; SUBSTITUTE stays yours.**

**Frees: WO-875 outright, the last 3 keys of WO-874, the 5 prefabs of WO-1100, and it unpins WO-861's
hero kits downstream.**

Your standing rule is *you tag the key, the CLI maps it verbatim, the CLI never picks.* It has been
read as "no VFX moves without her," and three tickets are parked behind it — including one where the
engine is **built and gated out**, not missing:

- **WO-875** — `SpellVfxFactory` already contains a full fire / frost / arcane / holy cast library, and
  `RegistryOnlyMotionVfx = true` suppresses it, so most hero casts are **silent**. The mapping here is
  *fire ability → the prefab named fire*. That is not a creative pick; it is reading a label.
- **WO-1100** — five ParticlePack prefabs have a **null material slot**. Restoring a prefab's own
  missing material is a repair, not a substitution.
- **WO-874** — `Boss_AttackImpact` / `Boss_PhaseTransition` / `Boss_Telegraph` have live hooks and no
  art. ⭐ **These three genuinely are yours** — there is no existing prefab that names itself the
  answer, so picking one is a choice.

**The line I would draw:** where the library already names the element or the prefab is being restored
to what it had, the lead proceeds and shows you a capture. Where a *new* effect must be chosen for a
hook, it comes to you. ⚠ You are red/green colourblind — this deliberately never asks you to pick
between two hues, only to accept a named element mapping.

## ✅ ANSWERED 2026-08-24 — 4. Who decides a save-schema bump? (WO-1154 §5)

> **RULED: the lead may bump under FOUR conditions, all required together.** Elevated to `FOUNDATIONAL_RULINGS.md` §5. ⛔ Rename / removal / reinterpretation / conversion / destructive migration still hers.

> ⭐ **RECOMMEND: the lead may bump for an ADDITIVE field with a read-migration; anything that changes
> or removes an existing field comes to you.**

**Frees: WO-1154's attunement state, and a class — every future feature that needs to remember
something hits this same question.**

Today the rule reads *"no schema bump unless the owner rules one,"* which means a two-line additive
field waits on you exactly as long as a destructive migration does. The repo already has the safe
pattern: v36, v37 and v38 were all **additive with a read-migration**, and a pre-v38 save simply reads
the default. The risk is not in adding a field; it is in **reinterpreting one that already exists** —
which is where a player's town changes under them.

⚠ **The reason this is not purely engineering:** a bump is the moment old builds and new builds stop
agreeing, and you own when that happens. The recommendation keeps that ownership for the cases that can
actually hurt a save, and stops it gating the ones that cannot.

## ⛔ SENT BACK 2026-08-24 — 5. When the bank is full and a reward lands? (WO-978 §6)

> **The owner found a contradiction in the recommendation:** it used a **crystal** example, but WO-1165 establishes crystals as the one UNCAPPED currency. **RULED: crystals remain UNCAPPED and always pay in full; capped resources (wood/iron/stone) pay what fits, discard the overflow, and disclose exactly what was collected. ⛔ No secret overflow wallet.** ⭐ Verified at source: crystals are NOT capped anywhere in code (`TownBankCapacity.cs:238-242`, `:478-482`; `EconomyService.cs:469-476`; regression `[no-crystal-cap]`). WO-978 → BLOCKED pending reconciliation of its own §1.

> ⭐ **RECOMMEND: pay up to the cap, keep the overflow nowhere, and SAY SO in words.**

**Frees: WO-978's behaviour half (its regression half is already unblocked), and it settles a class —
every reward path that credits into a capped bank.**

Right now a raid at full storage credits **0 crystals while the log and the popup both announce the
full amount**. That is the *"I did the raid and got nothing"* complaint, and it is **unfalsifiable from
our own logs** because every line agrees the player was paid.

The four candidates were: pay 0, pay partial, refuse, or overflow somewhere. Partial-and-say-so is the
one that never lies:

- **Pay what fits.** The player is not punished for the raid they actually won.
- ⛔ **State the shortfall in words** — *"Storage full — 240 of 500 crystals collected"* — never colour
  alone, never a number that quietly differs from the announced one.
- ⚠ **No overflow bank.** An overflow store is a second wallet with its own caps, its own UI and its
  own bugs, bought to avoid a sentence.

⭐ **This is the Clash answer too:** loot beyond your storage is simply not collected, and the game says
your storage is full rather than pretending otherwise.

## ✅ ANSWERED 2026-08-24 — 6. Client-authoritative outcomes (WO-1128)

> **RULED: accepted, but the trigger is HARDENED** — acceptable only while leaderboard standings have **NO MATERIAL CONSEQUENCE**. ⛔ NOT "while the leaderboard is cosmetic." Elevated to `FOUNDATIONAL_RULINGS.md` §6, cross-referenced with §3.

> ⭐ **RECOMMEND: yes — verify the CLOCK, stop there, and revisit only if the leaderboard becomes
> competitive.**

**Frees: WO-1128's close, and it sets the anti-cheat posture for everything after it.**

You asked whether it is correct that we only verify **server time**. It is, and the honest scope is
worth saying plainly: offline accrual is now server-reconciled and a forwards-clock claim gets scaled
down, but **action outcomes stay client-authoritative** — someone editing a local save to add a won
battle or a loot roll cannot be caught without simulating the game on the server.

Stopping at the clock is right today because **the opponents are NPCs**. A player who cheats takes
nothing from anyone else. Chasing the rest means server-side simulation, or device inspection — the
arms race the ticket deliberately refuses to enter.

⚠ **THIS COLLIDES WITH A RULING YOU JUST MADE.** `FOUNDATIONAL_RULINGS.md` §3 introduces a
**leaderboard** ("a shield removes you from it for the season"). The moment standing is contested,
client-authoritative outcomes stop being harmless — a fabricated clear outranks an honest one. ⭐ **My
recommendation holds only while the leaderboard is cosmetic.** If it is ever meant to be competitive,
that is the trigger to revisit this, and §3 should say so in the same change.

## ✅ ANSWERED 2026-08-24 — 7. "Push promos" (WO-1169 §5 Q4)

> **RULED: promo-code AUTHORING only.** ⛔ *"Do not let 'push promo' casually smuggle an entire notification platform into an admin-console work order."* Notifications = a separate ticket if ever.

> ⭐ **RECOMMEND: authoring a code. Notifications are their own pillar, not a line item in the admin
> console.**

**Frees: WO-1169 §5–§7 scoping, which is the only thing keeping that ticket open.**

The two readings are wildly different sizes. Authoring a promo code is a table and an admin form.
**Pushing a notification to players is infrastructure that does not exist anywhere in this repo** —
no push service, no device tokens, no consent surface, no send-time policy.

⚠ **And it collides with a ruling you already made.** `FOUNDATIONAL_RULINGS.md` §3 already writes the
notification copy law (*"a siege is massing"*, never *"your town is under attack"*) and sets a hard
fence: ⛔ **a notification may never be paired with a shield offer.** A promo push is, structurally, a
notification paired with an offer. If notifications are ever built, §3's fence has to be extended to
cover promos **in the same change** — otherwise the marketing surface walks straight through the door
the fence was built to close.

⭐ If you want both, they are two tickets and the code one ships first.

## ✅ ANSWERED 2026-08-24 — 8. First-raid army gate (WO-823 Phase E)

> **RULED: soften it. THE NUMBER IS 3 OF 10.** First-ever raid only; ⛔ must go through `ArmyReadiness`. WO-823 → READY.

> ⭐ **RECOMMEND: yes, soften it — the FIRST raid only, and the threshold number is yours.**

**Frees: WO-823's last outstanding phase, closing a ticket whose other four phases shipped 2026-08-01.**

Today the raid gate wants a full army (cap 10). For a brand-new player that is a long wait in front of
the single most important thing the game has to show them, and it is their first chance to find out
whether they like the combat at all.

The shape: a threshold used **only when the save has never completed a raid**; after the first raid
returns, the normal rule resumes. ⛔ It must go through `ArmyReadiness` — the same single source Phase A
built — not a second check inside the raid screen, or the grey-button-versus-open-gate bug comes back.

⭐ **Clash never gates your first attack on a full army** — it hands you troops and points you at a
target. ⛔ The number itself I will not invent; the ticket floated three of ten as an illustration, and
it is yours.

## ✅ ANSWERED 2026-08-24 — 9. Red UI panel waivers (WO-1060)

> **RULED: NO WAIVERS.** Allow-list stays at two; the four newly-red panels get fixed. ⭐ *"Do not celebrate creating a smoke alarm by taking the batteries out when it starts beeping."* WO-1060 → READY.

> ⭐ **RECOMMEND: none. Fix them; the allow-list stays at two.**

**Frees: WO-1060's acceptance, and keeps the new oracle meaningful.**

The touch/overlap oracle shipped and immediately went red on **43 real panels** — including a **buried
Close button** on the live Rumor Board sharing 194x112 px with an Accept button, where only one can win
the tap. Four panels are newly red and **not on the allow-list**, and the rule is that the list may only
ever **shrink**; adding to it takes your word. My word back is: don't. These are real defects and each
one is a small ticket.

⚠ **The second half of this one I am NOT bringing you, because it is not yours.** ~21 of the 43 are a
full-screen transparent tap-catcher overlapping the buttons it sits behind — the oracle's own rule
already excludes graphic-less hit areas as things that "cannot collide visually," and whether that
exclusion extends to the overlap assert is a question about the *tool*, not the *game*. The lead is
taking that call and will report which way, with the red count after.

## ⛔ MODIFIED 2026-08-24 — 10. Treasury check on the ship chain (WO-1159 §5)

> **RULED: a TWO-STATE policy, stronger than the proposal.** Cannot query (RPC down/rate-limited) → **WARN**, build may continue with explicit acknowledgement. Query SUCCEEDS and config is wrong → **BLOCK**. ⛔ Always invoke with `--multisig`. WO-1159 → READY.

> ⭐ **RECOMMEND: warn, don't block.**

**Frees: WO-1159's last open proposal; the treasury red itself is already resolved.**

The revenue vault's Squads threshold was re-read from chain on 2026-08-24 as **2-of-3, timeLock 0**, so
nothing is red today. The open question is whether the ship chain should *recompute* that every build
instead of trusting a sentence in a file — which is exactly the lesson the R2 bundle misses taught.

⚠ **The tradeoff is real:** it puts a **mainnet RPC round-trip on the ship path**, and public RPC is
rate-limited and occasionally down. Blocking means somebody else's outage can stop your build. Warning
means the check runs every time and is loud when it fails, without handing an outsider a veto.

⛔ Whichever you pick, the call must always pass `--multisig` — without it the tool reports success
having read no threshold at all, which is a green that proves the vault exists and nothing about
whether it is safe.

## ✅ ANSWERED 2026-08-24 — 11. Gear ability at max level (WO-814)

> **RULED: APPROVED** — per-rarity generic → weapons first → locked ability visible from Level 1. Ability identities stay hers. ⭐ Design caution recorded: favour abilities that CHANGE PLAYSTYLE, not *"+35% MORE DAMAGE."* WO-814 → READY.

> ⭐ **RECOMMEND: per-RARITY generic, weapons first. The ability list itself stays yours, later.**

**Frees: WO-814 from SPEC DRAFT to authorable — one ticket, and it is the smallest ask here.**

You floated *"add ability at lvl 5?"* on 2026-07-30 and it has sat because it was read as needing the
whole ability list before anything could move. It does not — three shape questions do:

- **Per-rarity generic, not per-item.** Per-item is richer and is authoring work that scales with the
  catalog forever. Per-rarity gives every item at max level a beat, at a fraction of the writing.
- **Weapons first, armour after.** Weapons already have the on-hit proc seam the talent system uses;
  armour rides the mitigation path and is a second piece of engineering for the same feature.
- **Show the locked line from level 1** — *"Lv 5: <ability>"* on the Improve preview — so the goal is
  visible the whole way up rather than a surprise at the end.

⭐ It mirrors the shipped troop pattern exactly (`troop-upgrades.json` `specialAbilities` with a level
threshold), so there is no new machinery. When you want to write the abilities, the slots will be there.

---

# Not rulings — these need your hands or your assets

Listed so they are not mistaken for decisions and do not sit invisibly.

- ✅ **RULED 2026-08-24 — WO-970: APPROVED.** The lead may reset the staff grip to **(0,0,0)** and send a capture to veto; her reasoning ties it to ruling 3 — restoring a neutral default after the underlying defect is fixed is a **REPAIR**, not a substitution. WO-970 → READY. Original text: **two authored nudges need a re-dial.** `_staffGripEuler = (0, 90, 0)` and `sword_A`'s
  rotation were dialled on top of a broken base that has since been fixed, so both are now compensating
  for a bug that is gone. ⭐ **One thing you could rule instead of doing:** may the lead reset the staff
  value to (0,0,0) and send you a screenshot to veto? That turns an editor session into a glance.
- ⛔ **NOT A RULING 2026-08-24 — she declined to turn it into one.** WO-831 stays an **art task**; status unchanged. Original text: **the 6 Echo emergence PNGs do not exist.** The code shipped 2026-08-02 with a safe
  fallback, so the beat currently degrades to the portrait. `Assets/Resources/Echoes/Emergence/`
  is still absent at HEAD (verified today). Art is yours; nothing else pends.

---

# Dropped — already answered, or not yours

Verified at source today, not assumed.

| Candidate | Why it is not on the list |
|---|---|
| **WO-1166 §1** | **RESOLVED 2026-08-24 by you.** The five competing accounts of Echo acquisition collapse to one: the first Echo is granted with the guide (`TutorialFlow.cs:1610`), the rest at thresholds. |
| **WO-1154** | **Already ruled 2026-08-24** — no numbers until the **crystal faucet rate** is measured (*"without knowing the faucet rate, any crystal number is dartboard economics"*). ⚠ The unblock is a **lead task**: run the measurement. Asking you for a number now would be asking you to overturn yourself. |
| **WO-1134** | **Substantially ruled 2026-08-21** — cooldowns (4h/8h/12h), attrition scaling, sub-linear reward, and the per-camp terminus (12/18/24 clears) are all answered in the ticket. Its Status line still says "owner numbers open" and is **stale**; the lead should flip it to READY. The one residual, loss stakes, is explicitly non-blocking and is now largely covered by `FOUNDATIONAL_RULINGS.md` §3. |
| **WO-1072** | **Decided.** The valuation curve shape is adopted and the base rate is anchored to the current impulse-crystal rungs — a derivation, not a choice. Status is READY TO IMPLEMENT. |
| **WO-915 §2** | **Superseded.** It asks whether Buy should default OFF for a public build; you took the mainnet decision explicitly, `RealmStorePurchase` is `defaultOn: true` (`FeatureFlags.cs:721`) with the reasoning recorded above it, sales are env-gated server-side (`MAINNET_SALES_ENABLED`), and the rewarded-ad path it proposed as a fallback went **default ON 2026-08-24**. The WO is stale; the lead should reconcile it. |
| **UI-002** | **You already ruled it 2026-08-22** — *"this is the money screen … every state must be implemented, none deferred."* Its "BLOCKED on a wallet-authority decision" line does not survive contact: the actual blocker named in its own regression matrix is that **`GetBalance` cannot distinguish a failure from a zero balance**. That is engineering. Lead's. |
| **WO-1169 §5 Q1, Q3** | **Lead decisions, taken.** Q1 (where the console lives) — extend the existing admin surface; `site/admin.html` already gained the Player-reports tab, so a second home would be the duplicated-state failure this repo keeps having. Q3 (promo authoring) — a script, because it is auditable in git. Q2 (F8 egress) and the wallet-join privacy call were **already ruled by you 2026-08-24**. |
| **WO-874, WO-887** | **Implemented 2026-08-22.** The only residue is WO-874's three untagged boss keys, folded into ruling 3. |
| **WO-978 (the seam)** | **Lead decided 2026-08-24** — a source-structural assertion, not `internal` + `InternalsVisibleTo`. Only the §6 behaviour question is yours, and it is ruling 5. |
| **MON-002** | Blocked on "a real Squads vault address." One now exists and was **re-verified on chain 2026-08-24** (2-of-3, timeLock 0). Lead reconcile, not a ruling. |

---

## ⭐ RULING 5 - CLARIFIED AND CLOSED 2026-08-24

Owner: **"yes the capped three whatever they're called."**

⭐ Recorded **structurally**: pay-what-fits-and-disclose applies wherever `TownBankCapacity.IsCapped()`
is TRUE; uncapped resources always pay in full. ⛔ **No resource-name list is hardcoded anywhere** — the
"stone" in the original wording would have gone stale the day WO-1163 lands.
