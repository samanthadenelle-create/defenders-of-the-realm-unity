# WebGL Build + Hosting Notes (WO-123)

> Overnight build result + the script-relay hosting kit. The WebGL build **succeeded** —
> `Builds/WebGL/` (185 MB, Brotli). Testers can have a browser link.

---

## Build result (2026-05-29 overnight)
- ✅ **`build-webgl.ps1` SUCCEEDED** in **18.5 min** (IL2CPP + Brotli). WebGL module is installed.
- Output: `Builds/WebGL/` (gitignored). `index.html` + `Build/` + `StreamingAssets/` + `TemplateData/`.
- **Payload (the gating metric):** `WebGL.data.br` = **174 MB**, `WebGL.wasm.br` = 13 MB → **~186 MB total**.
  Big. It loads, but it's a heavy first download for testers; **optimization is a later pass** (strip
  unused assets / texture compression / asset streaming).

## ⚠ Host choice — size drives this
**174 MB single `.data.br` file → Vercel may reject it** (deployment/file-size limits on the free tier;
even paid has caps that this brushes). So:

> ⚠ **2026-08-10: the "Vercel may reject it" premise is stale — see the corrected Vercel section below.**
> Vercel production is serving a **165 MB** single `.data.unityweb` today. itch.io is still a fine tester
> host; it is no longer the *only* one.

### ✅ Recommended for testers NOW: **itch.io**
Purpose-built for big WebGL games (handles 186 MB fine), zero header config:
1. Zip the **contents** of `Builds/WebGL/` (so `index.html` is at the zip root).
2. itch.io → Create new project → Kind = **HTML** → upload the zip → tick **"This file will be played in the browser"**.
3. Set viewport (e.g. 1280×720) → Save → it gives you a play link. Send it to testers.
- Testers press **F1** in-game for the dev portal (set level, +10k XP, jump-to-state) to run `docs/qa` cases.

### Vercel — the "100 MB PER-FILE BLOCKER" is STALE (corrected 2026-08-10)
> ⚠ **This section used to read: "Vercel — CONFIRMED BLOCKER (2026-07-01): 100 MB PER-FILE LIMIT …
> the single monolithic `WebGL.data.unityweb` is 113.7 MB … **This is the exact reason the project
> moved to itch (no per-file cap).**"** That was a true 2026-07-01 reading and it is **no longer true.**
> A seat reading it would have concluded Vercel *cannot* host this build and routed testers to itch —
> while a 157 MB single payload was already serving from Vercel production.
>
> **What is actually live (verified 2026-08-10 against production, not against a doc):** production's
> `index.html` serves **`Build/1d5ab8b897dfbf58bb924b35de0f09c1.data.unityweb` — 165,005,813 bytes**
> (~157 MB). One file, well past the old 100 MB figure, downloading today. **Vercel hosting is not
> blocked on file size.**
>
> **DO NOT read this as "size is fine."** 165 MB is a **real, open LOAD-TIME problem** — it is the
> tester's first-download experience, and it is exactly the payload WO-545 / WO-282 (moving
> `Resources/Heroes` into Addressables) were written to split. **Those never landed.** The correction is
> **"cannot host" → "should not be this big"**; the work below is still owed.

**Historical record (2026-07-01, kept for provenance):** `vercel deploy --prod` (CLI 54.6.1, authed as
`denelle-studios`, project `defenders-of-the-realm-v2`) uploaded the whole 130.9 MB payload fine, then
rejected with `"File size limit exceeded (100 MB)"` against a 113.7 MB `WebGL.data.unityweb`. That is
what drove the move to itch at the time.

**The deploy pipeline itself is VALIDATED** — auth OK, upload OK, `.vercelignore` correctly ships only
`Builds/WebGL` + `api/` + configs (not the 3 GB repo), build ships Brotli + `decompressionFallback`
(so no server `Content-Encoding` needed; the COOP/COEP headers in root `vercel.json` are what matter).
~~**The ONLY thing standing between us and a live Vercel URL is getting that one `.data` file under 100 MB.**~~
**SUPERSEDED 2026-08-10 — the live Vercel URL exists and serves a 165 MB `.data.unityweb`.** The
remaining problem is load time, not admissibility.

