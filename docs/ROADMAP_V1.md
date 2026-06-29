# ROADMAP — V1 Launch (Echoes of Elarion / Defenders of the Realm)

> **Date:** 2026-06-28 · **Branch:** `wip/village2-and-f8-tickets` · **HEAD anchor:** `8aa24c32`
> **Authoritative ground truth:** `CANON_GROUND_TRUTH_2026-06-26.md`. Combat shape:
> `docs/COMBAT_PIVOT_NORTHSTAR.md`. Vision/business: `docs/NORTH_STAR.md` (⚠ pre-pivot framing —
> read through the single-Knight lens). This roadmap is the **sequenced ship plan**; it does not
> restate design — it orders the existing design WOs into phases with gates and dependencies.
>
> **Honesty rule (§12 / §15):** every "Done" here is gate-verified or felt-closed; everything else
> is **spec / in-flight / unbuilt** and labeled as such. No status is asserted on faith.

---

## 0. The through-line (one paragraph)

V1 is the **solo-Knight offense loop**: walk the overworld from the castle hub, engage a roaming orc
rep, fight in an isolated real-time BattleArena, win → return richer; clear territory → Tree life-force
rises → echoes harvest faster → fund the skill tree and gear → raid harder. Base-building / tower-defense
is **V2-gated** (`ff.basebuilding` OFF). The job now is **polish, not architecture** (memory
`polish-phase-mechanics-solid`). Web3 (Pi + Solana Seeker) is a **payment rail + distribution channel**
layered on the already-built `WalletService`/`CurrencyKind`/PackStore seam — **not a new economy, and not
on the V1 critical path**. It is sequenced to begin its de-risking spike in **parallel** (isolated §9
Monetization/Backend lane) while the game polish finishes, and to *list* only after the gameplay is
felt-complete.

**Sequence at a glance (gates in brackets):**

```
P1 V1 Polish ─┬─[FELT-COMPLETE]→ P2 Content (dungeon gen + maps) ─[CONTENT-READY]→ P5 Store wiring
              │
              └ (parallel, isolated) P3 Web3 spike [WEBGL-IN-PI VIABLE] → P4 Pi auth → P6 Pi payments
                                                                                              │
                                          P7 Solana MWA wallet-read [READS ON DEVICE] ────────┤
                                                                                              ▼
                                                                          P8 Seeker dApp-Store listing
```

---

## Phase 1 — V1 POLISH (the felt-bug + UI backlog) · **ACTIVE NOW**

**Goal:** the core loop is not just wired but *feels* shipped — no strobing, dead buttons, blob
portraits, un-animated hero, or unbound combat HUD. This is the gate to everything else.

**Scope = the open ticket cluster** (QA→CLI→PO pipeline, `docs/TICKET_PIPELINE.md`):

| Area | Tickets / WOs | State |
|---|---|---|
| Resource flash, inventory blob, talent swap, dead Talk, seating editor, gear populate, wave-loop HUD, stray white bar, hero animator | WO-572 … WO-582 | **Done (gate-verified)** — pending PO felt-close |
| UI fidelity — real Blink Obsidian frames into the shared kit | task #40 (CLI-owned, `docs/UI_BLINK_TEMPLATE_CANON.md`) | **In progress** |
| Battle Arena combat ↔ HUD binding cluster | task #43 — RCA | **In progress** (instrument-first, §12) |
| Crown-tier victory star row · Outfits paper-doll preview · arena spell VFX (blocky purple cubes) · orient/seating tool on Gear screens | tasks #41, #42, #44, #45 | **Pending** |

**Gates:**
- **G1.1 (build):** `CompileGate` green + NUL-byte clean on every touched `.cs` (§1).
- **G1.2 (headless):** AutoPilot fleet + `DataRegression` pass; F8 break-log clean on a full loop run.
- **G1.3 (felt — PO only):** owner plays castle → overworld → arena → win → return and the loop *feels*
  right (no dead UI, hero animates, HUD binds, VFX acceptable). **PO closes; CLI never self-closes feel** (§13).

