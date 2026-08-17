> ⚠ **NUMBER COLLISION — this document does not own WO-301; `WORK_ORDER_301_party_persistence_wallet_keyed.md` does.**
> Referred to hereafter as **WO-301-B (party persistence, pre-wallet draft)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 301 — Party persistence (the roster backbone)

**Status: READY — BACKBONE (build early; companion presence + party UI both depend on it).**
**Lane:** 7 (Persistence) + 2 (Combat) + 4 (UI). **Origin:** owner playtest 2026-06-07 — "when I load
into town the companion doesn't register as in the party."

## Problem
There is **no persisted party roster**. So on a scene load / spawn, nothing tells the game who is in the
player's party or how big it is. Consequences the owner is seeing:
- The **companion doesn't know it's in the party** → it isn't spawned/positioned with the hero (compounds
  the open-world "companion not nearby" seam bug).
- The **UI party frames have no data** to populate.
- Party state resets on restart (in-memory only).

## Fix — a persistent party roster in GameState
1. **GameState field (saved):** a party roster — e.g. `List<string> PartyMemberIds` (companion/hero ids,
   in join order) + a derived `PartySize`. Wallet-keyed per the master-doc spec; add to SaveSchema
   (versioned, additive) so it round-trips across loads/sessions. (Source the canon from the companion
   roster — Sylas/Thrain/Grom/Elara, join order Sylas→Elara→Grom — see companion-roster-canon.)
2. **Spawn-from-roster:** on ANY scene spawn (village, open world, DTT), the companion deploy reads the
   roster → for each party member, spawn + position relative to the hero (shoulder/formation slot). This is
   what makes the companion "know where it should be" wherever the player spawns — and it pairs with the
   companion-follow fix (lerp across the village→OuterWorld seam, like the pet).
3. **UI from roster:** the HUD/menu party frames render one frame per roster member (reuse
   `VillageHudController.SetPartyMember` / `PartyHudBridge`). Party size drives how many frames show.
4. **Mutators:** AddToParty(id) / RemoveFromParty(id) (fires PlayerChanged/a PartyChanged event) so joining
   a companion (quest/beat) persists and immediately reflects in spawn + UI.
5. **Join trigger (owner 2026-06-07):** on **TUTORIAL COMPLETE** (the `CompanionMeeting` → `TutorialComplete`
   node / the tutorial-done hook), call `AddToParty(firstCompanion)` → **party count increases** and
   `companion.IsActiveParty()` becomes true. That's the canonical join moment (Sylas at beat-1). From then on,
   every spawn reads the roster → the companion appears with the hero, and a party UI frame shows.

## Why it's the backbone
- **Companion-nearby (open world)** needs to know the companion is a party member to spawn/follow.
- **Party UI frames** need the roster to populate.
- **Spawn-anywhere** ("where should they be when we spawn somewhere") = read roster → place relative to hero.
Build WO-301 FIRST (or alongside), then the companion-follow + party UI read from it.

## Notes
- Additive SaveSchema field (one-at-a-time GameState edit per §9). Migrate the pet PlayerPrefs blob if relevant.
- Reuses: `StoryCompanionInjector`/`CompanionSpawner` (spawn), `PartyHudBridge`/`VillageHudController` (UI),
  companion-roster-canon (the members + join order). Local WO.
