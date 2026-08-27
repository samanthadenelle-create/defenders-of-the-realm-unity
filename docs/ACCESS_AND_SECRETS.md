# ACCESS AND SECRETS — what is public, what is secret, and how to share it safely

**Status:** LIVE canon. Written 2026-08-27.
**Why this exists:** other AI seats keep reporting that they "cannot access the prod URL" or the
backend. **Most of what they think is blocked is not secret at all — it was simply never written
down.** This file separates the two, so a seat can self-serve the public half and never has to ask
for the secret half in an unsafe way.

---

## 1. NOT SECRET — copy these freely, they ship inside the game client

A URL that is compiled into a public APK is not a secret. It is in every player's hands already.
Treating it as one just blocks the seat that needs it.

| Fact | Value |
|---|---|
| **Live API base** | `https://defenders-of-the-realm-v2.vercel.app` |
| Where that is pinned | `EventTracker.cs:52`, `WebTrace.cs:76`, `MaintenanceService.cs:76`, `BenefactorsService.cs:59` — all `const` |
| Backend location | `api/` **in this repo** (Vercel serverless). Not a separate project. |
| Repo | `github.com/samanthadenelle-create/defenders-unity` |
| Working branch | `wip/village2-and-f8-tickets` (`master` is stale) |
| Firebase project | `defenders-of-the-realm-echos` |
| Database | Neon Postgres, host `ep-royal-hall-ap0rhvyb-pooler.c-7.us-east-1.aws.neon.tech` |
| Store listing | Solana dApp Store — `solanadappstore://details` (there is **no web URL**) |

**Endpoints you can hit right now with no credential** (a 200 with JSON means live; empty arrays are
fine):

```
GET  /api/leaderboard?metric=best_wave&period=all
GET  /api/auth/nonce?wallet=test
GET  /api/maintenance                     # the six operator kill switches
GET  /api/patronage/benefactors           # the Benefactors wall
```

**Credentialed but not secret-shaped** (you need a key, but the *path* is public):

```
GET  /api/admin/stats?view=ops            # header: X-Admin-Key
GET  /api/admin/console                   # the operator console page
POST /api/admin/ops                       # headers: X-Admin-Key AND X-Admin-Ops-Key
```

---

## 2. SECRET — never in the repo, never in chat, never in a ticket

| Name | What it opens | Lives in |
|---|---|---|
| `DATABASE_URL` | full read/write on the live player database | `.env.local` (local) + Vercel env (prod) |
| `ADMIN_DASH_KEY` | the READ half of the console and `admin/db.js` | same |
| `ADMIN_OPS_KEY` | the WRITE half — sealing areas, authoring promos | Vercel env only |
| `DISCORD_WEBHOOK_URL` | posts into the ops channel | `.env.local` |
| `DISCORD_BOT_TOKEN` | reads the ops channel | `.env.local` |
| `FIREBASE_APP_ID` | tester distribution | `firebase-appid.txt` (gitignored) or env |
| `SOLANA_*_RECIPIENT` / RPC | the money rail | `.env.local` + Vercel env |

> ## ⛔ NEVER PRINT, LOG, ECHO OR COMMIT A SECRET VALUE.
> Refer to secrets by **name, length, and shape only** — e.g. "`DATABASE_URL` present (len=165), host
> `ep-royal-hall-...neon.tech`". That is enough to debug every real problem. Every tool in this repo
> follows that rule; keep it.

**Also never render or log:** a wallet address, an email, or a real name. A player id is enough for
any support question.

---

## 3. HOW A SEAT GETS ACCESS — the answer to "I can't reach prod"

**You almost certainly can.** The credentials are already on this machine, in a gitignored file.

`.env.local` at the repo root holds them (confirmed ignored: `.gitignore:666` `.env*`). The
established pattern is: **read `process.env` first, fall back to `.env.local`.** Copy it verbatim from
`tools/schema-parity.mjs` → `resolveDatabaseUrl()`.

