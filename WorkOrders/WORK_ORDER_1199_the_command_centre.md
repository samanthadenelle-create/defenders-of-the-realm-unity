# WORK ORDER 1199 - the command centre: ship the whole chain programmatically

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1199 -> 1200 in the same edit)
**Silo:** Tooling / ops
**Ruling:** owner, 2026-08-25 - *"That should all be handled programmatically from command center or
via code."* This AMENDS `FOUNDATIONAL_RULINGS.md` section 8; read that amendment first.

---

## Why

In one session the owner personally pasted a migration, ran a parity check, hunted an env var through
a dashboard, read a deployment list for a rollback id, and copied a URL between two commands. Every
one of those is assembly, and every one is a place a step gets skipped.

⭐ **The old rule said a human must DECIDE and got implemented as a human TYPING.** Those are not the
same thing, and the typing is where the mistakes live.

## STOP THE ACTUAL USE CASE, and it changes the design

Owner, 2026-08-25: *"The idea, if I see sales slipping or am having an event."*

This is **not primarily a deploy script. It is a LIVE-OPS CONTROL SURFACE.** The deploy chain is how
an action reaches players; the reason to build it is that the owner needs to SEE a number moving and
ACT on it the same day.

Two verbs, and they are what the thing is for:

- **SEE** - is revenue slipping? which SKUs sell? what did today do versus last week?
- **ACT** - run a sale, run an event, change what a pack costs, and have players see it.

### STOP THE ARCHITECTURAL CONSEQUENCE - read this before designing anything

**A sale MUST be server-side only. It must NOT require a client rebuild.**

If running an event needs a Unity build, an R2 content push, an APK, and a store submission, the owner
is **days late to her own event** and the feature is worthless. That is the whole test of this design.

⭐ **The good news is the rails already exist and were built for a different reason:**

- `api/_lib/purchase-catalog.js:344` already computes a discounted `quotedUsd` and prices the SKR off
  it. **A discount is already a server-side concept.**
- `discount_bps` and `discount_reason` already exist on the quote row, and WO-1177 shipped a
  seven-day shortfall discount that uses them.
- **WO-1198** makes the client render the effective price AND the saving. ⭐ **That ticket is the
  PREREQUISITE for this one** - once the client shows what the server quotes, a server-side sale
  renders on a phone that was built weeks ago, with no update.

⚠ **So sequence WO-1198 BEFORE the sale controls here.** Without it a discount changes the SKR the
player sends while the screen still shows the old dollar price - a sale nobody can see, which is the
exact defect WO-1198 exists to fix.

### STOP SALES ARE SCHEDULED, AND THE BANNER SHIPS WITH THE SALE

Owner, 2026-08-25: *"I'm creating stuff for X, creating reels. Last thing I want is to have to go
script stuff. I'm eventually planning on working with you to create the content, set start and stop
times, and add banners promoting the sales."*

**So a promotion is one scheduled OBJECT carrying both the discount and the words**, not a switch
somebody flips and remembers to unflip:

    { skus | all, discountBps, startsAtUtc, endsAtUtc, bannerText }

The owner announces on X at a time. The sale must **start at that time with nobody at a keyboard**,
and it must **stop when it says it stops**.

#### STOP THE FAILURE MODE THAT COSTS REAL MONEY: a sale that does not end

A discount that fails to expire is not a sale - it is a **permanent price cut that was announced as
temporary**, and nothing on screen will look wrong. Nobody notices a bug whose symptom is "the price
is lower than it should be."

**Therefore the schedule FAILS CLOSED TO FULL PRICE.** ⛔ If the promotion cannot be read, parsed, or
its window resolved, the server quotes **NO discount** - never a lingering one, never a default-on
one. ⛔ The end time is enforced **SERVER-SIDE at quote time**, never by a client clock, never by a
cron that might not fire.

⚠ **Times are stored and compared in UTC and displayed in the owner's local time.** A sale that starts
an hour late because someone stored a local timestamp is a missed launch she cannot get back - the
announcement has already gone out.

#### The banner is server-driven too, for the same reason as the price

⛔ **A banner that needs a client rebuild is as useless as a sale that does.** The client fetches
active promotions and renders `bannerText`; the phone that was built weeks ago shows the new banner.

- ⛔ ASCII-only - non-ASCII renders as tofu in TMP on device.
- ⛔ Never carry the sale's meaning by colour alone; the owner is red/green colourblind. The banner is
  words, and WO-1198's saving line is words and numbers.
- ⭐ Banner copy is **authored by the owner**, and this ticket only has to carry a string she wrote.
  ⚠ It must degrade to NOTHING when no promotion is active - never a placeholder, never a stale banner
  for a sale that ended. A dead banner is worse than no banner, because it advertises a price the
  server will refuse to honour.

#### Why this is the strongest argument for WO-1198 landing first

A scheduled sale with no visible saving is invisible. The player sees a slightly different SKR number
and no reason to act. **WO-1198 turns the discount into the thing being advertised** - and the banner
and the price then tell the same story, from the same server, on an unmodified client.

⚠ **This section may deserve its own ticket** once the command centre's shape settles - scheduling,
promotion storage and a client fetch are meaningfully more than a deploy chain. Flagged rather than
assumed; the lead should split it if the lane gets large.

### The mirror law is the trap in this lane

⛔ `USD_ANCHORS` in `purchase-catalog.js` must equal the canonical client authoring EXACTLY, proven on
every run by `test/purchases.quote.test.js`, and its client sources are **both** `packs.json` copies
**and** `battle_monthly.json`.

