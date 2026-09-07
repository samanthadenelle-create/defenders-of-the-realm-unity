# GET-WELL PLAN - 2026-09-06 (CLI seat, from the read-only audit fleet + the owner's two felt-test batches)

**Status:** LIVE PLAN. Supersedes nothing; it sequences what `docs/READY_RCA_2026-09-06.md` (root causes) and
`docs/GROWTH_RCA_2026-09-06.md` (why no players) found. Every line below cites the audit that measured it;
the audits' evidence lives in the minted work orders (WO-1446 onward, banner in `CLI_LANES_WO_NUMBERS.md`).

---

## 0. THE DIAGNOSIS IN ONE PARAGRAPH

The game that people can install is `2026.08.17.328845`, twenty days and 897 commits behind HEAD, with the
retreat softlock, the frozen world clock and a dead wallet rail still in it. HEAD is healthier than that
build by a wide margin (tonight's tester build 358574 passed fifteen of the owner's felt-tests in one
sitting), but nothing ships it: the submission checklist is unticked from screenshots onward, there is no
icon or screenshot set in `publishing/media/`, and 101 commits are unpushed. Meanwhile three P0 data
defects sit unshipped or unwritten (cloud LOAD drops the town; the api renewal cap would 500 every wallet
session on deploy; the cheapest pack is unbuyable), the device runs at 22 fps with an empty town, and the
evidence ring is evicted in two seconds by one unthrottled log line. Nothing measures whether anyone
arrives. The cure is ordered, and most of it is cheap.

## 1. STOP THE BLEEDING (this week, in this order)

| # | Action | Owner / CLI | Evidence | Ticket |
|---|---|---|---|---|
| 1 | Do NOT deploy `api/` until `auth_sessions.signed_at` exists on live Neon. Owner runs `tools/run-schema-repair.mjs`; CLI adds the numbered migration and a test that fails when wallet-auth INSERTs a column absent from `api/migrations/`. The uncommitted work is preserved at `WorkOrders/patches/wo1441-api-renewal-cap.UNCOMMITTED.patch`. | Owner (one command) + CLI | wallet audit: sweep says `MISSING ON LIVE DB: auth_sessions.signed_at`; `wallet-auth.js:315` INSERTs it on the normal mint path | P0, minted |
| 2 | Cloud LOAD must restore the whole town, not seven currency fields. Route the backend payload through `ApplyPersisted` + `MigrateForImport`. Until then a reinstall loses the base. | CLI | save audit: `GameStateService.cs:2099-2145` | P0, minted |
| 3 | Set `ADMIN_OPS_KEY` on the deployment (the command centre Fail is the unset key answering `OPS_WRITE_NOT_CONFIGURED`); the refusal logging that lands tonight will prove it. | Owner (one env var) | READY RCA #5; WO-1244 lane | WO-1244 |
| 4 | Mirror `builders-hour` into `USD_ANCHORS` and `GooglePlayProductCatalog`; add the two node suites to the packs.json gate. | CLI | store audit: two node tests fail at HEAD | P0, minted; reopen WO-1388 |
| 5 | Throttle the EnemyAggro probe trace and drop its stack frames; fix the `TowerPreviewCamera` MSAA mismatch. These two are why the device log is unreadable and why 260 BREAKs fired in 144 s. | CLI | device audit | P0 x2, minted |
| 6 | Ship the tester build that passed tonight to the store as the UPDATE: owner supplies 4 screenshots + the 512 icon + approves the release notes; CLI executes `publishing/SUBMIT_CHECKLIST.md` to the end, as written. | Owner (media) + CLI (checklist) | growth RCA #2; checklist `:186-223` unticked | WO to mint after the owner's word |

## 2. PROVE THE THINGS THAT ARE CLAIMED FIXED (next device session, ten minutes of play)

One capture on build 358574 or later closes all of these; the markers are named so the read is one grep:

- WO-1441: `MintSessionAsync why=explicit-connect`, one `/api/game/save` 2xx, `offline queue DRAINED` (queue depth was 112).
- WO-1436 AC3 / WO-1439 AC3: a raid with ability faces tappable and the spire at zero garrison damage (the 14:37 capture already shows `stars settled: 3` and `ability bar bound ... class='mage'`).
- WO-1215: a hero holding a shield, `AttachOffHandProp MEASURED` + `registryProbe path=START` + a screenshot of the same hero.
- WO-1327: the fireball inside walls, with the projectile lifecycle trace the lane added.
- Perf: `LOW fps` samples on the post-fix build, to see whether the 22 fps empty town survives the log throttle.

**Precondition:** the F8 daemon stopped capturing at 13:42 today while the device played until 14:38; restart it and add a heartbeat (minted). Without it the next session is unrecorded again.

## 3. GET SEEN AND MEASURED (growth, cheapest first)

