# WORK ORDER 1160 — `api/` is committed but NOT deployed, and it has now DIVERGED from a shipped APK

**Status:** FIXED 2026-08-24 (`e2e07f1c0`, deployment `dpl_Gvyu7vQxZwMyM73bp7WjXC7xgnQd`; a second promotion `2c3ed6c24` followed with WO-1177) — awaiting owner felt-verify/close (PO closes, §13). *(Status audit 2026-08-24 — the block is REMOVED and it was PROBED, not assumed: `POST /api/purchases/quote` and `POST /api/auth/session` both now answer **400** (route exists, rejects an empty body) where §1 recorded **404**. The four owner-set post-deploy requirements: quote health check ✔ · session health check ✔ · deployed commit hash ✔ · purchase-quote smoke test — the WO-1177 migration verification read `quotes_still_present 1 / settled_quotes 1` against production, i.e. a real quote row issued AND settled; ⚠ if she wants a fresh device-side smoke test instead, that call is hers. Body unchanged.)* Prior line: BLOCKED — ⭐ **owner APPROVED the promotion 2026-08-24 (batch 2, ruling 2), ONE deployment only** (⛔ *"This does not create standing production-deploy authority"*). Still BLOCKED because the four post-deploy requirements she set are unmet: `/api/purchases/quote` health check · `/api/auth/session` health check · one purchase-quote smoke test · **capture the deployed commit hash**. A deploy of HEAD is running; the unblock is that evidence posted here. *(Prior line preserved:)* BLOCKED ON OWNER — the fix is a **production deploy of `api/`**, and web promotion is explicitly the owner's call (START_HERE §4: *"Web deploys stay preview-only … NEVER `--prod` — promotion is the owner's"*). No code change is required. ⚠ The Vercel CLI is **not installed on this machine** (`npm i -g vercel`).

**⚠ UPDATED 2026-08-24 — the ticket's scope GREW; it is no longer only "two routes are missing."** Eleven paths under `api/` + `site/` changed today and are undeployed, and one of them **repriced a live SKU**, so production is now in open disagreement with an APK that is on a device. **§7–§13 are tonight's material; §1–§6 are the original 08-23 finding and stand unchanged.**

**Minted:** 2026-08-23 (CLI) — banner bumped 1160 → 1161 in the same edit
**Priority:** ⛔ **P0 — it is the only thing between the go-live build and a working sale.**
**Provenance:** owner felt-test of the WO-1159 go-live APK: *"look at screenshot i dont see that"* — device screencap shows every Night Market card reading **"Price unavailable"**.

---

## 1. The proving data — a probe, not a theory

The game hardcodes `BackendRequestSigner.BackendBase = "https://defenders-of-the-realm-v2.vercel.app"`
(`Assets/_Modules/Core/Web3/BackendRequestSigner.cs:50`). Probed directly:

| endpoint | production | verdict |
|---|---|---|
| `POST /api/purchases/quote` | **404** — body `NOT_FOUND`, Vercel edge (`cle1::v66fk-…`) | **route does not exist** |
| `POST /api/auth/session` | **404** — same | **route does not exist** |
| `POST /api/auth/nonce` | 400 (rejects empty body) | exists |
| `POST /api/purchases/verify` | 400 (rejects empty body) | exists |

GET returns 404 on both new routes too, so this is a **missing route, not a method rejection**.
The two endpoints that answer are the OLD ones. **The two added by WO-1158 and WO-1157 today are
absent from production.** This matches the long-standing canon note that `api/` has been deployed to
**PREVIEW only** while the client hardcodes the prod domain.

## 2. What it causes — one visible symptom, one INVISIBLE one

**(a) Visible, and correct behaviour.** `PurchaseQuoteService` gets a 404, so no price is issued;
`PackDef.AmountFor(Skr)` returns **zero**; the card renders the words **"Price unavailable"** beside
the local USD anchor (`~ $2.99`), and the right rail reads *"Price unavailable right now. Nothing has
been charged; reopen the store to retry."* **This is WO-1158's fail-closed path working exactly as
designed** — the client refuses to invent a number rather than charge a made-up one. Nothing is
broken on the client. **No purchase is possible, so the quote can never be verified on this build.**

