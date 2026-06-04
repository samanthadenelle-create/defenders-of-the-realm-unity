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

### Vercel (the longer-term home — try, but expect the size caveat)
A `vercel.json` is already written into `Builds/WebGL/vercel.json` with the **Brotli headers** Unity needs
(`Content-Encoding: br` + MIME types) — without those the browser won't load the build. Deploy `Builds/WebGL/`
as its **own (second) Vercel project**. **If Vercel rejects the 174 MB `.data.br`:** host the `Build/` folder
on a CDN/object store (S3+CloudFront, Cloudflare R2, Backblaze) and point the loader at it, or use itch.

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
