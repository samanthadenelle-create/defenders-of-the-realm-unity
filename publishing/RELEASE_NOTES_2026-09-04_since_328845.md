# Release notes - dApp Store update since 2026.08.17.328845

**Status:** DRAFT, awaiting owner approval. Nothing here is pasted into
`publishing/config.yaml` until she signs off. **Do not edit config.yaml from this file.**

**Supersedes:** `publishing/RELEASE_NOTES_CANDIDATE_2026-09-03.md` (covered only the
last day of a three-week span) AND the `new_in_version` text at `config.yaml:155`,
which is banner-flagged **STALE since 2026-08-22**.

**Range:** live release `2026.08.17.328845` -> current build `2026.09.04.354315`.
756 commits since 2026-08-17; ~230 classified player-facing candidates, the rest
docs, board, gates, tools, CI and instrumentation.

---

## THE TEXT (paste into `new_in_version`)

> Structures now show wear from the first hit, so you can see what needs repair
> before you pay for it. Knight and Mage each reach a real castable ability on
> their very first talent point. Walk through all four castle gates instead of
> being teleported. Town no longer crawls or freezes after a fight, and retreating
> no longer locks you in a finished battle. The Night Market has a permanent spot
> on the HUD. Daily quests that could never be finished now can, and Stone has
> replaced Food across the realm.

**Character count: 501.** ASCII only, verified - no em dash, no ellipsis
character, no smart quotes.

---

## The five themes, and where each is proven

| Theme | Sourced at |
|---|---|
| **Damage you can see.** Structures scuff from the first point of damage, so the repair bill matches what is on screen. | `dabfeecf2`, `a055da803` |
| **Abilities arrive fast.** Knight and Mage first talent point each buys a castable, not a stat; talent tree axes and ability icons corrected. | `02f9b8a4f`, `f1ba5575f`, `03263bc39`, `c55cd7fb7` |
| **Combat and town stopped seizing up.** Retreat no longer locks the battle; two separate timeScale leaks that stranded town at 0.04 and 0.28 are contained; death animation no longer re-enters. | `99b574392`, `6879abd60`, `c558bc53f`, `34f86ebad` |
| **The town is walkable and readable.** Gate warp retired - all four castle openings are walked; storage containers scaled; Stone replaces Food; capped capacity shown. | `62425d2d1`, `3cd28c86c`, `a11899d58`, `8c03413ab` |
| **Things that could never work, now work.** The day-1 daily could not be completed by anyone; troop training was unreachable; the dungeon-to-town hang was a deadlock. | `ad1b592fc`, `890ff5656`, `468d328e3` |

Store surface (Night Market permanent HUD door, readable balance, portrait
layout): `bcecb5991`, `72f6d1953`, `d836d2f15`.

---

## Deliberately excluded, and why

- **Any "smaller download" claim.** The hero decimation to 50k was **reverted the
  same night** (`47aae2d8d` -> `e07e1b860`, owner: "smashed and shapeless"). The
  later commit wins. Do not restore this line.
- **Any purchase or revenue implication.** The pay path exists, but every unit of
  revenue so far is the owner's own. No traction language, no "players are buying".
- **Remote catalog seam** (`d37294950`) - shipped flag-gated OFF.
- **Clan chat** (`713d69bc4`) - gated local-only.
- **Map tab** - feature-flagged OFF (`FeatureFlags.MapTab`); realm travel is a stub.
- **Command Center, kill switches, Benefactors wall** (`58eb608e3`, `fa670277d`,
  `2a3586204`) - operator/web surfaces, not reachable by a reviewer in the APK.
- **Battle pass premium tier** (`71311beba`) - the season track earns by playing,
  but `premiumPassSku` is deliberately unauthored; no Buy button exists.
- **Google Play billing** (`c81b6861e`) - wrong storefront for this listing.

---

## Needs her decision

1. **Purchase sentence in the OTHER fields.** This text makes no purchase claim,
   so it is safe either way - but `long_description` and `testing_instructions`
   still carry one and must be checked against the shipped production flag.
2. **Battle pass.** Mention the monthly season track as a play-earned feature, or
   leave it out until the premium tier has content? Left out here.
3. **Build id.** This file names `2026.09.04.354315`; the 09-03 candidate named
   `2026.09.03.353999`. Confirm which APK is submitted.
