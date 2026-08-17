# WORK ORDER 1029 — Clans are a PlayerPrefs stub: ship donations, not wars

**Status:** BLOCKED ON BACKEND READINESS — ★ §4 RULED 2026-08-17: **BLOCK; do not bundle the `api/` promotion**

> Owner ruling 2026-08-17 (*"open ones follow your recommendations"*): this WO does **NOT** promote `api/`
> to production. It waits for a separate backend-readiness ticket that owns the promotion and the CORS fix.
>
> ⚠ WHY BLOCK RATHER THAN BUNDLE: promoting a backend inside a gameplay feature is precisely the
> structural-refactor smuggling `docs/ARCHITECTURE_PRINCIPLES.md` forbids — player-facing work must not
> carry infrastructure change in its pocket. It also hides the risk: if the promotion breaks auth, it
> surfaces as *"clan donations are broken"* rather than as what it actually is. `api/` is PREVIEW-only
> today and prod's nonce endpoint has no CORS.
>
> ### Second ruling — the free-text path is CLOSED
> Donations ship with **preset messages only**; no player-authored free text. Free text is a moderation
> surface, and per §4 it must not reach a networked build without an explicit ruling — this is that
> ruling, and it is **closed, not deferred**. Reopening it means owning moderation, reporting and a
> takedown path: a product decision, not a feature detail.
>
> Everything else in §5 stays as specced and is implementable the moment the backend ticket lands.
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1029 → 1030 in the same edit
**Lane:** Social / backend. ⚠ Depends on `api/`, which is **PREVIEW-only** today.
**Provenance:** `docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md` §3 ⓹.

---

## 1. The gap, measured

`Assets/_Modules/Core/Services/ClanService.cs` — **343 lines, and its own header states the limit**:

> *"SCOPE: LOCAL SINGLE-PLAYER STUB. The React repo talks to a real Vercel / Postgres backend; the
> Unity port does not yet."*

It owns the right data model (clan / member / message / role, ring-buffered chat capped at 100) in
**PlayerPrefs**. Explicitly not ported: server rank checks, rate limits, join cooldowns, Postgres
tables, clan invites, public clan lookup. `ClanChatPanel.cs` (417 lines) is the HUD over that stub.

Grep for `Donat*` across `_Modules`: **0 hits.**

## 2. Why donations specifically — and why NOT clan wars

Clash of Clans' retention engine is not the war. It is **the donation**: you ask, a human gives, and you
give back. It is the strongest social hook shipped in the genre because it is:

- **cheap** — no matchmaking, no scheduling, no balance
- **functional at N=2** — a clan of two works; a war of two does not
- **reciprocal** — creates obligation, which creates return visits
- **low-toxicity** — the interaction is a gift, so the failure mode is silence, not abuse

⚠ **Do NOT build clan wars first.** Wars need population, matchmaking, scheduling and balance, and they
are dead content until the game has a player base. Donations work on day one with two friends.

## 3. Why this is sequenced LAST

Social retention returns players **to something**. Right now the thing they would return to has two
open loops (WO-1026 raid consequence, WO-1028 creeping) and a broken hero pillar (WO-910). Shipping
retention before the loops close spends the one-time novelty of an invite on an incomplete game.

**Build the game worth returning to, then build the reason to return.**

## 4. ⛔ DECISION REQUIRED — the backend

Donations are **inherently networked**; they cannot be a local stub. Blockers, verified:

- `api/` is **PREVIEW-only** and prod's nonce endpoint has **no CORS** (anchor 2026-08-09)
- The `api/` backend **is in this repo** (memory `api-backend-in-repo`) — Vercel serverless, not a
  separate project. Read it directly; do not greenfield.
- Firebase Auth + Neon Postgres is the standing architecture (memory
  `firebase-auth-neon-architecture`) — identity exists, so donations have an owner key already

**The owner must decide:** promote `api/` to production (fixing CORS) as part of this WO, or block until
a separate backend-readiness ticket lands. **My recommendation: block.** Bundling a backend promotion
into a gameplay feature is exactly the structural-refactor smuggling `ARCHITECTURE_PRINCIPLES.md`
forbids.

## 5. Scope, once unblocked

1. **Request** — a player asks their clan for a specific troop/resource, bounded
2. **Fulfil** — another member gives from their own stock. ⚠ The cost must be **real** to the giver, or
   the gift means nothing; but it must be **small**, or nobody gives
3. **Receive** — the recipient gets it with clear attribution: *who* gave. The name is the hook
4. **A ledger** — given/received per member, visible. Reciprocity needs a record to work
5. Reuse the existing `ClanService` data model and `ClanChatPanel` surface. **Do not build a second
   clan system** — swap the PlayerPrefs persistence for the network bridge the header already
   anticipates

## 6. Explicitly OUT of scope

- Clan wars, war leagues, war matchmaking
- Clan levels / perks
- Free text chat — the React spec says *"no free text, ever"* (§6.4). ⚠ The existing
  `AddCustomMessage(text)` entry point is a **deliberate single-player stub deviation**; it must NOT
  survive into a networked build without an owner ruling, since it becomes a moderation surface
- Any change to the economy baskets (WO-947) or stockpile caps

## 7. Acceptance criteria

- [ ] A player can request, and another player can fulfil, across **two real devices**
- [ ] The giver's stock **decreases**; the receiver's increases; both persist across sessions
- [ ] The receiver sees **who** gave — attribution is the retention mechanic, not a nicety
- [ ] A given/received ledger is visible per member
- [ ] `ClanService`'s data model is **reused**, not duplicated — one clan system in the tree
- [ ] The free-text path is closed or explicitly ruled on by the owner
- [ ] Server-side validation of every grant — ⚠ a client-authoritative donation is a **free-resource
      exploit**. This is the WO-931 lesson: `StubWalletProvider` shipped a client-authoritative grant in
      a submitted build. **Do not repeat it.**

## 8. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. **Two-device test** — headless cannot prove a social loop
3. Adversarial check: a tampered client **cannot** grant itself resources
4. Owner felt-verifies: *"does receiving a gift from a person feel good enough to come back for?"*
