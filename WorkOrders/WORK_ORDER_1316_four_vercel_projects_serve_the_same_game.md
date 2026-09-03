# WORK ORDER 1316 — FOUR Vercel projects serve this game, and the repo deploys to only one of them

**Status:** BLOCKED - gate built and proven 2026-09-03; criteria 1, 2 and 5 need owner decisions (see IMPLEMENTATION RECORD below).
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

---

# IMPLEMENTATION RECORD - 2026-09-03

## What was measured (live, read-only, this session)

`GET https://<host>/index.html` and `/validation-key.txt`, plain HTTPS, no CLI, no preview URL:

| project | index.html bytes | index.html sha256 (12) | validation-key |
|---|---|---|---|
| `defenders-of-the-realm-v2` | **40,100** | `d7f9e59e5d63` | `79ec2d03...` (current) |
| `echoes-of-elarion` | **32,609** | `17dd5c88ad1b` | `79ec2d03...` (current) |
| `defenders-webgl` | 7,397 | `0bf8b4184bd8` | **`fef2a223...` (RETIRED July key)** |
| `defenders-backend` | 375 (API stub, no shell) | `dcc2d9ba0681` | 404 |

**The two production domains have DIVERGED AGAIN, one day after the hand patch.** The ticket recorded
both serving 26,443 bytes on 09-02. They now serve two different builds - not just different sizes but
different content-hashed Unity payloads:

- v2: `74efcee1....loader.js` / `669cdc07....data.unityweb` / `9012cdb9....wasm.unityweb`
- echoes-of-elarion: `5466bb13....loader.js` / `e1e3fcce....data.unityweb` / `80780cf8....wasm.unityweb`

That is the whole argument for a gate, measured rather than theorised.

**Second finding, and the reason the gate hashes instead of counting bytes:** the local build tree
`Builds\WebGL\index.html` is **also 40,100 bytes** yet hashes to `841282222aef`, not v2's
`d7f9e59e5d63` - a local rebuild after the last deploy re-hashed the payload at identical length. A
byte-length comparator (which criterion 4 permitted) would have called that a match.

## What was built

**`tools/web-ship.ps1`** - one file, mirroring `tools2-ship.ps1`'s shape per CLAUDE.md section 16.

- **The `$Surfaces` registry is the SINGLE source of truth** for hosts, project ids, roles and each
  project's purpose. Nothing else in the repo restates them; `-ListSurfaces` exists so a caller can
  read the registry instead of copying it.
- Roles: `production` (must agree byte-for-byte), `dormant` (nothing deploys to it; still gated - a
  divergent validation key withholds the marker), `api` (no game shell; reported, never compared).
- **Verify phase** fetches the PUBLIC production domains over plain HTTPS. Deliberately not
  `vercel curl` and deliberately not a preview URL: previews are SSO-gated, 302 to `sso-api`, and are
  not what a player or the Pi validator gets. No Vercel CLI is required for the verify phase.
- **Deploy phase** targets each non-chain production surface by **explicit `VERCEL_PROJECT_ID`**,
  never by whatever `.vercel/project.json` holds - one call, in one file, never "and also remember to
  do the other one".
- `-AgainstLocal <dir>` additionally asserts the served bytes equal a local build (criterion 4).
- **No `-Force`, no `-WarnOnly`.** Every incident this ticket documents was a human expected to
  remember a second command or read a warning; an override flag restores that exact hole.

**Marker: `WEB_PARITY_OK`** on a fresh `Builds\web-parity.log`. Also `WEB_SURFACES_OK`,
`WEB_DEPLOY_OK`, `WEB_SHIP_PUSH_OK`. Marker absence on a fresh log is a FAILURE, not an unknown.
Exit codes (0 / 16 / 20) are diagnostics only.