**(b) INVISIBLE, and the one nobody would have caught.** `/api/auth/session` is also 404, so
`BackendRequestSigner.TryAttachSession` fails and falls back to **signing every call individually** —
by design (*"FALLBACK, NOT REPLACEMENT"*, `BackendRequestSigner.cs:180`). So **WO-1157's one-prompt
fix is silently inert in production**: the wallet still asks three times. Had the quote endpoint been
deployed and the session endpoint not, the three prompts would have read as *"WO-1157 failed"* and
sent someone to debug working client code.

## 3. ⭐ WHY THE CANARY DID NOT CATCH THIS — the transferable lesson

The owner ran the mainnet 1-SKR canary **twice, successfully**, before this build. Both canaries
answer **`pinned: true` with no quote row and no rate**, and they settle against `/verify` — **which
exists in production.** So the canary exercised the transport, the wallet, the mint, the recipient,
the decimals and the entitlement seam, and **never once touched `/api/purchases/quote`.**

> **A proof-of-rail cannot prove a path it structurally bypasses.** This was written into WO-1158 and
> stated before the test — *"the canaries do not test the quote"* — and here is the material proof:
> the quote endpoint did not exist at all, and both canaries still passed.

The same shape as CLAUDE.md §16 (an unpushed R2 bundle installs, launches and plays) and WO-1138 (a
gate that reports success while asserting nothing). **Success on an adjacent path is not evidence.**

## 4. The fix

**Deploy `api/` to production.** No code change. `api/` is git-tracked in this repo, so a production
deploy from the repo root ships all four routes together.

⚠ Two constraints, both real:
1. **Promotion is the owner's call**, never the agent's (START_HERE §4). Preview deploys are the
   agent's ceiling.
2. **The Vercel CLI is not installed on this machine** — `npm i -g vercel` first.

⚠ And one trap recorded in canon: **deploy from the REPO ROOT.** `Builds/WebGL/` carries its own
`.vercel/project.json` pointing at a DIFFERENT Vercel project (`defenders-webgl`), so a deploy run
from there lands in the wrong project.

## 5. Acceptance — how to know it actually worked

Re-run the probe. **Judge by the HTTP code, not by the deploy log's own success claim** (this repo's
standing rule: markers and measurements, never a runner's self-report):

- `POST /api/purchases/quote` must stop returning 404. A **400/401 is SUCCESS here** — it means the
  route exists and is rejecting an unsigned empty body, which is correct.
- `POST /api/auth/session` likewise.
- Then, on device: the Night Market cards must show **real SKR digits** instead of "Price
  unavailable", and the first purchase of a session must show **two** wallet prompts, not three.

## 6. Related, found in the same screenshot — NOT part of this ticket

The store's legal band prints **`REWARDS DISTRIBUTOR 2JRm…nmNi`** directly beneath the claim
**"0% STORE FEE — EVERY PAYMENT REACHES THE REALM"**. `WalletRegistry.cs:16-18` states that wallet is
*"shown for transparency. **NOT a payment destination**"* — payments land in the Squads treasury vault
`9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj`. So a storefront that now takes **real money** is
printing a payment-adjacent claim next to the **wrong wallet**. Harmless while nothing could be
bought; a truthfulness problem now. **Owner ruling owed** (drop it, or swap it for the real
recipient). Rendered at `PackStore.cs:604` via `StoreLegalFooter.Build`.

---

# ⚠ 2026-08-24 UPDATE — the divergence, not just the absence

## 7. ⭐ THE HEADLINE: the mirror law is BROKEN ACROSS THE WIRE — `hearth-spark` is $4.99 in the client and $1.99 on the server

`api/_lib/purchase-catalog.js:64-68` carries the law in its own words:

> ⚠ MIRROR LAW: this table must equal the `pricing.usd` of the canonical client
> authoring EXACTLY. test/purchases.quote.test.js proves it on every run. **If the
> two ever disagree, the SERVER's figure is what the player is charged against**
> and what the card must display (§5: two prices on one screen is worse than a
> stale one).

**The mirror is green in the repo and BROKEN in production**, because only one half of it has shipped:

