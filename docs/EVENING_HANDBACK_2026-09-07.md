# EVENING HANDBACK - 2026-09-07 (CLI seat, 09:00 -> 14:4x)

**Status:** LIVE for one day. Read this first tomorrow; it supersedes `docs/MORNING_HANDBACK_2026-09-07.md`
as the newest record (that file stays frozen).

## 0. Branch model (new tonight)

`master` = production, fast-forwarded to f5d39acd1 and tagged `v2026.09.07-store-359722`; `dev` = the working line from the same head; `feat/synty-art-retheme` retired. Tomorrow: `git checkout dev`.

## 1. The one file you need

**dApp Store submission:** `Builds/Android/store/EchoesOfElarion-store-2026.09.07.359722.apk`
versionCode **359722**, SHA-256 `A0A96EF36F3E70AA9CB024A26A2ABE5980E9A18844BE8DE43627C934D6422329`,
no tester define, release certificate `733666ce...2443`. Full Gate A record + What's New text:
`publishing/RELEASE_NOTES_2026-09-07.md`. Portal steps (Gate E) are yours. Codes **359651** and
**359664** are burned in the portal by the earlier upload attempts; do not upload either.

Tester twin: **2026.09.07.359731** on the Seeker (installed 14:37:32, `adb dumpsys`) and distributed to Firebase testers 14:40 (`Builds/ship-evening-0907.status`: TESTER_APK_OK, SEEKER_INSTALL_OK, DISTRIBUTE_OK).

## 2. What landed today (every commit gated; markers named in each message)

- **Morning:** constant EXIT on Manage, MOVE reachable, boot never signs, crafting placement door, Build
  Collections categories (55d3a7c56); board ingest + Verify joins the owner queue (05de2d23a); the 15
  Manage screens closed on your word.
- **Midday wave (446/446):** WO-1579 purchase hold, 1584 store sell screen, 1585 attacks report, 1586 army
  muster gold, 1587 save carries schemaVersion + drain reasons, 1588 locked door, 1589 chest toasts,
  1590 stone shortfall words, 1596 Rough Stone fanfare, 1597 Manage hub.
- **api, all live in production:** absent schemaVersion accepted (a62927967 - the 07:53 cloud-save
  outage), nonce-header cap bypass closed + one migration runner (82e9d5730), migration 0022 default
  drop (b138aa20a), reset epoch / new-game cloud fix (fb196fbeb), SKU dropdown (b10038556 code; deploys
  with the next api push). You ran 0022 + 0023 in Neon (verified nullable / default NULL).
- **READY cycle, afternoon wave (454/454):** 1429, 1452, 1458, 1467, 1482, 1483, 1485, 1489, 1490, 1494,
  1500, 1505, 1526, 1534, 1535, 1573, 1575, 1576, 1577, 1578, 1580, 1582, 2004, 1598 client, 1599, and
  Grok's WO-1595 merged by explicit path after review.
- **Evening wave (456/456):** WO-1600 Jeweler card off the Title, 1601 Skills tree band + fit, 1602
  atmosphere traces (no cause yet), 1603 pursuit attribution + dead-hero stamp guard, 1604 biome drop.
- Branch pushed to origin through 3a93f288e (plus 4.6 GB of LFS objects that had never been pushed).

Board: `python tools/board_build.py` - Ready 34, Fixed 50 (yours to felt-test), Verify 1.

## 3. Proven on your device today

- `RESET ACCEPTED ... resetEpoch=1788805425` and `offline queue DRAINED - 16 markers` at 13:34: the cloud
  row holds the new town.
- `RenewSessionAsync held - session extended with NO wallet prompt` at 08:15 and 08:29.

## 4. Open, and who holds it

| Item | Holder |
|---|---|
| WebGL / Vercel deploy of this code | HELD by your ruling (13:5x); `tools/ship-web.ps1` when you say |
| Play AAB for 359722 | not cut; `google-play-aab-build.ps1` (the 359670 AAB predates WO-1600..1604) |
| WO-1602 cause (water then haze) | data: build the exe, `run-autopilot-fleet.ps1 -Count 1 -Graphics`, grep `[Flow:Atmos]` |
| WO-1603 device pulser | named by the next F8 quiescence capture (`deadchase-<id>` / owner tag) |
| The crossing that never landed the hero at (0,0,50) | needs a ticket (WO-1604 s Unproven) |
| Compact-modal title band above the plate (every compact modal) | needs a ticket (WO-1600 finding) |
| Garrison Hold / Hunter (WO-1595 s2.3), Grok's 1593 / 1594 | Grok's branch; CLI re-reviews on handback |
| Enemy stat pins: necromancer id collision; caveman / feral-wolf / tiefling-cultist rows | your balance ruling |
| Second device with an older reset epoch stays 409 | your ruling (WO-1598 gap) |
| CombatText dedupes on kind alone (pickup toast merges into a kill toast) | ticket candidate (WO-1589) |
| WO-1084..1087 from the Grok/UI seat | SUPERSEDED - misread frames + number collision; pointers to 1600..1604 |
| FOUNDER promo | `publishing/PROMO_LAUNCH_PLAN_2026-09-07.md` - run after the store update is live |

## 5. Honest gaps

- WO-1602 is instrumentation only; nothing about the water/haze is fixed.
- WO-1603 closes one provable hole; the device pulser is not yet named.
- Every FIXED ticket above is gated, not felt-tested; your Pass closes them.