**Exit milestone → `FELT-COMPLETE`:** the solo-Knight loop is felt-closed end-to-end. **This is the
single most important gate in the plan** — content, store wiring, and any listing all sit behind it.

**Dependencies:** none upstream. **Blocks:** P2, P5, P8 (you do not list an unpolished game on a
"quality-is-the-moat" store, `NORTH_STAR.md` §GTM).

---

## Phase 2 — CONTENT (dungeon generator + maps) · after `FELT-COMPLETE`

**Goal:** turn the one proven arena/raid into **replayable variety** so V1 has run length, using the
JSON-driven chunk-composer north star (memory `scene-chunk-dungeon-composer-northstar`) — *not* hand-built
levels.

**Design WOs (already specified):**
- **WO-479** scene chunk-composer — the foundation: capture→recipe→build, anchor-relative composable
  chunks (trap / choke / fake-wall / bridge=RegionGate / maze) + a progression-scaled **seed budget**
  that scales size/difficulty/enemy count/level.
- **WO-485** winding dungeon generator (rooms → connectors → graph).
- **WO-584** dungeon / outpost / arena **consolidation** (one generalized loop, by extraction not rewrite
  — see also task #46 dungeon map generator, task #47 gated dead-code removal).
- **WO-433 / WO-550** Village2 raid destination + polish (the first proof target).
- **Reward loop:** WO-431 raid rewards/victory → re-pointed to **skill points / gear / resources**
  (no companions, no base — `COMBAT_PIVOT_NORTHSTAR.md` "loop reward swap"; `ff.basebuilding` OFF gates
  WO-475 convert-on-clear out of V1).

**Gates:**
- **G2.1:** generator produces a navmesh-valid, completable map headlessly (AutoPilot can path start→boss→exit);
  seed budget scales as specified; regression-guarded (`SEAM-REACHABLE` analog for generated maps).
- **G2.2 (felt):** PO plays 2–3 generated dungeons and they read as varied + fair.

**Exit milestone → `CONTENT-READY`:** ≥1 generator pipeline shipping varied, completable runs feeding the
reward/skill-tree loop.

**Dependencies:** `FELT-COMPLETE` (build on a solid loop, not a shaky one). **Parallelizable with:** the
Web3 lane (P3–P4) — different silos, no Unity-gate contention beyond the single committer (§9, §11).

---

## Phase 3 — WEB3 SPIKE (WebGL-in-Pi viability) · **can start in parallel with P1/P2** (isolated lane)

**Goal:** answer the **one externally-unknown question** that reshapes the whole web3 architecture before
any payment work — *does our Unity WebGL build actually run acceptably inside Pi Browser on a mid-range
phone?* (`WORK_ORDER_pi_browser_integration.md` §6.1, §7 Phase 0).

**Work (no game logic, no `.cs` gameplay):**
- Cut a **trivial Unity WebGL build** (loading screen + one button), host at a real HTTPS domain with
  `validation-key.txt`, register the app + validate the domain in `develop.pi`.
- Add `pi-sdk.js`, call `Pi.init` + `Pi.authenticate(['username'])` in **Pi Browser on a real phone**.
- Lean on the **size lever**: Addressables-remote streaming (`docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md`
  T1 — stream heroes/enemies, ship a tiny base bundle) is *why* the single-Knight build can fit WebGL.

**Gate → `WEBGL-IN-PI VIABLE`:** loads + authenticates at acceptable speed on mobile.
- **If NO →** stop / re-scope (a lightweight companion web app, not the full game in Pi). **This gate is a
  real fork** — do not commit to Pi payments before it passes (§8 scope guard).

**Dependencies:** none on gameplay (isolated §9 lane); benefits from Addressables-remote being underway.
**Owner-gated:** approve the Phase-0 spike before spend.

---

## Phase 4 — PI AUTH (low-risk integration) · after `WEBGL-IN-PI VIABLE`

**Goal:** prove the full **client ↔ `.jslib` bridge ↔ backend** pipe with **no money**.

**Work (`WORK_ORDER_pi_browser_integration.md` §4, §7 Phase 1):**
- `PiBridge.jslib` (`Assets/Plugins/WebGL/`) + a C# `IPiPlatform` seam (`WebGLPiPlatform` real /
  `EditorPiPlatform` stub) — mirrors the existing `ISaveProvider`/`ISkrLedger` seam pattern; **no gameplay
  code learns about Pi.**