| half | value | where |
|---|---|---|
| client authoring (in the APK built tonight) | **$4.99** | `Assets/Resources/Data/Canonical/packs.json` + `Assets/StreamingAssets/Data/Canonical/packs.json`, both `pricing.usd: 4.99` — verified at source, byte-equal twins |
| server table, working tree (**undeployed**) | **$4.99** | `api/_lib/purchase-catalog.js:84` — `'hearth-spark': 4.99` |
| server table, **what production is actually running** | **$1.99** | the pre-`6bb61a810` value. WO-1069 repriced all three copies in one commit; only the client half reached a build |

So the standing consequence quoted above fires literally: **the player is shown $4.99 and quoted against $1.99.**
The reprice was deliberate — `packs.json:55` `_hiddenReason`: *"WO-1069 2026-08-24: repriced 1.99 -> 4.99 to stop
it dominating impulse-wood-small"* — which makes the **undeployed server the stale half**, not the client.

⚠ **One precision, so the ticket is not overstated.** §1's probe found `/api/purchases/quote` returning **404**
in production, so today the deployed backend does not answer a quote *at all* — the $1.99 is what the last
deployed copy of `purchase-catalog.js` carries (confirmed at `6bb61a810^:api/_lib/purchase-catalog.js:84`), not
a figure currently being served. **That is the trap, not a reprieve:** the moment a deploy lands, whichever
copy it carries becomes the charging authority silently and with no client-side disagreement signal. A
promotion of anything older than `6bb61a810` charges $1.99 against a $4.99 card. Deploy **HEAD**, and prove the
anchor per §11.1 rather than assuming the deploy carried it.

⚠ **The in-repo mirror test cannot see this.** `test/purchases.quote.test.js` compares two files in one tree.
It proves the *authoring* agrees and is structurally blind to whether the server half was ever deployed. Same
shape as §3 above: **a check that structurally bypasses the failure mode passes while the failure is live.**

## 8. The verified undeployed file list

Derived at source, not recalled:

```
git log --since="2026-08-24 00:00" --name-only --format="" -- api/ site/ | sort -u
```

| file | commit(s) | what is stranded |
|---|---|---|
| `api/_lib/purchase-catalog.js` | `6bb61a810` | **the `hearth-spark` reprice — §7** |
| `api/bug-report.js` | `5e4265d91`, `05f14790b` | the `wallet` column write path + the cascade instrumentation. ⚠ **The database was rebuilt tonight and accepted `report_id 1`, but the deployed endpoint is still the OLD code** — so `wallet` stays NULL and the admin `bugreports` view stays broken until this deploys. This is the PROD-017 surface. |
| `api/game/save.js` | `3b66c7d5b` | *"NO WALLET SAVE HAS EVER BEEN WRITTEN — the raw-body guard did not know sessions exist."* The fix exists in the tree only. |
| `api/promo/redeem.js` | `3b66c7d5b` | same raw-body / session guard |
| `api/referral/claim.js` | `3b66c7d5b` | same raw-body / session guard |
| `api/admin/db.js` | `4f8c2f23d` | the purchase tables were invisible to every console; adds the money views |
| `api/admin/stats.js` | `ecbd5047a` | SERVER-TRUTH purchase stats — the deployed view counts what the **client** claimed |
| `api/admin/schema-shape.js` | `936da0c3b` | the schema-parity endpoint |
| `api/auth/session.js` | `b605b1521` | three swallowed catches now named — **and this is the route §1 proved returns 404 in production**, so anyone debugging a 500 here is debugging a route that does not exist |
| `api/schema.sql` | `5e4265d91`, `4c939a023` | schema of record — documentation, **not a runner** (§10) |
| `site/admin.html` | `5e4265d91`, `05f14790b`, `55bb991a4` | the console front-end for the views above |

*(Eleven paths: ten under `api/`, one under `site/`. Two of the ten — `schema.sql` and the `_lib` table — are
not routes; the other eight are.)*

## 9. ⛔ THE ORDERING CONSTRAINT — deploy BEFORE anyone transacts

**`/api/purchases/verify` runs AFTER the transfer settles.** The chain settles first, always: `verify.js:267`
logs `purchase_quote_expired_after_payment`, i.e. the endpoint learns about the fault *after* the money moved,
and `:41` says so in player-facing words — *"The price quote had expired by the time this payment settled."*

