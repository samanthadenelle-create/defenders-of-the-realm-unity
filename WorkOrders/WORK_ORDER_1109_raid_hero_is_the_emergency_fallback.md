# WORK ORDER 1109 — Every raid spawns the EMERGENCY pill-hero, because `RaidHeroSpawner` does not exist

**Status:** DONE 2026-08-16 (`256fa9ee3`) — RESULT filed; pending PO felt-verify (the raid-entry HP-carry difficulty change is hers)
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1109 -> 1112 in the SAME edit (1110, 1111 minted alongside)
**Lane:** Hero lifecycle / raid entry. ⚠ `HeroControlEnsurer.cs` + `SceneRouter.cs`.
**Provenance:** SME readiness audit of the raid pillar, 2026-08-16, run against the owner's question
*"can i finally test raids fully?"*. Found by tracing the hero spawn path, not by reading comments —
**the comment is the bug.**

---

## 1. The finding

`HeroControlEnsurer.cs:36-41` states, as fact:

> *"RaidHeroSpawner builds the REAL class body one frame after load."*

**`RaidHeroSpawner` does not exist.** Zero files, zero references anywhere in the repo outside that
comment. This is canon's *"comments lie"* warning (`CLAUDE.md` MANDATORY FIRST STEP) in its purest
form — a seat reading that header would conclude the raid hero path is designed and wired, and be
wrong.

**What actually happens on every raid entry:**

1. `SceneRouter.GoRaid` (`SceneRouter.cs:456`) does **no `DontDestroyOnLoad` marking** of the hero.
2. So `HeroControlEnsurer.TryRecoverCarriedHero` (`:125-151`), which is keyed on the DDOL scene,
   finds nothing.
3. So `Ensure()` falls through to `SpawnEmergencyHero()` (`:540`) — whose FIRST LINE is
   **`FlowTrace.Fail("Hero", "EMERGENCY pill spawned...")`**.
4. `HeroBodySwapper` then swaps a real class FBX onto the `HeroBody` child. **If that resolve fails,
   the player controls a lavender capsule.**

## 2. Why this matters more than "it works anyway"

- **It is playable, so it hides.** The body swap usually succeeds, so the raid looks fine and nobody
  investigates. That is exactly why it survived to now.
- **It poisons the F8 signal.** A `FlowTrace.Fail` lands in the break-log on EVERY raid entry. Per
  `CLAUDE.md` §14 the owner's F8 captures are triaged live — a permanent, expected Fail line trains
  every seat (and the owner) to ignore a `Fail` from the Hero system. **A trace that always fires is
  worse than no trace**, and canon already records that lesson.
- **The raid hero carries no continuity.** Because the town hero is never carried across, the raid
  hero is reconstructed rather than transported. Any state the real hero holds that is not re-derived
  by the emergency path is silently absent in raids. **Scope this before fixing** — it is the part of
  this ticket with unknown depth.

## 3. Fix shape (two options — pick one, do not do both)

**Option A — carry the hero (preferred if continuity matters).** Mark the hero `DontDestroyOnLoad` in
`SceneRouter.GoRaid` so `TryRecoverCarriedHero` succeeds, and seat it at the raid's
`HeroStartPoint_PlayerSpawn` (⚠ **verified present** — baked into the raid scenes, e.g.
`RaidBase_raider_camp_small.unity:10615`). This makes the raid hero literally the town hero.

**Option B — build the spawner the comment promises.** Write the missing `RaidHeroSpawner` that
constructs the real class body on raid load, and have `Ensure()` defer to it.

**Either way:** the emergency path must go back to being what its name says — an emergency. After the
fix, `SpawnEmergencyHero` firing in a raid is a REAL defect and its `FlowTrace.Fail` must mean
something again.

⚠ **Do not "fix" this by downgrading the Fail to a Warn or deleting it.** That would silence the
alarm instead of the fault, and `CLAUDE.md` §12 forbids stripping instrumentation. The Fail is
correct; what is wrong is that the fallback is the normal path.

## 4. Acceptance

- Entering a raid produces **no** `EMERGENCY pill spawned` line in the break-log — proven by a capture,
  not by inspection.
- The hero in the raid is the player's real class body at the baked `HeroStartPoint_PlayerSpawn`.
- Killing the intended path (temporarily) still lands the player in a playable emergency hero — the
  fallback must survive, it just must not be the default.
- The stale `HeroControlEnsurer.cs:36-41` comment is corrected in the same commit (§15: canon updates
  ride the change) — either naming the real spawner or describing the carry.

## 5. ⚠ Related, deliberately NOT in this ticket

The raid path has **no scene-level test coverage at all**: no harness ever loads a `RaidBase_*` scene.
AutoPilot screenshots the two pre-raid panels and stops. Everything from BEGIN ASSAULT onward — hero
spawn, deploy tray, troop pathing, spire damage, victory screen, return — is code-verified only and
has never been exercised in editor or headless. **This ticket would have been caught by one headless
raid load.** See WO-1111.
