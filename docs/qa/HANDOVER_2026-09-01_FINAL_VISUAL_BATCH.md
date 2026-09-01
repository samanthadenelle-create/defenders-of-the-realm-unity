# Handover — Final Visual Batch and Windows Test Build

Date: 2026-09-01  
Branch: `feat/synty-art-retheme`  
Parent before this check-in: `ca4bf5821`

## Delivered state

- WO-1294 Blink skill artwork and the nine canonical troop portraits are integrated through the
  shared UI catalog. The player quick-swap contract is three slots.
- The Talent Tree header presents `WISDOM` in one plate. Its three quick-swap slots are centered,
  circular icon overlays.
- The founding-choice modal fill reaches the wider frame.
- The shared build-collection browser puts action buttons in a footer below each card. Gathering,
  Realm, Defenses, and every other collection use that common layout.
- Tutorial combat waits for its intro dialogue to close before spawning the scripted town wave, so
  combat starts with the HUD available.
- The Synty castle perimeter is seated on merged-world y=0 from measured renderer bounds. Walls,
  gates, and towers no longer float.
- All four castle gateways are authored permanently open: the paired leaves are swung outward and
  the portcullis is removed from the passage. `GateTraversalInjector` remains the bidirectional
  NavMesh safety crossing. There is no opening animation in this build.
- The accumulated branch also includes the current mobile Obsidian navigation/UI work, combat
  animation/occupancy foundation, Synty structure re-theme, public-town/showcase API work, site
  updates, regression additions, imported UI art, and associated work-order/proof artifacts.

The durable chronological record is in `BATCH_STATE.md`, especially sections 7.14 and 7.15.

## Verification completed

- Compile: `Builds/final-visual-batch-compile3.log` — `COMPILE_GATE_OK`.
- Full data regression: `Builds/final-visual-batch-regression2.log` —
  `REGRESSION_OK 340/340 suites`.
- Perimeter proof: `Builds/final-visual-batch-perimeter-open2-proof.log` —
  `PERIMETER_PROOF_OK`.
- Visual captures: `docs/ui-evidence/wo1290_synty_perimeter/`.
- Windows build: `Builds/build.log` — `[DesktopBuild] SUCCEEDED`, 2,071 MB in 47.6 seconds.
- Executable: `Builds/Windows/DefendersOfTheRealm.exe`, 667,648 bytes, SHA-256
  `157DCCDAF52EBCA0E0759FAF35DD53A6985ED577698CA33603047F0F2004CE7E`.
- Fresh game assembly SHA-256:
  `C3E00CEED2F18828D109BD8EB336FF2F708643679DE972FBF9F0C60CA83FD02E`.
- Fresh overworld scene payload (`level3`) SHA-256:
  `EB9D6F8F591CE9F0EA55E58AB4F70D4C41C063CB925B52960912B16882955EE4`.
- R2 push: `R2_PUSH_OK 0 uploaded (0.0 MB), 526 unchanged`.
- R2 parity: `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=192`.
- Anonymous CDN probe: `R2_CHECK_OK`.
- Exact Windows catalog public HEAD:
  `StandaloneWindows64/catalog_2026.09.01.350657.hash` returned HTTP 200, length 32.

The earlier Windows “internet connection required” screen was caused by the matching Windows R2
catalog and bundles returning 404, not by a Windows network-code regression. The matching catalog is
now present, parity-verified, and publicly readable.

## Owner test checklist

1. Launch `Builds/Windows/DefendersOfTheRealm.exe` on a clean/first-run profile and confirm the title
   flow proceeds without the internet-required modal.
2. Start the tutorial and verify the final scripted battle shows the HUD as combat begins.
3. Walk through a castle gate from both directions. Confirm the open leaves match traversal and no
   wall edge catches the hero.
4. Open Gathering, Realm, and Defenses. Confirm each card's action button sits below the information
   card at desktop and narrow/mobile aspect ratios.
5. Open the Talent Tree. Confirm the single `WISDOM` plate and three centered circular skill icons.
6. Confirm the founding-choice modal background reaches the full frame width.

## Known notes

- Unity 6.0.4.8f1 emits a shutdown-only Lifecycle Management `NullReferenceException` after successful
  batch runs. The asserted success markers are present, Unity exits 0, and there are no compiler
  errors. This is treated as editor shutdown noise, not a product failure.
- The prior Windows player is preserved at `Builds/previous/Windows-20260901-152746`.
- The new player was deliberately not launched; owner testing is next.
- Local `tmp/`, `Logs/`, and `.tmp-solana-recover/` are excluded through `.git/info/exclude`. They
  contain runtime captures, browser Local Storage/session material, and recovery metadata and are not
  part of the project check-in.

