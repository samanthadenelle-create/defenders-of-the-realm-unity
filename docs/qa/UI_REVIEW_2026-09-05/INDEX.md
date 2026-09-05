# UI review walk - headed, on the owner's Seeker, build 2026.09.05.355952 (2026-09-04 23:40-23:52)

Method: `adb shell input tap` along the screen graph, `screencap` at every node, logcat harvested for the
`[Flow:*]` line each tap emits. A tap with no trace line is a finding, not a miss. Owner: "you can use my
phone to run a headed test". Sprint doc: `docs/SPRINT_2026-09-05_ui_coherence_and_the_reason_to_play.md`.

| # | PNG | Node | Proving trace | Verdict against the one question |
|---|---|---|---|---|
| 00 | `00-title-or-hub.png` | cold launch -> welcome-back in town | `claim(cold-load) SKIPPED - claim (resume) already pending`; `Claim #1 1671s`; `DEFERRED: 'Title' not a hub` -> `RELEASED: Main_Castle_Overworld` | PASS - WO-1383 + Lane G proven: `27m`, Wood/Iron/Stone rows, one COLLECT, never over Title |
| 01 | `01-harvest-result-modal.png` | COLLECT -> Harvest Result | `CollectAll banked=5171`, `pending=0` | FAIL - **WO-1392**: "Collected 1979 of 2393, 414 wood LOST" right after the popup promised +672; numbers do not reconcile; the loss is silent at COLLECT time |
| 02 | `02-hud-town.png` | HUD after close | `context -> Town` | see 15 |
| 03 | `03-manage-launcher.png` | MANAGE launcher | `Manage/Queues screen opened` | PASS - four cards, one question each |
| 04 | `04-manage-defense.png` | Defense tab | `tab -> Defense` | review pending |
| 05 | `05-manage-buildings.png` | Buildings tab | `tab -> Buildings` | review pending |
| 06 | `06-manage-research.png` | Research tab | `research browse -> 17 perk row(s) (17 locked)` (was 0) | PASS - WO-1390 proven: locked rows, the reason sentence, an UPGRADE door; NIT: door label truncates `UPGRADE CATHE...` |
| 07 | `07-manage-troops.png` | Troops (rebuilt) | `troops browse: 9 def(s) -> 2 Train, 2 Upgrade` | PASS - WO-1382 proven: rail + card + `Train one: 45s . Ready` (WO-1387) + TRAINING NOW |
| 08 | `08-troops-after-train.png` | TRAIN 1 FOOTMAN | `Train CTA 'troop-footman'` -> `train job enqueued (45s)` -> `TRAINING NOW rows=1` -> `notice: Training started.` (79 ms) | PASS - the tap the old build never logged; chip 0/2 -> 1/2, bar `43s left` |
| 09 | `09-troops-queue-drawer.png` | OPEN QUEUE | `queue drawer BUILT 1 row(s): FinishNow=1 Ad=1 Cancel=1` | PASS (WO-1368 proven) |
| 10 | `10-troops-after-upgrade.png` | UPGRADE TO L4 (tap) | none - the drawer was still open | FAIL - **WO-1393**: the drawer overlays the card and QUEUE (top-right) does not close it; the `IN QUEUE - TRAINING` header is clipped under the rail |
| 11 | `11-research-upgrade-door.png` | Research door, first attempt | none; store opened | FAIL - **WO-1393**: Manage closed under the tap and the HUD's Night Market card (now large) caught it |
| 12 | `12-hud-after-store-close.png` | store CLOSE | `context -> Town` | - |
| 13 | `13-research-again.png` | Research tab again | - | - |
| 14 | `14-research-door-result.png` | Research door -> upgrade page | `BuildingUpgradePanelMvvm:Update()` | FAIL - **WO-1391**: Cathedral page - noise in the 3D preview, `Missing resources` with 4000 wood vs 1280 cost, `UPGRAD...`, empty-box glyph, off-kit styling; the page every Research door lands on |
| 15 | `15-hud-before-journey.png` | HUD | - | REVIEW: Heartfire row now readable + inside the plate (WO-1384 proven); Night Market card framed + large (proven); `Heartfire is full` line clipped at the plate bottom; FLAG chip sits on top of the card (flagged, `FlagCaptureButton.cs:173`) |
| 16 | `16-journey-deck.png` | JOURNEY (tap) | none | the second BACK keyevent consumed the tap - walk resumes here |

## Resume point
From the HUD: JOURNEY (1570,1095) -> Raids card -> RaidSelection -> RaidDeploy (WO-1385 bands) -> Realm Map;
then BUILD, HERO (equipment / bag / skill tree), TALK; then the Night Market sections. Coordinates in
2670x1200; each tap proven by its trace line before the next.
