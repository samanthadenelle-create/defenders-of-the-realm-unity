# WORK ORDER 1244 - The Command Center CONSOLE: build the surface WO-1169 specced

**Status:** FIXED 2026-08-27 - the console is built and gated. READ half: `api/admin/stats.js?view=ops` (additive, SELECT-only) returning toggles, promos, player reports and ops history; proven HTTP 200 against the LIVE database. WRITE half: `api/admin/ops.js`, a DIFFERENT file behind a SECOND secret (`ADMIN_OPS_KEY`), POST-only, no CORS, fail-CLOSED when that key is unset. No refund, grant or edit of `purchase_quotes`/`purchase_entitlements` exists anywhere in it, and a test asserts the table names never appear. Phone-first ASCII page, no framework, no build step, key held in memory only. 41 tests, 25 mutations proven RED; the lead independently re-verified the second-key mutation (1 fail, restored 41/41). Whole JS suite 136/136, SCHEMA_PARITY_OK 19 tables.

> ⛔ **ONE OWNER ACTION BLOCKS THE FELT-VERIFY: `ADMIN_OPS_KEY` IS NOT SET ON THE DEPLOYMENT.** Until it is, every write answers `OPS_WRITE_NOT_CONFIGURED` and says so with the remedy on screen. Reads work now; flipping a toggle from the phone (acceptance 5) cannot work until that env var exists. It must be a DIFFERENT value from `ADMIN_DASH_KEY` - a second key that equals the first is one key.
>
> **Acceptance 2 (phone-width screenshot) is the owner's.** Headless Edge on Windows enforces a ~500px minimum window, so no shot taken here is a true 390px viewport. The gate page is proven to render (`proof/img/wo1244-console-gate.png`) and `.gate{max-width:440px}` is correct, but a real phone is the only honest check.
**Silo:** Backend (`api/`) + admin console surface
**Severity:** P1 for operations. The game is LIVE and takes REAL MONEY as of 2026-08-27, and there is
still no console. Every pillar has working backend and no way to look at it.
**Origin:** Owner, 2026-08-27: *"i want a seperate ticket as a complete command center as i
envisioned in the docs"*.

---

## This ticket BUILDS what WO-1169 SPECCED. Read WO-1169 first.

`WorkOrders/WORK_ORDER_1169_command_center.md` is the vision and stays the authority on WHAT and WHY.
It is a **SPEC**; this is the build. Do not re-derive its findings - they were verified at source.

Owner's original framing (2026-08-24): *"I wanna really start thinking about how we set up a command
center. I think we have a structure in place but we need to be able to transaction log the
troubleshoot things. See tickets maybe even push promos."*

⭐ **Her read was right: this is NOT greenfield.** Every pillar has working backend today. What is
missing is a SURFACE.

## ⚠ NAME COLLISION - three things share this name. Keep them straight.
- **WO-1169** - the SPEC (money observability). The vision.
- **WO-1199** / `tools/command-centre.ps1` - the DEPLOY CHAIN. A PowerShell script that gates,
  promotes and rolls back. Unrelated to this console except that both are "the command centre" in
  conversation.
- **WO-1244** (this) - the CONSOLE.

## The pillars

| Pillar | Backend that exists | Surface today |
|---|---|---|
| 1. Transaction log | `purchase_quotes`, `purchase_entitlements`, `/purchases/*` | ⛔ none |
| 2. Troubleshoot | `bug_reports`, `analytics_events` (87k+ rows), `/bug-report`, `/trace`, F8 `break-log.jsonl` | raw rows via `admin/db.js`, `tools/db-viewer/index.html` |
| 3. Tickets | `WorkOrders/*.md` -> `BOARD.html` | dev tickets ONLY - no player-issue view |
| 4. Promos | `promo_codes`, `promo_redemptions`, `/promo/redeem` | ⛔ none - codes inserted BY HAND |
| 5. **Operator toggles** | **WO-1243** (in flight) | this console is the intended home |