**`tools/command-centre.ps1`** - new blocking **step 6b** after promotion, judged by
`Assert-FreshMarker 6 'WEB_PARITY_OK'`, i.e. by the marker on a fresh log and never by an exit code.
It calls `web-ship.ps1 -VerifyOnly -AgainstLocal Builds\WebGL`. `-VerifyOnly` is deliberate while
criterion 1 is unanswered - see the proposals below.

## Proof

| check | result |
|---|---|
| PowerShell 5.1 parse, `web-ship.ps1` | `PSVersion 5.1.26100.9278 Desktop`, `PARSE_ERRORS=0`, 1953 tokens |
| PowerShell 5.1 parse, `command-centre.ps1` after the edit | `PARSE_ERRORS=0`, 2484 tokens; `-LibraryOnly` path still loads |
| `-ListSurfaces` | `WEB_SURFACES_OK total=4 production=2`, exit 0 |
| **RED 1** - live run, real divergence | `WEB_SHIP_REFUSED reason=PRODUCTION_INDEX_DIVERGENT`, both diverging hashes logged |
| **RED 2** - live run, `-ParityPath /validation-key.txt` | `WEB_SHIP_REFUSED reason=DORMANT_SERVES_DIVERGENT_VALIDATION_KEY_defenders-webgl` (criterion 5, made loud) |
| **RED 3** - `-AgainstLocal Builds\WebGL` | `WEB_SHIP_REFUSED reason=LOCAL_INDEX_NOT_SERVED_BY_PRODUCTION` (criterion 4) |
| **GREEN** - agreeing surfaces + `-AgainstLocal` | `WEB_LOCAL_MATCH index=1`, `WEB_LOCAL_MATCH validationKey=1`, `WEB_PARITY_OK ... sha=d7f9e59e...`, exit 0 |

The GREEN run used a scratchpad copy of the script with every registry URL pointed at one real
production host, so the surfaces genuinely agree. It exercises the full success path - both parity
comparisons, both `-AgainstLocal` comparisons, and the dormant-agrees branch - without mutating
production. **The success path is proven, not only the refusal** (a guard shipped earlier in this repo
aborted every good run while exiting 0).

## PRODUCTION CHANGES PROPOSED, NOT PERFORMED

No `vercel deploy`, no alias change, no project setting was touched. Read-only HTTP and
`-ListSurfaces` only. For the lead / owner:

1. **Criterion 1 (owner) - which URL is in the Pi Developer Portal?** Still not discoverable from this
   machine, and both production domains serve the same validation key, so the key cannot discriminate.
   Her answer decides which project is genuinely production.
2. **Deploy the sibling production surface.** `echoes-of-elarion` is 32,609 bytes behind. The
   sanctioned single command is `powershell -NoProfile -File tools\web-ship.ps1` (deploy + verify,
   needs `VERCEL_TOKEN`). ⚠ **Its deploy phase is code-complete but UNPROVEN** - this session was
   forbidden to run a deploy. Recommend the lead's first run be `-VerifyOnly`, then the full form
   under supervision.
3. **Flip step 6b to the full form** (drop `-VerifyOnly` in `command-centre.ps1`) once 1 and 2 land,
   so the chain deploys both production surfaces in one call. It is a one-token change, documented at
   the call site.
4. **`defenders-webgl` (criterion 5): retire it.** It is a live URL serving a retired validation key
   and nothing deploys to it. ⛔ Deleting or pausing a Vercel project is the owner's call - proposing
   only. If she prefers to keep it live, the alternative is to bring its validation key up to date;
   either clears the gate.
5. `defenders-backend` is a superseded API stub (`.vercelignore:17` re-includes `/api`, so the API
   ships with the main deploy). Registry role `api`; no gameplay surface. Retirement is optional and
   also hers.

## Not touched

`.vercelignore`, `tools/r2-ship.ps1` (called, never edited or re-inlined), `api/_lib/purchase-catalog.js`,
prices/SKUs/entitlements, `BOARD.html`, `tools/board_build.py`, any `.cs`, any `.unity`. Nothing
committed - the lead is sole committer. `BOARD.html` was not regenerated (WO-1339's lane).