- Thin backend `POST /pi/verify` (accessToken → trusted uid via Pi `/me`), Cloudflare Worker (`pi-backend`),
  Server API Key as a **backend secret only** (never in the WebGL bundle).
- Sign the Pioneer in, greet by username, persist the Pi uid against the (local→cloud) save.

**Gate:** Pioneer authenticates and is greeted; uid persists; bridge round-trips in real Pi Browser.

**Dependencies:** P3 viability. **Builds toward:** P6 payments (same backend, extended).

---

## Phase 5 — MONETIZATION WIRING (packs / SKR store) · after `CONTENT-READY`

**Goal:** make the **already-~70%-built** store loop real and *placed in-scene* — the gameplay-side of
monetization, independent of any chain going live.

**State (do NOT greenfield — `PIPELINE_STATE.md` §5):** `PackStore` + `PackCatalog` + `packs.json`,
`WalletService` + `CurrencyKind` (SOL/USDC/SKR, devnet-stub), `GlimmerCurrencyService`,
`CosmeticShopPanel` all **BUILT**. Store **redesign already wired this arc** (WO-501: type filter, slim
list, 3D preview, buy/sell+equip). The gaps are **wiring + a held-SKR premium layer**:

**Design WOs:**
- **`WORK_ORDER_skr_store_design.md`** — held-SKR premium store, `ISkrLedger` (staged local→cloud→Solana),
  PackStore on-ramp, **ethical covenant** (cosmetics/convenience only — never power) + validator firewall.
- **`docs/monetization-v2-spec.md`** — locked spec; §12 owner yield-funded capped reward pool.
- **Scene-wiring re-enable** (the known trap): `BuildMarketplace` is **disabled** until PackStore gets its
  **OWN PanelSettings + a code-built UI** (UXML does not render in builds — memory
  `uxml-uidocuments-dont-render-in-builds`). Re-enable only after that.

**Gates:**
- **G5.1:** store opens in-scene on the right panel, lists from `packs.json`, buy/sell/equip + SKR-store
  discount path works against the **devnet stub** (no chain dependency).
- **G5.2 (covenant regression):** no SKU grants combat power; `SkrStakingRegression`-style invariant green
  (`paid + reserved <= funded`; no stat/cap perk).

**Dependencies:** `CONTENT-READY` (something worth spending on) for *felt* value; technically the store
can be wired against stubs anytime. **Feeds:** P6 (Pi as a real on-ramp to SKR), P8 (on-device SKR utility).

---

## Phase 6 — PI PAYMENTS (Sandbox → Mainnet) · after P4 + P5

**Goal:** add Pi as **one more rail** on PackStore — Pi *buys SKR*, exactly like USD/SOL do; everything
downstream of "payment completed" already exists.

**Work (`WORK_ORDER_pi_browser_integration.md` §3, §7 Phase 2):**
- `Pi.createPayment` in the bridge; backend `/approve` + `/complete` + `/reconcile` + an orders table
  (paymentId → sku/amount/uid). **The same thin backend the staged T2 cloud-save needs — build once.**
- Wire `CurrencyKind.Pi` into PackStore and the `skr-pouch-*` SKUs.
- **Security (non-negotiable):** grant the SKU **only after a 200 from Pi `/complete`** — server-verified,
  never the client's word. Idempotent reconcile for incomplete payments.
