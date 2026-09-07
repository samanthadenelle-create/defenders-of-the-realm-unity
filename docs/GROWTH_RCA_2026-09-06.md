# Growth RCA - why there is no exposure and no player base (2026-09-06)

**Question (owner, verbatim):** *"determine why I can not gain any exposure and grow a base."*
**Method:** every claim below is read from the tree, a live tool call, or a fetched page this session, and is
cited. Where a number could not be read, it says UNPROVEN and names what would settle it.
**Author:** CLI seat (Fable lead), read-only. No code, no deploy, no git state touched.

---

## THE ONE-PAGE ANSWER

**There is no measurable exposure because there is no measurement, one storefront, one public build that is
twenty days and 897 commits behind, and no distribution work has been executed since 2026-08-19.** In order
of weight:

**1. The only door is a 150k-device store with no web catalogue, and the game's page inside it has not been
updated since 2026-08-17.** The landing page's one call to action is `solanadappstore://details?id=...`
(`site/index.html`, the "Get it on Solana dApp Store" button) - a deep link that only resolves on a Seeker.
Solana Mobile's own docs describe the dApp Store as "pre-installed on every Seeker device" and name no public
web listing ([docs.solanamobile.com](https://docs.solanamobile.com/solana-mobile-stack/dapp-store)); the
install base is reported at 150,000+ devices across 57 countries with 175+ dApps
([solanamobile.com](https://solanamobile.com/),
[Medium](https://medium.com/@omspatil980/mobile-apps-on-solana-a-deep-dive-into-the-seeker-powered-ecosystem-bb2a2ee6aa65)).
So the addressable audience is ~150k people who own one phone, browsing an in-device store among ~175 apps.
Anyone else who hears the name cannot install it at all.

**2. The public build is `2026.08.17.328845` and every fix since has never shipped.** `CANON_GROUND_TRUTH_2026-09-03.md:23-24`
records the live release and "756 commits, ~230 player-facing" behind it as of 09-03; `git log --since=2026-08-17`
is now **897 commits**, 55 of them with P0/softlock/crash/never in the title. The update has never been
submitted: `publishing/SUBMIT_CHECKLIST.md:186-223` is unticked from the screenshot capture step through
"submit"; `publishing/media/` contains only `README.md` - **no 512x512 icon, no screenshots**
(`config.yaml:70-71` `TODO(OWNER) - file does not exist yet`); the release notes are marked
`DRAFT, awaiting owner approval` (`publishing/RELEASE_NOTES_2026-09-04_since_328845.md:3`); the 08-22
submission record still reads `Review result: PENDING` (`SUBMISSION_READY_2026-08-22.md:270`). Strangers who
do install get the build from before the retreat softlock fix, the timeScale freezes fix, the "first talent
point buys a castable" change, and the wallet session rail (release notes lines 19-40, and WO-1440 RESULT §7b:
the store build predates the session rail, so a wallet holder's cloud save and promo redeem are dead there).
*(Good news, verified: that build still shipped `Assets/Resources/Enemies` and `Assets/Resources/Structures`
locally - `git ls-tree 5bc773833` - so public players are NOT seeing capsule enemies.)*

**3. Nothing measures whether anyone came.** Vercel Web Analytics is **not enabled** on the landing-page
project (`get_web_analytics` on `prj_rnbaJwN6CsuNGuRLtagf6oMFO3sY` -> `404 Web Analytics not found`, called
this session). The game's `EventTracker` posts `session_start` to `api/events/track` (`EventTracker.cs:53,143`),
so install-side numbers exist in Neon's `analytics_events` - but no seat can read them (DATABASE_URL is
redacted for agents, `tools/run-schema-repair.mjs:8-9`) and nothing in the tree reports them. Firebase App
Distribution has **one tester, the owner** (memory `firebase-app-distribution`). There is no dashboard, no
weekly number, no way to tell zero from a hundred.

**4. The marketing assets exist but were built once and never run.** `docs/marketing/SOCIAL_CONTENT_KIT.md`
(last commit 2026-07-19) still carries the retired tagline "Hold the last light" and "Pi Browser" as a
platform, both superseded (`CLAUDE.md` §7). The landing page carries Discord (`discord.gg/zDdwdy3duB`) and
X (`@EchoesOfElarion`) links (fetched this session), but no posting cadence, community size, or content
calendar is recorded anywhere in the repo - UNPROVEN either way, and unprovable from here. The Shareable Echo
Cards idea flagged as "a real viral/marketing asset, usable pre-launch" (`docs/ECHO_SOCIAL_VISION.md:14-16`)
was never built (no `EchoCard`/share path in `Assets/_Modules`). The referral and showcase APIs exist
(`api/referral/*`, `api/showcase/*`, client `ReferralService.cs`, `TownShowcaseClient.cs`) with no evidence of
a player ever reaching them.

**5. The name does not own its search results.** A web search for "Echoes of Elarion" returns the landing
page, then a Royal Road novel of the same name and an itch.io game by another developer titled *Echoes of
Elarion* (search this session). The snippet Google shows for the site still says "coming to the Solana dApp
Store" - stale crawl of pre-08-19 copy. A stranger who hears the name has a one-in-three chance of landing on
someone else's product.

**Bottom line:** the game is reachable by ~150k phones through one in-device store, listed there with a
20-day-old build and no refreshed media, described on the web by a page that cannot install it, with zero
instrumentation of who arrives. That is not a retention problem or a quality problem yet; it is a
distribution-and-measurement problem, and it is cheap to fix in that order.

---

## DETAIL

### D1. Where the game is available today (read from the tree)
| Channel | State | Evidence |
|---|---|---|
| Solana dApp Store (Seeker) | LIVE, build 2026.08.17.328845; update never submitted | `config.yaml:47` App NFT `5MG4at...yFe6`; `SUBMIT_CHECKLIST.md:186-223` unticked; `SUBMISSION_READY_2026-08-22.md:270` PENDING |
| Firebase App Distribution | tester build 358574 tonight; one registered tester (owner) | memory `firebase-app-distribution` |
| Google Play | script exists, no listing evidence | `google-play-aab-build.ps1`; `api/_lib/google-play-*.js` server side only |
| Pi Browser / WebGL | parked by owner ruling 2026-09-02 | memory `apk-is-the-vision-pi-is-parked`; `vercel.json` outputDirectory `Builds/WebGL` |
| Web landing | live, no analytics | fetched `https://echoes-of-elarion.vercel.app/`; project `echoes-of-elarion` |

### D2. What a stranger can find
- Landing page: title, hook "They gave their souls to survive", screenshots (title screen, Heart, Grom/Thrain/Sylas,
  a defence scene), a five-second muted loop, QR for desktop, Discord + X links, one CTA that only a Seeker can
  open. Privacy and Terms resolve (`config.yaml:57-62`, probed HTTP 200 on 08-08).
- No press kit folder, no trailer beyond the 5 s loop, no store-page screenshots on disk (`publishing/media/`).
- Search: name collision with two unrelated products (Royal Road, itch.io).

### D3. What the first ten minutes deliver
- A guest can play without a wallet: the guest rail exists (`BackendRequestSigner.cs:11,139,186`, `X-Guest-Id`),
  and the title falls back to a "Connect Wallet" corner button (WO-1420 body). So the store build does not
  wall off non-crypto players - good.
- Abilities: "Knight and Mage each reach a real castable ability on their very first talent point" is in the
  UNSHIPPED release notes (`RELEASE_NOTES_2026-09-04_since_328845.md:19-20`); public players are on the old
  ladder. Memory `retention-is-the-business-problem` names this as the lever.
- The wallet session rail, cloud save, and promo redeem for wallet holders are dead in the public build
  (WO-1440 RESULT §7b, WO-1441 RESULT).

### D4. Shipped vs HEAD
897 commits since the live build; the checklist's own Gate A was found recording the wrong APK on 09-03
(`CANON_GROUND_TRUTH_2026-09-03.md:73`). The signing certificate of the live release has never been captured
(`:156-163`), which the checklist itself calls the one cheap close: install the candidate over the live store
install on the Seeker and see whether Android accepts the update.

### D5. External
- dApp Store: pre-installed on Seeker, portal-based publishing at `publish.solanamobile.com`, review 3-5
  business days (`SUBMIT_CHECKLIST.md:220`), no public web catalogue found in the docs.
- Comparable indies: the consistent answer in postmortems is that the first thousand come from a community the
  developer already runs (Discord playtests, devlogs, one platform's own discovery engine), not from a listing
  ([pushtotalk.gg](https://www.pushtotalk.gg/p/asking-game-devs-where-they-found),
  [a16z soft-launch](https://a16z.com/mobile-game-soft-launch/),
  [Discord dev playbook](https://discord.com/blog/the-game-developer-playbook-part-one-getting-started-on-discord)).
  Solana Mobile's own Discord Developer role is named in the checklist (`:221`) as the escalation path.

---

## THE CHEAPEST ACTIONS THAT CHANGE THE ANSWER (ranked)

1. **Ship the update.** Capture the four screenshots + 512 icon from build 358574 (owner-action, one evening:
   `publishing/media/README.md` has the exact specs), approve the drafted release notes (owner-action), then
   run `SUBMIT_CHECKLIST.md` to the end (CLI-action, gated). Spend: none. This alone moves public players from
   the 08-17 build to one with 230 player-facing fixes.
2. **Turn on the counters.** Enable Vercel Web Analytics on `echoes-of-elarion` (owner-action, one click in the
   dashboard, free tier) and add a read-only `api/admin/stats` view of `analytics_events` session_start by day
   and by `playerId` kind (CLI-action; the endpoint family exists under `api/admin/`). Until this exists,
   every growth conversation is guesswork.
3. **Give the web a real door.** The landing CTA needs a path for non-Seeker visitors: at minimum a mailing
   list / Discord CTA above the fold and a "how to get a Seeker" line; better, the parked WebGL or a Play
   listing as a second store (CLI-action for the page; owner ruling for Play). Spend: Play developer account
   $25 one-time if chosen.
4. **Run the kit.** Refresh `SOCIAL_CONTENT_KIT.md` to current canon (CLI-action, one pass), then post on a
   cadence in the Solana Mobile Discord and X, with the Seeker QR (owner-action, recurring). Ask Solana Mobile
   publisher support for featuring consideration once the update is live - the checklist already names the
   address.
5. **Own the name.** Register the X handle and Discord as the canonical links in the store listing and add a
   one-line "not the novel / not the itch.io game" disambiguation in page copy (CLI-action). Consider whether
   the series title *Defenders of the Realm: Echoes of Elarion* should lead in metadata (owner ruling).
6. **Build the one viral asset already designed.** Shareable Echo Cards (`ECHO_SOCIAL_VISION.md:14`): local,
   no backend, one tap. CLI-action, one lane.
7. **Widen the tester pool** beyond one: Firebase App Distribution groups cost nothing (owner-action: invite
   emails; CLI-action: nothing).

**What is still UNPROVEN and would take one action to settle:** the real install/session count (owner runs
one query against `analytics_events` with her DATABASE_URL, or grants a read-only stats endpoint); whether the
live listing's store page has the old "Hold the last light" copy (owner opens it on the Seeker); Discord/X
member and follower counts (owner reads them).
