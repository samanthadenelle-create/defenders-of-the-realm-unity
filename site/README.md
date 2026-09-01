# Echoes of Elarion — marketing site

Static HTML/CSS one-pager for the Solana dApp Store listing and public brand surface.

| Portal field | Path | File |
|---|---|---|
| Website | `/` | `index.html` |
| Privacy policy | `/privacy` | `privacy.html` |
| Terms of Use | `/terms` | `terms.html` |

**Vercel project:** `echoes-of-elarion`  
**Package:** `com.denellestudios.echoesofelarion`  
**Deep link:** `solanadappstore://details?id=com.denellestudios.echoesofelarion`

## Constraints

- No framework, no build step, no CDN, no webfonts
- All images local under `assets/` (plus root `qr-dappstore.png`)
- Canon tagline: **Echoes of a Forgotten Civilization** (never restore retired “last light of the Heart”)
- Honest deep-link fallback uses Page Visibility — no elapsed-time redirect hack

## Design (2026-08 redesign)

Shipped-indie look: full-bleed title key art hero, About + secondary art, three hero cards
(Grom / Thrain / Sylas), four gameplay pillars, Get-it + QR, Support + footer with Privacy,
Terms, Discord, and X.

### Assets

| File | Role |
|---|---|
| `assets/title-screen-a-web.jpg` | Landscape hero background (UI chrome cropped) |
| `assets/title-screen-b-web.jpg` | Portrait About visual |
| `assets/battle-worn-heroes-web.jpg` | Party/squad banner above hero cards |
| `assets/grom-web.jpg` / `thrain-web.jpg` / `sylas-web.jpg` | Individual hero cards (~200KB each) |
| `assets/key-art-web.jpg` (+ favicon / apple-touch) | App-icon mark, og:image, Get-it badge |
| `assets/trailer-web.mp4` (+ `trailer-poster.jpg`) | Portrait living-poster loop (muted autoplay) |
| `qr-dappstore.png` | Seeker deep-link QR |

Source PNGs (`grom.png`, etc.) and full title screens remain for archival; pages reference the `-web` JPEGs.

## Local preview

```bash
python3 -m http.server 8765 --directory /workspace/eoa-site
# open http://127.0.0.1:8765/
```

## Deploy (owner only — separate Vercel project)

This directory is its own Vercel project (`echoes-of-elarion`), **not** the WebGL/`api` project.
Deploy from **inside** this directory so only these files upload:

```powershell
cd <repoRoot>\site
vercel link    # first time: project echoes-of-elarion
vercel --prod
```

`vercel.json` sets `cleanUrls`, disables git-triggered deployments, and pins `outputDirectory` to `.` with no build command.

## Support & social

- Email: support.eoa@icloud.com
- Discord: https://discord.gg/zDdwdy3duB
- X: https://x.com/EchoesOfElarion

## Legal pages

`privacy.html` and `terms.html` are presentation renders of the studio policy docs. Do not invent
claims here — re-render from source markdown when policy text changes. Pre-deploy / `noindex`
banners are removed; pages are public.
