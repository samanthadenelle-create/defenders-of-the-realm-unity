# WORK ORDER 1199 - the command centre: ship the whole chain programmatically

**Status:** READY - REVISION 2 RETURNED 2026-08-25 as a NEAR PASS with TWO one-line fixes outstanding. NOT harvestable, and nothing has landed on this branch - `tools/command-centre.ps1` appears in no commit reachable from HEAD (`git log --all -- tools/command-centre.ps1` is empty), so the work lives in the dev lane's own tree. What the lane got RIGHT, said first: B1 is fixed and load-bearing - `Invoke-Captured` sets `$ErrorActionPreference = 'Continue'` function-locally, shadowing the script-scope `'Stop'` - reproduced under a Stop harness (6 lines survived, native exit code propagated, outer preference intact) AND backed by a NEGATIVE CONTROL that captured exactly 1 line without it; B3 is fixed STRUCTURALLY, the rebuild branch being UNREACHABLE for this artifact rather than merely detected, with success now an alias-ID poll instead of a prose regex and the identifier traced build -> proof -> promote; B2 is NEUTRALISED rather than eliminated and the write-up says so rather than rounding up; polling is bounded at 180s with named refusals; all prior regressions hold. TWO FIXES OUTSTANDING, both one-liners: (1) `vercel curl` would refuse on EVERY run - the subcommand is real and does carry protection bypass, but its bespoke arg parser does not whitelist `--no-color`, so that flag reaches the real curl binary and kills it, and it would fail only AFTER the compile gate, the regression, R2 parity, schema parity and a 25-minute WebGL build; (2) the remote index file is never deleted before the fetch and the fetch's exit code is discarded, so a failed fetch plus a stale byte-identical file from an earlier run hashes itself and prints a GREEN marker for a deployment that was never contacted - precisely the false-pass class this ticket exists to prevent. Full verdict at the foot of this file.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1199 -> 1200 in the same edit)
**Silo:** Tooling / ops
**Ruling:** owner, 2026-08-25 - *"That should all be handled programmatically from command center or
via code."* This AMENDS `FOUNDATIONAL_RULINGS.md` section 8; read that amendment first.

---

⚠ **NAME COLLISION - this is NOT WO-1169.** `WO-1169 "The Command Center"` is a separate, live ticket (SPEC, Backend / money observability): the transaction log, the joined troubleshoot view, the admin stats/DB probes and promo-code authoring. **This ticket (1199) is Tooling / ops** - the programmatic ship-and-live-ops chain. Same name, one letter apart, different scopes; ⛔ check which one you pulled before starting.

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

### STOP THE STORE MIRROR - `command-centre -Store`, four columns

Owner, 2026-08-25: *"I should have a screen that reflects store at that moment in real time"* ->
*"I don't need the UI"* -> **"just the packs, MSRP and sale price and percentage."**

⭐ **That is the whole output. Do not add columns.**

⛔ **AND THE PRESENTATION IS NOT THE POINT.** Owner: *"The screen is stupid, the data is what I care
about."* The table is just a legible way to read four numbers. Add `-Json` emitting the same values as
an object, so the data can be diffed day over day, fed into a post, or compared against yesterday
without anyone re-typing it. ⚠ Same source either way - if the JSON and the table can ever disagree,
something is being computed twice.

    STORE  2026-08-25 14:52 UTC  production
    PACK                      MSRP     SALE     OFF
    hearth-spark             $4.99        -       -
    impulse-wood-medium      $2.99    $2.39     20%
    founders-vow            $49.99        -       -

⚠ **A pack with no SALE simply has no sale.** If a pack is not sellable to the viewer it comes back
with no price at all - that absence is visible in the table and needs no extra column to explain it.

#### The one rule that decides whether this helps or lies

**It renders from the LIVE quote endpoint - the same call the game makes.**

⛔ It must NOT read `packs.json`, `USD_ANCHORS`, or any local config. The moment it does, it becomes
another copy of a fact that already lives in three places, it drifts, and it confidently prints a
store that does not exist - this repo's dominant failure aimed at the surface the owner would trust
most.

⭐ **If the table and the game disagree, the table is WRONG BY CONSTRUCTION**, because both answers
came from the server. That property is the entire value.

#### Whose eyes it looks through

