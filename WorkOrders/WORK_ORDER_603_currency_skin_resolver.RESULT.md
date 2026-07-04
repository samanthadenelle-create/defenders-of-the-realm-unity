# WORK ORDER 603 — Currency Skin Resolver — RESULT

**Status:** IMPLEMENTED (edit-only; orchestrator gates + commits + deploys)
**Lane:** Monetization/Backend (isolated — no scene files, no gameplay/combat/world code touched)
**Date:** 2026-07-03

---

## 1. Chosen injection mechanism — Option B (runtime `skin.json`), + a URL-param override

One build artifact serves both skins. The active skin is resolved at runtime, synchronously,
BEFORE the first view builds, in this order (first hit wins):

1. **URL query param** `?skin=pi | ?skin=skr` (WebGL) — mirrors the existing
   `FeatureFlags.ApplyUrlActivationOnce` pattern; allow-listed to `pi|skr` only so a crafted
   link can only swap the skin, never game state. The SKR Vercel deployment appends `?skin=skr`.
2. **`skin.json` `"active"` field** — `Assets/Resources/Data/Canonical/skin.json` (WebGL-safe
   via `CanonicalJson`), mirrored to `StreamingAssets/Data/Canonical/skin.json`.
3. **`"pi"` default** — the live production skin. An un-configured / offline / garbled-json boot
   ALWAYS lands on today's Pi behaviour → **zero regression**.

**Rationale:** matches the WO rule ("Pi feed → Pi; otherwise → SKR"), keeps ONE build artifact
(no dual-build), needs no rebuild to switch (config/URL only), reuses the project's existing
WebGL-safe JSON loader (`CanonicalJson`) and URL-flag convention, and defaults to Pi so the live
deployment is untouched. The single cold-start read is `Resources.Load` (synchronous even on
WebGL) — no fetch round-trip, unlike the WO's Option-B fetch concern.

**Resolver file:** `Assets/_Modules/Core/Platform/CurrencySkinResolver.cs`
**Record file:** `Assets/_Modules/Core/Platform/CurrencySkin.cs` (both in `DeNelle.Core`, namespace `DeNelle.Core.Platform`)

### `skin.json` shape

```json
{
  "version": 1,
  "active": "pi",
  "skins": {
    "pi":  { "skinId": "pi",  "currencySymbol": "π",    "currencyName": "Pi",  "authMode": "PiSdk",        "brandingKey": "pi_network", "storeCtaVerb": "Spend Pi",   "identityKeyKind": "PiUid",        "bindIdentityOnAuth": false },
    "skr": { "skinId": "skr", "currencySymbol": "$SKR", "currencyName": "SKR", "authMode": "SolanaWallet", "brandingKey": "seeker_skr", "storeCtaVerb": "Spend $SKR", "identityKeyKind": "WalletPubkey", "bindIdentityOnAuth": true  }
}}
```

`CurrencySkin` carries exactly the WO-required fields: `SkinId (pi|skr)`, `CurrencySymbol (π|$SKR)`,
`CurrencyName (Pi|SKR)`, `AuthMode (PiSdk|SolanaWallet)`, `BrandingKey`, `StoreCtaVerb`,
`IdentityKeyKind (PiUid|WalletPubkey)`, plus `BindIdentityOnAuth` (identity-migration gate) and a
`ResolveIdentityKey(piUid, walletPubkey)` selector. Hardcoded `PiDefault`/`SkrDefault` fallbacks
guarantee the game never boots without a skin. **Presentation reads `CurrencySkinResolver.Active` —
no view hardcodes π/Pi/$SKR.**

---

## 2. Full Pi-reference audit (file:line)

