# WORK ORDER 700 — Android APK: Seeker device test build (phase A) → MWA sign-in (phase B)

**Status: READY TO IMPLEMENT (Phase A)** (owner ask 2026-07-13: "package it as an APK with the
Solana SDK wallet-connect hook in place so I can test it on my Seeker phone").
**Lane:** Platform/Build (CLI-owned — batchmode + build machine). **Type:** NEW build target.
**Numbering:** 700 from the banner (699 minted this session); confirm + Notion row on claim.
**This IS grant milestone M1** (docs/SOLANA_MOBILE_GRANT_APPLICATION_2026-07.md) starting early —
the deliverable doubles as the dApp Store submission artifact.

## Phase A — plain APK on the Seeker (no crypto; the fast felt-test)

The owner can sideload and felt-test the GAME on device before any wallet work — Seeker is
Android; sideloading needs no store, no MWA.

1. **Build machine prerequisites — OWNER CONFIRMS ALREADY INSTALLED (2026-07-13):** the
   Android build tooling (Unity Android Build Support / SDK/NDK) is already on the machine
   from prior setup. CLI: verify with a dry `-buildTarget Android` switch in batchmode (module
   presence is confirmed by the editor accepting the target — takes minutes, catches version
   drift), then proceed straight to the build. No Unity Hub step expected.
2. **Android player settings:** IL2CPP + ARM64; **package name RULED (owner 2026-07-13):
   `com.denellestudios.echoesofelarion`** — owner approved the DenelleStudios/EchoesOfElarion
   id; UI seat normalized to all-lowercase per Android convention and corrected the
   transcription "Alerion"→"Elarion" (canon spelling — flagged to owner, standing unless
   overruled). The dApp Store matches releases by this name forever — it is now FROZEN.
   Min SDK per current Seeker OS; landscape per the game's mobile aspect;
   `Application.targetFrameRate` sanity for mobile.
3. **Store-safe by construction:** crypto assemblies COMPILE OUT exactly as the store-build
   path already does (Phase A ships no wallet code — proves the compliance seam on Android).
4. **Signing — owner ruling recorded (2026-07-13):** CLI generates the release keystore; the
   OWNER stores the keystore file backup + passwords in a PHYSICAL OFFLINE location on creation
   (same paper/offline discipline as the wallets-of-record seed phrases — never committed,
   never in chat, never in a synced folder). CLI hands the credentials to the owner at creation
   and keeps only what the local build needs; the RESULT documents the procedure WITHOUT the
   secrets. NOTE for CLI: losing an Android release keystore permanently orphans the package
   name — the offline backup is load-bearing, treat its existence as an acceptance item.
   Debug-signed is fine for the first sideload; release keystore before any store submission.
5. **Reuse the WebGL lessons:** CanonicalJson (StreamingAssets reads differ on Android — the
   Resources-first loader is already the fix, verify the 6 StreamingAssets-only catalogs list);
   code-built uGUI everywhere (no UXML risk); Lean Touch input is native here.
6. **Deliverable:** `Builds/Android/EchoesOfElarion.apk` + a one-line sideload instruction
   (Settings → allow from source → install). Fleet can't run on-device — the owner IS the
   device gate (PO felt-pass on the Seeker).

## Phase B — MWA sign-in (the "wallet connect hook", grant M1 proper)

7. Implement the **Mobile Wallet Adapter provider** behind the existing `IWalletProvider`-class
   seam (the same slot the web/devnet stubs fill): connect → authorize → session persist →
   sign. Unity path: Solana Mobile's Unity SDK / MWA bindings (CLI evaluates the current
   solana-mobile Unity package vs a thin JNI bridge; document the pick — this becomes the
   open-source public-good deliverable of the grant).
8. Seed Vault custody by construction (MWA routes signing to the device wallet; the game never
   sees keys). Sign-in surface reuses the existing platform sign-in controller pattern
   (PiSignInController is the shape: bounded timeouts, retryable button, never hard-block boot).
9. Gate: connect + authorize + a devnet memo/sign round-trip on the owner's Seeker, traced
   (`[Flow:Wallet]` step-in/out per §12).

## Acceptance
- [ ] A: APK installs + runs on the owner's Seeker; core loop playable with touch; no crypto
      symbols in the build (assembly audit); catalogs load (no StreamingAssets misses).
- [ ] B: MWA connect/authorize/sign round-trip on device, session survives app restart,
      declining authorization leaves the game fully playable (wallet is a rail, never a wall).
- [ ] COMPILE_GATE_OK on the Android target + RESULT with the exact build command + keystore
      procedure (passwords excluded) so the build is repeatable.

## What NOT to touch
Web/Windows build paths · the store-build assembly split (Phase A must prove it, not modify it)
· wallet stubs serving the web build. Owner holds all keystore secrets + wallet approvals.

*Cross-refs:* grant application M1 (docs/SOLANA_MOBILE_GRANT_APPLICATION_2026-07.md) ·
`docs/wallets-of-record.md` (publisher wallet = dApp Store identity) · dApp Store publishing:
docs.solanamobile.com/dapp-publishing/overview (portal + NFTs + 2-5 day review).
