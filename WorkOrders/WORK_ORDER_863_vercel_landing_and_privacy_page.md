> ## DEPLOYED 2026-08-05 - both URLs are LIVE and PUBLIC
>
> | Field the Publisher Portal asks for | URL |
> |---|---|
> | **Website** | `https://echoes-of-elarion.vercel.app/` |
> | **Privacy policy** | `https://echoes-of-elarion.vercel.app/privacy` |
>
> Vercel project `echoes-of-elarion` (`prj_rnbaJwN6CsuNGuRLtagf6oMFO3sY`), production target,
> deliberately SEPARATE from the `api/` backend project per section 3.
>
> ⚠ **Vercel SSO protection was ENABLED on creation** (`all_except_custom_domains`) and had to be turned
> OFF. Left on, a store reviewer hits a Vercel login wall instead of the policy - which would fail the
> listing while every internal check passed. **Re-check this after any project recreate.**
>
> Both URLs fetched and verified publicly readable with no auth wall. Pre-deploy fixes applied first:
> the NOT-READY banner and the three empty screenshot slots removed (empty slots read as unfinished to a
> reviewer - worse than a minimal page), `noindex` removed from both pages, and the support address
> un-wrapped from `class="placeholder"`/`class="tofill"`, which rendered the real email as an alarm-red
> error chip with a warning glyph in all five places.
>
> **Still owed (owner):** approved screenshots. The section is REMOVED rather than left as empty slots;
> restore it per the comment in `site/index.html` when real frames exist.

> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 03942cc5 + 60a6f42f; site/index.html and site/privacy.html are live, and the DEPLOYED banner at the top of this same file already said so while the Status line still read READY TO IMPLEMENT - that self-contradiction is now removed.
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 863 — Vercel one-pager + hosted privacy policy (dApp Store listing URLs)

**Status:** DONE
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** Web/Backend (static site on Vercel). No Unity/game code.
**WO#:** UI-seat block (860–899); 860=weapons, 861=characters, 862=treasure, **863**=this.
**Origin:** owner 2026-08-03, mid Publisher-Portal setup — the dApp Store listing asks for a **website URL** (she has
none) and separately requires a **privacy-policy URL** (authored `docs/PRIVACY_POLICY.md`, not yet hosted). One static
deploy clears BOTH.

---

## 1. Goal
Ship a tiny public static site on **Vercel (production)** that yields two stable `https://` URLs the owner pastes into
the Publisher Portal:
- **`/`** — a one-page landing/marketing page (the "website" field).
- **`/privacy`** (or `/privacy.html`) — the hosted **privacy policy** (the required listing URL).

Keep it **separate from the `api/` backend project** so this public URL is stable and does NOT depend on the pending
`api/`-to-prod promotion. A standalone minimal Vercel project (or a dedicated static deploy) is cleanest.

## 2. Content
### Landing page (`/`)
- **Title:** the game's public/store name. Package is `com.denellestudios.echoesofelarion` → **"Echoes of Elarion"**
  is the likely store title; **`OWNER CONFIRM`** the exact title + whether "Defenders of the Realm" is a subtitle.
- **Tagline:** canon = **"Echoes of a Forgotten Civilization"** (canon-strings; do NOT use retired "Hold the last light").
- **Short description** + a couple sentences of long description (reuse the store long-desc once written; placeholder
  now, `OWNER CONFIRM` final copy).
- **2–3 screenshots** — pull clean frames from `Builds/ui-capture*` / `RunCaptureHeadless` (same set the store listing
  will use). Embed as local assets (no hotlinking).
- **"Coming to the Solana dApp Store (Seeker)"** framing — accurate; do NOT claim it's live yet.
- **Support email** — `OWNER CONFIRM`: a support address the owner is OK publishing publicly. **Do NOT publish her
  personal HP email** — use a studio/support address she designates (e.g. a DeNelle Studios support inbox).
- **Footer:** studio = **DeNelle Studios**, a link to **/privacy**, © year.

### Privacy page (`/privacy`)
- Render **`docs/PRIVACY_POLICY.md`** to clean HTML verbatim (it already declares analytics + wallet-address
  collection — the reason the store requires the URL). Keep the content authoritative; do not paraphrase/alter it.

## 3. Build + deploy (implementer's discretion; simplest path)
- Plain **static HTML/CSS** — no framework needed. Self-contained (inline or local CSS; local images). Mobile-first
  responsive (the audience is phones/Seeker); light+dark fine.
- **Deploy to Vercel PRODUCTION** so the URL is public + stable (the listing needs a live URL, not a preview that
  rotates). A dedicated small project (e.g. `echoes-of-elarion` / a `site/` dir) — NOT folded into the `api/` project.
- Custom domain optional/later; a `*.vercel.app` production URL is sufficient for the Portal.
- Store the source in-repo (e.g. `site/` or `web/landing/`) so it's tracked + redeployable.

## 4. Owner inputs (small — page can be built with placeholders first)
1. Public store **title** (Echoes of Elarion?) + any subtitle.
2. **Support email** to publish (NOT the personal HP address).
3. Final short/long **description** copy (or approve a draft).
4. Which **screenshots** (CLI proposes 2–3 from the capture set; owner picks/approves).

## 5. Acceptance criteria
- [ ] Two live public `https://` URLs on Vercel **production**: landing (`/`) + privacy (`/privacy`).
- [ ] Privacy page content matches `docs/PRIVACY_POLICY.md` verbatim.
- [ ] Landing shows title, tagline, description, 2–3 screenshots, support email, DeNelle Studios footer, /privacy link.
- [ ] Mobile-responsive; no broken images; no external hotlinks (self-contained).
- [ ] NOT coupled to the `api/` backend deploy; the URL is stable regardless of api prod state.
- [ ] Source tracked in-repo; redeployable.

## 6. Do NOT
- Do NOT publish the owner's personal email; use an owner-designated support address.
- Do NOT fabricate claims (no "available now", no fake reviews/ratings, no invented features) — accurate marketing only.
- Do NOT entangle with the `api/` project or its preview/prod state.
- Do NOT alter the privacy-policy text — host it as authored.
