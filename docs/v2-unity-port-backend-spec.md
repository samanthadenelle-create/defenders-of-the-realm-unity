# v2 Unity Port — Backend + Operational Spec

> **🔒 DECISION LOCKED 2026-05-18 — OPTION A.** Following the React-client decommission, the backend layer (Vercel serverless functions + Vercel Postgres + Solana RPC + wallet-signed-nonce auth) **continues unchanged**. The Unity C# client replaces the React TS client as the **canonical and only** consumer of these endpoints. UGS / PlayFab / Supabase were considered as managed-BaaS alternatives but rejected — the load-bearing custom server logic (wallet auth, treasury verification, server-side honeypot anti-cheat, OFAC screening, rewards gating) is not a drop-in fit for any feature-BaaS, and migrating would throw away existing work. The `defenders-of-the-realm` repository is being restructured to **backend-only** (React `src/` deletion is owner-side; the `api/` directory + Postgres schema + Vercel deployment continue as before). This spec is authoritative for that architecture going forward. — Per owner directive and Claude Code (Unity session) recommendation.

**Status:** Authoritative operational contract for the Unity port stream. Supplements `docs/v2-unity-port-spec.md` (architecture + 8-week build order + asset pipeline). The Claude Code session running in the Unity stream reads this as its **second required context document**, after the original v2 spec.

**Owner:** Samantha Denelle / DeNelle Studios.
**Publisher:** **DeNelle Studios**.
**Game:** *Defenders of the Realm* — a cozy 3D tower-defense game.

**Canon names (non-negotiable, copy verbatim everywhere):**
Town — **Avalon**. World-tree — **Elarion** (also called "the Heart"). Mage player hero — **Blaise**. Antagonist — **Alduin the Mournful**. Brand symbol — **The Heart-Wing** (dragon). Tagline — **"By lantern. By oath. By Heart."**

**Hard-rule callout (repeated in §16):** API responses use only **200 / 400 / 401 / 404 / 500** status codes. No 201, 202, 403, 405, 409, 422, 429, 503, etc. This is owner-locked per `CLAUDE.md` and is verified by the React project's API audit. The Unity stream inherits the rule absolutely — anywhere a rate-limit or auth-fail or duplicate-write would conventionally return 429 / 403 / 409, the Unity-callable endpoint must return 400 with a descriptive `error` string instead.

---

## Part 1 — Mental model

The original `docs/v2-unity-port-spec.md` answered *"how do we build the foundation?"* — Unity project shape, asmdef topology, the C# port table, the data-extraction protocol, the 8-week build order. By the time this spec is read, that foundation is shipped: the village loads, Wave 1 fires, the dungeon walks, the ATB engine resolves a battle. The Heart is glowing violet at world origin and Blaise can walk around it.

This spec answers the next question: ***how do we make the app actually work end-to-end as a deployed product?***

### Client vs backend — the distinction the decommission preserves

When the React **client** was decommissioned (2026-05-18), the **backend stayed alive**. The two are independent layers:

- **Backend** = Vercel serverless TypeScript functions in `api/` + Vercel Postgres + Solana RPC integration + custom auth/anti-cheat/OFAC/rewards logic. **Continues unchanged.** Lives in (what was) the React project's repo, soon to be backend-only after the React `src/` deletion. Same deployment URL, same DB connection, same Solana program integrations.
- **React client** = `src/` directory, Vite build, React+Three.js gameplay code, Vercel-served SPA. **Decommissioned 2026-05-18.** Code preserved as design reference; no longer deployed.
- **Unity client** = `defenders-unity/` project. **Becomes the canonical and only client.** Makes the same HTTPS calls to the same backend endpoints; the C# `HttpClient` replaces the TypeScript `fetch`, request/response shapes stay identical.

Practically: the backend doesn't care who's calling. Whether the client is React or Unity, the `api/inbox.ts` function returns the same JSON. The Unity stream's job is to **build the C# client equivalent of every React service call** — same endpoints, same auth flow (wallet-signed nonce), same Result<T> error handling, same status codes (200/400/401/404/500 only).

### What changes operationally under Option A

- The `defenders-of-the-realm` repository structure changes — `src/` deleted, `public/` mostly deleted (except `audio/` if any server-side handler references it), `api/` + `package.json` + Vercel deployment config preserved. Repo may be renamed to `defenders-backend` to reflect its new identity, though that's owner-optional.
- The Vercel deployment continues serving the API endpoints but stops serving the React SPA. The `defenders-of-the-realm.vercel.app` URL either (a) shuts down the SPA routing while keeping the `/api/*` routes alive, (b) is replaced by a static "coming soon" landing page, or (c) redirects to the eventual dApp Store listing. Owner-decision; see Part 18.
- The database (Vercel Postgres) is unchanged — same schema, same migrations, same queries. The Unity C# client calls the same endpoints, gets the same data shapes back.
- All server-side logic (wallet-nonce verification, treasury reads, anti-cheat ingestion, OFAC screening, rewards calculation) continues running in TypeScript inside Vercel functions. The Unity client never replicates this logic — it submits raw events, awaits server decisions, honors server responses.

### What this spec covers

That covers everything around the gameplay loop:

- **Backend services** the Unity client calls (clan, chat, future leaderboards, future reward-distribution endpoints).
- **Database schema** for everything the client persists server-side (which is intentionally minimal — the game loop is offline-first).
- **Wallet integration** — Mobile Wallet Adapter (MWA) on Seeker, Phantom/Solflare deep-links elsewhere, devnet discipline, pack purchase flow.
- **Secrets management** — what lives in env vars, what lives in hardware Seed Vault, what NEVER appears anywhere a build tool can read.
- **Environment config** — dev / staging / production, build-time-only switching.
- **Build + deploy pipeline** — Android APK target, dApp Store submission packet, versioning.
- **Save state + cloud sync** — local Application.persistentDataPath today; optional wallet-bound Postgres sync for v1.2.
- **Monetization integration** — cozy-covenant enforcement, pack catalog, entitlement check on game start.
- **Player rewards economy** — Streams A/B/C, all gated behind audit + pentest + anti-cheat clearance.
- **Anti-cheat integration** — five layers per `docs/anti-cheat-spec.md`; client emits raw events, server is the source of truth.
- **Logging + telemetry** — what's safe to log (wallet addresses) and what's not (PII, message contents, save dumps).
- **OFAC sanctions screening** — blocklist refresh cadence and check points.
- **Privacy + compliance** — GDPR / CCPA / COPPA, data retention, deletion procedure.
- **Audio system** — Unity AudioMixer mapped to the canonical mix from `docs/audio-mix-spec.md`.
- **Hard rules summary** — the 9 from `docs/claude-code-handoff.md` §2, restated for the Unity context.