### Client (Unity C#)
| # | Surface | Location | Disposition |
|---|---|---|---|
| 1 | Auth button + Pi branding (violet fill, "Sign in with Pi" / "Pi: {user}" / "Retry Pi sign-in" / "Signing in…"), auto-sign-in, `/api/pi/verify` call | `Assets/_Modules/Core/Platform/PiSignInController.cs` (whole file; VerifyUrl :24, BuildButton :210-233, SignInAsync :97-165) | **WIRED** — Pi button/polling gated behind `AuthMode==PiSdk`; SKR shows a teal "Connect Wallet" button routed to the resolver. Pi path byte-identical. |
| 2 | Pi SDK abstraction seam (`IPiPlatform`, `PiPlatform`, `WebGLPiPlatform`, `EditorPiPlatform`) | `Assets/_Modules/Core/Platform/IPiPlatform.cs`, `PiPlatform.cs`, `WebGLPiPlatform.cs`, `EditorPiPlatform.cs` | Left as-is — only exercised by the Pi path (never entered under SKR). Not player-facing branding. |
| 3 | Owner dev-tools gate reads `PiSignInController.SignedInUsername` / `OnSignedIn` | `Assets/_Modules/HUD/OwnerDevToolsOverlay.cs:94-112` | Left — owner-only, inert under SKR (no Pi sign-in). |
| 4 | Bug-report identity reads `PiSignInController.SignedInUid` | `Assets/_Modules/HUD/BugReportVM.cs:179-184` | Left — null under SKR → falls back cleanly. Not branding. |
| 5 | TODO comment referencing `PiSignInController` | `Assets/_Modules/Core/Diagnostics/PrivacySensitiveUi.cs:21-22` | No action (comment). |
| 6 | NeonDB identity key (`GameState.BoundWallet` → `playerId`) | `Assets/_Modules/Core/State/GameStateService.cs` (BindWallet :627, playerId :966/1245/1297) | **WIRED via resolver** — identity selection centralized in `CurrencySkin.ResolveIdentityKey`; bind gated behind `BindIdentityOnAuth` (Pi=false, SKR=true). |

**Not found (important):** there is **no hardcoded `π` currency symbol** in any view (the only `π`
is a math comment, `ProjectileMover.cs:124`), and **no "Spend Pi" literal** anywhere. The in-game
currencies are Gold/Crystal/Wood/Iron/Food/Wisdom/Glimmer, and the pack store
(`PackStore.cs` / `PackCatalog.cs`) is **already multi-rail SOL/USDC/SKR** — there is no "Pi" purchase
rail. So the in-game currency display and store CTAs are already Solana-native / currency-agnostic;
nothing there needed reskinning. The `CurrencySymbol`/`StoreCtaVerb` fields are provided for any
future currency lockup or CTA that wants them.

### Backend / config
| # | Surface | Location | Disposition |
|---|---|---|---|
| 7 | Pi `/me` token verification endpoint | `api/pi/verify.js` | Pi-only; entered only by the Pi path. Left. |
| 8 | Cloudflare Worker Pi payment approve/complete (`PI_API_KEY`, `PI_APP_ID`, `api.minepi.com`, entitlement `"pi_pack_small"`) | `pi-backend/src/index.ts`, `pi-backend/wrangler.toml` | Pi payment rail; SKR pays via the in-client Solana wallet path (`WalletService.Pay`), not this Worker. Left. |
| 9 | NeonDB save/load auth | `api/game/save.js`, `api/game/load.js`, `api/_lib/wallet-auth.js` | **Already Solana-native**: `player_id` = base58 wallet pubkey, verified by an ed25519 wallet signature. SKR aligns natively. See follow-up B. |
| 10 | WebGL page template hard-loads the Pi SDK `<script src="https://sdk.minepi.com/pi-sdk.js">` | `Assets/WebGLTemplates/Pi/index.html:11` | Build-time template (baked into the single artifact). Harmless under SKR (`window.Pi` unused). See follow-up C. |

---

## 3. What was wired

