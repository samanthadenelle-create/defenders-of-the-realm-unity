# Solana Mobile Builder Grant — Application Draft (2026-07)

**Apply at:** https://airtable.com/appw7jfRXG6Joia2b/pagGNMPX6qleBYHNp/form
**Program:** Solana Mobile Builder Grants (solanamobile.com/grants). Separate note: the next
Colosseum hackathon (Q1 2026 wave has passed; watch for the next) runs mobile grants alongside —
this draft serves both.
**Their six criteria, each answered below:** mobile-first Android UX · Solana Mobile Stack (MWA +
Seed Vault) · scope + milestones · team ability · clear use of funds · public-good commitment.

---

## Project name
Echoes of Elarion (Defenders of the Realm) — *Hold the last light.*

## One-liner
A fun-first action RPG + settlement builder where reclaiming a dying world powers your economy —
bringing real game quality and real SKR utility to Solana Mobile.

## What it does / problem it solves
Echoes of Elarion is a mobile-first action RPG and settlement builder (Unity 6, playable in-browser
today). You are Grom, the last Knight of Elarion, defending the world-tree at the heart of a dying
realm: fight real-time battles, reclaim territory to strengthen the tree's life force, and command
autonomous Echo spirits that harvest resources while you play or while you're away. Those resources
feed a fully player-designed settlement — place your buildings, defenses, and walls, then trigger
escalating enemy waves to test what you built, press your luck for bigger rewards, repair, upgrade,
and push further. One loop: combat feeds the economy, the economy funds the stronghold, the
stronghold lets you fight deeper.

The problem: mobile web3 gaming has an empty-stadium problem — extraction-first apps with no game
underneath. Echoes of Elarion is fun-first by design (no pay-to-win; crypto as a swappable rail),
and on Solana Mobile it gives SKR real utility: a Solana wallet layer (SOL/USDC/SKR with Jupiter
swap integration) is already built into the codebase, with skin-in-the-game tournament brackets
(buy-in, prize pot, house rake) designed on that rail. A genuinely playable, polished game is what
the dApp Store catalog is missing.

## Criterion 1 — Mobile-first implementation (Android)
- The game is built mobile-first today: touch-native input (gesture driver + virtual d-pad),
  phone-aspect UI verified per-panel, WebGL build playable on mobile browsers now.
- **The grant-funded work IS the native Android/Seeker build:** Unity Android target with the
  crypto modules compiled in (they live in isolated assemblies — store builds compile them out,
  the dApp Store build compiles them in; compliance by construction).
- Native features roadmap: haptics on combat hits/wave breaches, notification hooks for the
  offline economy ("your echoes filled the silo", "your walls took damage"), and Seeker-aware
  perks (see SKR below).

## Criterion 2 — Solana Mobile Stack use
- **Mobile Wallet Adapter (MWA):** our wallet layer is a pluggable provider abstraction
  (`IWalletProvider`-style seam; devnet stub + web adapters exist). Milestone 1 implements an MWA
  provider behind the SAME seam — sign-in, session, and payment signing routed through MWA on
  Seeker/Android, no game-code changes.
- **Seed Vault:** all signing flows (pack purchases, tournament buy-ins, cosmetic mints) route
  through MWA → Seed Vault custody; the game never touches key material.
- **SKR-native economy:** SKR is a first-class currency in the wallet layer today. Planned Seeker
  perks: SKR staking trickle rewards (already defined in our stake-rewards resolver), tournament
  buy-ins/prizes in SKR, and dApp Store exclusive cosmetics.
- **dApp Store launch** is the explicit end milestone.

## Criterion 3 — Scope + milestones (12 weeks, phased)
- **M1 (weeks 1–4) — Native Android build + MWA sign-in.** Unity Android target hardened
  (performance pass: our perf model is budget-by-construction — single controlled hero, throttled
  autonomous AI, bounded agent counts); MWA provider implemented behind the existing wallet seam;
  Seed Vault-custodied sign-in on device. Deliverable: installable APK, wallet sign-in demo video.
- **M2 (weeks 5–9) — SKR economy live on testnet.** Pack store + crystal SKUs purchasable via MWA
  on testnet; SKR staking perks wired; offline-economy notifications. Deliverable: end-to-end
  purchase flow on device, testnet.
- **M3 (weeks 10–12) — dApp Store submission + launch.** Store listing, compliance pass, launch
  build; co-marketing with the Solana Mobile team. Deliverable: live dApp Store listing.

## Criterion 4 — Team ability to execute
Solo founder (HP B2B operations background — the game is architected and managed with the same
bounded-context discipline) directing an AI-agent development pipeline with hard engineering
gates: headless compile/regression gates on every change, an autonomous bot fleet that plays the
game in parallel headless instances and files ranked bug tickets, and a formal QA→implement→
felt-verify pipeline. Shipping cadence to show for it: a playable web build updated near-daily
(preview: defenders-of-the-realm-v2 on Vercel; itch.io build live), ~700 tracked work orders, a
versioned save system at schema v30, and a codebase organized into 15+ bounded assemblies.
This pipeline is itself evidence the milestones will ship: the process is instrumented, gated,
and already delivering.

## Criterion 5 — Use of funds ($10,000)
- $4,000 — development time: MWA/Seed Vault integration + Android build hardening (M1/M2).
- $2,000 — device + compliance: Seeker hardware, Android test devices, dApp Store submission.
- $1,500 — art/audio finishing for the launch build (owner-directed AI art pipeline costs).
- $1,500 — backend infrastructure for launch (server-side purchase verification, leaderboards).
- $1,000 — launch marketing assets (trailer cut from the in-game intro video, store creatives).

## Criterion 6 — Public good / open source commitment
We will open-source the **Unity ↔ Mobile Wallet Adapter provider** we build in M1, as a
documented, reusable package with a reference scene — plus a write-up of the full "Unity game to
dApp Store" pipeline (build settings, compliance-by-assembly pattern for compiling crypto in/out
per channel). Unity is the biggest game engine and its Solana Mobile tooling is thin; this is the
missing on-ramp for game studios, written by a team actually shipping through it.

## Links to include on the form
- Playable web build (current preview): https://defenders-of-the-realm-v2-9ncz1sks9.vercel.app
- itch.io build: (owner fills)
- Repo: (private — GitHub; access on request) · X/socials: (owner fills)
- Contact: sammie.denelle@hp.com (or the project account — owner picks)

---
*Honesty guardrails held in this draft: tournaments are "designed on the built rail" (not live);
the wallet stack is devnet/testnet today — mainnet is post-grant; no Android build exists YET —
that is what the grant funds. Do not let any edit claim otherwise.*