The Unity agent operating against this spec **does NOT touch the React codebase under any circumstance** (per `docs/v2-unity-port-spec.md` Part 10). All work lives in the Unity repo at `C:\Users\Kayden-Laptop\Documents\defenders-unity\`. Every operational decision the agent makes is logged as a row in `defenders-unity/docs/unity-decisions.md` (the Unity equivalent of `docs/unity-decisions.md` referenced in the original v2 spec).

When this spec doesn't cover an operational question, the agent makes a defensible call, logs it in the decisions log, and continues. It does NOT block waiting for owner clarification on the merely-tactical. It DOES stop and flag if a decision is **irreversible AND user-visible** (a canon name, a real wallet address, a pack price, a privacy-affecting default).

---

## Part 2 — Backend services architecture

### 2.1 The shape

The React project ships with a small set of Vercel serverless functions in `api/` backed by **Vercel Postgres** (Neon under the hood). The Unity client **calls the same endpoints**. There is no separate Unity backend, no separate Unity database, no fork of the API. The backend is the single source of truth for clan + chat + (future) leaderboard + anti-cheat data; both clients consume it.

This avoids the worst-case nightmare of two divergent backends for the same game. Both clients see the same chat messages, the same clan rosters, the same leaderboard standings. A player who plays on the React web build at lunch and the Unity Seeker build at night sees a continuous social state.

### 2.2 The HTTP client

Unity calls the Vercel endpoints via either `UnityWebRequest` (built-in, fine for v2 foundation) or **BetterHttp** (a third-party package — recommended for production because of better retry, cancellation, JSON deserialization, and async/await ergonomics via UniTask). Decision: **start with UnityWebRequest in v2 foundation; migrate to BetterHttp if/when the boilerplate becomes painful.** Record the decision in `unity-decisions.md`.

Wire every call through `_Modules/Core/Services/ApiClient.cs` (already specced in `docs/v2-unity-port-spec.md` Part 3 row 145). The ApiClient exposes typed methods per endpoint:

```csharp
public interface IApiClient {
  UniTask<Result<InboxResponse>> GetInbox(string recipientCode, long since);
  UniTask<Result<SendMessageResponse>> PostMessage(string senderCode, string recipientCode, string phraseId);
  UniTask<Result<ClanChatResponse>> GetClanChat(string clanId, long since);
  UniTask<Result<ClanMeResponse>> GetClanMe(string memberCode);
  // … one method per endpoint …
}
```

The `Result<T>` pattern is mandatory — every call returns `{ ok: bool, data?: T, error?: string }`. **The ApiClient NEVER throws.** Network failures, server errors, validation errors all become `{ ok: false, error: "..." }`. The Unity UI layer reads `ok` and renders either the data or the error toast (Sonner-equivalent in Unity — recommend a simple `IToastService` over UI Toolkit).

### 2.3 The endpoint inventory

These are the React-project endpoints the Unity client must mirror. Schemas live in the React project's `api/` directory; the Unity agent reads them once and writes matching C# DTOs in `_Modules/Core/Services/ApiDtos.cs`.

| Endpoint | Method | Purpose | Unity caller |
| --- | --- | --- | --- |
| `/api/inbox?code=<recipientCode>&since=<unixMs>` | GET | Poll player mailbox (1:1 chat) | Mailbox UI |
| `/api/message` | POST | Send a templated chat message | Mailbox compose UI |
| `/api/clan/chat?clanId=<id>&since=<unixMs>` | GET | Read clan chat | Clan chat panel |
| `/api/clan/chat` | POST | Send a clan chat message | Clan chat panel |
| `/api/clan/create` | POST | Create a clan | Clan creation flow |
| `/api/clan/join` | POST | Join an open clan | Clan browser |
| `/api/clan/invite` | POST | Push-invite a contact | Clan officer panel |
| `/api/clan/leave` | POST | Leave the current clan | Clan settings |
| `/api/clan/lookup?clanCode=<code>` | GET | Look up a clan by code | Clan browser |
| `/api/clan/manage` | POST | Officer/leader management actions | Clan officer panel |
| `/api/clan/me?memberCode=<code>` | GET | The caller's current clan state | App boot |
| `/api/bug-report` | POST | Submit a bug report | Settings → Report a Bug |

### 2.3a Shipped-client endpoint audit — ⚠ BACKEND NEVER CONNECTED

> **Reality (owner, 2026-05-29):** the Unity client ships these backend-calling services
> with real-looking Vercel URLs, but **none of the routes were ever deployed/connected** —
> they are UI + client stub code built *ahead* of a backend that does not exist. Treat the
> calls below as the **ground-truth contract to build the backend against**, not as live
> features. Every mismatch/security item is a **pre-deploy gate**, not a live bug. See
> `WORK_ORDER_107`.

| Client call | Caller | In §2.3? | Pre-deploy action |
| --- | --- | --- | --- |
| `/api/game/save`, `/api/game/load?playerId=<wallet>` | `GameStateService` | future as `/api/save/sync` | canonicalize path; **add §2.6 wallet-signed-nonce auth (currently unauthenticated — public wallet = anyone can overwrite a save)** |
| `/api/events/track` | `EventTracker` | no (spec: `/api/events/ingest`; draft: `/api/metrics`) | pick ONE canonical telemetry name |
| `/api/promo/redeem` | `PromoCodeService` | **missing** | add endpoint + spec row |
| `/api/referral/generate`, `/api/referral/claim` | `ReferralService` | **missing** | add endpoint + spec row |
| `/api/tower-swap/log` | `TowerSwapService` | **missing** | add endpoint + spec row |
| `/api/bug-report` | `HelpMenu` | yes | fix base host (points at v1 `.vercel.app`, not `-v2`) |
| `/api/clan/*` | `ClanService` / `ClanChatPanel` | yes | ✅ in §2.3 |

### 2.4 Endpoints that don't exist yet but Unity will eventually call

These are the **forward endpoints** the Unity port should be scaffolded to accept once the React stream adds them. The agent does NOT build the React side; it only writes the Unity-side DTOs and ApiClient methods, ungated, returning `{ ok: false, error: "not yet enabled" }` until the server endpoint exists.

| Future endpoint | Purpose | Spec ref |
| --- | --- | --- |
| `/api/events/ingest` | Anti-cheat raw event ingestion (Layer 1) | `docs/anti-cheat-spec.md` §3 |
| `/api/rewards/claim` | Player claims an earned reward drop | `docs/anti-cheat-spec.md` §8 + monetization §12 |
| `/api/leaderboard?period=weekly` | Weekly leaderboard standings | `docs/monetization-v2-spec.md` §12 Stream B |
| `/api/honeypot/trigger` | Server-side report of honeypot detection (server emits, Unity never calls) | `docs/defensive-hardening-spec.md` §2 |
| `/api/entitlements?wallet=<address>` | List of packs owned by a wallet | `docs/monetization-v2-spec.md` §8.1 |
| `/api/save/sync` | v1.2 cloud-save endpoint | §8 of this doc |
| `/api/delete-account` | GDPR/CCPA data deletion | `docs/cyber-audit-end-to-end-spec.md` §3.B.2 |
| `/api/my-data` | GDPR data access request | same |

### 2.5 HTTP status codes — owner-locked

Per `CLAUDE.md`, the API contract uses **only** these five status codes:

- **200** — success (even with a body that says `{ ok: false, error: "..." }` for soft-fail cases like rate-limit; actually most React-project endpoints DO return 400 for rate-limit per the existing pattern, see `api/inbox.ts`)
- **400** — client error (bad input, rate-limit exceeded, missing field, unknown phrase, malformed code)
- **401** — authentication required / failed
- **404** — resource not found (clan doesn't exist, invite token consumed, save not present)
- **500** — server error (DB down, RPC unreachable, unexpected exception)

The Unity ApiClient maps any other status it receives (defensively) into `{ ok: false, error: "Unexpected response (HTTP <code>)" }`. **The Unity agent does not invent new status codes; if a server-side return value seems to want a different status, the agent files an `unity-decisions.md` row and uses 400 with a descriptive error string.**

### 2.6 Request authentication — the wallet-signed-nonce pattern

The React project currently runs an **auth-free** chat model — the client supplies its own `senderCode` and trust is bounded by the templated-phrase design (per `docs/mvp-chat-spec.md` §2, no free text → no moderation surface). The Unity stream **inherits this model verbatim** for v2 foundation. The chat / clan endpoints don't have real auth; the client identifies itself by passing its 6-character invite code.

For the **future** endpoints that move real money or grant real rewards (`/api/rewards/claim`, `/api/entitlements`, `/api/save/sync`, `/api/delete-account`), Unity must use the **wallet-signed-nonce** pattern:

1. Unity calls `GET /api/auth/nonce?wallet=<address>` → server returns a random 32-byte nonce, cached for 60 seconds.
2. Unity asks MWA to sign the nonce with the player's wallet → returns a base64 signature.
3. Unity calls the protected endpoint with headers `X-Wallet: <address>` + `X-Signature: <base64-sig>` + `X-Nonce: <nonce>`.
4. Server verifies: the signature recovers to the claimed wallet, the nonce is unused + unexpired, the wallet matches the resource being accessed.
5. On success: serve the request. On failure: 401 with `{ ok: false, error: "Invalid signature." }`.

The nonce endpoint (`/api/auth/nonce`) is itself unauthenticated; rate-limit per IP to prevent enumeration. **This pattern is spec'd here but not built until the corresponding write endpoints exist.** Until then, Unity's auth code path is the no-auth code-as-identity pattern.

### 2.7 Rate limiting

Server-side, per the existing React patterns:

- Per-sender chat rate: **30 messages / hour** (already enforced in `api/_db.ts` `insertMessageRateLimited` with a transactional advisory lock).
- Per-actor clan management: **60 actions / 10 min**.
- Per-IP for nonce requests: **20 / hour** (recommend; not yet built).
- Per-wallet payout requests: **1 / minute** (when payouts ship).

Hits the rate limit → server returns **HTTP 400** (NOT 429, per the owner-locked status-code rule) with `{ ok: false, error: "Rate limit exceeded — try again in a few minutes." }`. The Unity client's ApiClient surfaces this as a toast.

### 2.8 Error handling on the Unity side — never throw

Every ApiClient method returns `Result<T>`. The implementing class catches every exception at the call boundary:

```csharp
try {
  var req = UnityWebRequest.Get(url);
  await req.SendWebRequest();
  if (req.result != UnityWebRequest.Result.Success) {
    return Result<T>.Err($"Network error: {req.error}");
  }
  var body = JsonConvert.DeserializeObject<T>(req.downloadHandler.text);
  return Result<T>.Ok(body);
} catch (Exception ex) {
  return Result<T>.Err($"Unexpected error: {ex.Message}");
}
```

UI layer is `Result`-aware everywhere. Toasts surface `result.error`. Logging is structured: `Debug.LogWarning($"[ApiClient] {endpoint} failed: {result.error}")` — never logs the request body (may contain PII per Part 12), never logs the full response (may contain other players' data).

---

## Part 3 — Database schema + migrations

### 3.1 Single source of truth

The database is **Vercel Postgres**, provisioned + owned by the React project. The Unity client **does not own any schema**, **does not run any migrations**, and **does not touch the Postgres connection directly**. Unity talks to the database exclusively through the Vercel functions.

This is non-negotiable. A second client running its own schema would diverge instantly. The Unity agent records this constraint in `unity-decisions.md` row 1.

### 3.2 Tables Unity reads or writes (via the API)

| Table | Owner | Unity touches it via | Persistence |
| --- | --- | --- | --- |
| `messages` | React project | `/api/inbox`, `/api/message` | Forever (player can delete on request) |
| `clans` | React project | `/api/clan/*` | Forever until disbanded |
| `clan_members` | React project | `/api/clan/*` | While member; row deleted on leave |
| `clan_messages` | React project | `/api/clan/chat` | 90-day retention (per privacy §14) |
| `clan_invites` | React project | `/api/clan/invite` | Consumed on accept; expired after 7 days |
| `clan_join_log` | React project | (internal cooldown tracking) | 30 days |
| `bug_reports` | React project | `/api/bug-report` | Forever (write-only) |

### 3.3 Tables that don't exist yet but Unity's future calls assume

These are new tables the React stream adds when the corresponding endpoints land. The Unity agent does NOT create them; the agent assumes the schema below when writing DTOs.

**`wallet_entitlements`** — pack purchase history per wallet. One row per (wallet, pack_sku, tx_hash). Idempotent on tx hash. Columns: `id BIGSERIAL PK, wallet_address TEXT, pack_sku TEXT, purchase_rail TEXT CHECK ('stripe','usdc','sol','skr'), tx_hash TEXT, stripe_session_id TEXT, amount_usd_at_purchase NUMERIC(10,2), granted_at TIMESTAMPTZ, UNIQUE(wallet_address, tx_hash, stripe_session_id)`.

**`leaderboard_scores`** — per-period leaderboard standings. One row per (wallet, period_id, metric). Columns: `wallet_address TEXT, period_id TEXT, metric TEXT, score BIGINT, last_updated_at TIMESTAMPTZ, PRIMARY KEY(wallet_address, period_id, metric)`.

**`achievement_grants`** — one-time SKR drops per achievement per wallet. Columns: `wallet_address TEXT, achievement_id TEXT, granted_at TIMESTAMPTZ, payout_skr NUMERIC(10,4), payout_tx_hash TEXT, PRIMARY KEY(wallet_address, achievement_id)`. **Idempotency is structural** — the PK prevents double-grants.

**`gameplay_events`** — raw anti-cheat ingestion table (per `docs/anti-cheat-spec.md` §3.1). One row per emitted client event. Columns per the spec. Retention: **1 year** then purge.

**`game_stats`** — server's authoritative view of each player's progression (per `docs/anti-cheat-spec.md` §3.4). Columns per the spec.

**`honeypot_triggers`** — server-side log of honeypot detection hits (per `docs/defensive-hardening-spec.md` §2). Columns: `id BIGSERIAL PK, wallet_address TEXT, session_id TEXT, matcher_id TEXT, evidence_jsonb JSONB, detected_at TIMESTAMPTZ`. Server-only — Unity never reads this table or knows the matcher IDs.

**`review_queue`** — anti-cheat manual-review queue per `anti-cheat-spec.md` §8.1.

**`save_blobs`** — v1.2 cloud-save (per Part 8 of this doc). Columns: `wallet_address TEXT PRIMARY KEY, save_blob_b64 TEXT, save_hash TEXT, schema_version INTEGER, updated_at TIMESTAMPTZ`. One row per wallet; latest-write-wins.

**`deletion_requests`** — GDPR/CCPA deletion log per `docs/cyber-audit-end-to-end-spec.md` §3.B.2. One row per (wallet OR email, requested_at). Triggers a purge cascade across every table.

### 3.4 Schema versioning + migrations

- All migrations run via the React project's existing tooling. The agent never invokes `psql` or runs SQL directly against Vercel Postgres.
- New tables are created lazily by the corresponding endpoint's `ensureSchema()` (the same pattern `api/_db.ts` uses today — `CREATE TABLE IF NOT EXISTS` cached for warm function instances).
- Schema changes that affect the Unity client (a new column, a renamed column, a deleted table) are announced in the React stream's Friday rollup (`docs/spec-changes-week-N.md`) and absorbed Monday morning per `docs/v2-unity-port-spec.md` Part 8.
- The Unity agent's DTOs in `_Modules/Core/Services/ApiDtos.cs` are kept in lock-step. A `SchemaTests.cs` (per the data-extraction-protocol convention) verifies the DTOs round-trip through the live endpoint at CI time.

### 3.5 Connection pooling + transactions

- Vercel Postgres uses **connection pooling** by default (`POSTGRES_URL` is the pooled connection string; `POSTGRES_URL_NON_POOLING` is direct). The React side already uses the pooled string for read paths and `db.connect()` for transactional writes (per `api/_db.ts`).
- Unity does NOT manage any connection — it just makes HTTP calls.
- Long-running transactions: only the React side has them (e.g. the `insertMessageRateLimited` advisory-lock pattern). Unity is unaware.

### 3.6 Backup + restore

- Vercel Postgres provides **automated daily backups** with 7-day retention by default.
- Restore procedure documented in `docs/incident-response-plan.md` (per `docs/cyber-audit-end-to-end-spec.md` §3.B.3).
- The Unity agent does NOT manage backups, restores, or DR. If a DB incident affects players, the React-side incident response plan owns it; Unity simply degrades to offline-only.

### 3.7 NO PII rule

Per `docs/cyber-audit-end-to-end-spec.md` §3.B.2:
- **Wallet addresses are pseudonymous identifiers** per GDPR Art. 4 — allowed, retained until user requests deletion.
- **Real names, email addresses (outside Stripe billing), phone numbers, physical addresses are FORBIDDEN** in any table the Unity client writes to.
- **Chat message contents** are technically PII risk (a player can type a name) — the templated-phrase model bounds this for 1:1 chat; clan chat has the same constraint.
- **Save blob contents** (when cloud sync ships in v1.2) are stored encrypted-at-rest by Vercel Postgres, and never logged.
- **The Founder's Vow banner inscription** (8 chars, player-set) may technically be a name; classified as low-PII per the cyber-audit privacy matrix.

---

## Part 4 — Wallet integration architecture

### 4.1 The SDK

Use the official **Solana Mobile Unity SDK** from `solana-mobile/solana-unity-sdk`. At agent spinup, verify the latest version against the package README — the package URL or version may have moved. The SDK provides:

- **Mobile Wallet Adapter (MWA)** on Android — talks to the Seeker's hardware-backed Seed Vault or to any other MWA-compliant Android wallet (Phantom, Solflare, Backpack).
- **Deep-link wallet flow** on iOS / desktop — opens the user's external wallet via `solana:` URI scheme; wallet signs, returns to the game via URI callback.
- **Devnet, testnet, mainnet** RPC clients.
- **SPL token transfer** transaction builders (USDC, SKR).

The SDK is wrapped in `_Modules/Wallet/WalletService.cs` per `docs/v2-unity-port-spec.md` Part 3 row 142. The wrapper exposes a stable Unity-flavored async surface (`UniTask<Result<...>>` everywhere); module code never imports the SDK types directly.

### 4.2 The connect flow

1. Player taps **"Connect Wallet"** on the Title scene (per Part 3 row 121).
2. `WalletService.Connect()` opens the MWA prompt (Android) or a deep-link picker (iOS/desktop).
3. The wallet returns the public address.
4. Unity stores the address in `WalletState` (a runtime ScriptableObject — not persisted, re-connect needed each session for security).
5. The Title scene's "Connect" button replaces with the truncated address (`Cxx…YQ`); a "Disconnect" button appears.

The wallet connection is **session-only**. The save state remembers "yes, this player has wallet history" but does not store the address itself across sessions — the player re-connects each launch. This matches the React project's behavior and is a cyber-audit requirement (no long-lived auth tokens on the client).

### 4.3 Wallet addresses — public, safe to embed as constants

The Unity project's `data/wallets.json` file (per `docs/v2-unity-port-spec.md` Part 4) carries these **public** addresses, all of which are also documented in `docs/wallets-of-record.md`:

| Wallet | Public address | Purpose |
| --- | --- | --- |
| **Publisher / Studio** | `C5ummRoS1bB73gnBC57VqpGfD9QjM9g1iv3vc7cDbgYQ` | dApp Store identity, grant receipt, treasury multisig signer B |
| **Rewards Distributor** | `2JRmEmrqUbhTiHX3u5bes5kHYZeZkJ2V1cMWubxwnmNi` | Pays Stream A/B/C reward drops to players |
| **Dev / Staging** | `3Eeww2hyBUhiLi7AS2xsjZbfZQ2fmPFq8yh53vNzgaHe` | Gas-only Solflare desktop wallet for devnet testing |
| **Private SKR stake source** | **NOT REFERENCED IN CODE** | Owner-held off-chain; Option A per wallets-of-record §1.1 |
| **Revenue treasury (SOL)** | _pending Squads multisig provisioning_ | Receives SOL pack purchases |
| **Revenue treasury (USDC)** | _pending Squads multisig provisioning_ | Receives USDC pack purchases |
| **Revenue treasury (SKR)** | _pending Squads multisig provisioning_ | Receives SKR pack purchases |

These addresses can appear in code, in JSON, in committed files, in error messages. They are **public ledger data**.

### 4.4 PRIVATE KEY HANDLING — HARD RULES

These rules supersede every other concern in this spec. The Unity agent **never** violates them; if it cannot proceed without violating one, it stops and asks the owner.

1. **Private keys, seed phrases, and wallet passwords NEVER appear in code.** No source file, no JSON, no localized string, no comment, no markdown doc, no log line, no error message, no exception body. There is no exception.
2. **Private keys NEVER appear in environment variables.** Not `.env`, not `.env.local`, not Vercel env vars, not Unity build profile env vars. Hot wallet keys for backend services that need to sign (e.g. the Rewards Distributor, when it's wired up server-side) live in **Vercel env vars on the React-server side ONLY**, not anywhere the Unity build pipeline can reach.
3. **Private keys live only on hardware.** Owner-side: the Seeker's Seed Vault (hardware secure element). Player-side: each player's own wallet, on their own device.
4. **Signing happens through the wallet UI.** The Unity client builds an unsigned transaction → passes it to MWA → the wallet's UI shows the transaction details → the player approves → the wallet returns the signed transaction. **The Unity client never sees the key.** Not even momentarily, not even in memory.
5. **The Unity agent does not ask the owner for a private key or seed phrase.** Ever. If the agent needs a wallet to test devnet flows, it generates a fresh devnet keypair in a test fixture (`Assets/Tests/Fixtures/DevnetTestWallet.cs`) — the keypair is gitignored and used only for automated tests.
6. **The owner does not paste a private key into any AI chat surface.** If asked (the agent should never ask, but if context confusion causes one), the owner says "no" and the agent stops.

CI enforcement of these rules: the secret-grep guard from `docs/defensive-hardening-spec.md` §3.2, ported to the Unity repo as `defenders-unity/scripts/check-no-secrets.sh`, runs on every PR and blocks merges that contain any of:

- `BEGIN (RSA )?PRIVATE KEY` — PEM-encoded private key
- `"private_key":` — JSON service-account key
- `mnemonic.{0,40}=.{0,4}".{20,}` — mnemonic phrase assignment
- `whsec_[A-Za-z0-9]{20,}` — Stripe webhook secret
- `sk_live_[A-Za-z0-9]{20,}` — Stripe live secret key
- `sk-[A-Za-z0-9]{40,}` — Anthropic API key (also `sk-ant-` prefix)
- `helius-rpc\.com/\?api-key=` — Helius RPC with embedded key

### 4.5 The pack purchase flow

1. Player taps a pack in the Store UI (`_Modules/Wallet/PackStore.uxml`).
2. `PackStore.Buy(packSku, currency)` calls `WalletService.BuildPurchaseTx(packSku, currency)` which:
   - Reads the pack price from `data/packs.json`.
   - Builds an SPL token transfer (USDC, SKR) or native SOL transfer instruction.
   - Sets the recipient to the appropriate treasury wallet from `data/wallets.json`.
   - Returns the unsigned transaction.
3. `WalletService.SignAndSend(unsignedTx)` passes the transaction to MWA for signing.
4. The wallet UI shows the player: amount, recipient, fee. Player approves.
5. The signed transaction is submitted to the Solana RPC (devnet in v2 foundation).
6. Unity awaits **finalized** confirmation (~1s on Solana finality; show a spinner).
7. Unity calls `POST /api/entitlements/verify` (future endpoint per Part 2) with `{ wallet, packSku, txHash, currency }`.
8. The server independently fetches the transaction from Solana RPC (NEVER trusts the client claim), verifies destination + amount + token mint + finality, and writes an `wallet_entitlements` row idempotently keyed on `(wallet, tx_hash, pack_sku)`.
9. Server returns `{ ok: true, entitlement: { ... } }`.
10. Unity applies pack contents to local `GameState` (cosmetics unlock, economy tops up, convenience items appear).

If step 8 fails (server-side verification), the transaction stays on-chain but no entitlement is granted; player sees "Purchase recorded — pending verification." Unity retries the verification endpoint on next app boot.

### 4.6 Payment rails — per `docs/monetization-v2-spec.md` §4

- **SKR** (Solana Seeker phone's native token) — the grant-credibility vector. Required path.
- **SOL** — native Solana.
- **USDC** — stable, predictable. Most international users.
- **Stripe (USD)** — web-only path (Stripe Checkout doesn't fit cleanly in a native Unity Android build). For v2 Unity, Stripe is **deferred** — Unity ships wallet rails only at launch, and Stripe-paying users use the React web build. Decision recorded in `unity-decisions.md`. If owner directs Stripe-in-Unity later, the integration pattern is: open Stripe Checkout in an Android `CustomTabsIntent`, wait for callback URL, query the server for entitlement.

### 4.7 Devnet vs mainnet discipline — HARD RULE

The Unity port runs **devnet only** until BOTH:
- The cyber audit (`docs/cyber-audit-end-to-end-spec.md`) closes green, AND
- An external penetration test closes green.

Per `docs/claude-code-handoff.md` §2 hard rule 6. Mainnet wallet integration in Unity is a **build-time flag** in `_Modules/Wallet/WalletNetwork.cs`:

```csharp
public enum WalletNetwork { Devnet, Mainnet }
public static class WalletConfig {
  public const WalletNetwork DEFAULT = WalletNetwork.Devnet;
}
```

Switching the const requires:
- A `unity-decisions.md` row with owner sign-off.
- A separate commit that does NOTHING else (the structural-vs-behavior rule from `docs/claude-code-handoff.md` §2 hard rule 1).
- The owner has confirmed both gates above are green.

The agent does NOT flip this flag on its own initiative. Ever.

---

## Part 5 — Secrets management

### 5.1 The three buckets

Every value used by the project lives in exactly one of these buckets:

| Bucket | Examples | Lives where |
| --- | --- | --- |
| **NEVER in code or env vars** | Private keys, seed phrases, wallet passwords, Stripe secret keys, Postgres production password (the URL is OK; the URL embeds the password) | Hardware (Seeker Seed Vault, owner's password manager). For server keys: Vercel env vars on the **React-server side ONLY**. |
| **Unity build-profile env vars (gitignored)** | `API_BASE_URL`, `SOLANA_RPC_URL`, `FEATURE_FLAGS`, `DEV_TOOLS_ENABLED` | `defenders-unity/.env`, `defenders-unity/.env.local`, etc. Loaded at build time by a custom Unity Editor script (`Assets/Editor/EnvLoader.cs`) and baked into a generated `Generated/BuildConfig.cs`. The .env files are gitignored. |
| **Public + safe to embed** | Wallet addresses, Solana program IDs, Helius public RPC endpoint (no API key), Stripe **publishable** key, dApp Store publisher ID, canon strings | Anywhere — committed code, ScriptableObjects, JSON data files |

### 5.2 The Unity project's `.gitignore` additions

The base Unity gitignore (from Unity's GitHub template) covers `Library/`, `Temp/`, `Logs/`, `*.csproj`, etc. The Unity port adds:

```
# Secrets
.env
.env.local
.env.*.local
*.jks
*.keystore
*.p12
keystore.properties

# Generated config
Assets/Generated/

# Test wallets (devnet only, but still treat as secrets)
Assets/Tests/Fixtures/DevnetTestWallet.*

# Build outputs
Builds/
*.apk
*.aab
*.ipa

# Unity-specific
Library/
Temp/
Logs/
UserSettings/
obj/

# IDE
.vs/
.vscode/
.idea/
```

### 5.3 Pre-commit hook

Recommended: `defenders-unity/.husky/pre-commit` runs `scripts/check-no-secrets.sh` (ported from `docs/defensive-hardening-spec.md` §3.2). Cheap, fast, catches the accidental `git add .env` or copy-pasted key.

Owner-side responsibility: **never paste a private key, seed phrase, or wallet password into any AI chat surface, ever.** If the agent asks for one (which it should never do), the owner declines and the agent halts.

---

## Part 6 — Environment config

### 6.1 The three environments

| Environment | Purpose | Wallet network | RPC | Postgres | Dev tools | Logging |
| --- | --- | --- | --- | --- | --- | --- |
| **dev** | Local agent development | Devnet | Helius devnet public | Shared dev Postgres on Vercel | All visible | Verbose, includes `Debug.Log` |
| **staging** | Owner pre-launch validation | Devnet | Helius devnet public | Shared staging Postgres on Vercel | Visible (gated by env) | Production-like, structured |
| **production** | Real players, dApp Store APK | **Mainnet** (only after audit + pentest green) | Helius mainnet (with API key, server-side) | Production Postgres on Vercel | **HIDDEN** | Structured only, no `Debug.Log` |

### 6.2 Unity build profiles

Unity 6 LTS supports build profiles natively. The Unity agent creates three:

- **Development** — IL2CPP off (Mono for faster iteration), debugging symbols on, `DEV_TOOLS_ENABLED=true`, devnet RPC, dev API base URL (`https://defenders-of-the-realm-dev.vercel.app/api`).
- **Staging** — IL2CPP on, debugging symbols off, `DEV_TOOLS_ENABLED=false` (toggle via in-game cheat code for owner-only access), devnet RPC, staging API base URL.
- **Release** — IL2CPP on, no debugging symbols, no dev tools (compile-defined out, not just hidden), mainnet RPC, production API base URL.

The build profile injects scripting define symbols (`DEV_TOOLS`, `DEV_TOOLS_AVAILABLE`, etc.) that compile-out dev-only code paths from Release builds. **Compile-out, not runtime-hide** — a release APK does not even contain the dev panel's bytecode. This is a security property: even if a user dumps the APK, the dev tools aren't there.

### 6.3 Build-time, not runtime, switching

Environment switching is intentionally a **build-time configuration**:

- A Release APK CANNOT be retargeted at devnet by flipping a runtime flag. The RPC URL is baked in at compile time.
- A Development build CANNOT accidentally point at mainnet (the Mainnet enum value triggers a compile-time assert when paired with the Development profile).
- This eliminates an entire class of "I forgot to flip the staging toggle before shipping" incidents.

The cost: rebuilding to switch environments. Worth it. Logged as a decision in `unity-decisions.md`.

### 6.4 Environment variable loading

`Assets/Editor/EnvLoader.cs` is an editor-only script that runs as part of the build pipeline:

1. Reads `.env`, `.env.local`, `.env.<profile>.local` (per dotenv resolution order).
2. Generates `Assets/Generated/BuildConfig.cs` with `public const string API_BASE_URL = "..."`.
3. Generated file is gitignored.
4. Runtime code reads `BuildConfig.API_BASE_URL` — a plain const, IL2CPP can inline it.

This avoids reading env vars at runtime (Unity's `System.Environment.GetEnvironmentVariable` doesn't work the same way on Android / IL2CPP).

---

## Part 7 — Build + deploy pipeline

### 7.1 Android build target

The Unity port's primary deployment is **Android APK** for the Solana Mobile dApp Store. Settings per `docs/v2-unity-port-spec.md` Part 2:

- **Scripting backend:** IL2CPP (release builds; Mono OK for development).
- **API compatibility level:** .NET Standard 2.1.
- **Target architecture:** ARM64 only (Seeker requires; rejects 32-bit).
- **Target API level:** match the current Solana Mobile dApp Store minimum (verify at submission; as of 2026-05 = Android 13 / API 33).
- **Texture compression:** ASTC 6×6 (Android default for the project).
- **Package name:** `studios.denelle.defendersoftherealm` (matches the React project's dApp Store identifier from `docs/solana-dapp-store-submission.md` §4.1 — confirm at submission; the current TWA APK packet uses `com.defendersoftherealm.game`, which may conflict; **the agent flags this as an owner decision in `unity-decisions.md` rather than picking one unilaterally**).
- **APK signing:** the Android keystore is the highest-value secret in the project. Per `docs/cyber-audit-end-to-end-spec.md` §3.B.4: stored on encrypted disk, NEVER in repo, NEVER in cloud sync without encryption-at-rest, backed up in three locations (encrypted USB in a safe + paper recovery sheet + primary copy). Keystore passphrase passed to `gradlew` via env var, never via CLI args (would leak into shell history). Filename gitignored (`*.jks`, `*.keystore`, `keystore.properties`).

### 7.2 iOS build target — DEFERRED

iOS support is out of scope for v2 foundation. Solana Mobile is Android-first; the Seeker is the primary device; iOS users have the React web build. The Unity project's IL2CPP path is iOS-compatible, so the deferral is just "we don't ship the .ipa," not "the code can't compile for iOS." Logged as a decision; revisit for v2.1.

### 7.3 dApp Store submission

Per `docs/solana-dapp-store-submission.md`:

1. **Pre-flight requirements:**
   - Release-signed Android APK from Unity (the .apk produced by `gradlew assembleRelease` after Unity export).
   - Publisher Solana wallet with ~0.2 SOL: `C5ummRoS1bB73gnBC57VqpGfD9QjM9g1iv3vc7cDbgYQ` (the Studio wallet).
   - KYC/KYB verification passed (one-time, owner-side).
   - App icon (512×512 PNG, no alpha) — Elarion tree silhouette in violet.
   - Feature graphic (1024×500 PNG).
   - Min 4 screenshots at 1080×1920 (consistent orientation; portrait recommended).
   - Privacy Policy publicly hosted.
   - Terms of Use (optional but recommended).
   - Support email (`samanthadenelle@gmail.com` or hosted /support page).

2. **Listing fields** (all from `docs/solana-dapp-store-submission.md` §4 — verbatim):
   - App name: **Defenders of the Realm**
   - Short description (≤30 chars): **Tend the Heart. Hold the dark**
   - Tagline (in-game): **By lantern. By oath. By Heart.**
   - Category: Games → Strategy
   - Content rating: Everyone 10+
   - Price: Free (with in-app purchases — cosmetic only at launch)
   - Wallet required: No (wallet is optional)

3. **Review timeline:** ~3-5 business days per the Solana Mobile publisher portal.

4. **Versioning:** semantic versioning **MAJOR.MINOR.PATCH** (e.g. `v1.0.0`). Build number is a monotonically incrementing integer (Android `versionCode`). Every release bumps the build number; Major/Minor/Patch tracks the user-visible version.

### 7.4 Cloud Build

Unity Cloud Build (a Unity Pro feature) is **optional but recommended** for CI — it builds the APK on every commit, runs tests, archives the artifact. Cost: ~$30/month (Unity Pro Plus tier includes it; Personal tier doesn't). Decision deferred to owner — Unity flag in `unity-decisions.md` as an open question, falls back to local-machine builds until owner provisions a subscription.

### 7.5 Hard rule — no live mainnet treasury activation until both gates green

Per `docs/claude-code-handoff.md` §2 hard rule 6:

> **No payouts of real SKR to wallets until cyber audit AND external pentest both close green.**

The Unity port inherits this rule. Concretely:
- The pack purchase flow can be built and tested on devnet now.
- Mainnet flip (Part 4.7) requires both gates green AND explicit owner approval.
- The Rewards Distributor wallet (`2JRmE…`) does NOT pay any real SKR until both gates green AND the `services/contests.ts` boolean flag (React-server side) is set to `true`. Unity simply receives reward grants via inbox messages from the server — Unity is downstream of the gate.

---

## Part 8 — Save state + cloud sync

### 8.1 Local save (v1)

Unity's persistent save lives on the device:

- **Storage:** JSON file in `Application.persistentDataPath` (e.g. `/data/data/studios.denelle.defendersoftherealm/files/save.json` on Android). NOT `PlayerPrefs` — PlayerPrefs is XML-based, easy to introspect via ADB, and has size caps that the full save schema would brush against.
- **Encryption:** AES-256 with a key derived from the device's installation ID (`SystemInfo.deviceUniqueIdentifier`). NOT for security against the player (the player owns the device and the save; they can crack it if they want), but for casual tamper-resistance and to keep the file from being trivially editable by a curious teen.
- **Schema:** mirrors the React project's `src/store/saveSchema.ts` Zod shape. Ported to C# as `_Modules/Core/State/SaveSchema.cs` with `[Serializable]` records and Newtonsoft.Json attributes (per `docs/v2-unity-port-spec.md` Part 3 row 119).
- **Versioned migrations:** every schema change bumps the `version` field and adds a migration step in `_Modules/Core/State/SaveMigrator.cs`. On app start, the loader reads the file's version → runs each missing migration in order → writes back. If the migration chain throws, fall back to a fresh save with a one-line toast: "Save data couldn't be loaded — starting fresh. (Old save backed up.)" and rename the broken file to `save.broken.json` for diagnostics.

### 8.2 Save schema additions for Unity

The Unity port adds to the schema:
- `version` (int, current = 1).
- `lastPlayedVersion` (string, e.g. "1.0.3" — for migration-need detection).
- `unityBuildId` (string — for telemetry diagnostics on which build wrote the save).

Otherwise the schema mirrors the React project. The cross-stream sync protocol (per `docs/v2-unity-port-spec.md` Part 8) ensures any v1 schema change lands in `data/save-schema.json` and the Unity port absorbs it next Monday.

### 8.3 Cloud sync — v1.2 feature (NEW, NOT in v2 foundation)

Currently each device has its own save. A player who plays on the React web at lunch and the Unity Seeker app at night has two separate saves. This is the **v1.2 scope** — cross-device sync via the backend.

**Architecture:**
1. Player connects wallet on Unity.
2. Unity hashes the local save (`SHA-256` of the JSON).
3. Unity uploads to `POST /api/save/sync` with `{ wallet, save_blob_b64, save_hash, schema_version }` (auth via wallet-signed-nonce per Part 2.6).
4. Server upserts the `save_blobs` row keyed by wallet.
5. On next launch on a different device, Unity calls `GET /api/save/sync?wallet=<address>` → gets back the latest blob.
6. Unity compares server's blob hash vs local hash:
   - **Same:** no-op, continue.
   - **Server newer:** download + merge (or replace).
   - **Local newer:** push local up.
   - **Diverged** (both modified since last sync): show a modal — *"Your save on this device differs from your other devices. Which would you like to keep?"* with "This Device" / "Other Devices" / "Show me both" options.

**Conflict resolution:** latest-write-wins by default; explicit user prompt on diverged saves. The merge-or-replace UI is intentionally interrupt-driven (an action-required modal, not auto-dismissing per the `CLAUDE.md` Game UX convention).

**NOT in v1.** Spec'd here for v1.2 so the Unity port can be scaffolded to accept the endpoint when it lands. Until then, every device's save is independent.

### 8.4 Privacy implication

Cloud save means the save blob (encrypted on the wire by HTTPS; encrypted at rest by Postgres) leaves the player's device. The Privacy Policy must disclose this in v1.2 if/when the feature ships. GDPR deletion request must purge `save_blobs` rows in addition to all other tables.

---

## Part 9 — Monetization integration

### 9.1 Pack catalog

The pack catalog is data-driven from `data/packs.json` (shared with the React project per the data-extraction protocol). Each pack has:

```json
{
  "sku": "hearth-spark",
  "tier": 1,
  "name": "Hearth Spark",
  "tagline": "The Folk send a small thanks for tending the Heart.",
  "pricing": { "usd": 1.99, "usdc": 1.99, "sol": 0.018, "skr": 25 },
  "contents": {
    "cosmetics": ["pet-skin-aether-warm"],
    "economy": { "glimmer": 25, "crystals": 200, "food": 50, "coins": 100 },
    "convenience": [{ "item": "instant-build-token", "count": 1 }]
  },
  "packExclusiveCosmetic": "pet-skin-aether-warm"
}
```

Unity loads this on boot via `_Modules/Wallet/PackCatalogLoader.cs` and hydrates `PackDef` ScriptableObjects in memory.

### 9.2 Pack purchase flow

Already covered in Part 4.5. Summary: pack-tap → wallet sign → on-chain submit → server verify → entitlement write → client grant.

### 9.3 Entitlement check on game start

On app boot (after wallet connect):
1. Unity calls `GET /api/entitlements?wallet=<address>` (future endpoint per Part 2.4).
2. Server returns the wallet's `wallet_entitlements` rows.
3. Unity reconciles: any owned pack whose contents haven't been applied to local `GameState` → apply now (cosmetics unlock, economy top-up, convenience items refunded).
4. Reconciliation is idempotent — re-running on the next boot doesn't double-grant.

For non-wallet players (devnet-only path, no entitlement check needed) and for players who haven't yet connected, the entitlement check is skipped silently — they just don't have packs.

### 9.4 Cozy covenant enforcement — HARD RULES

Per `docs/monetization-v2-spec.md` §2:
- **No combat-stat-changing items in any pack.** No "more damage per shot," no "longer tower range," no "higher hero HP," no "stronger walls." Walls don't get stronger when bought; towers don't fire farther; the hero doesn't deal more damage.
- **No FOMO countdowns.** Packs don't expire. The seasonal pass is permanent unlock with no expiry.
- **No in-game store pop-ups.** The store is always player-initiated. The "Found a coin pouch" Heart-altar event (per `monetization-v2-spec.md` §7.1) is the ONLY discovery prompt allowed, and it's <1% probability per session and dismissible.
- **Every pack item is also earnable in-game.** No pack-exclusive item gates progress. The "pack-exclusive cosmetic" is a unique reskin, not a unique gameplay capability.
- **Covenant statement visible in the store UI.** The store screen displays: ***"You are never required to spend anything. Ever."***

### 9.5 Test enforcement

The React project has a `packs.test.ts` that asserts every pack's items pass the covenant filter. The Unity port writes the equivalent test at `Assets/Tests/Editor/PackCovenantTest.cs` (Unity Test Framework, EditMode):

```csharp
[Test]
public void EveryPackPassesCovenantFilter() {
  foreach (var pack in PackCatalog.All) {
    foreach (var item in pack.Contents.AllItems) {
      Assert.IsFalse(ItemRegistry.IsCombatStat(item.Sku),
        $"Pack {pack.Sku} contains combat-stat item {item.Sku} — COVENANT VIOLATION");
      Assert.IsTrue(ItemRegistry.IsEarnable(item.Sku),
        $"Pack {pack.Sku} contains non-earnable item {item.Sku} — COVENANT VIOLATION (must be also earnable in-game)");
    }
    Assert.IsFalse(pack.HasExpiryTimer, $"Pack {pack.Sku} has expiry — COVENANT VIOLATION");
  }
}
```

CI runs this test on every PR. A failure blocks merge.

### 9.6 Founder's Vow — the one ethical scarcity exception

The Founder's Vow (Tier 5, $49.99 / 0.45 SOL / 600 SKR) is the only pack with launch-window scarcity. The pack itself is gated by `Date.now() < FOUNDER_WINDOW_END` (set to v1 launch date + 30 days, per `docs/monetization-v2-spec.md` §4 — confirm value at launch). After the window, the pack disappears from the store but every existing buyer keeps their pack contents (including the permanent in-village banner with 8-char inscription).

The Unity store UI surfaces a small note when the window is open: *"Founder's Vow — available during launch window only."* No countdown timer, no urgency framing. Just the fact.

---

## Part 10 — Player rewards economy

### 10.1 The three streams

Per `docs/monetization-v2-spec.md` §12, the player-rewards economy has three streams, all funded by **yield** on the owner's 1M SKR private stake (Option A per `docs/wallets-of-record.md` §1.1), never by pack revenue.

| Stream | Yield share | Trigger | Payout cadence | Per-event payout |
| --- | --- | --- | --- | --- |
| **A — Achievement drops** | 40% of yield | First-time milestone completions (wave 5, wave 30, first dungeon, first bond rank 5, etc.) | Immediate on milestone | 0.5–25 SKR |
| **B — Weekly leaderboard** | 40% of yield | Weekly skill-based leaderboard | Monday 00:00 UTC | 1.25–40 SKR per slot |
| **C — Seasonal tournament** | 20% of yield | Quarterly tournament | Season end | Up to ~1,000 SKR for winner |

### 10.2 The Rewards Distributor

All payouts go through the **Rewards Distributor** wallet: `2JRmEmrqUbhTiHX3u5bes5kHYZeZkJ2V1cMWubxwnmNi`. This wallet:
- Is hardware-backed (Seeker Seed Vault).
- Holds only the SKR currently committed to upcoming payouts; **drains as paid out**.
- Is funded periodically from the owner's private stake source (off-record per Option A).
- Is excluded from anti-cheat heuristics (founder allow-list per `docs/anti-cheat-spec.md` §5).

### 10.3 The three gates

Payouts to player wallets are gated behind **all three** of:
1. **Audit + pentest green** (per `docs/claude-code-handoff.md` §2 hard rule 6).
2. **Anti-cheat clearance** (per `docs/anti-cheat-spec.md` — wallet behavior score ≥ 50 for any payout > 5 SKR, plausibility validation for the triggering event, no honeypot trigger in the session).
3. **OFAC clearance** (per `docs/cyber-audit-end-to-end-spec.md` §11 — recipient wallet not on the SDN list).

The `services/contests.ts` boolean flag is **FALSE** until all three gates pass. Unity is downstream of this flag — Unity simply never receives reward grants until the server starts emitting them.

### 10.4 Streams B + C — additional legal gate

Per the existing locked rules, **Streams B and C are additionally gated behind a legal opinion on skill-based contest defensibility.** The lawyer's opinion is stored in `docs/legal-opinion-aml-<date>.md` per the cyber-audit spec. Unity has no role here — the lawyer + owner gate the server side; Unity simply receives grants when they're emitted.

### 10.5 How Unity surfaces a reward

When a reward is granted server-side, an **inbox message** arrives in the player's mailbox (the same `/api/inbox` endpoint that delivers chat messages). The message carries:
- A phrase ID indicating it's a reward (a new `reward_*` phrase namespace, e.g. `reward_achievement_wave_30` / `reward_leaderboard_weekly`).
- A structured payload (JSON in a new column or in the message body, per the schema the React stream defines when this ships) with: amount in SKR, reason, transaction hash of the actual payout, link to view on Solscan.

Unity's mailbox UI renders reward messages with a small SKR-coin icon and a "View payout" link that opens Solscan in the system browser. Players can dismiss; the payout is already in their wallet — the inbox message is a notification, not a claim button.

### 10.6 Why pull-based vs push-based

We could push notifications via Android's FCM, but FCM requires Google services + an additional setup process. The inbox-as-mailbox pattern is simpler, works without push permission, and is consistent across Web (the React build), Android (Unity), and any future iOS port. Logged decision in `unity-decisions.md`.

---

## Part 11 — Anti-cheat integration

Per `docs/anti-cheat-spec.md`. The Unity client's anti-cheat responsibilities are deliberately minimal — almost all the work is server-side.

### 11.1 The five layers (server-side; Unity contributes raw data)

1. **Server-authoritative event validation** — Unity emits raw events; server is the source of truth for milestone completion, leaderboard scores, etc. Unity NEVER claims rewards locally and asks the server to confirm; it claims via the server, which independently validates.
2. **Wallet behavior scoring** — server queries Solana RPC for wallet age, transaction history, holdings. Unity contributes nothing here.
3. **Statistical anomaly detection** — server analyzes session-aggregate metrics (timing variance, frame-rate consistency, achievement claim ordering). Unity contributes the raw telemetry samples.
4. **Honeypot achievements** — entirely server-side per `docs/defensive-hardening-spec.md` §2. **The Unity client never knows which events are honeypots.** Unity emits the same generic events for everything; the server's `services/honeypot-detector.ts` pattern-matches against its private definitions.
5. **Economic disincentives** — server enforces caps + decay. Unity surfaces these as in-game UI text when the player hits a cap.

### 11.2 Unity client responsibilities

1. **Send raw events.** Every meaningful gameplay event posts to `/api/events/ingest` (future endpoint per Part 2.4) with: `{ kind, payload, seq, sessionId, frameRateSample, timestamp }`. The server decides what counts. Unity does NOT claim achievements locally and then ask the server to confirm; the server emits achievement grants on its own.

2. **Honor server's response on reward eligibility.** When the player hits a milestone, Unity does NOT post a "give me my reward" request. Unity simply emits the milestone event; the server runs its plausibility checks + wallet behavior score + anomaly detection, and IF the wallet clears, the server initiates the SKR payout from the Rewards Distributor and sends an inbox message. Unity has no "claim" button.

3. **Never expose honeypot names or detection logic in client code.** CI guard `defenders-unity/scripts/check-no-honeypot-leak.sh` (ported from `docs/defensive-hardening-spec.md` §3.3) runs on every PR:

   ```bash
   # Honeypot detection is server-side ONLY per defensive-hardening-spec.md §2.
   # The string "honeypot" must not appear in Assets/_Modules/, Assets/Scripts/,
   # or any C# file under the Unity src/ tree.
   LEAKS=$(git grep -i 'honeypot' -- 'Assets/' 2>/dev/null || true)
   [ -z "$LEAKS" ] && echo "✅ No honeypot references in Unity client code" || exit 1
   ```

4. **Provide telemetry the anomaly detector needs.** Per `docs/anti-cheat-spec.md` §5:
   - Per-30s frame-rate samples (`{ ts, fps }`).
   - Per-input event timing (tap timestamps, key presses) — aggregated as inter-event intervals, sent in batches.
   - Per-session: total play time, scenes visited, abilities used.

### 11.3 Session ID

Each app launch generates a fresh UUID v4 session ID. All events in that session carry it. The session ID is NOT persisted across launches — a session is bounded by app foreground/background lifecycle. Server uses session IDs to group events for the anomaly detector.

### 11.4 Sequence numbers

Each event in a session has a monotonically increasing `seq` integer. Server detects gaps or rewinds as a tampering signal. Unity generates seq locally and never resets within a session.

### 11.5 Failure modes

If the events endpoint is unreachable (network failure, server down), Unity queues events locally (in-memory only, not persisted) and flushes on reconnect. **If the queue exceeds 1000 events, drop oldest** — bounded memory, no DoS surface. The lost telemetry slightly degrades anomaly detection for that session; not a correctness issue.

---

## Part 12 — Logging + telemetry

### 12.1 Unity-side logging

- `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` for development.
- Production builds: structured capture via `Application.logMessageReceived` → JSON line writer → uploaded to telemetry endpoint (future).
- **`Debug.Log` is stripped in IL2CPP release builds** via the `[Conditional("DEBUG")]` attribute on a wrapper class. Production binaries do not even contain the log strings.

### 12.2 Server-side logging

- Vercel function logs (the platform-native log stream, viewable in the Vercel dashboard).
- Structured JSON for parseability: `{ ts, endpoint, wallet?, status, durationMs, error? }`.

### 12.3 What NEVER gets logged

Per `docs/cyber-audit-end-to-end-spec.md` §3.B.6:
- **Wallet seed phrases** — impossible by construction; Unity never has them.
- **Private keys** — same.
- **Real names, real emails (outside Stripe billing logs server-side), phone numbers** — Unity doesn't collect these.
- **Chat message contents** — already constrained to templated phrases; phrase IDs are fine to log, the rendered text is not.
- **Full save state dumps** — never logged client-side, never logged server-side.
- **Player-typed strings** (banner inscription, letter-to-the-next text) — never echoed in logs.

### 12.4 What CAN be logged

- **Wallet addresses** — pseudonymous identifiers per GDPR Art. 4. OK.
- **Game state IDs** — wave number, scene name, encounter ID. OK.
- **Encounter outcomes** — win/loss/draw, hero/pet HP remaining. OK.
- **Performance metrics** — FPS, frame time, memory pressure. OK.
- **API endpoint + status code** — for monitoring. OK.
- **Error messages** — as long as they don't echo user input back (sanitize before logging).

### 12.5 Telemetry — optional

**Unity Analytics** is the built-in path; it's free, integrates without extra packages, and gives FPS / crash data on real hardware. The Unity agent enables it in the Staging + Release profiles only (off in Development to avoid polluting dashboards with dev-loop data). An **opt-out toggle** in the Settings menu lets players disable it; OS-level Limit Ad Tracking is respected.

**Sentry** (or another third-party error-tracking service) is an open question for the owner. Recommend deferring to v1.1; for v2 foundation, Application.logMessageReceived → local log file rotation is sufficient.

### 12.6 Crash reporting

Unity Analytics provides crash reporting out of the box for Android. The crash report includes:
- Stack trace (with IL2CPP symbols if symbol upload is configured — recommended).
- Device model, OS version, free memory, free storage.
- The wallet address IFF connected.
- Last N log lines (sanitized — see §12.3).

No PII in crash reports. Crash reports are accessible to the owner via the Unity Dashboard.

---

## Part 13 — OFAC sanctions screening

### 13.1 The blocklist

Per `docs/cyber-audit-end-to-end-spec.md` §3.B.8 + §11:
- Maintain a curated list of OFAC-sanctioned Solana addresses in `services/sanctions.ts` (server-side, React project).
- Sources: the US Treasury OFAC Specially Designated Nationals (SDN) list, filtered for Solana addresses.
- Stored as a simple JSON array of base58 addresses (currently ~12 known addresses; the list is small but high-impact).

### 13.2 Check points

The server checks the OFAC blocklist:
- On **every wallet connect** — if the connecting wallet is on the list, refuse the connection with HTTP 400 + `{ ok: false, error: "Service unavailable in your jurisdiction." }`. Do NOT echo the OFAC fact specifically (don't tip off attackers about the detection).
- On **every payout destination** — before a payout from the Rewards Distributor, check the destination address. If sanctioned, abort the payout, log to `review_queue` with reason `ofac_destination`, and notify the owner.

### 13.3 Refresh cadence

- **Quarterly refresh** from OFAC's SDN list. Owner runs a manual procedure (pulling the SDN list, filtering for Solana addresses, diffing against the current `services/sanctions.ts`, owner-approving the diff, pushing the update).
- **Emergency refresh** if a high-profile sanctioned address is added between quarterly cycles (e.g. a major hack proceeds get OFAC-flagged) — owner pushes within 24 hours of awareness.

### 13.4 Unity's role

Unity doesn't directly maintain the blocklist. Unity calls the server endpoints; the server applies the check. The Unity-side user experience on a sanctioned wallet connect is a single toast — *"Service unavailable in your jurisdiction."* — and the wallet connect is refused.

### 13.5 Unity wallets are pre-cleared

The three project wallets (`C5umm…`, `2JRmE…`, `3Eeww…`) and the future revenue treasury wallets are owner-controlled, not on the SDN list, and explicitly allow-listed in `services/sanctions.ts` to prevent accidental self-block.

---

## Part 14 — Privacy + compliance

### 14.1 The data classification (recap)

Per `docs/cyber-audit-end-to-end-spec.md` §3.B.2, every piece of data is classified:

| Field | Personal data? | Sensitive? | Retention |
| --- | --- | --- | --- |
| Wallet address | YES — pseudonymous (GDPR Art. 4) | No | Until user deletion request |
| Save state | No (game data) | No | Local until reset; cloud (v1.2) until deletion |
| Banner inscription (8 chars) | If contains player's name, yes | No | Until user deletion request |
| Clan / chat messages (phrase IDs) | If player typed a name in clan chat — bounded by templated phrases | Possibly | **90 days then purged** |
| Achievement / leaderboard records | YES (linked to wallet) | No | Indefinite (public ledger pattern) |
| Gameplay events (anti-cheat) | YES (linked to wallet) | No | **1 year then purged** |
| IP address (server logs) | YES — GDPR direct identifier | No | **≤7 days** (Vercel default) |
| Browser fingerprint, cookies, ad IDs | **Not collected** — verify before each release | n/a | n/a |
| Email (Stripe checkout, when used) | YES — direct identifier | No | Per Stripe policy + deletion endpoint |

### 14.2 GDPR

- **Lawful basis** for processing wallet address: legitimate interest (the player connected the wallet to play the game).
- **Lawful basis** for processing chat / gameplay events: consent (acceptance of the Terms of Use).
- **Data subject rights:** access (`GET /api/my-data`), deletion (`POST /api/delete-account`), rectification (deletion + re-create).
- **Breach notification:** within 72 hours per Art. 33. Procedure in `docs/incident-response-plan.md`.

### 14.3 CCPA

California users have the same right to deletion. The deletion endpoint serves both GDPR and CCPA — no separate code path needed.

### 14.4 COPPA

The game is rated **Everyone 10+** (per `docs/solana-dapp-store-submission.md` §4.3). The Unity port shows a **first-play age gate**: *"This game is for ages 13+. Are you 13 or older?"* with Yes / No buttons. "No" → declines to proceed and shows a parental-permission message. The age gate is one-time-per-install (persisted in save state); not a runtime check. **The Unity agent does NOT collect a birthdate** — just the binary "13+ confirmed."

### 14.5 Data retention

- **Chat messages**: 90 days then auto-purged (cron in the React server, runs nightly).
- **Wallet entitlements**: indefinite (proof of purchase).
- **Gameplay events**: 1 year (anti-cheat analysis window).
- **IP addresses in logs**: 7 days (Vercel default).
- **Save blobs (v1.2)**: until user deletion request.
- **Bug reports**: indefinite (low volume, useful for diagnostics).

### 14.6 Privacy Policy hosting

Per `docs/solana-dapp-store-submission.md` §0, the Privacy Policy must be publicly hosted at a stable URL. Options:
- Same domain as the live game (`https://defenders-of-the-realm.vercel.app/privacy`) — preferred, single-source-of-truth, easier to maintain.
- A separate documentation site (Notion, GitBook).

**Open question for the owner — Part 18.** Unity's About screen links to whichever URL is provided via `BuildConfig.PRIVACY_POLICY_URL`.

### 14.7 Data deletion procedure

Documented in `docs/incident-response-plan.md` (per `docs/cyber-audit-end-to-end-spec.md` §3.B.3). Summary:
1. User requests deletion via in-game Settings → "Delete My Data" (Unity) or via email to support.
2. Unity calls `POST /api/delete-account` with wallet-signed-nonce auth.
3. Server cascades delete across: `wallet_entitlements`, `leaderboard_scores`, `achievement_grants`, `gameplay_events`, `game_stats`, `clan_members` (the wallet leaves any clans), `messages` (both as sender and recipient), `save_blobs`.
4. Server returns success.
5. Unity clears local save, returns to title screen.
6. Server logs deletion timestamp + wallet address to `deletion_requests` (for audit, NOT for re-creating the data).

---

## Part 15 — Audio system + asset pipeline

### 15.1 Unity AudioMixer

Per `docs/audio-mix-spec.md` §7 (the Unity port note):
- Unity's AudioMixer with mixer groups: **Master / Music / SFX / UI / Voice / Ambient**.
- Six AudioSource components, one per music track (title / village / dungeon / battle / victory / defeat).
- Per-track defaults loaded from `data/audio-mix.json` (the shared canonical mix file):

| Track | Default volume | Loop | Fade-in | Fade-out |
| --- | --- | --- | --- | --- |
| `title` | 0.6 | yes | 1200ms | 1000ms |
| `village` | 0.4 | yes | 1200ms | 1000ms |
| `dungeon` | 0.25 | yes | 1200ms | 1000ms |
| `battle` | 0.7 | yes | 600ms | 600ms |
| `victory` | 0.7 | no | 200ms | 800ms |
| `defeat` | 0.5 | no | 1500ms | 1500ms |

### 15.2 Master volume slider

Settings menu has a master volume slider, range **0..1.5×**. Multiplies all track default volumes. Persisted in save state. Default: 1.0.

### 15.3 Volume nudges

Per `audio-mix-spec.md` §4, certain events temporarily dip music to let dialogue or moments land:
- Lore-stone read (dungeon): 0.25 → 0.12 for 6s, then restore.
- First Watch stop fires (village): 0.4 → 0.28 for 5s.
- Boss intro cinematic: dungeon → silence until battle starts.
- Dragon hero-banner first-load (title): 0.6 → 0.4 for 8s.

Implemented as a `MusicDirector` MonoBehaviour with a `NudgeVolume(track, toVolume, durationMs, fadeMs)` coroutine that tweens the AudioSource volume over the duration, then restores.

### 15.4 Audio files

Same files as React project, copied (not re-encoded) from `public/audio/` to `Assets/Audio/`:
- `/audio/title.mp3`
- `/audio/village.mp3`
- `/audio/dungeons/echoes-beneath-elarion.mp3`
- `/audio/battle.mp3`
- `/audio/victory.mp3`
- `/audio/defeat.mp3`
- Plus all SFX files (one-shots for clicks, ability casts, wall damage, etc.).

Import settings: **Vorbis compression**, Quality 70 for music tracks (`Streaming` load type, `Compressed In Memory`), Quality 80 for SFX (`DecompressOnLoad`).

### 15.5 First-tap audio unlock

Mobile browsers require a user gesture before audio plays. Unity Android doesn't have this constraint, BUT Unity's WebGL build (if ever shipped) would. Since v2 foundation is Android-only, this is a no-op — but the `MusicDirector` is built defensively so any queued `PlayTrack()` calls before the first frame fire on first user input.

### 15.6 Reduced-motion accessibility

Per `audio-mix-spec.md` §5, `prefers-reduced-motion: reduce` (or the platform equivalent on Android — `Settings.Global.TRANSITION_ANIMATION_SCALE = 0`) snaps all fades to instant. Implemented in MusicDirector via a check at fade-start.

---

## Part 16 — Hard rules summary

These are the 9 hard rules from `docs/claude-code-handoff.md` §2, restated for the Unity context.

1. **Never refactor behavior and change gameplay in the same commit.** Every refactor commit is purely structural. Log gameplay bugs found mid-refactor in `unity-decisions.md` followups, address separately. Identical rule as React.

2. **No `_Modules/<A>` imports `_Modules/<B>` runtime.** Cross-module coordination flows through `_Modules/Core/Services/`, `Data/` ScriptableObjects, or `_Modules/Core/State/`. Enforced by the per-module asmdef topology (per `docs/v2-unity-port-spec.md` Part 2). Equivalent of the React lint rule.

3. **`Core/`, `Data/`, `Localization/`, `Assets/Audio/` are LEAVES.** They never import from `_Modules/`. Identical layering as React.

4. **Every module has an `OWNERSHIP.md`** declaring `owns / may consume / may NOT`. Verify on every new module. Identical rule.

5. **File size ceilings:** logic/state/services ≤ 500 lines; rendering orchestrators (scene controllers) ≤ 700 lines. C# can be terser than TypeScript so the limits are forgiving. Extract before merging if a file passes them.

6. **No payouts of real SKR to wallets until cyber audit AND external pentest both close green.** Unity is downstream; mainnet wallet flip requires owner sign-off. See Part 4.7 + Part 7.5 + Part 10.3 of this spec.

7. **Cozy covenant: every pack must be earnable in-game; no combat-stat items in any pack; no FOMO countdowns; no in-game store pop-ups.** Enforced by `PackCovenantTest.cs`. See Part 9.4.

8. **No JavaScript obfuscator added to the build pipeline. Ever.** Translated to Unity: **no IL2CPP-on-top-of-Mono code obfuscator (e.g. Beebyte's Obfuscator, Garbage Man) added to the build pipeline.** IL2CPP itself already gives reasonable name mangling for free; further obfuscation produces theatrical security at high cost (debuggability, build-time, brittle reflection-using code paths). The same ADR rationale from `docs/defensive-hardening-spec.md` §1 applies verbatim — the threats are server-authoritative; obfuscating the client is defending the front door of an empty house.

9. **End-of-day daily log.** Every working day the agent writes `defenders-unity/docs/daily-log-<date>.md` — one paragraph: what shipped, what was harder than expected, anything that drifted scope. Identical rule as React.

---

## Part 17 — Cross-stream sync protocol

Repeats `docs/v2-unity-port-spec.md` Part 8 for completeness, with operational additions:

- **Friday rollup (React stream):** the React project's agent emits `docs/spec-changes-week-N.md` summarizing every spec change.
- **Monday absorb (Unity stream):** the Unity agent's first task every Monday is to read the latest rollup. Update affected ScriptableObjects, DTOs, and ApiClient methods.
- **Major operational changes** (treasury wallet rotation, OFAC list refresh, schema-breaking DB migration, status-code-rule amendment, wallet-network flip) **pause the Unity stream until owner approval**. The agent logs `paused awaiting owner: <reason>` in `unity-decisions.md`.
- **Minor operational changes** (new pack added, new phrase added, new endpoint with no breaking change) are absorbed in the same Monday session.

The pace asymmetry holds: Unity is research; React is shipping. If React's spec evolves faster than Unity can absorb, Unity stays slower. Unity does not race.

---

## Part 18 — Open questions for owner

Decisions the agent cannot make alone. The Unity agent logs each as a row in `unity-decisions.md` with `Reversible? <see notes>` and proceeds with the documented fallback until the owner answers.

1. **Cloud save sync — v1.2 or never?** Spec'd in Part 8.3. Fallback: each device's save is independent. *Reversible.*

2. **Stripe webhook secret — owner provisions via Vercel env var.** The Unity port does NOT need this (Stripe rail is React-web-only in v2). But: when Stripe-in-Unity is built (v2.x), the server-side webhook receiver needs the secret. Owner provisions via the Vercel dashboard, NOT via chat. *Owner-action-required.*

3. **Unity Cloud Build subscription — yes/no?** ~$30/month. Fallback: local-machine builds. *Reversible.*

4. **iOS build path — v2.1, sooner, or never?** Spec'd as deferred in Part 7.2. Fallback: Android-only. *Reversible.*

5. **Sentry or other error-tracking service — yes/no?** Fallback: Unity Analytics + log file rotation. *Reversible.*

6. **Privacy policy hosting — same domain as the live game (`/privacy`), or separate?** Fallback: same domain (simpler). *Reversible but disruptive to existing links if changed.*

7. **KYC/KYB documentation — ready for dApp Store submission?** Owner-side, one-time, ~1-2 week turnaround. Required for submission, not for development. *Blocks submission only.*

8. **Package name resolution — `studios.denelle.defendersoftherealm` (Unity native) vs `com.defendersoftherealm.game` (the TWA APK packet from `docs/solana-dapp-store-submission.md`).** The two cannot coexist on a Play Store account; the dApp Store may also reject the second submission with a conflicting bundle ID. Owner picks one; agent migrates if needed. *Reversible only before first dApp Store submission; irreversible after.*

9. **OFAC SDN list refresh — owner or agent?** Currently spec'd as owner-manual quarterly. Agent could be tasked with the quarterly diff if owner provides the SDN list URL and a sign-off cadence. *Reversible.*

10. **Treasury wallet provisioning timing — when does the Squads multisig setup land?** Spec'd as pending in `docs/wallets-of-record.md` §4. Until provisioned, pack purchase testing on devnet uses placeholder addresses; mainnet purchases blocked. *Owner-action-required.*

---

## Appendix A — File-existence fallbacks

The original v2 spec Appendix A still applies. Additional fallbacks for this operational spec:

| Referenced doc | Fallback | Status |
| --- | --- | --- |
| `docs/persistence-onchain-spec.md` | (exists; cite directly for the v1.1 on-chain save design) | Confirmed exists |
| `docs/incident-response-plan.md` | Not yet authored; cyber-audit-end-to-end-spec.md §3.B.3 is the placeholder | Pending — written during the cyber audit pass |
| `docs/threat-model.md` | Not yet authored; cyber-audit-end-to-end-spec.md §3.B.1 is the placeholder | Pending — written during the cyber audit pass |
| `docs/privacy-compliance-matrix.md` | Not yet authored; cyber-audit-end-to-end-spec.md §3.B.2 is the placeholder | Pending — written during the cyber audit pass |
| `docs/legal-opinion-aml-<date>.md` | Not yet authored; lawyer engagement TBD | Pending — owner action |

---

## Appendix B — Document version history

| Version | Date | Notes |
| --- | --- | --- |
| 1.0 | 2026-05-19 | Initial publication. Operational contract for the parallel Unity port stream. Locks: HTTP status code rule (200/400/401/404/500 only); wallet-signed-nonce auth pattern for future protected endpoints; build-time-only environment switching; private-key handling hard rules; cozy covenant test enforcement; cloud-save sync as v1.2; OFAC quarterly refresh cadence; package name as Part 18 open question. |

---

_Tend the Heart. Hold the dark. Tend the keys. — and in C# this time._
