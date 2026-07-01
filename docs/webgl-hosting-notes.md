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

### ✅ Recommended for testers NOW: **itch.io**
Purpose-built for big WebGL games (handles 186 MB fine), zero header config:
1. Zip the **contents** of `Builds/WebGL/` (so `index.html` is at the zip root).
2. itch.io → Create new project → Kind = **HTML** → upload the zip → tick **"This file will be played in the browser"**.
3. Set viewport (e.g. 1280×720) → Save → it gives you a play link. Send it to testers.
- Testers press **F1** in-game for the dev portal (set level, +10k XP, jump-to-state) to run `docs/qa` cases.

### Vercel — CONFIRMED BLOCKER (2026-07-01): 100 MB PER-FILE LIMIT
**Proven, not guessed.** `vercel deploy --prod` (CLI 54.6.1, authed as `denelle-studios`, project
`defenders-of-the-realm-v2`) uploaded the whole 130.9 MB payload fine, then **rejected with
`"File size limit exceeded (100 MB)"`.** Vercel caps **individual files at 100 MB** — and the single
monolithic `WebGL.data.unityweb` is **113.7 MB** (post-trim; was 174 MB), ~14 MB over. The *total* size
is fine; it's the one `.data` file. **This is the exact reason the project moved to itch (no per-file cap).**

**The deploy pipeline itself is VALIDATED** — auth OK, upload OK, `.vercelignore` correctly ships only
`Builds/WebGL` + `api/` + configs (not the 3 GB repo), build ships Brotli + `decompressionFallback`
(so no server `Content-Encoding` needed; the COOP/COEP headers in root `vercel.json` are what matter).
**The ONLY thing standing between us and a live Vercel URL is getting that one `.data` file under 100 MB.**

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

## Relay loop (since CLI can't deploy)
Run the host steps above; **paste me the exact output / the resulting URL / any error**, and I'll adjust
(headers, loader path, compression). Same tight relay we've used all night — just in the hosting lane.

## Next (later, not blocking testers)
- **Shrink the 174 MB `.data`** — biggest tester-experience win (faster load). Audit `Resources/` + textures.
- **Web build = no-crypto/Stripe variant** per NORTH_STAR (crypto SDKs don't run on WebGL) — verify wallet
  paths are compiled out / no-op on web.
