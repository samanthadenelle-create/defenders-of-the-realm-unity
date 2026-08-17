<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 120 — Backend Spec ↔ Shipped-Client Reconciliation

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High (contains a security item) — but pure docs + a few Unity URL constants; **clean of the red Unity gameplay tree**
**Lanes:** spec/docs (this repo `docs/`) · Unity URL constants + save-auth (CLI) · actual endpoints (backend repo `defenders-of-the-realm`, Kayden)
**Source:** CLI completeness review of `docs/v2-unity-port-backend-spec.md` vs the client's actual `UnityWebRequest` calls (2026-05-29).

---

## Why

**Reality check (owner, 2026-05-29): the backend was NEVER connected.** The client ships
a full set of backend-calling services + UI (clan/chat, promo, referral, cloud-save,
telemetry, tower-swap, leaderboard) with real-looking Vercel URLs — but **none of those
routes were ever deployed.** The backend exists only as specs + draft endpoints. The
client is built *ahead* of a backend that doesn't exist.

So this is **not** "reconcile a drifted live API." It's: **the client's actual calls are
the ground-truth contract the backend must be built against** — section A is that contract.
Nothing here is a *live* bug (nothing's connected); every item is a **pre-deploy gate** —
must be true the day the backend is first stood up. The framework spec (auth, status codes,
rate limits, schema, secrets, env, deploy) is solid; the endpoint inventory just needs to
match what the client already calls before anyone wires it up.

---

## A. Shipped-client endpoint audit (ground truth — add this table to the spec as §2.3a)

| Endpoint the client calls | Caller | Base host | In spec §2.3/2.4? | Action |
|---|---|---|---|---|
| `/api/game/save`, `/api/game/load?playerId=<wallet>` | `GameStateService` | `-v2.vercel.app` | only as future `/api/save/sync` | **reconcile path + add auth** (see C, D) |
| `/api/events/track` | `EventTracker` | `-v2.vercel.app` | no (spec says `/api/events/ingest`; draft says `/api/metrics`) | **pick one canonical name** (see B) |
| `/api/promo/redeem` | `PromoCodeService` | `-v2.vercel.app` | **missing** | add to §2.3 + backend endpoint |
| `/api/referral/generate`, `/api/referral/claim` | `ReferralService` | `-v2.vercel.app` | **missing** | add to §2.3 + backend endpoint |
| `/api/tower-swap/log` | `TowerSwapService` | `-v2.vercel.app` | **missing** | add to §2.3 + backend endpoint |
| `/api/bug-report` | `HelpMenu` | **`.vercel.app` (v1!)** | yes §2.3 | **fix base host** (see E) |
| `/api/clan/*` | `ClanService` / `ClanChatPanel` | `-v2.vercel.app` | yes §2.3 | ✅ in sync |

---

## B. Telemetry — pick ONE canonical name (🟠)

Three names exist for "client sends an event": client `/api/events/track`, spec `/api/events/ingest`, draft `/api/metrics`.
- **Decision needed (owner):** canonicalize. Recommend **`/api/events/track`** (already shipped in the client → least churn) and point the metrics draft + WO-106 dashboard at it.
- Update `docs/draft-backend-endpoints/metrics.ts` + **WO-106** to read from the `events/track` table, not `/api/metrics`.
- Note: anti-cheat §3 `/api/events/ingest` is a *different* stream (raw anti-cheat events) — keep it separate, just don't confuse it with player telemetry.

## C. Cloud save — reconcile the path (🟠)

Client ships `/api/game/save` + `/api/game/load`; spec §2.4 lists `/api/save/sync`. Pick the shipped pair as canonical (least churn), update §2.4, and make the backend serve `/api/game/save` + `/load`.

## D. 🔴 SECURITY — authenticate save/load

`/api/game/save` + `/load` are keyed only by `?playerId=<BoundWallet>` (a **public** on-chain address) with **no signature** → anyone who knows a wallet can load/overwrite that player's save. This **violates the spec's own §2.6** (wallet-signed-nonce required for save endpoints).
- **Backend:** require the §2.6 wallet-signed-nonce (`X-Wallet`/`X-Signature`/`X-Nonce`) on save/load; reject mismatched wallet with 401.
- **Unity (CLI):** add the nonce fetch + MWA-sign + headers to `GameStateService` save/load calls. Until backend enforces, ship the client code path behind the existing env flag.

## E. Base-URL drift — one env-configured host (🟡)

Three hosts in client constants: `-v2.vercel.app` (most), `.vercel.app` (bug-report → **v1 deploy**), `.app` (wallet share link). 
- **Unity (CLI):** route every backend constant through the §6 env-config base (`EnvironmentConfig`/build profile) instead of hardcoded strings. Fix `HelpMenu` bug-report to the v2 host.

---

## Acceptance
- [ ] Spec §2.3 adds promo/redeem, referral/generate, referral/claim, tower-swap/log rows
- [ ] New §2.3a "shipped-client endpoint audit" table (section A above)
- [ ] Telemetry canonical name chosen + applied to metrics draft + WO-106
- [ ] Save path canonicalized (`/api/game/save` + `/load`) in §2.4
- [ ] §2.6 auth applied to save/load — backend enforces, Unity signs (security item closed)
- [ ] All Unity backend URLs route through one env-config base; bug-report off the v1 host

## Lane split
- **Docs (this repo):** §2.3 + §2.3a + §2.4 edits — can be done now.
- **CLI (Unity):** URL-constant env routing + save-auth client path — through the brace/compile gate, after the tree is green.
- **Backend (Kayden):** the 4 missing endpoints + save-auth enforcement + canonical telemetry table.

🤖 Drafted by the build-connected CLI from a client↔spec contract audit.
