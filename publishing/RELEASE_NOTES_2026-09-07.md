# Release notes - build 2026.09.07.358574

**Status:** DRAFT, awaiting owner approval. Nothing here is pasted into
`publishing/config.yaml` or a store portal until she signs off.

**Range:** live public release `2026.08.17.328845` (built 2026-08-17 13:45,
`Builds/android-build.log`) -> declared stamp `2026.09.07.358574`
(`ProjectSettings/ProjectSettings.asset:148,177`).

**Supersedes:** `publishing/RELEASE_NOTES_2026-09-04_since_328845.md` (stopped at
`2026.09.04.354315`) and `publishing/RELEASE_NOTES_CANDIDATE_2026-09-03.md`.
Both stay on disk as frozen point-in-time drafts.

**Audience:** players. No ticket numbers, no internal words, ASCII only.

---

## THE TEXT (paste into `new_in_version` / Play "What's new")

### Fixed

> Retreating no longer locks you inside a finished battle, and town no longer
> crawls or freezes after a fight. The long hang when leaving a dungeon for town
> is gone. Daily quests that could never be finished now can. Troop training is
> reachable again. Starting a new game no longer inherits the last save's
> welcome-back rewards, and signing in with a wallet no longer fails on save.

### New

> Manage is rebuilt: nine screens, buildings as picture cards with real
> portraits, and a door to pick up and move a structure you already placed.
> Knight and Mage each reach a real castable ability on the very first talent
> point. Walk through all four castle gates instead of being teleported.
> Heartfire arrives and now gates your raids, and every raid camp says what it
> pays before you commit. The Journey deck opens Dungeons, the Realm Map and the
> Season Track, the Wardrobe is reachable, and the Night Market has a permanent
> spot on the HUD. Structures show wear from the first hit, so you can see what
> needs repair before you pay for it.

### Balance

> Stone has replaced Food across the realm and is mined at the Quarry. Troops are
> paid for in gold, and gold also hires mercenaries. Quest rewards now scale with
> where and how hard the quest is. Storage buildings were rescaled and now show
> their capped capacity, and income is held rather than burned when a store is
> full.

**Word count of the three quoted blocks: 239. ASCII verified - no em dash, no
ellipsis character, no smart quotes.**

---

## Where each line is proven

| Claim | Commit |
|---|---|
| Retreat no longer locks the battle | `99b574392` |
| timeScale leaks that stranded town at 0.04 / 0.28 | `6879abd60`, `c558bc53f` |
| Dungeon-to-town deadlock | `468d328e3` |
| Day-1 daily could never be completed | `ad1b592fc` |
| Training reachable again | `890ff5656` |
| START NEW inherited the old welcome-back | `57d3437a2` |
| Wallet signature rail no longer 500s on save/redeem | `0f35490ad` |
| Manage rebuilt, nine screens | `949e848a0`, `a6bbc523d`, `9ad5c7e3c` |
| Move a placed structure | `32659c0f6` |
| Building portraits, six ladders | `85866703e`, `3c677027e` |
| First talent point buys a castable | `02f9b8a4f`, `f1ba5575f` |
| Gate warp retired, all four gates walked | `62425d2d1` |
| Heartfire is the one raid gate | `44d46128d`, `da1773f1e` |
| Camp cards say what the raid pays | `87393bfeb` |
| Journey deck: Dungeons, Realm Map, Season Track | `5f48aa7bd` |
| Wardrobe reachable | `e94027216` |
| Night Market permanent HUD door | `bcecb5991`, `d836d2f15` |
| Structures show wear from the first point of damage | `dabfeecf2`, `a055da803` |
| Food -> Stone across the realm | `a11899d58`, `5625f9af8` |
| Quarry pays Stone | `9a9e65c8a` |
| Troops cost gold; gold hires mercenaries | `281902df0` |
| Quest rewards scale by placement and difficulty | `80439a18e` |
| Storage containers rescaled | `3cd28c86c` |
| Capped capacity shown | `8c03413ab` |
| Income held, not burned | `d75d0044c` |

The Realm Map line is safe to publish now: the `MapTab` feature flag was DELETED
2026-09-05 (`Assets/_Modules/Core/FeatureFlags.cs:843`), so the Map is reachable.
The 09-04 draft's exclusion of it no longer applies.

---

## Deliberately excluded, and why

- **Any purchase, pack, price or revenue language.** Builder's Hour and the store
  funnel shipped, but this text makes no purchase claim, so it is safe whichever
  way the production purchase flag is ruled.
- **Battle pass premium tier** - the season track earns by playing; the premium
  SKU is deliberately unauthored, so no Buy button exists.
- **Google Play billing plumbing** - infrastructure, not a player-visible change.
- **Command Center, kill switches, migration runner, drift oracle, parity gates**
  - operator and backend surfaces a player never sees.
- **Clan chat** - gated local-only.
- **Remote catalog seam** - shipped flag-gated OFF.
- **Any "smaller download" claim** - the hero decimation was reverted the same
  night it landed.

---

## Needs her decision

1. Which text goes to which store. The three blocks above are one body; Play
   allows 500 characters in "What's new" and the dApp Store field is longer.
   A Play-length trim needs her pick of which block leads.
2. Whether the season track is mentioned as a play-earned feature. Left out.
3. Whether `long_description` and `testing_instructions` keep their purchase
   sentence. This file does not touch them.
