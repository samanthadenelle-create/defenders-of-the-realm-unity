# Missing-Components Audit — Defenders of the Realm (v2 Unity foundation)

**Auditor:** Product-analyst gap-analysis pass (read-only)
**Date:** 2026-05-19
**Scope:** What a shippable game needs that is NOT built yet — priority focus on
the monetization loop. Audited against `docs/v2-unity-port-spec.md` (esp. Parts 4
& 9), `docs/monetization-v2-spec.md`, the nine `Assets/_Modules/` modules, the
canonical JSON under `Assets/StreamingAssets/Data/Canonical/`, and the existing
gap records (`docs/qa/bug-log.md`, `docs/unity-decisions.md`,
`docs/audit/security-audit.md`, `docs/audit/architecture-review.md`).

**This is a gap analysis, not a bug log.** Bugs in already-built features live in
`bug-log.md` (18 entries) and are cross-referenced, not re-litigated. This doc
catalogues things that **do not exist**. The Solana SDK deferral is a recorded,
accepted decision (`unity-decisions.md` Weeks 5-7; SEC-001) — noted where it
gates a gap, not re-argued.

---

## Executive summary

The v2 foundation is, structurally, in good shape: nine modules with clean asmdef
isolation, a tested pure-C# ATB engine, a defensible save layer, and a wallet
module that runs end-to-end on a devnet stub. The architecture review's verdict —
"SOLID, with concerns" — holds. But measured against a *shippable game*, large
swaths are simply absent, and the monetization loop in particular is a **half a
loop**: it can simulate a purchase and grant an entitlement in memory, but it has
no purchase confirmation, no real settlement, no receipt, no restore-purchases,
no convenience-token redemption, and no treasury-transparency surface beyond a
single address string. The store can take a (fake) payment; it cannot yet *give
the player what they paid for* in any durable, auditable way.

The single biggest theme: **systems were ported as compiling C#, but the
connective tissue that makes a game shippable — confirmation dialogs, options
menus, audio wiring, an inventory to receive purchased items, scene integration —
was deferred and largely never scheduled.** The Week-8 acceptance gate is an
*end-to-end playthrough*; almost none of the end-to-end glue exists.

**P0 gap count: 14** (blocks ship — devnet-loop or core-game). These are listed
in the priority index below. P1: 17. P2: 13.

**Top monetization gap (the one to fix first):** there is **no
purchase-confirmation step and no durable entitlement record**. `PackStore.Purchase`
goes button-click → `WalletService.Pay` → in-memory `OwnedItemIds.Add(sku)` with
no confirm-before-sign modal (SEC-004) and no record of *what was bought, in what
currency, for how much, against which tx signature*. A player who buys a pack,
then edits or loses their `PlayerPrefs` save, has no proof of purchase and no way
to restore it — and the game has no idea a real transaction ever happened. For a
title with five real-money packs this is the gap that turns "we took your money"
into "we have no record of it." Everything else in monetization is downstream of
fixing the entitlement model.

---

## Priority index — the P0 gaps (14)

| # | Gap | Section |
|---|-----|---------|
| P0-1  | No purchase-confirmation modal before signing | §1.4 |
| P0-2  | No durable entitlement record (rail, amount, tx hash, timestamp) | §1.5 |
| P0-3  | No restore-purchases / entitlement re-sync | §1.6 |
| P0-4  | No receipt surface or purchase history | §1.7 |
| P0-5  | Convenience tokens are bought but cannot be received or redeemed (no inventory) | §1.8 |
| P0-6  | No devnet SKR mint — the spec's headline devnet deliverable cannot run (BUG-011) | §1.9 |
| P0-7  | Solana SDK unresolved — no real devnet settlement (BUG-010, CODE-002) | §1.9 |
| P0-8  | No settings / options menu (audio, quality, controls) | §2.1 |
| P0-9  | No audio system or Audio Mixer wiring — game ships silent | §2.2 |
| P0-10 | No pause system | §2.3 |
| P0-11 | No tutorial / onboarding gameplay — `Onboarded` is never set true | §2.5 |
| P0-12 | Breach→ATB→return loop unintegrated; wrong-scene + wrong-wave (BUG-008, CODE-001) | §3 / Village, BattleATB |
| P0-13 | Weeks 4-7 scene integration never completed — no end-to-end build exists | §3 / all gameplay |
| P0-14 | Save persistence unverified end-to-end; entitlements client-trusted only (SEC-006) | §2.8 |

