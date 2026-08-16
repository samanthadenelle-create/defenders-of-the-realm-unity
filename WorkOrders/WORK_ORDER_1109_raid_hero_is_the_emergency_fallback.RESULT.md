# RESULT — WO-1109 every raid spawned the EMERGENCY pill-hero

**Date:** 2026-08-16  **Seat:** CLI (commit `256fa9ee3`)
**Status:** DONE — pending PO felt-verify

Found by the SME readiness audit run against the owner's *"can i finally test raids fully?"*

## What shipped

1. **The lying header, retracted.** `HeroControlEnsurer`'s header stated as fact: *"RaidHeroSpawner builds
   the REAL class body one frame after load."* **That class does not exist** — zero files, zero
   references. Canon's "comments lie" warning in its purest form. `GoRaid` never marked the hero
   `DontDestroyOnLoad`, so `TryRecoverCarriedHero` found nothing and `Ensure()` fell to
   `SpawnEmergencyHero` — whose FIRST LINE is `FlowTrace.Fail`. A Fail has landed in the F8 break-log on
   **every raid entry**, training every seat and the owner to ignore Hero Fails. The header is rewritten
   to quote its own false claim and retract it (§15).
2. ⚠ **The bigger finding: the raid hero has NEVER had a mana pool or ability casting.** The emergency rig
   never attaches `HeroAbilities` / `HeroAbilityInput` at all, so Q/W/E/R have been dead in every raid
   ever played, and `HeroProgression` + the ability HUD bridge both key off `HeroAbilities` so they never
   migrated either. It looked fine because everything else — class, loadout, gear — IS re-derived from
   PlayerPrefs / `GameStateService`.
3. **Option A (carry).** `GoRaid` now carries the hero via a new optional `beforeLoad` hook on
   `LoadSceneWithFade`. Inline was wrong twice: an unregistered scene aborts the load AFTER the hero was
   detached + DDOL'd (orphan), and the fade + save flush left the player driving a detached hero around a
   live town for hundreds of ms. **The carry detaches from parent BEFORE DDOL** — not cosmetic:
   DDOL-ing the root once dragged `WaveManager`, `HeartController` and the Tree of Life into the
   destination (owner F8 2026-07-10).
4. ⚠ **The spec was incomplete and implementing it as written would have shipped a LEAK.** `FindLoco` uses
   `FindObjectsByType`, which **returns DDOL objects** — so a carried hero is "found", `Ensure()` takes
   the `hero != null` branch, and the re-home never runs. The hero would live in DDOL for the rest of the
   session, surviving every later Single load. Added a raid-scoped re-home that seats at the baked
   `HeroStartPoint_PlayerSpawn`, reusing the existing helper.

## Deliberately NOT done

- **The alarm and the fallback are both UNTOUCHED.** `SpawnEmergencyHero`'s `FlowTrace.Fail` is
  byte-for-byte unchanged and still reachable, and the new oracle FAILS if either is weakened. After this,
  that Fail firing in a raid MEANS something again.

## Owner decision left open

- ⚠ **FELT-VERIFY, a real difficulty change:** entering a raid at 30% HP now **keeps** 30% HP
  (`HeroHealth._hp` is runtime-only and was never carried). That is correct continuity and matches every
  other seam, but it removes today's free full heal. Her call.

## Oracle

`RaidHeroCarryRegression` → `RAID_HERO_CARRY_OK`. Pins the carry, the detach-before-DDOL order, the
raid-scoped re-home, and that the emergency Fail + fallback are not weakened.
