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

## Follow-up closure (2026-09-01)

- Restored the owner-approved legacy stone Archer Tower ladder: Castle Round at L1, Castle Square at
  L2, and Medieval Big at L3. The retired wooden tower assets remain in the project for compatibility
  but are no longer selected by `tower_ground_archer`. All three restored prefabs are registered in
  `Structure_Art` and were uploaded with this build.
- Rebuilt the perimeter with upright, ground-seated corner towers. The four permanently open gates now
  have short bidirectional `NavMeshLink` passages for enemies/troops; optional hero warps use the same
  measured inner/outer seats. Gate traversal is default-on.
- Moved the recommended starter settlement from a hardcoded C# transform table to the dual-copy
  canonical `starter-settlement-layout.json`. It stores stable catalog IDs plus placement transforms;
  `CatalogRegistry` resolves whichever art each ID currently selects.
- Shared Obsidian buttons now retain one authoritative text child, removing the doubled Pause `CLOSE`.
  HUD medallion art is fitted inside a true-square circular bound.
- Restored the five-action peaceful dock (`Build`, `Talk`, `Hero`, `Journey`, `Manage`) and the Skip
  Tutorial button's raycast target. Journey cards now show the canonical Quest and Raid artwork.
- Mage primary dispatches the authored Q spell, Ranger primary dispatches the authored Q bow attack,
  and the adaptive primary face mirrors that ability's icon/caption/cooldown. Knight remains a basic
  weapon attack; Block drives the existing Animator `Block` bool with the authored
  `sword and shield block idle` clip, producing a visible held offhand/shield pose.
- Fresh compile: `Builds/compile_final_followup.log` — `COMPILE_GATE_OK`.
- Fresh full regression: `Builds/regression_final_followup2.log` —
  `REGRESSION_OK 341/341 suites`.
- Fresh Windows build: `Builds/build_final_followup.log` — `[DesktopBuild] SUCCEEDED`, 2,071 MB in
  52.0 seconds. Player: `Builds/Windows/DefendersOfTheRealm.exe` (667,648 bytes); game assembly
  SHA-256 `340900CC532D2F368911751648B17DFDAC10481EFBD4C628E8E502834F2D5C9D`; overworld `level3`
  SHA-256 `63433F00782CA616D47347D38C0B2CC0DE25EE6F70AFE7CBE619A5D225731856`.
- R2: `R2_PUSH_OK 5 uploaded (0.4 MB), 524 unchanged` and
  `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=195`; anonymous access passed
  `R2_CHECK_OK`. The exact Windows catalog returned HTTP 200 with length 32. This keeps the earlier
  first-run “no internet” fix valid for the follow-up build as well.

## Final felt-test and Firebase closure (2026-09-01)

- Replaced the provisional Journey portraits with the owner's exact locked wide-card assets from
  `quests.png` and `Raids.png`; both cards now use the shared full-card surface.
- Equipment had no gameplay unlock requirement. The gray card was caused by a missing runtime panel
  host, so `EquipmentPanelBootstrap` registers `PanelId.EquipmentPanel` before Hero opens.
- Raised Skip Tutorial above the full-screen dialogue raycast canvas. The prior player log recorded
  `SHOW`/`RESTORED` but no `TAPPED`, confirming pointer interception rather than a state bug.
- The shared Close plate already contains `CLOSE`; live TMP labels are hidden when that authored plate
  loads, removing the doubled word while preserving a missing-art fallback.
- Removed the floating repair quad and world-space `Repair?` label that rendered edge-on as the yellow
  line/squashed text. The subtle ground disc and actionable HUD repair prompt remain.
- Defense reports use matching obsidian card wells instead of a raw black list beside a beige panel.
- Mana, Vigor, and Focus remain intentional class-resource names for the second hero bar; they are not
  unlock requirements.
- Compile: `Builds/compile_final_ui_batch.log` — `COMPILE_GATE_OK`.
- Full regression: `Builds/regression_final_ui_batch.log` — `REGRESSION_OK 341/341 suites`.
- Windows build: `Builds/build_final_ui_batch.log` — `[DesktopBuild] SUCCEEDED`, 2,083 MB.
  Fresh module hashes: Core `2922A2FDC2D15DA4D63A40F3D3D9C91B2F20B395A5398D5283562C68E4D0585E`,
  HUD `4AAEDE713AABB58877FB57FE69984483FA3177A562759645BF86F466D3B943D0`, Village
  `19168FBD00B2CA3775257609C8F20B4009B30DD35901F509D64CB15978B637A8`.
- Tester APK version `2026.09.01.351238 (351238)`: 543,703,055 bytes, SHA-256
  `8C9D1BB964557F22C596B122922483F61591BA0C6EDB13361F6149A015BFEAE3`.
  `Builds/apk-build.log` records `[AndroidBuild] SUCCEEDED`; schema parity passed all 42 tables.
- R2: `R2_PUSH_OK 49 uploaded (85.5 MB), 529 unchanged`; full parity passed with
  `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=228`.
- Firebase App Distribution succeeded to group `testers`: release `2026.09.01.351238`, release ID
  `43nnpnk9lad7g`. Console:
  https://console.firebase.google.com/project/defenders-of-the-realm-echos/appdistribution/app/android:com.denellestudios.echoesofelarion/releases/43nnpnk9lad7g