So **a price or schema fault on that path is discovered with the money already gone, and there is no refund
route on an SPL transfer.** That is the entire sequencing argument: the deploy must land **before the next real
transaction**, not after the next bug report about one.

## 10. ⚠ THE NEXT 22-DAY GAP IS ALREADY WRITTEN AND UNRUN

`tmp/neon-migration-wo1177-discount.sql` exists on disk, is idempotent (`ADD COLUMN IF NOT EXISTS` /
`CREATE INDEX IF NOT EXISTS`), and **has not been run.** It adds `purchase_quotes.discount_bps` +
`discount_reason` and their partial index. Its own header names the hazard:

> this repo has NO MIGRATION RUNNER — a migration is a human running a file. **PROD-017 exists because a
> reconcile authored on 2026-08-02 was committed and never reached the database, and nobody noticed for 22
> DAYS.** A second forgettable file is the failure mode, not the fix.

**If WO-1177's code lands before this deploy, those two columns are the next gap.** Run the migration in the
same cut as the deploy, and prove it with that file's §5 — a real discounted quote read back — never with a
schema match. ⛔ It is an **ALTER, never a rebuild**: `purchase_quotes` holds the owner's settled mainnet
canary row and must not become empty.

## 11. Acceptance — evidence, never action

⛔ *"Deployed successfully"* is not acceptance. This repo's standing rule is markers and measurements, never a
runner's self-report. All three must be **observed**:

1. **The quote carries the new anchor.** A deployed quote for `hearth-spark` returns the **$4.99** USD anchor,
   not $1.99, matching `packs.json`. This also re-closes §5's original probe: a 400/401 on an unsigned body
   proves the route *exists*; a real signed quote proves it is running **today's** table.
2. **A real bug submission lands with a non-NULL `wallet`.** Not a schema check — an actual in-game/F8 report,
   then the row read back showing the server-verified wallet in the `wallet` column.
3. **`/api/admin/db?view=bugreports` returns rows instead of a 500.**

## 12. ⛔ SCOPE HONESTY — do NOT argue this from live money exposure

Nothing here is money-facing yet, and this ticket must not pretend otherwise:

- **`MAINNET_SALES_ENABLED` gates every wallet but the owner's** — `api/_lib/purchase-catalog.js:178`, an ENV
  switch that is not on.
- **No player has ever completed a purchase.** The only settled row is the owner's own chain-confirmed canary
  (391 SKR), named in the migration file's header.

**The real argument is narrower and sufficient: the client and the server disagree RIGHT NOW, and the APK
carrying the client half is already on a device.** A divergence does not become a defect on the day someone
pays — it is already one, and §9 is the reason the day someone pays is the worst possible day to find it.

## 13. Ticket hygiene — why this is an UPDATE and not a new number

This surface is already owned by WO-1160 (*"`api/` has never been promoted to production"*). Tonight's finding
is the same root with a larger blast radius, so it lands **here**. A second ticket on one surface is the
duplicate-of-record problem PROD-016 already demonstrates. **No number was minted and the
`CLI_LANES_WO_NUMBERS.md` banner was not bumped.**

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 2: **promotion APPROVED — ONE deployment only.**

**Recorded by the UI seat from `OWNER_RULINGS_OWED_2.md` §2. Do not re-decide.**

⛔ **Owner, verbatim:** *"This does not create standing production-deploy authority."* `START_HERE` §4
stands intact — web deploys remain preview-only, and the next `--prod` needs its own word from her.
This is a **per-deploy** approval for this one promotion of `api/`, nothing wider.

### ⛔ The FOUR post-deploy requirements she set — all four are owed as evidence

1. **`/api/purchases/quote` health check** — must no longer 404 in production.
2. **`/api/auth/session` health check** — must no longer 404 in production (this is the one whose
   absence made the WO-1157 one-prompt fix silently inert).
3. **One purchase-quote smoke test** — an actual quote round-trip, not just a 200.
4. ⭐ **Capture the DEPLOYED COMMIT HASH.** Her reasoning, worth keeping:
   > *"large reduction in future 'which build is actually live?' archaeology"*

⚠ **STATUS STAYS `BLOCKED` UNTIL THE LEAD POSTS THAT EVIDENCE.** A deploy of HEAD is running at the
time of writing; approval is not completion. The unblock is the four checks above, posted in this
ticket with the commit hash.
