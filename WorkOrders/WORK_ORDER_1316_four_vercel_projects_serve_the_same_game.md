# WORK ORDER 1316 — FOUR Vercel projects serve this game, and the repo deploys to only one of them

**Status:** READY TO IMPLEMENT
**Silo:** Web / Deploy
**Minted:** 2026-09-02 (CLI) while promoting the Pi build to production on the owner's instruction.
**Severity:** P1 process defect — a deploy can report success while the live app never changes.

## What was measured

`vercel project ls` on team `samanthadenelle-creates-projects`:

| project | production URL | last updated (at 06:20) | validation key served |
|---|---|---|---|
| `defenders-of-the-realm-v2` | defenders-of-the-realm-v2.vercel.app | 54s | `79ec2d03` |
| **`echoes-of-elarion`** | **echoes-of-elarion.vercel.app** | **2 days** | `79ec2d03` |
| `defenders-webgl` | defenders-webgl.vercel.app | 28 days | **`fef2a223`** (the retired July key) |
| `defenders-backend` | defenders-backend.vercel.app | 63 days | - |

**`.vercel/project.json` in this repo points at `defenders-of-the-realm-v2`** (`prj_qUmuwr8BN492oZH8yRuvPZMN3e0J`).
`echoes-of-elarion` is a **different project** (`prj_rnbaJwN6CsuNGuRLtagf6oMFO3sY`, created 2026-08-05).

## Why this is the dangerous shape

Running `vercel deploy --prod` from this repo updates `defenders-of-the-realm-v2` **and nothing else**.
It prints a success message and a ready deployment. Meanwhile `echoes-of-elarion.vercel.app` — the
domain named after the game, and the one this repo's own docs and canon treat as production —
**keeps serving whatever it last got.**

Measured on 2026-09-02: after a successful `--prod` deploy, `echoes-of-elarion.vercel.app` was still
returning the **7,396-byte pre-gate** `index.html` while `defenders-of-the-realm-v2.vercel.app` served
the new **26,443-byte** one. Same game, two prods, silently divergent.

⚠ This is the same class as CLAUDE.md sec.16 (content pushed for the wrong platform) and sec.2's
stale WO block: **a second copy of state that nothing keeps in sync, where the success signal comes
from the copy you touched, not the copy users hit.**

`defenders-webgl` is the proof it has already bitten: it still serves the **July** validation key that
WO-1313 retired, i.e. it has been stale for ~7 weeks and nobody noticed, because nothing deploys to it
and nothing checks it.

## What was done today (not the fix — a manual patch of the symptom)

Both `defenders-of-the-realm-v2` and `echoes-of-elarion` were deployed to production by hand (the
latter via `VERCEL_PROJECT_ID` override). Both now serve the 26,443-byte build with the rotation, the
input shim and the `79ec2d03` key, and the loader/wasm/data plus the R2 catalog it requests all
return 200.

**That is a manual step a human has to remember, which is exactly the failure mode this repo keeps
being bitten by.** It is not a fix.

## ⚠ THE QUESTION THIS TICKET CANNOT ANSWER — needs the owner

**Which URL is registered in the Pi Developer Portal?** That determines which project is genuinely
production for Pi, and it is not discoverable from this machine. Both candidates currently serve the
correct validation key, so the key does not discriminate between them.

Until that is answered, do NOT delete or repoint any project.

## Acceptance criteria

1. The owner states which URL Pi points at. Record it in canon (`docs/HANDOVER.md` +
   `CANON_GROUND_TRUTH_<date>.md`) so no seat has to guess again.
2. ONE project is designated production. The others are either retired, or explicitly documented as
   dormant with a banner saying so.
3. The ship path deploys to the designated project **by id**, not by whatever `.vercel/project.json`
   happens to hold. If two must stay live, the script deploys to both in one call — never "and also
   remember to do the other one".
4. A post-deploy VERIFY step that fetches the **public production URL** and asserts the served
   `index.html` matches the local build (byte length or a content hash), failing loudly on mismatch.
   A deploy that cannot prove the live URL changed is not a deploy. Today's `overnight-webgl-deploy.ps1`
   has no such check.
5. `defenders-webgl` (July key, 28 days stale) is retired or updated. A live URL serving a retired
   validation key is a hazard on its own.

## What NOT to touch

- ⛔ Do NOT delete any Vercel project before criterion 1 is answered. One of them is the live Pi app.
- ⛔ Do NOT edit `.vercelignore`. Vercel does not honour `.gitignore` on CLI deploys; that allowlist is
  the only thing keeping the ~3 GB repo under the 2 GiB upload cap.
- ⛔ Do NOT make the ship chain deploy to "all projects found". That would resurrect
  `defenders-backend` and any future experiment as production surfaces.
