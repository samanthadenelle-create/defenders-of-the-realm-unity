# WORK ORDER 517 — Arena battle-exit music: restore the CONTEXT track, not hardcoded Overworld

**Status:** COMPLETE — OWNER ACCEPTED 2026-08-22 · **Silo:** Audio (code) · **Type:** EXISTING (bug) · **Priority:** Med (V2 arena loop)
**Source:** owner F8 in `MainCastle_Hall` — *"battle music playing in town after died in arena"* (2026-06-26 preview build).

## Completion evidence — 2026-08-22

Win/loss/flee retain the result-cue delay and then ask `WorldMusicDirector` to re-evaluate the
hero's actual zone. The bootstrap fallback uses the same position-aware Village/Overworld choice;
no hardcoded Overworld restore remains. `ArenaReturnMusicRegression.RunStandalone` emitted
`ARENA_RETURN_MUSIC_OK`.

## Root cause (PROVEN from the owner's Player.log — not inferred)
- `BattleArena.Resolve` schedules `RestoreAmbientAfter` which calls **`CoreServices.Audio.PlayMusic(MusicTrack.Overworld)`** — `Assets/_Modules/Village/Arena/BattleArena.cs:1157`. It is **hardcoded to Overworld** on every exit (win/loss/flee).
- Player.log line 70093 = that restore firing (Overworld); line 74301 = the F8 flag immediately after, while the hero was in **MainCastle_Hall** (the castle hub, which wants TOWN music).
- The return from the arena is **additive** (no scene reload), so the hub's scene-music path (`AudioService.HandleSceneMusic`) never re-fires, and `WorldMusicDirector` only re-evaluates on a world transition — so the hardcoded Overworld track persisted in town until the hero walked back to OuterWorld (log line 140875 shows `WorldMusicDirector.Apply` finally correcting it).

## Fix
In `BattleArena.RestoreAmbientAfter` (BattleArena.cs:1154-1158): do NOT hardcode `MusicTrack.Overworld`.
Restore the track for **where the hero actually returns**:
- Preferred: force `WorldMusicDirector.Apply(...)` to re-evaluate on return (it already computes the correct
  town-vs-overworld track — `Assets/_Modules/Village/World/WorldMusicDirector.cs:91-100`). Resolve it Core-clean
  (Village→Core ok) or via the existing reflection seam if needed.
- Fallback: pick `Town` vs `Overworld` from the return context (is the return position inside the hub ring /
  is `VillageHudController.InVillage`?) instead of blanket Overworld.

## Acceptance
- Die/win/flee in the arena and return to the castle hub → **town music** resumes (no Overworld/battle track
  lingering in town).
- Return to an OuterWorld encounter spot → Overworld music resumes (unchanged).
- The victory/defeat sting still gets its beat before the restore (don't cut the climax — keep `RewardCueSeconds`).

## Notes
Only reachable while the BattleArena loop runs (`ff.overworldencounter` ON). It is OFF for V1 (arena cut), so
this is a V2-arena polish item — but it's a clean, data-proven fix ready whenever the arena is live.
</content>