Prices are **per-wallet**: `walletAllowed` passes the owner unconditionally BEFORE
`MAINNET_SALES_ENABLED` is consulted, so her own view is the ONE view that cannot show her what a
player sees. Default to **no wallet**; accept an optional address to view as someone specific. The
header line says which.

#### It fails LOUD, never stale

⛔ Cannot reach the server -> print the failure, exit non-zero, print NO table. Never a cached or
locally-computed shelf. A harness that re-authored the panel it photographed cost a morning today by
exactly this mistake, and this would make the same error about money.

#### Acceptance

1. At `startsAt - 1 minute` it prints no SALE; run again at `startsAt + 1 minute` and the SALE and OFF
   columns are populated. ⭐ Prove the schedule with the clock, not by reading the config that set it.
2. Run with no wallet and with a non-owner address - the output reflects `MAINNET_SALES_ENABLED`
   truthfully rather than the owner's privileged view.
3. Killing the endpoint yields a visible failure and a non-zero exit, never a stale table.
4. ⛔ Grep proves it reads no local price source.
5. ASCII-only, and the OFF column is a NUMBER - never a colour, never a bar.

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

---

## ⛔ REVISION REQUIRED - 2026-08-25 (CLI lead, after a full static audit of the returned script)

**Returned artifact:** `tools/command-centre.ps1` (branch `codex/wo1199`, 255 lines), covering steps
1-8. ⛔ **Not safe to harvest and commit as it stands.** Full source evidence: `tmp/wo1199_verify.md`.

### The frame - and it is the point of this note

The lane proved **only the refusal path**: a deliberate missing-credential run refused with a named
step, a marker, a log, a reason and exit 20. That refusal fires in the first few statements
(`command-centre.ps1:98-105`), so **no step ever executed and no run log was ever written.**

⭐ **This is the repo's `prove-the-success-path-not-just-the-refusal` lesson, exactly.** A prior guard
shipped that refused correctly, aborted every good run, and exited 0 the whole time.
⛔ **Failure-only acceptance is not acceptance** - a refusal test exercises none of the machinery.

⚠ No blame in this: the refusal work is correct as far as it goes, and the refusal messages are good
ones. The gap is that they prove the first three statements of a 255-line chain.

A static audit now finds the success path **cannot succeed as written**, and worse, that it **can
print `COMMAND_CENTRE_OK` for a release that never went live.**

### The four blockers - all must be fixed

**B1 - `Invoke-Captured` discards everything after the first stderr line.**
`$ErrorActionPreference = 'Stop'` (`command-centre.ps1:19`) is in scope inside the function, so in
`& $Command *>&1 | ForEach-Object { $_.ToString() } | Tee-Object` (`:53`) the FIRST stderr record from
a native command becomes a **terminating** error; the `catch` (`:55-58`) keeps only that one line and
every later line, stdout included, is lost. **Probed, not theorised:** five stderr lines in, one line
out; the identical body with `'Continue'` captured all nine. ⛔ Affects every call site that shells a
binary - `:143` node, `:151` vercel inspect, `:185` vercel deploy, `:218` vercel promote, `:249`
vercel promote (rollback).

**B2 - the Vercel CLI writes ALL human output to stderr.** Confirmed at source in the installed
v56.4.0 bundle: `var output = new Output(process.stderr, ...)`
(`dist/chunks/chunk-OX7KI3LF.js:4674`), and on a non-TTY the spinner degrades to a plain stderr line
(`:4560-4566`). Only machine payloads reach stdout. `vercel inspect` fires
`spinner('Fetching deployment "..."')` BEFORE the JSON (`dist/commands-bulk.js:40584`).
⭐ **This is what makes B1 fatal rather than cosmetic: a fully credentialed, entirely correct run dies
at step 4 with `INVALID_INSPECT_JSON`.**

**B3 - ⛔⛔ THE DANGEROUS ONE. `vercel promote <preview> --yes` REBUILDS; it does not ship the artifact
that was inspected and byte-proven.** A preview has `target !== 'production'`, so `--yes` sets
`promoteByCreation = true` (`dist/commands-bulk.js:53433-53442`), auto-confirming the prompt that says
*"A new deployment will be built using your production environment"*. That path POSTs a **new**
deployment, prints `Successfully created new deployment of <project> at <url>`, and `return 0`
**without waiting** (`:53444-53463`). `Successfully` matches the step-6 regex (`:220`), so `STEP_6_OK`
prints; step 7 then probes the **OLD** production, gets its 200, and the chain prints
`COMMAND_CENTRE_OK`. ⭐ **A broken build passes the entire chain, and nothing rolls back** - the chain
has already exited 0. Corroborated independently by this repo's own captured record:
`OVERNIGHT_REPORT_2026-08-10.md:263`. The script's own comment at `:212-213` asserts the property the
CLI does not provide.