```js
// The pattern. Never print the value it returns.
function resolveDatabaseUrl() {
    if (process.env.DATABASE_URL) return process.env.DATABASE_URL;
    try {
        const text = readFileSync(join(HERE, '..', '.env.local'), 'utf8');
        for (const line of text.split(/\r?\n/)) {
            const m = line.match(/^\s*DATABASE_URL\s*=\s*(.*)$/);
            if (!m) continue;
            let v = m[1].trim();
            if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) v = v.slice(1, -1);
            if (v) return v;
        }
    } catch { /* fall through to an honest failure */ }
    return null;
}
```

In PowerShell or bash, load it into the process only — never into a log:

```bash
export $(grep -E '^DATABASE_URL=' .env.local | sed 's/["'"'"']//g' | head -1)
```

**⭐ WHY THIS PATTERN IS THE RULE, NOT A CONVENIENCE.** `.githooks/pre-push` invokes
`tools/schema-parity.mjs` with a **bare environment**. Before the fallback existed, every
`api/schema.sql` change was blocked with "no DATABASE_URL in env", and the only way through was a
human exporting a secret by hand at the exact moment they were being told no. A gate whose remedy is
"a human remembers a second command" is not a gate — it is a speed bump people learn to route around,
and the routing around is what eventually ships the unverified schema.

**If a tool genuinely cannot see a secret, fix the tool's resolution — do not weaken the check, and
do not paste the value into a prompt.**

---

## 4. HOW THE OWNER SHARES A SECRET SAFELY

**Order of preference. Stop at the first one that works.**

1. **Do not share it at all.** Ask what the seat is actually trying to do. Nine times in ten the seat
   needs a *result* (does this row exist? is the toggle open?), which the owner can run herself in
   one command, or which the console at `/api/admin/console` already shows. A secret shared to answer
   a yes/no question is a permanent risk taken for a temporary need.
2. **Put it where the tool already looks.** Add it to `.env.local` on the machine, or to Vercel →
   Settings → Environment Variables. The seat then reads it through §3 and never sees the value in a
   transcript.
3. **A password manager's secure-share link**, with an expiry, if it must cross machines.
4. **Rotate immediately afterwards** if it passed through anything less than the above.

> ### ⛔ NEVER paste a secret into a chat, a prompt, a work order, a commit message, a screenshot,
> or a Discord channel.
> **This has already happened here.** `api/DEPLOY.md` step 1 opens with *"ROTATE the Neon credential
> first — the old connection string was pasted in chat."* That rotation is now permanently step one
> of the deploy checklist, which is what a leaked credential costs: not a bad afternoon, a
> permanent scar in the procedure.

**⚠ The ops Discord channel has an automated reader.** Assume anything posted there may be **acted on
by another agent**. Post facts a machine can safely consume; never post a secret, and never post
instructions.

**If a secret does leak:** rotate first, tell the owner second, write down what it opened third. Do
not investigate before rotating.

---

## 5. THE ONE OWNER ACTION CURRENTLY OUTSTANDING

`ADMIN_OPS_KEY` is **not yet set on the Vercel deployment.** Until it is, every write through
`api/admin/ops.js` answers `OPS_WRITE_NOT_CONFIGURED` (with the remedy on screen) and the operator
console is read-only. Reads work today.

Set it in **Vercel → Settings → Environment Variables**, then redeploy.

> ⚠ **It must be a DIFFERENT value from `ADMIN_DASH_KEY`.** A second key that equals the first is one
> key, and the whole point of the second key is that reading the money tables and writing to them are
> separately gated.

---

## 6. WHAT "SECURE" MEANS FOR AN AI SEAT SPECIFICALLY

- A seat's transcript is a **log**. Anything a seat prints may persist somewhere neither of you
  controls. That is why the rule is name-and-length, never value.
- A seat should **read** secrets through a file the tooling already resolves, never through the
  conversation.
- A seat must **never** ask the owner to paste a credential. Ask instead for the credential to be
  placed in `.env.local` or Vercel, then say which name you need.
- **Outward-facing actions need explicit owner approval every time** — posting to Discord, deploying
  to production, distributing a build, writing to the live database. Approval for one is not approval
  for the next.

---

Related: `docs/CLI_OPERATIONS_RUNBOOK.md` (how to run everything), `api/DEPLOY.md` (backend deploy
checklist), `api/DB_SETUP.md` (tables and migrations), `CLAUDE.md` §16 (content shipping).