**The right fix = WO-545 (Addressables-remote), NOT a compression hack.** The `.data` is dominated by
`Resources/Heroes` (138 MB raw: 84 MB Textures + ~40 MB regenerable `.fbm` dupes). Moving heroes/enemies
out of `Resources/` into per-entity Addressable groups (reusing the already-shipping
`gear_assets_all_*.bundle` pattern) splits the monolith — V1 ships Knight in the pack, mage/ranger/cleric
stream from CDN on unlock — and drops the base `.data` far below 100 MB. See
`docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` (T1 Addressables-remote) + WO-191 Phase 2.
**Fallback if a fast fix is needed:** host `Build/` on an object store (R2/S3+CloudFront) + point the loader
at it (Vercel Blob or external CDN), or keep using itch for tester links until WO-545 lands.

## The `vercel.json` (also placed in `Builds/WebGL/`)
```json
{
  "headers": [
    { "source": "/Build/(.*)\\.wasm\\.br", "headers": [
      { "key": "Content-Encoding", "value": "br" },
      { "key": "Content-Type", "value": "application/wasm" } ] },
    { "source": "/Build/(.*)\\.js\\.br", "headers": [
      { "key": "Content-Encoding", "value": "br" },
      { "key": "Content-Type", "value": "application/javascript" } ] },
    { "source": "/Build/(.*)\\.data\\.br", "headers": [
      { "key": "Content-Encoding", "value": "br" },
      { "key": "Content-Type", "value": "application/octet-stream" } ] }
  ]
}
```

## ⚠ Deploy scripts — the two disagree, and the markers belong to the older one (recorded 2026-08-10)

Two overnight deploy scripts live at the repo root and canon has been quoting a procedure that is a
blend of both. Read this before running either.

- **Prefer `overnight-webgl-deploy.ps1`.** It writes its status to **`Builds\overnight-chain-status.txt`**.
- The marker names canon quotes — **`CHAIN_START`, `WEBGL_BUILD_OK`, `DEPLOY_URL`, `CHAIN_DONE`** —
  belong to the **OLDER `webgl-vercel-overnight.ps1`** (which writes `Builds\webgl-chain-status.txt`).
  **Grepping for those markers after running the preferred script finds nothing, and reads exactly like
  "the deploy never ran."** They are two procedures, not one.
- `webgl-vercel-overnight.ps1` invokes bare **`vercel deploy --yes` with no `--token` and no `--scope`**,
  so it only works if the Vercel CLI is already interactively authed on that machine — which a detached
  overnight run is not. That is a silent-failure path, not a preference.
- ⚠ **TRAP: never `cd Builds\WebGL` and deploy from there.** That folder carries its own
  `.vercel/project.json` pointing at a **DIFFERENT Vercel project** — `defenders-webgl`,
  `prj_ox8fqdHbD7lkrKEyxy0dtQAjphGc` — while the repo root is linked to the real one,
  `defenders-of-the-realm-v2`, `prj_qUmuwr8BN492oZH8yRuvPZMN3e0J`. Deploying from inside `Builds\WebGL`
  ships to a project nobody is looking at. **Always deploy from the repo root.**
  *(As of 2026-08-10 `Builds/WebGL/` is not on disk, so the stray link file is absent — the trap is
  dormant, and returns with the next WebGL build + link.)*
- Related standing property: **`.vercelignore:17` (`!/api`) allowlists `/api`**, so **every `--prod` from the repo
  root re-ships `api/` to production** alongside the WebGL payload. There is no WebGL-only promotion.

## Relay loop (since CLI can't deploy)
Run the host steps above; **paste me the exact output / the resulting URL / any error**, and I'll adjust
(headers, loader path, compression). Same tight relay we've used all night — just in the hosting lane.

## Next (later, not blocking testers)
- **Shrink the `.data`** — biggest tester-experience win (faster load). Audit `Resources/` + textures.
  **STILL OPEN as of 2026-08-10, and larger than when this was written:** production serves a
  **165,005,813-byte** `.data.unityweb`. WO-545 / WO-282 (heroes out of `Resources/` into Addressables)
  are the named fix and **never landed**.
- **Web build = no-crypto/Stripe variant** per NORTH_STAR (crypto SDKs don't run on WebGL) — verify wallet
  paths are compiled out / no-op on web.