**B4 - step 5's byte-proof would fetch a Vercel LOGIN PAGE.** `:196-209` fetches
`"$previewUrl/index.html"` with no auth header and no bypass token; previews on this project sit
behind deployment protection (`OVERNIGHT_REPORT_2026-08-10.md:258-261` - an anonymous fetch returned
the login HTML and correctly reported MATCH=NO). Even with B1 and B2 fixed, a correct run refuses at
`INDEX_HASH_MISMATCH` - ⚠ and it does so **after a ~25-minute WebGL build**, so the feedback loop is
brutal.

### Also fix, in the same pass

- **The step-6/8 promotion regex is too weak.** `(?i)(promoted|promotion.*completed|success)` (`:220`,
  `:251`) is satisfied by `"Promotion has been queued and will begin when the active rolling release
  completes successfully."` (`dist/commands-bulk.js:53474-53476`, then `return 0`). The genuine
  completion string is `Success! <project> was promoted to <url> (<id>)` (`:53412`), emitted only after
  `promoteStatus` polls to `jobStatus === 'succeeded'`. ⛔ **Judge promotion by the OUTCOME, never by
  output prose.**
- **Step 8 proves the command ran, not that the rollback took effect.** `$productionHost` is never
  re-resolved after the rollback promote (`:249-255`). Close it by **POLLING**
  `vercel inspect $productionHost --format=json` until `.id -eq $rollbackId`, refusing on timeout.
  ⭐ **Polling, not a single check** - the alias does not flip synchronously. The same closure is owed
  to step 6: production content is never verified post-promote at all.
  ⚠ Related consequence of B1: a rollback that SUCCEEDED currently reports
  `COMMAND_CENTRE_REFUSED step=8 reason=ROLLBACK_PROMOTION_FAILED`, exit 28 - at 3am, mid bad release,
  telling the operator production is still broken when it has in fact been restored.
- **Step labels are reused three ways** - `step=5` for the `VERCEL_TOKEN` check (`:98-105`), the WebGL
  build (`:170`) and the preview deploy (`:183`); `step=3` twice (`:98-105`, `:141`). The marker text
  carries the real meaning, so a refusal line survives an incident - but the number misleads while
  someone is reading fast. **Note only; one comment line fixes it.**

### ⭐ CONFIRMED FINE - do not re-churn these

- ⭐ **No failure path writes an OK token into the log it is judged on.** All four markers clear:
  `COMPILE_GATE_OK` (`Assets/Editor/CompileGate.cs:57,60-65,131-137`), `SCHEMA_PARITY_OK`
  (`tools/schema-parity.mjs:185,199,204`), `R2_PARITY_OK` (`tools/r2_sync.py:401`,
  `tools/r2-ship.ps1:111`). The only residual is `tools/r2_sync.py:426`, which holds the literal OK
  token inside an argparse `help=` string - never written to a judged log with the arguments as they
  stand, recorded only because it is one flag-rename away.
- `.id` from `vercel inspect` IS the correct value to feed `vercel promote` (it takes
  `url|deploymentId`, `dist/commands-bulk.js:8343-8360`), so the **rollback target is right** - it is
  the verification that is missing, not the id. `--format=json`, `--no-color` and `--yes` are all
  valid flags here; no unknown-flag refusal.
- `auth_nonces` self-prunes that wallet's rows before insert (`api/_lib/wallet-auth.js:186-203`), so
  step 7 causes **no unbounded growth**; and the proof wallet is the Solana system program, whose
  private key does not exist, so the nonce it writes is unusable.
- **Judging by marker rather than exit code is CORRECT** per CLAUDE.md section 8. Keep it.
- ⛔ **The explicit UTF-16 decode of the R2 parity log is CORRECT** (`:137`, `-Utf16`) and was
  independently re-confirmed today: `Builds/r2-parity.log` really is UTF-16 and a plain grep returns
  zero hits on it. ⛔ **Do not "simplify" it.** ⚠ Related note, not a defect: there is no
  `#requires -Version` / edition guard, and under PowerShell 7 `Tee-Object` writes UTF-8 - so a green
  R2 parity would read `MARKER_ABSENT` under `pwsh`. One `#requires` line closes it.