- Develop entirely in **Sandbox/Testnet**, then flip to mainnet.

**Gate:** end-to-end Sandbox buy of a Token Pouch in Pi credits SKR via the existing fulfillment path; then
the same on mainnet (KYC'd Pioneer).

**Dependencies:** P4 (auth + bridge + backend), P5 (the SKU/fulfillment sink). **Owner-gated + legal-gated**
for any SKR *payout* (the rebate / option-b path waits behind the §12 legal sign-off; cosmetics + discount
ship first).

---

## Phase 7 — SOLANA MWA WALLET-READ (the Seeker on-ramp) · parallel with P6

**Goal:** the *other* web3 rail — read on-device SKR for the Seeker pitch, **no new staking contract**
(`WORK_ORDER_skr_staking_and_seeker.md` Part A Stage 3 + §B4 Phase 0).

**Work:**
- Wire **Mobile Wallet Adapter (MWA)** through the existing `SolanaWalletProvider` seam on an Android build
  (Unity MWA SDK / Magicblock `Solana.Unity-SDK`). Games use **MWA, not Seed Vault directly.**
- Connect a wallet, read the player's **held/staked SKR**, unlock the **Keeper** cosmetic tier from
  on-chain proof (read-only — the staking-loyalty layer's V2 resolution; V1 ships as an off-chain virtual
  lock behind `ISkrLedger`).

**Gate → `READS ON DEVICE`:** MWA connects + reads SKR balance/stake on a real device.

**Dependencies:** none on Pi (separate rail). **Owner decision to confirm first:** is in-game SKR the
**same mint** as Solana Mobile's SKR? (Recommended YES — strongest pitch; `skr_staking_and_seeker.md` §1 ⚠).

---

## Phase 8 — SEEKER dApp-STORE LISTING · after `FELT-COMPLETE` + `READS ON DEVICE`

**Goal:** distribution — list the single-Knight V1 on the Solana dApp Store (zero-fee, 2,500+ dApps,
real builder incentives), leading with **"a shipping game that runs on the Seeker's own token + rewards
staking it"** (`WORK_ORDER_skr_staking_and_seeker.md` Part B).

**Checklist (§B3):**
- New **dedicated signing keystore** (a Play-store key is rejected).
- Owner **KYC/KYB** publisher account + **publisher wallet** (separate from treasuries), **~0.2 SOL** for
  mint/ArDrive.
- Assets: 512² icon, 1200×600 banner, ≥4 1080p screens/videos + narrative-voice listing copy.
- **Signed release APK** (single-Knight V1, Addressables-remote to keep size small — PWA→APK also eligible,
  converging with the Pi WebGL build: one small build, two ecosystems).
- Publisher-Policy compliance (ethical, no P2W; confirm any contest/wager posture's legal footing first).

**Gate:** passes the **2–5 business-day** dApp Store review; live listing with SKR utility front-and-center.

**Then (Phase 8.1):** apply for **dApp-Store Season / builder grants / hackathons** (Season-1 precedent
~750k SKR / qualifying team) — the staking-loyalty layer is a direct "we drive SKR staking" qualifier.

**Dependencies:** `FELT-COMPLETE` (quality is the moat), `READS ON DEVICE` (full Solana-utility credit),
P5 (SKR utility wired).

---

## Cross-cutting dependencies & gating summary

| Milestone | Unlocks | Hard prerequisite |
|---|---|---|
| `FELT-COMPLETE` (P1) | P2 content, P5 store felt-value, P8 listing | — (active now) |
| `CONTENT-READY` (P2) | P5 felt-value, run length for launch | `FELT-COMPLETE` |
| `WEBGL-IN-PI VIABLE` (P3) | P4 auth, P6 payments | Addressables-remote size lever |
| Pi auth pipe (P4) | P6 payments | P3 |
| Store wired (P5) | P6 on-ramp sink, P8 utility | (stubs anytime; felt-value needs P2) |
| Pi payments (P6) | Pi revenue rail | P4 + P5 (+ legal for SKR payout) |
| `READS ON DEVICE` (P7) | P8 full Solana credit, Keeper tier | same-mint owner decision |
| Seeker listing (P8) | distribution + grants | `FELT-COMPLETE` + `READS ON DEVICE` + P5 |

**Two lanes run concurrently (§9 / §11):**
- **Gameplay lane:** P1 → P2 → P5 (sequential; the single Unity gate + sole committer is the coordination point).
- **Web3/Backend/Distribution lane:** P3 → P4 → P6, and P7 — **isolated** (own domain, own thin backend,
  seams the game ignores). They converge only at P5 (the SKR/SKU sink) and P8 (the listed build).

**The honest critical path to a *listed, polished* V1 is the gameplay lane** (P1→P2), with the web3 lane
de-risking in parallel so that the moment gameplay is felt-complete, payments + listing are ready to flip
on. Do **not** let the shiny web3 work pull focus off `FELT-COMPLETE` (memory
`follow-canon-orchestrate-not-solo-guess`; `NORTH_STAR.md` guardrails).

---

## What is explicitly NOT in V1 (gated to V2)

- **Base-building / CoC build mode, troop training, manned tower-mages, player-triggered escalating
  waves, watch-or-continue base defense** — all behind `ff.basebuilding` (OFF). Existing
  barracks/WaveManager/towers/GarrisonController sit **dormant, flag-gated** until evidence earns them in
  (`COMBAT_PIVOT_NORTHSTAR.md` PHASING; memory `combat-pivot-single-hero-northstar`).
- **Convert-on-clear** (cleared outpost → your base, WO-475) — it *is* base creation → waits behind the gate.
- **Multi-hero / companions / pets-in-battle** — retired (single-hero spine).
- **Real on-chain SKR staking contract** — never built; V2 *reads* the protocol's existing Guardian
  delegation, off-chain virtual lock in V1.
- **Store build (Google Play / iOS fiat IAP)** — a separate compliant channel (crypto compiled OUT via
  `DeNelle.Wallet`/`DeNelle.Web3` asmdef exclusion); not part of the V1 web3/Pi/Seeker path.

---

## Source canon (kept in sync per §15)

- `CANON_GROUND_TRUTH_2026-06-26.md` — current reality anchor.
- `docs/COMBAT_PIVOT_NORTHSTAR.md` — single-Knight combat/economy shape (V1 vs V2 phasing).
- `docs/NORTH_STAR.md` — vision + business model + GTM (read pre-pivot framing through the single-Knight lens).
- `WorkOrders/WORK_ORDER_479_scene_chunk_composer.md`, `WORK_ORDER_485_winding_dungeon_generator.md`,
  `WORK_ORDER_584_dungeon_outpost_arena_consolidation.md` — content/dungeon generation.
- `WorkOrders/WORK_ORDER_skr_store_design.md`, `docs/monetization-v2-spec.md` — store + SKR economy + covenant.
- `WorkOrders/WORK_ORDER_pi_browser_integration.md` — Pi WebGL spike → auth → payments → listing (phased).
- `WorkOrders/WORK_ORDER_skr_staking_and_seeker.md` — SKR staking-loyalty + Solana Seeker dApp-Store pitch.
- `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` — staged local→cloud→Solana; Addressables-remote (the
  WebGL/mobile size lever the whole web3 path depends on).
- `docs/TICKET_PIPELINE.md` — QA→CLI→PO flow gating every P1/P2 felt-close.

> **Maintenance:** when a phase gate flips or a referenced WO ships, update this file **in the same change**
> (§15) and supersede the stale line — never leave two live roadmaps disagreeing.
</content>
</invoke>
