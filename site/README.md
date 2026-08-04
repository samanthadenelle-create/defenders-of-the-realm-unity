# `site/` — public one-pager + hosted privacy policy (WO-863)

The two stable public URLs the Solana dApp Store Publisher Portal asks for:

| Portal field | Path | File |
|---|---|---|
| Website | `/` | `index.html` |
| Privacy policy (required) | `/privacy` | `privacy.html` (served without the extension via `cleanUrls`) |

Plain static HTML + one stylesheet. **No framework, no build step, no CDN, no webfonts, no
remote images** — everything the page loads is in this directory. The only graphic is an
inline SVG sigil.

## ⛔ NOT DEPLOYED. Do not deploy until this checklist is clear.

Both pages currently render a bright pink **PRE-PUBLICATION DRAFT — DO NOT DEPLOY** banner
and are marked `noindex`. That is on purpose: the page is not publishable yet.

- [ ] **Public store title** — canon (`canon-strings.json`) says `gameTitle` = *Echoes of
      Elarion* and `gameSubtitle` = *Defenders of the Realm* (the series/franchise label);
      the package is `com.denellestudios.echoesofelarion`. The owner still has to confirm the
      exact title she is putting on the listing and whether the series label appears as a
      subtitle. → remove `OWNER_CONFIRM_STORE_TITLE` in `index.html`.
- [ ] **Support email** — a studio/support inbox the owner is OK publishing.
      **Never the owner's personal HP address.** → replace `SUPPORT_EMAIL_PLACEHOLDER`
      in `index.html` with a `mailto:` link.
- [ ] **Description copy** — the About/What-you-do text is a draft written only from systems
      that exist in the build. Owner approves or rewrites.
- [ ] **Screenshots** — see below. Three empty slots.
- [ ] **Privacy policy is still a DRAFT.** `docs/PRIVACY_POLICY.md` is dated 2026-07-23,
      is labelled "for owner review before hosting", and still contains unfilled `{{...}}`
      fields (support email, effective date, children's age, ads + telemetry-toggle wording).
      Fix them **in the markdown**, then re-render `privacy.html` from it. Publishing a
      privacy policy that says "draft" is worse than not publishing one.
- [ ] Delete the `<div class="predeploy-banner">` block from **both** pages.
- [ ] Delete the `<meta name="robots" content="noindex, nofollow">` tag from **both** pages.

## The privacy page is verbatim

`privacy.html` is a presentation-only render of `docs/PRIVACY_POLICY.md`. No wording is
changed, added, reordered or paraphrased — including the `{{...}}` fields, which appear
exactly as authored (the pink highlight on them is CSS, not a text edit). The policy declares
analytics + wallet-address collection, which is precisely why the store requires the URL, so
it must stay authoritative. **When the markdown changes, re-render this file from it.**

## Screenshots

Empty on purpose. The full headless capture set (`Builds/ui-capture/`, `Builds/ui-capture-archive/`,
`Builds/UICaps/`) was reviewed on 2026-08-04 and **nothing in it is fit for a public page** —
every frame carries at least one of: a debug overlay (`Do Flag`, overlapping `Skip`/`Tutorial`),
clipped or overlapping text, an empty state, or unauthored placeholder data
(`Upgrade cost not authored for this tower`, `?` region tiles, `relic_drowned_ledger`).
A bad frame on a store listing is worse than no frame, so the slots were left sized instead.

To fill them: drop the approved PNGs in `site/assets/`, then in `index.html` replace each
`<li><div class="shot">…</div></li>` with

```html
<li><img src="/assets/NAME.png" width="2340" height="1080" alt="…" loading="lazy"></li>
```

Slots are sized to the game's landscape capture ratio (2340 × 1080). Local files only —
never hotlink.

## Deploying (its own Vercel project — NOT the `api/` one)

The repo root is already linked to the `defenders-of-the-realm-v2` project
(`/.vercel/project.json`) which serves `Builds/WebGL` + the `api/` functions. This site must
**not** be folded into it, so that this public URL is stable regardless of the pending
`api/`-to-prod promotion. Deploy from **inside this directory** so the CLI creates a separate
`site/.vercel` link (gitignored) and uploads only these files:

```powershell
cd D:\EoA\site
vercel link          # first time only: create/select a NEW project, e.g. echoes-of-elarion
vercel --prod
```

`vercel.json` here sets `cleanUrls`, disables git-triggered deployments, and pins the output
directory to `.` with no build command.