### ⛔ ACCEPTANCE FOR THE REVISION - a refusal test is no longer sufficient evidence

Required, in writing, with the handback:

1. **A test proving `Invoke-Captured` returns ALL output** from a process that writes multiple stderr
   lines and then stdout. ⭐ **This is provable locally with a synthetic process - no Vercel, no
   credentials** - so there is no excuse for leaving it unproven.
2. **Evidence that the promoted artifact is the SAME one that was byte-proven**, or an explicit design
   change to a flow where that is structurally guaranteed. ⚠ **Propose the mechanism** rather than
   waiting for one to be dictated - but B3 must end up **structurally impossible, not merely
   detected.**
3. **Rollback verified by polling the alias to the expected id**, with the poll bounded and its
   timeout a **REFUSAL**.
4. ⛔ **State plainly which acceptance items remain OPS-OWNED and cannot be closed by the dev lane.**
   Acceptance items 1, 2, 3 and 6 above need the Unity gate, a real deploy + promote, and two induced
   live failures. Hand those back as a **named slice with the executor split written down** - do not
   silently leave them open and do not claim them.

⚠ Steps 1-3 of the chain (the gate half) are sound and are not in scope for this revision. Everything
that is wrong lives in the deploy half, steps 4-8.

---

## 2026-08-25 - WO-1199 REVISION 2 verdict: NEAR PASS, two one-line fixes

Source: completed verification, `tmp/wo1199_verify2.md` (read-only both trees, no `vercel` command run,
script never executed; Vercel behaviour read from the installed bundle v56.4.0).

### ⭐ What the lane got RIGHT - said first, and without hedging

- **B1 FIXED, and load-bearing.** `Invoke-Captured` sets `$ErrorActionPreference = 'Continue'`
  function-locally (`command-centre.ps1:59-60`), shadowing the script-scope `'Stop'` at `:21`.
  ⭐ REPRODUCED under a `'Stop'` harness: all 5 stderr lines plus the trailing stdout JSON survived
  (`LINECOUNT=6`), native exit code propagated (`RC2=7`), outer preference intact
  (`OUTER_PREF_AFTER=Stop`). ⭐ And a NEGATIVE CONTROL - the same body WITHOUT that one line captured
  exactly 1 line. Credit the method explicitly: a negative control is what turns "it passed" into
  "the fix is why it passed."
- **B3 FIXED STRUCTURALLY**, which is what the acceptance demanded. `--skip-domain` is rejected unless
  the target is production (`dist/commands/deploy/index.js:1345-1348`) and sets
  `autoAssignCustomDomains = false` (`:1566`); `promoteByCreation` is gated on
  `deployment.target !== "production"` (`dist/commands-bulk.js:53430-53442`), so ⛔ the rebuild branch is
  UNREACHABLE for this artifact - not detected, unreachable. Control falls through to
  `POST /v10/projects/<id>/promote/<deploymentId>` (`:53467`), a re-alias not a build. The prose
  success regex is gone; success is an alias-ID poll (`Wait-ProductionDeployment`, `:80-99`, call at
  `:278`). The identifier was traced build -> proof -> promote (`:228-231` -> `:236-238` -> `:251` ->
  `:276` -> `:278`): the proven artifact and the promoted artifact are the same deployment.
- **B2 NEUTRALISED, not eliminated** - state the distinction honestly. The CLI still writes every human
  line to stderr (`dist/chunks/chunk-OX7KI3LF.js:4674`); the capture boundary stops it being fatal.
  One prose parse survives: the candidate URL regex (`:227-231`).
- **Polling FIXED.** Bounded at a 180s deadline (`-AliasTimeoutSec 180`, `:17`); timeout is a named
  refusal in step 6 (`ALIAS_POLL_TIMEOUT`, `:278-280`) and step 8 (`ROLLBACK_ALIAS_POLL_TIMEOUT`,
  exit 28, `:313`); and a successful rollback still exits non-zero
  (`POST_DEPLOY_DB_PROOF_FAILED_ROLLED_BACK`, 27, `:310-311`).
- **Ops handback CORRECT.** Items 1, 2, 3, 6 named and matching the WO acceptance list
  (`WORK_ORDER_1199_the_command_centre.md:249-259`); 4 and 5 correctly NOT claimed as ops.
