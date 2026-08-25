# Rulings owed — batch 2, 2026-08-24

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

## 1. Does the game require an internet connection? (PROD-012 §4) — three yes/nos

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

## 2. May the lead promote `api/` to production? (WO-1160)

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

## 3. VFX: must you tag every key by hand, or may the lead map into a library you already own?

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

## 4. Who decides a save-schema bump? (WO-1154 §5, and every save-adjacent feature after it)

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

## 5. When the bank is full and a reward lands, what happens? (WO-978 §6)

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

## 6. Do we accept that a player can still cheat their own outcomes? (WO-1128)

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

## 7. "Push promos" — do you mean authoring a code, or notifying players? (WO-1169 §5 Q4)

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

## 8. Does the first raid still require a full army? (WO-823 Phase E)

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

## 9. Do any of the 43 red UI panels get waived? (WO-1060)

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

## 10. Treasury check on the ship chain: block, warn, or manual? (WO-1159 §5)

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

## 11. Gear ability at max level — what shape? (WO-814)

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

- **WO-970 — two authored nudges need a re-dial.** `_staffGripEuler = (0, 90, 0)` and `sword_A`'s
  rotation were dialled on top of a broken base that has since been fixed, so both are now compensating
  for a bug that is gone. ⭐ **One thing you could rule instead of doing:** may the lead reset the staff
  value to (0,0,0) and send you a screenshot to veto? That turns an editor session into a glance.
- **WO-831 — the 6 Echo emergence PNGs do not exist.** The code shipped 2026-08-02 with a safe
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