1. **Ship the update** (item 1.6). Everything else is downstream of a public build that is not twenty days old.
2. **Turn on measurement**: enable Vercel Web Analytics on the landing project (it returned 404 tonight) and expose a read-only stats endpoint over the `session_start` events already landing in Neon. Zero and one hundred are indistinguishable today.
3. **A door for non-Seeker phones**: the landing page's only call to action is a `solanadappstore://` deep link. Add the Google Play or APK path the AAB lane already builds, gated by the same R2 parity proof.
4. **Run the social kit** after refreshing it (it still carries the retired tagline and "Pi Browser"). Publish the cadence; record community size somewhere a seat can read.
5. **Own the name**: the search results for "Echoes of Elarion" are a novel and another itch.io game; the snippet still says "coming to".
6. **Echo Cards** (`ECHO_SOCIAL_VISION.md:14-16`), the one shareable asset the design named, never built.
7. Widen the Firebase tester pool past one (the owner).

## 4. MAKE THE HARNESS TELL THE TRUTH (so the next twenty days do not repeat these)

- `SessionRegression` has never run and its `6/6` is a hardcoded label: add it to the check-in gate and derive the count (P0, minted).
- Six suites claim to MEASURE and are text lint; about 40% of the harness is text matching. Start with the two HUD suites whose justification is factually wrong (P0, minted).
- Two suites pin a four-face action-bar model that is never bound while the dock ships five faces (P1, minted).
- Allowlists: every exemption gets a WO, a date and a remove-by; a ratchet fails on undated ones (P1, minted).
- Nine orphan suite files; the fleet judges by exit code and asserts file existence (P2, minted).
- The gate that signs off `packs.json` must run `node --test`; `REGRESSION_OK` cannot see the server half.

## 5. BOARD AND CANON HYGIENE (one commit each)

- Ten tickets were already landed in two bundled commits and never flipped; a sweep caught them tonight. Rule recorded in memory: at every gate commit, cross-check READY tickets against the diff.
- Thirteen ManageRedesign WOs carry no Status line and are invisible to the board (minted, with the true state of each).
- `CLAUDE.md` section 8 is a dated snapshot that has rotted (save schema v38 vs 41; 19 asmdefs vs 25); convert to a pointer table. Five docs say the branch is pushed; 101 commits are not. Three ground-truth anchors present as current (minted).

## 6. RAID AND ECONOMY, AFTER THE ABOVE

- A three-star clear banks 25 wood of the 1800 promised (bank full, repeat-clear x0.25): the deploy screen must quote what will bank, and spoils above cap must be retained, not LOST (P1, minted).
- `RaidDeployScreen` has no backdrop; rally flag magenta; troop tray unreadable; top band overlaps (minted).
- WO-1438 (push the breach) is in a lane; the navmesh under a felled wall is unmeasured (`holeNavmesh=` on the next capture decides whether a carving obstacle is still needed).
- Echo harvest split ignores the authored per-Echo rate (minted); `Grant` discards the clamped remainder (WO-1445); loot-vs-harvest burn needs one owner ruling (minted as SPEC).
- WO-1444 needs one word from the owner: paint the queue count on the pill (recommended) or keep the picture and toast FULL.

## 7. WHAT THE OWNER DECIDES (each is one word or one command)

1. Run `tools/run-schema-repair.mjs` with the database URL (item 1.1).
2. ~~Set `ADMIN_OPS_KEY` on the deployment (item 1.3).~~ DONE by the owner 2026-09-06 22:5x (her word). PROVEN LIVE by the owner the same hour: she minted a custom SKU through the command centre, an ops WRITE, which the endpoint refuses with OPS_WRITE_NOT_CONFIGURED while the key is unset (api/admin/ops.js:177-184). A CLI probe cannot see past the read key (prod answered UNAUTHORIZED).
3. Approve shipping build 358574 (or its store-shaped twin without `TESTER_BUILD`) as the store update, and supply the four screenshots + icon.
4. WO-1444: (a) count on the pill, or (b) keep the picture.
5. Loot into a full bank: lost (Clash shape) or retained (the WO-1434 law extended).
6. The api renewal-cap patch: repair and ship with the migration, or revert the working copy.

## 8. WHAT "WELL" LOOKS LIKE (the exit criteria)

- The public store build is less than a week behind HEAD, and the submission checklist is executed as written every time.
- Every P0 on the board has a captured proof line, not a source read.
- Web Analytics and the events endpoint report a number; the number is on the board weekly.
- `SESSION_GUARDS_OK`, `CHECKIN_SUITE_OK` and `REGRESSION_OK` all appear on fresh logs from the same day before any ship.
- No READY ticket on the board is already implemented at HEAD (the sweep runs after every bundled commit).
- The device log survives a ten-minute session without evicting its own boot window.