**So the AUTHORED price is mirrored and cannot move server-side alone. A DISCOUNT is not.** A sale
must therefore be expressed as a **discount applied to an authored price**, never as an edited anchor.
⛔ Editing anchors to run a sale breaks the mirror test and needs a client ship - which defeats the
entire purpose.

### What SEE needs

Read-only, from data that already exists: `purchase_quotes` (issued, consumed, `discount_bps`,
`usd_anchor`) and `purchase_entitlements`. ⚠ An admin surface already exists at `api/admin/db.js` and
`api/admin/stats.js`, key-gated by `ADMIN_DASH_KEY` - **extend it, do not greenfield a second one.**

⚠ **Two honest cautions on any revenue view:**
- Devnet SKR is **9 decimals**, mainnet is **6**. ⛔ Never aggregate across networks without
  normalising, and never quote a devnet figure as revenue.
- A quote ISSUED is not a sale. Only a **consumed** quote with a `consumed_tx` is money.

## What it must do, in order, refusing at every step

    tools/command-centre.ps1   (or .mjs - pick one and say why)

1. **Gate.** `COMPILE_GATE_OK`, then `REGRESSION_OK <n>/<n>`. ⛔ Judge the MARKER on a FRESH log,
   never the exit code - this repo's runners exit 0 on refusals and FAILs.
2. **Content parity**, if the run touches anything shipped: `R2_PARITY_OK` via `tools\\r2-ship.ps1`.
   ⚠ `Builds/r2-parity.log` is **UTF-16** - a naive grep reads the marker as ABSENT, and absence means
   FAILURE under section 16. **Decode before judging.** This already produced one false red today.
3. **Schema parity** against production: `SCHEMA_PARITY_OK`.
   ⭐ This is the step the amendment BUYS - WO-1173 requirement (d) was declared unsatisfiable because
   no deploy script existed to hook. Hook it here.
4. **Capture the rollback target BEFORE promoting** - resolve the current production deployment id and
   write `Builds/PROD_ROLLBACK.txt`. ⛔ Recorded afterwards it names the thing being escaped.
5. **Deploy to PREVIEW**, verify the preview actually serves the new build.
6. **Promote that exact preview URL.** ⛔ Not `--prod`: promoting a verified preview ships the artifact
   that was inspected; `--prod` re-uploads a fresh uninspected one.
7. **Post-deploy PROOF, not a smoke test.** Hit an endpoint that genuinely reaches the database - the
   pattern that worked today is `GET /api/auth/nonce`, which WRITES a row, so a 200 proves the
   credential and the connection, not merely that the app booted. ⛔ An endpoint that dies at an auth
   check before touching the DB proves nothing; do not use one.
8. **AUTO-ROLLBACK on a failed step 7**, promoting the id captured at step 4, then exit non-zero with
   the reason. ⭐ This is what earns the removal of the human: the chain must be able to undo itself.

## STOP What automation does NOT license

**Automating the act does not automate the JUDGEMENT.** The chain may promote without asking; it may
not promote without PROVING. ⛔ An automated deploy that skips a gate is strictly worse than the manual
process it replaces, because nobody is watching.

⚠ This is a live store listing on a money path. The safety moves from "a human is in the loop" to
**"the chain refuses"** - it is not dropped.

## Hard constraints

- ⛔ **Secrets never appear in a command line, a log, or a committed file.** `DATABASE_URL` and any
  Vercel token come from the environment. ⚠ PowerShell's PSReadLine writes every command line to
  `ConsoleHost_history.txt` on disk - that is how a live credential ended up in a plain-text file
  today. If the script prompts for anything, it must not echo it.
- ⛔ **`.vercelignore` re-includes `/api`**, so every promotion re-ships the backend. There is no
  WebGL-only promotion. Say so on screen before promoting - the operator should know what is shipping.
- ⛔ **Pure ASCII.** `PowerShellEncodingRegression` fails the gate on a non-ASCII `.ps1`, and PS 5.1
  reads a BOM-less non-ASCII file as ANSI, which can swallow whole statements while reporting zero
  parse errors.
- **Every refusal names the step, the marker it wanted, and the log it read.** A chain that stops
  without saying which gate said no is a chain people start bypassing.
- ⛔ Do NOT re-inline the R2 push or verify - call `tools\\r2-ship.ps1`. That pair was copy-pasted into
  two chains once and had already drifted (section 16).

## Should it also do the non-deploy chores?

⭐ Yes, and they are the cheaper half. A `-Status` mode that answers, in one screen:

- gate markers and their log freshness,
- `SCHEMA_PARITY_OK` / what drifted,
- which production deployment is live, and **what commit it was built from**,
- **which env vars are set** - `MAINNET_SALES_ENABLED` in particular, since nobody could determine its
  state today without a dashboard,
- whether the committed `api/` differs from what production is running (**the client/server skew that
  made the store unable to price**).

⚠ That last line is the one that would have saved the most time today.

## Acceptance

1. A full run from a clean tree gates, deploys, promotes and PROVES, with every marker judged on a
   fresh log.
2. A deliberately failed gate **refuses to promote** and names which gate said no.
3. A deliberately failed post-deploy check **rolls back automatically** and exits non-zero.
4. `Builds/PROD_ROLLBACK.txt` is written BEFORE the promote, every time.
5. No secret appears in any log, argument, or committed file - prove it by grepping the run's output.
6. ⛔ Seen RED on 2 and 3. A chain never observed refusing is not evidence that it refuses.

## What NOT to touch

- ⛔ `tools/schema-parity.mjs` and `tools\\r2-ship.ps1` are READ-ONLY here - call them, do not edit
  them.
- ⛔ Do not weaken any existing gate to make the chain pass.