- **Regressions all hold.** No OK token on a failure path into a judged log (`Write-Run` `:31-35` writes
  only to the never-judged `Builds/command-centre.log`); the `-Utf16` R2 decode preserved (`:179` ->
  `:118`); marker-not-exit-code preserved; ASCII 0, NUL 0, braces 55/55, parens 83/83, parse clean.
  ⭐ The verifier also probed the NEW UTF-16 exposure - `Tee-Object` writes the schema log and it is read
  WITHOUT `-Utf16` (`:186`) - and found `Tee-Object` emits a BOM (`FIRSTBYTES=FF FE 53 00 ...`) which
  .NET detects, so `PLAIN_MATCH=True`. ⛔ Recorded so nobody later "fixes" it into a bug.

### THE TWO FIXES REQUIRED - both one-liners

**FIX 1 - ⛔ BLOCKER. `vercel curl` will refuse on EVERY run.** The subcommand is real and genuinely
carries deployment-protection bypass (`curlCommand`, `dist/chunks/chunk-2KNVJ7ET.js:2589-2650`;
`getOrCreateDeploymentProtectionToken`, `dist/commands-bulk.js:15341-15360`, which even auto-creates the
secret). But it uses a bespoke arg parser - `parseCurlLikeArgs`, `dist/commands-bulk.js:15419-15476`,
whitelists `VC_STRING_FLAGS = {--deployment, --protection-bypass}` and
`VC_BOOLEAN_FLAGS = {--yes, --help, --trace, --json}` (`:15403-15404`) - which does NOT contain
`--no-color`, so that flag is forwarded to the real `curl` binary, which dies with
`curl: option --no-color: is unknown`, `EXIT=2`, no output file. ⭐ Proven by replaying the CLI's own
parser in node (`toolFlags = ["--no-color","--silent","--show-error","--output","<path>"]`) and then
probing the real curl binary. ⚠ Step 5 (`command-centre.ps1:251`) would refuse on every run, AFTER the
compile gate, the regression, R2 parity, schema parity and the ~25-minute WebGL build.
**Fix: drop `--no-color` from the `vercel curl` invocation** (it is valid on `deploy`/`inspect`/`promote`,
which go through `parseArguments`; `curl` is the one command that bypasses that merge).

**FIX 2 - ⚠ HIGH, a FALSE PASS.** `$remoteIndex` (`:243`) is never deleted before the fetch, and the
fetch's exit code is `| Out-Null`'d (`:250-253`); `:254` reads the file unconditionally. So a FAILED
fetch plus a stale byte-identical file from an earlier run **hashes itself** and prints
`STEP_5_OK marker=CANDIDATE_CONTENT_MATCH` for a deployment that was never contacted. ⛔ That is the
exact class this whole ticket exists to prevent - a green marker for something that did not happen.
**Fix: `Remove-Item $remoteIndex -Force -ErrorAction SilentlyContinue` before the fetch AND judge the
fetch's exit code.**

### ALSO NOTED - lane's judgement, not blocking

- MEDIUM: the candidate URL is still recovered from prose (`:227-231`,
  `'https://[a-z0-9-]+\.vercel\.app'` + `Select-Object -Last 1`), and that pattern can match the
  production alias. Safe today; harden by taking the URL from stdout only.
- MEDIUM: the step-6 poll timeout refuses WITHOUT rolling back, while a queued promotion may still land
  (the `202` path, `dist/commands-bulk.js:53473-53477`).
- LOW: `WEBGL_BUILD_OK` is declared (`:208`) but never checked (`:210-216` judge artefact + log
  freshness only); `vercel curl` silently creates a project-level bypass secret
  (`dist/commands-bulk.js:15355-15359`) - ⚠ worth the owner knowing; the new capture test is wired to no
  gate and its `-LibraryOnly` dot-source runs `:22-29` before the `:131` early return, truncating
  `Builds\command-centre.log`.

### Close

⛔ Still NOT harvestable, but the objection is now NARROW. Two fixes close the static half.
⛔ The success path remains ENTIRELY UNEXECUTED - ⭐ and FIX 1 is precisely the argument for why
acceptance items 1/2/3/6 still require the live ops run: a defect that only appears at runtime, on a step
that costs 25 minutes to reach, is exactly what a static audit cannot promise to catch twice.