---

# 1. MONETIZATION (priority section)

The monetization loop, audited end-to-end. State of each link in the chain:
**store UI → catalog/pricing → purchase → transaction → entitlement grant →
persistence/integrity → transparency → confirmation → restore → receipt.**

## 1.1 What is BUILT (the working half)

For an honest baseline — these exist and work on the devnet stub:

- **`PackStore.cs`** — renders all five packs from `PackCatalog`, one card each:
  name, tagline, USD reference, three rail chips (SOL/USDC/SKR), a contents
  summary, a Buy button, an "Owned" state. Cozy disclaimer line wired.
- **`PackCatalog.cs` + `packs.json`** — all five canonical packs hydrate
  correctly (Hearth Spark → Founder's Vow), with `pricing`, `contents`, and the
  `currencyDisclaimer`. Verbatim from `monetization-v2-spec.md` §4–§5.8.
- **`WalletService` + `IWalletProvider` + `StubWalletProvider`** — connect,
  balance, simulated `SendPayment` with a ~1s finality delay and a fabricated tx
  signature. Devnet-gated by a `const` plus a defense-in-depth Mainnet block.
- **`WalletConnectDialog.cs`**, **`WalletRegistry.cs`** (loads `wallets.json`),
  **`WalletEndpoints.cs`** — connect UI and RPC/mint config scaffolding.
- **`ApplyPackContents`** — credits crystals/food/coins into `GameState.Resources`
  and records the pack SKU + cosmetic SKUs in `OwnedItemIds`, then `Save()`s.

That is a genuine devnet-stub demo loop. The gaps below are everything between
that demo and a shippable monetization system.

## 1.2 Pack store UI — gaps

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-UI-1 | **No pack detail page.** `monetization-v2-spec` §7.2 specifies a per-pack modal: full contents list with cosmetic *preview renders*, every economy amount, every convenience item with its description, the four rail tabs, the live USD reference, a Gift button. The Unity store collapses all of this into one card with a one-line text summary (`DescribeContents`). No modal, no per-item breakdown, no previews. | P1 | 3-5d |
| MON-UI-2 | **No store discovery entry points.** §7.1 specs three: a `🛍` HUD glyph, a post-wave Damage-Report "Quick Repair Pack" CTA (>60% wall damage), and the rare Heart-altar coin-pouch event. None exist — `VillageHudController` has no store hook; there is no Damage Report modal at all. The store is unreachable from gameplay. | P1 | 2-3d |
| MON-UI-3 | **No cosmetic preview rendering.** Pack cosmetics are bare SKU strings (`cosmetic.lanternlight.hero-outfit`). There is no cosmetic system, no SKU→asset registry, no preview render. A player buying a "hero outfit" sees nothing — there is nothing to show and nothing to equip. | P1 | 1-2wk |
| MON-UI-4 | **No Gift flow.** Covenant rule C6 (`monetization-v2-spec` §2, §7.2) makes every pack giftable; the spec calls it "the heart" of the generosity posture. Entirely absent. Defensible to defer (needs the social/backend layer), but it is a covenant item, so it must be a *recorded* deferral. | P2 | 1-2wk |
| MON-UI-5 | **Founder's Vow launch-window not enforced.** `packs.json` carries `founderOnly: true` and the card shows a "Launch window only" tag, but nothing gates purchasability by date. §4 requires the banner to stop being purchasable after v1.1. | P2 | 0.5d |

## 1.3 Pack catalog + pricing display — gaps

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-PR-1 | **USD reference is static, not a live oracle.** §4.1 requires `60 SKR ≈ $4.99 USD as of <timestamp> UTC` from a price oracle. The Unity store shows `pricing.usd` from `packs.json` as a fixed `"$4.99 reference"` string — there is no oracle, no timestamp, no live conversion. For a crypto-native rail this is a real UX miss (a SKR holder cannot see what their SKR is worth right now). | P1 | 3-5d |
| MON-PR-2 | **Stripe / fiat rail entirely absent.** §2 / §4 / §7.3 make Stripe the *primary* revenue rail (most players will not have a wallet). The Unity port reads only SOL/USDC/SKR — Stripe is "web-only, out of scope" per `packs.json` `_schemaNotes`. That is a defensible scope call for the *Unity* port, but it means the Unity build can never be a standalone shippable storefront: a non-wallet player has no way to pay. This needs an explicit product decision (web-view checkout? defer all paid packs to the React client?). | P1 | recorded decision + 1wk |
| MON-PR-3 | **No seasonal pass (Keeper's Almanac).** `monetization-v2-spec` §6 calls the pass "the spine of long-term monetization" — 30-tier track, free 10-tier parallel, milestone unlocks. There is no pass data file, no pass UI, no tier-track system, nothing. Packs are the entry point; the pass is retention — and retention is unbuilt. | P1 | 2-3wk |
| MON-PR-4 | **`SchemaTests.cs` for `packs.json` / `wallets.json` missing.** Spec Part 4 mandates a schema test per data file. The wallet-critical files have none — cross-stream drift in pricing or addresses would not be caught at build time. SEC-007 also asks for runtime sanity checks (reject non-positive price, implausible base58). | P1 | 1d |

## 1.4 Purchase confirmation — **TOP GAP**

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-CF-1 | **No confirm-before-sign modal.** (= SEC-004, P0-1.) `PackStore.Purchase` runs button-click → `WalletService.Pay` → `SendPayment` with no in-game step echoing pack name, exact native amount, currency, and destination address before the transaction is built. The status banner only says "Confirming…" *after* the call is in flight. §7.4 explicitly specs the modal text ("You will sign a transaction sending X USDC…"). The wallet app's own prompt is the only backstop today — the game must not lean solely on it, especially with a money-movement path reading two tamperable JSON files. | **P0** | 1-2d |

This is the headline monetization fix. It is also cheap. It should land before
any build that touches a real wallet.

## 1.5 Purchase → transaction → entitlement-grant flow — gaps

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-EN-1 | **No durable entitlement record.** (P0-2.) The grant model is `OwnedItemIds.Add(sku)` — a flat `List<string>` in `PlayerPrefs`. `monetization-v2-spec` §8.1 specifies a full entitlement row: `identity_kind`, `identity_value`, `pack_sku`, `purchase_rail`, `tx_hash`, `amount_native`, `amount_usd_at_purchase`, `granted_at`, `fulfilled_at`. None of `purchase_rail`, `tx_hash`, `amount`, or any timestamp is persisted — the rich `PaymentResult` (which *has* the tx signature and amount) is discarded after `ApplyPackContents`. There is no proof-of-purchase, no audit trail, no idempotency key. This is the structural root of MON-EN-2/3/4 below. | **P0** | 3-5d |
| MON-EN-2 | **No idempotency / double-grant guard.** §8.3 requires "same tx hash → already-recorded → no double-grant." With only an SKU set, a re-run of `Purchase` for an *un-owned* pack would re-pay and re-grant; and there is no tx-hash uniqueness check. (`IsOwned` blocks a repeat of an *owned* pack, but that is SKU identity, not transaction identity.) | P1 | 1-2d |
| MON-EN-3 | **Grant is client-side and instantaneous; no backend verification.** §7.3–§7.5 + §8.3 require the backend to verify the on-chain tx (destination, mint, amount within 1% tolerance, finality) and *then* write the entitlement. The Unity flow grants contents the instant `PaymentResult.Ok` returns from the (stub) provider. There is no `payment-verifier` equivalent, no API client call. On the stub this is invisible; against a real chain it means the game trusts an unverified client claim. | P1 | backend dependency |
| MON-EN-4 | **No identity model.** §7.6 entitlements key on a wallet address OR a Stripe email. The Unity save has no identity field at all — `OwnedItemIds` is anonymous and tied to the local `PlayerPrefs` blob. A player who reconnects a different wallet, or reinstalls, is a new anonymous player with zero entitlements. Identity-merge (§7.6) is explicitly v1.1, but *some* identity anchor is needed even for v1. | P1 | 1wk |

## 1.6 Restore purchases — gap

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-RS-1 | **No restore-purchases flow.** (P0-3.) `monetization-v2-spec` §8.6 (state slice) says the client "on app boot fetches latest from backend to catch entitlements purchased on another device." The Unity port has no such fetch — entitlements live only in local `PlayerPrefs`. Reinstall, new device, or a cleared save = all paid packs gone, unrecoverable. App-store policy (Apple/Google) *requires* a restore-purchases path for non-consumable IAP; a real-money game cannot ship without it. | **P0** | 3-5d (needs the entitlement record + identity from §1.5) |

## 1.7 Receipts — gap

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-RC-1 | **No receipt surface or purchase history.** (P0-4.) After a purchase, `PackStore` shows a transient status line (`… unlocked — tx Ab…Yz`) and nothing else. There is no persistent receipt, no purchase-history screen, no per-transaction record the player can revisit. §7.3 mentions an email receipt for the Stripe rail. With no entitlement record (§1.5) there is nothing to render a receipt *from*. A real-money purchase with no durable receipt is both a trust gap and, for fiat, likely a compliance gap. | **P0** | 2-3d (downstream of MON-EN-1) |

## 1.8 Convenience-token redemption — gap

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-TK-1 | **Convenience tokens are sold but cannot be received or used.** (P0-5.) Every pack's `convenience` array (instant-build, instant-repair, harvest-auto-collect, xp-weekend) parses fine, but `ApplyPackContents` has an explicit comment: *"the v2 foundation has no token tray yet; they are flagged for the Week-8 inventory pass."* That pass never happened. The tokens are silently dropped — a player buys a Patron pack expecting 10 instant-builds + 10 instant-repairs + 2 auto-collects + an XP weekend and receives **zero** of them. Needs: (a) a token-balance model in `GameState`; (b) an inventory/tray UI; (c) redemption hooks in the Build menu (instant-build/repair), the harvest loop, and the XP system. This is the bent-covenant layer (§5.3) — the very thing the monetization compromise was *for* — and it is non-functional. | **P0** | 1-2wk |
| MON-TK-2 | **Glimmer currency unmodelled.** Every pack grants Glimmer (the cosmetic-shop currency); `GameState.Resources` has no Glimmer field, so the economy layer silently loses it (`packs.json` `_schemaNotes` admits this). Defensible while there is no cosmetic shop, but the pack's stated contents are then a lie-by-omission. | P2 | 0.5d (field) + cosmetic shop |

## 1.9 Transaction settlement (Solana) — gaps

The SDK deferral is accepted; these are the *consequences* that remain open.

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-TX-1 | **Solana Unity SDK unresolved → no real devnet settlement.** (= BUG-010, CODE-002, P0-7.) `SolanaWalletProvider` is fully written behind `#if SOLANA_SDK` but every SDK call is an unverified `// SDK-VERIFY:` guess. Until the package resolves (the UniTask-collision blocker per `unity-decisions.md`), the *only* provider is the stub. Week-7/Part-9.4 acceptance ("a devnet transaction goes through") is unmet. | **P0** | unknown — SDK reconciliation |
| MON-TX-2 | **No devnet SKR mint.** (= BUG-011, P0-6.) `WalletEndpoints.SkrMintDevnet` is empty. The spec's headline Week-7 deliverable — "buy a Hearth Spark pack with 25 devnet SKR" — literally cannot run. SOL + USDC rails would work once the SDK lands; the SKR rail (the *grant-credibility vector*, §3) fails cleanly with an error. Owner must supply the mint. | **P0** | owner input |
| MON-TX-3 | **Always-create-ATA bug breaks repeat SPL purchases.** (= SEC-003.) The USDC/SKR path unconditionally prepends a create-ATA instruction; the 2nd purchase to the same recipient is rejected on-chain and the player is over-charged rent. Inside the `#if SOLANA_SDK` block — fold into the SDK reconciliation. | P1 | 0.5d |
| MON-TX-4 | **Treasury wallets not provisioned.** `monetization-v2-spec` §8.2 specifies three separate treasury wallets (SOL/USDC/SKR), Squads multisig, distinct from the publisher wallet. `WalletService` comments confirm these "are not yet provisioned" and the Rewards Distributor stands in. Payments currently route to a dev/staging recipient. Real launch needs the treasuries. | P1 | owner input |
| MON-TX-5 | **Payment-recipient path fails *open*, not closed.** (= SEC-005.) If `wallets.json` is missing/corrupt, `WalletRegistry` silently falls back to a hard-coded constant for the *payment destination*. A money path should fail closed. | P1 | 0.5d |

## 1.10 Rewards Distributor / treasury transparency — gaps

| ID | Gap | Priority | Effort |
|----|-----|----------|--------|
| MON-TR-1 | **"Transparency display" is one address string.** Spec Week 7 calls for a Rewards Distributor / treasury-transparency surface mirroring the v1 React pattern. The Unity implementation is a single `Label` (`store-treasury`) reading `RewardsDistributorAddress`. There is no explorer link, no balance/flow display, no explanation of what the Rewards Distributor *does*, no link to `wallets-of-record.md`. For a project whose monetization pitch is ecosystem credibility, a bare base58 string is thin. | P1 | 1-2d |
| MON-TR-2 | **No on-chain explorer deep-link** for the treasury address or for a completed purchase's tx signature. A player who just paid has no one-tap way to verify their own transaction on Solscan/Explorer — directly undercuts the "verifiable, not trusted" posture. | P2 | 0.5d |

---

# 2. CORE GAME COMPLETENESS

Components a shippable game needs that are not present. These are not in any
module's "ported" column — they were simply never scoped.

## 2.1 Settings / options menu — MISSING (P0-8)

There is **no settings menu anywhere** — grep across `_Modules` finds no
settings/options screen. Spec Part 2 promises a player-switchable quality level
(`Seeker_Low/High/Desktop`), Part 9.5 promises audio at spec mix levels, and
`Core/` is specced to own "settings." A shippable game needs, at minimum: audio
volume sliders (master/music/sfx/ui — see §2.2), quality selector, control/input
options, language selector (Localization is installed but has no UI), and a
credits screen. **Effort: 3-5d. Priority: P0** — no app ships without an options
menu, and the quality selector is a spec Part 2 commitment.

## 2.2 Audio system + Audio Mixer — MISSING (P0-9)

This is one of the most material gaps. There is **no audio code in any module** —
the grep for `AudioSource`/`AudioClip`/`AudioMixer` matches only comments and two
generated `PanelSettings.asset` files. Specifically missing:

- No `AudioMixer` asset, no mixer groups (Master/Music/SFX/UI/Voice/Ambient per
  Part 2 and `audio-mix-spec.md` / `audio-mix.json`).
- No audio director / scene-music controller. `SceneRouter`'s own header says an
  "Audio/Core director listens for scene loads" — that director **does not exist**.
- No music crossfade at scene transitions (Part 9.5 acceptance gate).
- No SFX wiring for abilities, enemies, building, UI.
- `audio-mix.json` (Part 4 canonical file) is **not present** in
  `StreamingAssets/Data/Canonical/` — there is no mixer-volume source.
- Known content gap on top: dungeon audio files missing (BUG-004).

As built today the game **ships completely silent.** Part 9.5 is a hard
acceptance gate. **Effort: 1-2wk (system) + import pass. Priority: P0.**

## 2.3 Pause system — MISSING (P0-10)

No pause menu, no `Time.timeScale` handling, no pause input action. A
tower-defense game with wave timers and an ATB battle needs pause for both UX and
platform compliance (incoming-call/backgrounding behavior on mobile). **Effort:
1-2d. Priority: P0.**

## 2.4 Title → studio → game scene flow — PARTIALLY BUILT (P1)

The arrival sequence is the most-complete onboarding piece: `TitleController`
chains `SplashLoading` (studio bumper) → `StoryIntroController` (cold open) →
title screen, all wired through `OnboardingSceneBuilder`. Gaps:

- **Title's Connect Wallet button is still a Week-1 stub** — `OnConnectWalletClicked`
  only logs. It is never wired to the real `WalletConnectDialog`/`WalletService`
  from the Week-7 module. P1, 0.5d.
- **No quit-to-title path** from gameplay. P1, 0.5d.
- **No loading screen** between heavy scene loads beyond the fade overlay. P2.
- The bumper/intro depend on `studio-bumper.mp4` + `heart-wing.jpg` — present in
  `Onboarding/Art|Video/` (verified). OK.

## 2.5 Onboarding / tutorial — MISSING the actual tutorial (P0-11)

The **Onboarding module is built only as far as the cinematics.** The cold open
plays; that is the cinematic intro. But there is **no tutorial, no first-run
teaching, and no pet-creation flow.** Concrete evidence: `GameState.Onboarded`
is documented as "true once pet creation + tutorial are complete" — and
`StoryIntroController` admits "There is no Week-1 scene yet where the player
finishes onboarding." So `Onboarded` is **never set to true** in normal play.
Consequences:

- No first-time-user teaching of build/place, abilities, waves, the Heart — a new
  player is dropped into the village with no guidance.
- Pet creation (the React `pet creation` step) does not exist; the three starter
  pets are just deployed.
- Because `Onboarded` never flips, the cold-open `ShouldAutoPlay` gate **re-plays
  the intro cinematic on every launch** — a real bug-shaped consequence of the
  missing flow.

**Effort: 1-2wk. Priority: P0** — a shippable game needs first-run teaching, and
the never-flipping flag is actively broken.

## 2.6 Main-menu / save-slot UI — MISSING (P1)

The title screen has only Start + Connect Wallet. There is **no save-slot UI, no
Continue vs New Game distinction, no multiple-save support, no delete-save.**
`GameStateService` persists a single `PlayerPrefs['dotr-save']` blob — one
implicit slot. Start always loads the Village; there is no "continue where you
left off vs start over" choice, and no way to see or manage saves. **Effort:
3-5d. Priority: P1.**

## 2.7 Accessibility — MISSING (P1)

No accessibility provisions found: no colorblind-safe palette option (the game
leans hard on threat-state *color* — serene/vigilant/warning/danger violet
gradient), no text-size scaling, no subtitle/caption system for the cold open and
Bryn dialogue, no input remapping, no reduce-motion / reduce-flashing option (the
withering vignette and gate shimmer are flashing effects). Increasingly a
store-listing requirement on both mobile platforms. **Effort: 1wk across systems.
Priority: P1.**

## 2.8 Analytics, crash/error handling, save integrity — MISSING (P1 / P0-14)

- **Analytics: none.** No event funnel, no crash/ANR reporting (Unity Cloud
  Diagnostics, Sentry, or similar). A launched game with no telemetry is flying
  blind — especially a monetized one (no funnel = no idea where purchases drop
  off). P1, 2-3d to wire a provider.
- **No global error boundary.** `async UniTask` flows are individually
  try/caught, but there is no top-level handler/UI for an unexpected exception —
  the player just sees a frozen or broken scene. P1, 1-2d.
- **Save integrity (P0-14, = SEC-006).** The save is plain JSON in `PlayerPrefs`
  with no HMAC/signature. `OwnedItemIds` *is the pack-entitlement ledger* — a
  well-formed edited save grants paid packs for free. The security audit asks for
  this to be at minimum a *recorded, conscious* acceptance in `unity-decisions.md`
  (it is not yet) and, before a mainnet economy, server-authoritative
  entitlements. Tied directly to the §1.5 entitlement-record gap. P0 to *record*
  the acceptance; P1+ to harden.

## 2.9 Exterior wilderness — ROUGH, not missing (P1)

Recorded as BUG-002 / `unity-decisions.md` Week-3 flag, Task #14 still open: the
exterior Terrain renders black/unlit, the sky is an orange void (no skybox),
props float off the Terrain, the per-direction fog gradient is deferred
(BUG-018). The interior village is the complete Week-3 deliverable; the exterior
is owner-flagged "finetune later." Not net-new scope — listed here for
completeness as a known-incomplete shippable surface. **Priority: P1** (a visible
broken skybox is not shippable). Effort: 3-5d.

## 2.10 Other shippable-game gaps

| Gap | Priority | Effort |
|-----|----------|--------|
| No win/lose / game-over flow surfaced to the player (run-end screen, retry) | P1 | 2-3d |
| No HUD damage-report / post-wave summary modal (also blocks MON-UI-2) | P1 | 2-3d |
| Localization package installed + `en.json` present, but no language-switch UI and only English authored | P2 | 1d UI |
| No app-icon / splash-image / store-listing art pipeline | P1 | 1-2d |
| No build/CI pipeline for the Week-8 APK + EXE deliverable; Unity license handshake is flaky (BUG-014) | P1 | 2-3d |
| Knight / Ranger hero classes are placeholders; v2 ships Mage only (spec-sanctioned, noted) | P2 (v2.1) | — |
| Status effects (Frost Nova slow, Ice Wolf frostbite) record but do nothing (CODE-007) | P1 | 1-2d |
| No object pooling — per-cast VFX + per-spawn primitives allocate; risks the 60 FPS / 400 MB gates (CODE-005) | P1 | 2-3d |

---

# 3. PER-MODULE GAP TABLE

State as of 2026-05-19. "Built" = real, reviewed C# exists; "Stubbed" = compiles
but mock/placeholder behavior or unintegrated; "Empty" = asmdef/folder only.

| Module | State | What exists | Gap vs. spec |
|--------|-------|-------------|--------------|
| **Core** | Built | `GameState` SO, `GameStateService` (59/59 EditMode tests pass), `SaveSchema` + `SaveMigrator`, `SceneRouter`, `Theme`, `Constants`, `IDamageable` seam. | Spec says Core owns **settings** — no settings system (§2.1). No audio director though `SceneRouter` assumes one (§2.2). No Core bootstrap scene / `[RuntimeInitializeOnLoadMethod]` guaranteeing `GameStateService` exists (ARC-005). Save has no integrity protection and `OwnedItemIds` is a trusted entitlement ledger (§2.8). Single implicit save slot (§2.6). Migrator swallows exceptions silently (CODE-003). |
| **Data** | Stubbed | Canonical JSON under `StreamingAssets/Data/Canonical/` (canon-strings, en, packs, wallets, enemies, waves, buildings, abilities, pets, lore-fragments, themes, realm-map, dungeons/healers-cottage). | **No ScriptableObject assets in `Assets/Data/`** — spec Part 3/4 wants SO caches of the JSON; none exist (the SO-generation step was skipped). Part-4 canonical files **not present**: `towers.json`, `enemy-roles.json`, `heart.json`, `walls.json`, `questlines.json`, `audio-mix.json`, `gameDesign.json`, `tooltips.json`. No `SchemaTests.cs` for any file (Part 4 mandate; esp. wallet-critical — MON-PR-4). |
| **BattleATB** | Built | Pure-C# engine (types/rng/targeting/actions/ai/combat/turn/state/defs/scaling) with EditMode tests + golden RNG vectors; `ATBRuntimeState`; `BattleController`; `BattleHUD` UXML/USS. | `BattleController.ReturnAfterResult` hard-codes return-to-Village; battle result is produced but **never consumed** — no Heart/building/wave consequence applied on return (BUG-008, CODE-001, ARC-002, P0-12). Per-enemy breach-roster mapper is a stub (BUG-009). `ATBBattle.unity` scene-wiring unverified end-to-end. |
| **Village** | Built (code) / Unintegrated (scene) | Walls, gates, Heart, buildings + build menu, hero + abilities, waves, enemies, all compile. | **Week-4 scene integration incomplete** (BUG-017, P0-13): NavMesh bake, prefab/layer wiring, UIDocument hookup not verified — Wave 1 has never been confirmed playable end-to-end. White centerpiece render bug (BUG-001). Status effects no-op (CODE-007). Exterior rough (§2.9). `_enemyMask` defaults to `~0` (CODE-008). No VFX/enemy pooling (CODE-005). |
| **Dungeons** | Built (code) / Unintegrated (scene) | `DungeonController`, `DungeonHero`, `DungeonCameraRig`, `Lantern`, `Bryn` + dialogue/bubble, `LoreStone`, `EncounterTrigger`, `RandomEncounterTable`, `Checkpoint`, `DungeonRuntimeState`. | **Dungeon scene wiring outstanding** — none of it is wired into `Dungeon_HealersCottage.unity` (Weeks 5-7 flags). Wall-collider "no walk-through" gate unverified (BUG-007). Dungeon BGM + lantern-flicker audio missing (BUG-004). A non-canon placeholder lore fragment (BUG-005). ATB return-to-dungeon broken (BUG-008). Run seed is wall-clock, non-reproducible (CODE-010). |
| **Pets** | Built | `Pet`, `PetCatalog`, `PetDeployer`; three starter species; bond data in `pets.json`. | Bond *progression* (XP thresholds → perks) — unclear it is wired to gameplay XP. **Pet creation / selection flow missing** (part of the missing onboarding, §2.5). Pet cosmetic skins from packs have nothing to apply to (MON-UI-3). `_enemyMask = ~0` (CODE-008). |
| **Wallet** | Stubbed | `WalletService` + `IWalletProvider` + `StubWalletProvider` (working devnet mock), `SolanaWalletProvider` (written, `#if SOLANA_SDK`, unverified), `PackStore` + UXML/USS, `PackCatalog`, `WalletConnectDialog`, `WalletRegistry`, `WalletEndpoints`. | The whole of §1 above. Headline: no confirm modal (P0-1), no durable entitlement record (P0-2), no restore (P0-3), no receipt (P0-4), no token redemption (P0-5), no SKR mint (P0-6), SDK unresolved (P0-7), no detail page / discovery / previews / Gift / seasonal pass / Stripe / oracle. Runs only on the stub. |
| **HUD** | Built (code) / Unintegrated | `VillageHudController.cs` (18KB) + `VillageHud.uxml`/`.uss` — resource bar, wave countdown, etc. (The architecture review's ARC-003 "HUD has no implementation" is **stale** — the controller now exists.) | Not confirmed wired into `Village.unity` / verified at runtime (part of P0-13). No store glyph / discovery hook (MON-UI-2). No damage-report modal (§2.10). No pause button (§2.3). |
| **Onboarding** | Partially built | `TitleController`, `SplashLoading` (studio bumper), `StoryIntroController` (cold open), `CanonStrings`, title UXML/USS, `OnboardingSceneBuilder`. | **No tutorial / first-run teaching, no pet-creation flow** — `Onboarded` is never set true, so the cold open re-plays every launch (P0-11). Title Connect Wallet still a stub (§2.4). No save-slot/Continue UI (§2.6). |

---

# 4. Recommended close-out order

1. **Monetization integrity core** — MON-CF-1 (confirm modal), MON-EN-1
   (entitlement record), then MON-RC-1 (receipt) + MON-RS-1 (restore) +
   MON-TK-1 (token redemption) build on that record. This is the spine; fix it
   first because §1.6/§1.7/§1.8 are all blocked on §1.5.
2. **Core-game shippable basics** — settings menu (§2.1), audio system + mixer
   (§2.2), pause (§2.3). Audio is a hard Part-9 gate and the longest pole.
3. **Integration pass** — close P0-13 / P0-12: wire Weeks 4-7 systems into
   scenes, run Wave 1 and the breach→ATB→return round-trip end-to-end. Until this
   runs there is no playable build to QA.
4. **Onboarding/tutorial + the never-flipping `Onboarded` flag** (§2.5).
5. **Solana real-rail** — resolve the SDK (P0-7), reconcile `// SDK-VERIFY:`,
   obtain the devnet SKR mint (P0-6), fix the ATA bug (MON-TX-3).
6. **Launch hardening** — analytics, save integrity, accessibility, restore on a
   real identity, CI/build pipeline, seasonal pass, Stripe rail decision.

Devnet monetization loop is *workable* once §1.4, §1.5, §1.8, and the SDK/mint
land. A *real launch* additionally needs the Stripe rail decision, backend
verification + identity, server-authoritative entitlements, the seasonal pass,
and the full accessibility/analytics/compliance layer.

_Tend the Heart. Hold the dark. Ship the loop._