- **New:** `CurrencySkin.cs` (record + `SkinAuthMode`/`SkinIdentityKeyKind` enums + `ResolveIdentityKey` + Pi/SKR defaults) and `CurrencySkinResolver.cs` (skin.json load, URL override, `Active`, `IsSkr`, `WalletConnectRequested` hook) — single source of truth.
- **New data:** `skin.json` in both `Resources/Data/Canonical/` and `StreamingAssets/Data/Canonical/` (active = pi).
- **`PiSignInController.cs`:** resolves `CurrencySkinResolver.Active` in `Start()` before building the button; Pi button + auto-sign-in only run under `AuthMode==PiSdk`; under `SolanaWallet` the corner button becomes a teal "Connect Wallet" that fires `CurrencySkinResolver.RequestWalletConnect()`; on Pi sign-in success, binds the Pi UID as identity **only if** `BindIdentityOnAuth` (default off → zero regression).
- **New:** `WalletSkinBootstrap.cs` (`DeNelle.Wallet`) — installs at boot **only under the SKR skin**, subscribes the resolver hook, drives `WalletService.Connect()` (auto-selects real `SolanaWalletProvider` when the Solana SDK define is set, else the devnet stub), and binds the connected pubkey as the NeonDB identity when the skin opts in. This closes the connect loop at the service level (Core cannot reference `DeNelle.Wallet`, so this is the seam).

**Zero-regression guarantee:** default skin = `pi`; under the Pi skin the Pi button, colour, labels,
auto-sign-in, verify flow and identity behaviour are unchanged, and `WalletSkinBootstrap` never
subscribes. Every new cross-module call uses `?.` / guarded handlers (no silent failures, §12).

---

## 4. NeonDB migration note (FLAGGED — not run)

- **No schema migration is required for the SKR skin.** `player_data.player_id` already stores a
  base58 wallet pubkey and `api/game/save.js` already authenticates with an ed25519 wallet
  signature — the SKR skin is native to the existing schema.
- The Pi skin's `BindIdentityOnAuth` stays **false**, so existing Pi players' NeonDB identity is
  unchanged. Flipping it true (binding Pi UID as `player_id`) would introduce a **mixed keyspace**
  (Pi UIDs alongside wallet pubkeys) AND require a **Pi-UID auth branch in `save.js`/`load.js`**
  (today the save path only accepts a wallet signature). That is a deliberate future decision — **not
  run here.**

---

## 5. Flagged follow-ups

- **A — Solana wallet-connect UI completeness (main gap):** `WalletSkinBootstrap` makes the button
  functional at the service level (opens MWA/deep-link with the real SDK, mock-connects with the
  stub, binds identity). But the richer connect UX — address display, disconnect, network badge —
  currently lives in `WalletConnectDialog.cs`, which is **UXML-based and does not render in WebGL
  builds** (CLAUDE.md §8). A **code-built** connect/account panel is needed for a polished SKR UX.
  Also, the real on-chain path requires the Solana Unity SDK (`SOLANA_SDK` define) to be compiled in;
  without it the SKR build mock-connects via `StubWalletProvider` (fine for a demo, not real funds).
- **B — Backend Pi-UID save auth branch:** only needed if the Pi skin ever wants server-authoritative
  saves keyed on the Pi UID (see §4). Not required for the SKR submission.
- **C — Pi-free SKR page template:** `WebGLTemplates/Pi/index.html:11` loads the Pi SDK at the page
  level (build-time, harmless under SKR). A fully Pi-free SKR page would use a separate WebGL template
  — a build-config change, optional polish, not runtime-skinnable.
- **D — Branding wordmark assets:** `BrandingKey` (`pi_network` / `seeker_skr`) is exposed but no view
  currently renders a wordmark sprite (Pi identity today is only the button colour + label). If a
  wordmark is added later it must resolve via `BrandingKey` — no Pi logo image surface exists to swap
  today.

---

## 6. Handoff to orchestrator / UI

- **Gate + commit:** four new/edited `.cs` (brace/paren/NUL-clean, verified) + two `skin.json`.
  Files: `CurrencySkin.cs`, `CurrencySkinResolver.cs`, `PiSignInController.cs` (edited),
  `WalletSkinBootstrap.cs`, `Resources/.../skin.json`, `StreamingAssets/.../skin.json`.
- **Deploy (orchestrator):** the SKR preview is the same build served with `?skin=skr` on the URL
  (or set `skin.json` `"active":"skr"` for that deployment). Hand the resulting Vercel preview URL to
  UI for the Seekerthon submission — that deploy/preview step is the orchestrator's, per the WO.