⭐ **WO-1169 section 3 is already DONE** (`4f8c2f23d` + `ecbd5047a`): the purchase tables are in
`api/admin/db.js`'s probe list and `stats.js` has a purchases view. The DATA is reachable. Build the
surface on top; do not redo it.

## ⛔ THE READ/WRITE BOUNDARY - the load-bearing architectural rule

WO-1169 states it and it is not negotiable:

> **READ-ONLY IS THE CONTRACT, NOT A PHASE.** `api/admin/db.js` and `stats.js` are read-only BY
> CONSTRUCTION - every statement a SELECT with a hard LIMIT.

So this console has **two distinct halves and they must not be merged**:
- **The READ half** (pillars 1-3) goes through the existing read-only endpoints. Do NOT add a write
  path to `db.js` or `stats.js` for refunds, grants or edits. If a write surface is ever wanted
  there, it is separate and separately audited.
- **The WRITE half** (pillars 4-5: authoring a promo code, flipping a toggle) is a NEW, narrowly
  scoped, audited endpoint. Every write is attributable and timestamped.

⚠ A console that can both read the money tables and write to them is one bug away from being the
worst thing in the repo. Keep the halves apart at the endpoint level, not merely in the UI.

## ⭐ IT MUST WORK FROM A PHONE

The owner will see an exploit or a failed purchase on her phone, not at a desk. WO-1243's purpose is
containment - *"if we see someone finds a hack, we seal that area and patch"* - and a control she
cannot reach in seconds is not a control.
- A plain HTML page served from `api/`, gated by `ADMIN_DASH_KEY`. **No framework, no build step.**
- Responsive: it is used one-handed on a phone, in a hurry.
- ⛔ The owner is **red/green colourblind**. State - never hue alone. A toggle that is off must SAY
  off.

## What each pillar needs

1. **Transaction log** - settled purchases from `purchase_entitlements` (the SERVER's record), with
   the quote->settle funnel. ⭐ WO-1169 section 2's finding is the point: the analytics view counts
   what the CLIENT said, the entitlement table records what was actually PAID. **Show both and
   SURFACE THE DISAGREEMENT** - a client-completed purchase with no entitlement is a grant given
   without settlement, and that row is the alert. ⛔ Do NOT blend them into one number and do NOT
   reconcile silently.
2. **Troubleshoot** - bug reports and traces joined to the player, so a report is answerable without
   a raw-row hunt. The F8 device bridge (WO-1227) now delivers captures; they belong here too.
3. **Tickets** - `BOARD.html` already covers DEV tickets. What is missing is PLAYER issues. Link the
   two rather than building a second board.
4. **Promos** - an authoring surface. Codes are inserted by hand today, which is both slow and the
   kind of thing that gets a decimal wrong at 11pm.
5. **Operator toggles** - WO-1243's six switches (farming/raiding/arena/dungeons/store/server) with
   their message field, current state, and WHEN each was last flipped.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. ⭐ **A screenshot of the console open ON A PHONE-WIDTH VIEWPORT**, showing at least the toggles
   and the transaction log. It is a phone tool; proving it on a desktop proves the wrong thing.
3. A regression asserting the read/write split: the read endpoints remain SELECT-only, and the write
   endpoint is separately gated and attributable. Prove RED first (WO-1138).
4. ⛔ Never log or render a wallet, an email, or a real name. Player id is enough.
5. Owner felt-verifies by flipping a toggle from her phone and seeing it take effect.

## What NOT to touch

- ⛔ `api/admin/db.js` / `stats.js` read-only contract. Additive reads only.
- ⛔ `tools/command-centre.ps1` - that is the DEPLOY CHAIN (WO-1199), a different thing that happens
  to share the name.
- ⛔ WO-1243's toggle semantics (fail-open, no device cache). This console is a surface ONTO them,
  not a second authority over them.
