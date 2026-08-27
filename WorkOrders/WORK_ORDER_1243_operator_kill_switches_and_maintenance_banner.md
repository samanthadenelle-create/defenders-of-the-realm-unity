# WORK ORDER 1243 - Operator kill switches: toggle a broken area off, and TELL the players

**Status:** FIXED 2026-08-27 - six DB-driven operator kill switches, end to end. SERVER: `maintenance_toggles` (5 columns, 6 rows seeded in the LIVE DB), `api/_lib/maintenance.js`, `GET /api/maintenance`, enforced server-side in `purchases/quote.js` (store), `leaderboard/submit.js` (arena) and `game/save.js` (server). CLIENT: `DeNelle.Core.Ops` (MaintenanceService / MaintenanceCatalog / MaintenanceBannerDriver) with a real REFUSAL at all five area seams - arena, dungeons, farming, raids, store - plus the rolling `MAINTENANCE ON <AREA>` banner. Fail-OPEN on every failure path per the owner ruling, proven by `MaintenanceTogglesRegression` (registered in DataRegression). Operator control via `tools/maintenance-toggle.mjs` + `tools/command-centre.ps1`. Gated `COMPILE_GATE_OK` + `REGRESSION_OK 308/308 suites`.

> **REMAINING: owner felt-verify by flipping a toggle** (the PO closes, section 13).
>
> **KNOWN LIMIT, stated rather than hidden:** only THREE of the six switches have server-side teeth - `store`, `arena` and `server`. `farming` / `raiding` / `dungeons` have no server seam of their own: they are client-simulated and reach the backend only inside the opaque save blob. For those three the seal is a CLIENT COURTESY GATE (defeatable by a modified client) and `server` is the real lever. This is a property of where those systems live, not a gap in this ticket - closing it means giving those pillars a server seam first.
**Silo:** Backend (`api/`) + HUD banner + command centre tooling
**Severity:** P1 for operations. The game is LIVE and takes real money; today there is no way to
close a broken area without shipping a build.
**Origin:** Owner ruling 2026-08-27.

---

## The ruling, in her words

> *"those should be a toggle by a toggle in command center - controlled by table to flag off key
> components or entire site, leaving a maintance message"*
> *"the areas should be farming broken, raidingf broken, arena broken dungeons broken store broken
> or maintanace window toggles driven from the db"*
> *"there should be a rolling banner wit the notice to all players mainatance on farming, or
> maintance on raids"*
> *"toggle raiding (if raids are broken) toggle dungeons(if broken) store(if broken) or server"*

## What this is, and what it is NOT

**It is an OPERATOR capability.** The owner decides an area is broken and closes it. It is not an
automatic response to infrastructure health, and nothing in it should try to detect brokenness.

## The six toggles, DB-driven

| Toggle | Closes |
|---|---|
| `farming` | harvesting / collectors |
| `raiding` | raids |
| `arena` | arena |
| `dungeons` | dungeon entry |
| `store` | the store / purchase surface |
| `server` | THE WHOLE GAME - full maintenance window |

Each row carries at minimum: the toggle id, on/off, and an operator-authored message.

## The banner

When a toggle is OFF, **a rolling banner tells EVERY player**, naming the area:
`MAINTENANCE ON FARMING`, `MAINTENANCE ON RAIDS`. Not a silent refusal - a player who taps a closed
area must already know why.

⚠ **A closed area must REFUSE, not merely warn.** A banner without an actual gate is decoration, and
the whole point is that the broken thing stops being reachable.

## ⭐ NO CACHING - owner ruled

Every check is live against the server. No last-known-good on the device.
⚠ The owner was shown the consequence and chose it anyway: without a cache, an offline player falls
back to the default. Do NOT add a cache "to be safe" - it was considered and rejected.

## ⭐ THE FAILURE MODE - OWNER-CONFIRMED 2026-08-27

**An unreachable / timing-out / malformed table means EVERYTHING STAYS ON.** Fail-OPEN.

Reasoning, recorded so a future seat does not "correct" it into consistency with the dungeon rule:
- ⛔ **This is the OPPOSITE of the WO-1223 dungeon-portal ruling, ON PURPOSE.** There, absence must
  not GRANT access to content - so it fails closed. Here, absence must not DENY access to the whole
  game - so it fails open. The two rules are about different things: correctness vs availability.
- With no cache (owner-ruled above), EVERY check hits the server. Fail-closed would mean a 30-second
  database blip shows a maintenance screen to every player of a live, paying game, for an outage
  that is not happening.
- A toggle closing an area must always be a DELIBERATE ACT the owner took, never an accident of the
  network.

**OWNER CONFIRMED IT, and her reason is the stronger one.** Verbatim: *"correct cause i cannot help
if server is unreachable"*.

The argument is about CAPABILITY, not blast radius: if the server is unreachable she cannot fix
anything, cannot flip a toggle, and cannot author a message - so closing the game buys nothing and
costs every player their session. A shutdown is only useful when she can DO something, and that is
exactly when the table is readable.

**The accepted trade, stated honestly:** an emergency shutdown is impossible while the DB is also
unreachable. The owner has accepted this knowingly - it is not an oversight to be "fixed" later.

⚠ If the owner prefers the store to fail closed (it is the money surface), that is a one-line change
and a legitimate ruling - raise it, do not assume it.

## Required

1. **A table in `api/`** (this repo - Vercel serverless, git-tracked here) plus a read endpoint.
   ⛔ `api/schema.sql`'s seeds use `ON CONFLICT DO NOTHING`, which does NOT back-fill an
   already-provisioned database. That trap shut two dungeons in production this week (WO-1223). If
   rows must exist in the live DB, SAY SO - the lead writes them.
2. **A client gate per area** that actually refuses entry, plus the rolling banner.
3. **Command-centre control** - `tools/command-centre.ps1` is the operator surface (WO-1199). Adding
   the toggle commands there is the point of "a toggle in command center".
4. **Instrumentation** (section 12): every refusal traces WHICH toggle closed it. A player reporting
   "raids do nothing" must be triageable from a log line, not a theory.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. A regression proving: each toggle closes ONLY its own area; `server` closes everything; an
   unreachable table leaves everything ON (the fail-open ruling); and a closed area REFUSES rather
   than only warning. Prove each RED first (WO-1138).
3. ASCII-only strings; no meaning by colour alone (the owner is red/green colourblind) - the banner
   must read as maintenance from its WORDS.
4. Owner felt-verifies by flipping a toggle and seeing the banner.

## What NOT to touch

- ⛔ The WO-1223 dungeon-status fail-CLOSED gating. It is a different system with the opposite
  correct default. Do not unify them.
- ⛔ Do not add device caching. Owner-ruled.
- ⛔ Do not build brokenness DETECTION. The operator decides.
