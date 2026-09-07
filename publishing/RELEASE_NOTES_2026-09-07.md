# Release notes - the 2026-09-07 store update

**Status:** DRAFT for the owner's approval (she asked for these 2026-09-07 09:2x:
*"that will be a production build since it fixes the session state and wallet ...
so I need release notes"*). Nothing here is pasted into `publishing/config.yaml`
or a store portal until she signs off.

**Range:** live public release `2026.08.17.328845` (built 2026-08-17 13:45,
`Builds/android-build.log`) -> the STORE-shaped build cut from commit `05de2d23a`
(no `TESTER_BUILD` define). Its stamp is minted by the build itself and recorded
in the "Build identity" block at the bottom once the APK exists - the tester
build on the Seeker and at Firebase this morning is `2026.09.07.359405` and
carries `TESTER_BUILD`, so it is NOT the store candidate (overnight-apk-build.ps1:36-44).

**Supersedes:** the 2026-09-06 draft of this same file (range ended at
`2026.09.07.358574`), `publishing/RELEASE_NOTES_2026-09-04_since_328845.md` and
`publishing/RELEASE_NOTES_CANDIDATE_2026-09-03.md`. The older two stay on disk as
frozen point-in-time drafts.

**Audience:** players. No ticket numbers, no internal words, ASCII only.

---

## SHORT TEXT - Google Play "What's new" (500-character limit)

> Sign in once and stay signed in: your wallet is only asked for when you buy or
> redeem a code. Manage is rebuilt: nine full-screen pages, buildings as picture
> cards, an exit button on every page, and a door to move a structure you already
> placed. Heartfire gates your raids and every raid camp says what it pays.
> Training troops costs time, not gold. Stone replaces Food and is mined at the
> Quarry. Retreat, dungeon exits and welcome-back rewards are fixed.

Character count of the block above: 456 (limit 500), measured 2026-09-07. ASCII verified (0 non-ASCII bytes in this file).

## FULL TEXT - Solana dApp Store `new_in_version` / portal "What's New"

### Fixed

> You are no longer asked to sign in with your wallet every time you open the
> game. Sign in once and the game remembers you for the day; the wallet is only
> asked for when you buy something or redeem a code. Retreating no longer locks
> you inside a finished battle, and town no longer crawls or freezes after a
> fight. The long hang when leaving a dungeon for town is gone. Daily quests that
> could never be finished now can. Starting a new game no longer inherits the
> last save's welcome-back rewards, and the welcome-back screen now claims
> exactly what was banked while you were away. Every Manage page has an exit
> button, and picking up a placed structure to move it works again.

### New

> Manage is rebuilt: nine pages that fill the screen, buildings as picture cards
> with real portraits, whole buildings on every tile, a research tree with a
> painting per school, and a queue drawer that shows what is building, training
> and researching. Knight and Mage each reach a real castable ability on the very
> first talent point. Walk through all four castle gates instead of being
> teleported. Heartfire arrives and now gates your raids, and every raid camp
> says what it pays before you commit. The Journey deck opens Dungeons, the
> Realm Map and the Season Track, the Wardrobe is reachable, and the Night
> Market has a permanent spot on the HUD. Structures show wear from the first
> hit, so you can see what needs repair before you pay for it. Dungeon doors
> look like doors. Ogres now show up as ogres.

### Balance

> Stone has replaced Food across the realm and is mined at the Quarry. Training
> and upgrading troops costs time, not gold: gold is only spent to skip the wait
> or to hire reinforcements. Quest rewards now scale with where and how hard the
> quest is. Storage buildings were rescaled and now show their capped capacity,
> and income is held rather than burned when a store is full.

Word count of the three quoted blocks: 333 (measured 2026-09-07). ASCII verified - no em dash, no
ellipsis character, no smart quotes.

---

## Where each line is proven

| Claim | Commit |
|---|---|
| Sign in once; sessions renew without a signature for 12 h (`auth_sessions.signed_at`) | `77e8e8941` |
| Boot never signs; wallet only for purchases and codes (owner ruling 2026-09-07) | `55d3a7c56` |
| Wallet session established at connect, save 2xx, offline queue drained | `c6fd7d686` |
| Wallet signature rail no longer 500s on save/redeem | `0f35490ad` |
| Retreat no longer locks the battle | `99b574392` |
| timeScale leaks that stranded town at 0.04 / 0.28 | `6879abd60`, `c558bc53f` |
| Dungeon-to-town deadlock | `468d328e3` |
| Day-1 daily could never be completed | `ad1b592fc` |
| Training reachable again | `890ff5656` |
| START NEW inherited the old welcome-back | `57d3437a2` |
| Welcome-back claims what was banked; RAID line removed; both harvest surfaces agree | `c0c30f715` |
| Exit button on every Manage page | `55d3a7c56` |
| MOVE a placed structure reachable again | `55d3a7c56`, `32659c0f6` |
| Manage rebuilt, nine full-screen pages | `949e848a0`, `a6bbc523d`, `9ad5c7e3c`, `c0c30f715`, `94808e2e2` |
| Building portraits, six ladders; whole buildings on tiles | `85866703e`, `3c677027e`, `94808e2e2` |
| Research tree painting column, queue drawer | `c0c30f715`, `94808e2e2` |
| First talent point buys a castable | `02f9b8a4f`, `f1ba5575f` |
| Gate warp retired, all four gates walked | `62425d2d1` |
| Heartfire is the one raid gate | `44d46128d`, `da1773f1e` |
| Camp cards say what the raid pays | `87393bfeb` |
| Journey deck: Dungeons, Realm Map, Season Track | `5f48aa7bd` |
| Wardrobe reachable | `e94027216` |
| Night Market permanent HUD door | `bcecb5991`, `d836d2f15` |
| Structures show wear from the first point of damage | `dabfeecf2`, `a055da803` |
| Dungeon doors look like doors | `c0c30f715` |
| Ogre renders its model (was a capsule) | `cd57a1c1e` |
| Food -> Stone across the realm | `a11899d58`, `5625f9af8` |
| Quarry pays Stone | `9a9e65c8a` |
| Training costs time only; gold skips the clock / hires reinforcements | `65d5a7eae` |
| Quest rewards scale by placement and difficulty | `80439a18e` |
| Storage containers rescaled | `3cd28c86c` |
| Capped capacity shown | `8c03413ab` |

**Corrected from the 2026-09-06 draft:** that draft's Balance block read *"Troops
are paid for in gold"*. That is the state the owner reversed on 2026-09-04
(WO-1387, closed on her Pass 2026-09-07T00:49): training and upgrades cost time,
gold only skips. The line above says what ships.

**Not claimed, deliberately:** the store update media (four landscape
screenshots + icon, `publishing/media/` still empty), the orc caster art
(stand-in sheet), the three hub card paintings and rectangular troop portraits
(art asks, not in this build).

---

## Build identity (filled when the store-shaped APK lands)

- Source commit: `05de2d23a` (branch `feat/synty-art-retheme`)
- Store APK: `BUILD PENDING` - `overnight-apk-build.ps1` WITHOUT `-Tester`
- `versionName` / `versionCode`: `BUILD PENDING`
- SHA-256: `BUILD PENDING`
- AAB: `BUILD PENDING` - `google-play-aab-build.ps1` from the same commit
- R2 parity on the APK's catalog: `BUILD PENDING`
