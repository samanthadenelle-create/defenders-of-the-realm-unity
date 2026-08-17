<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 162 — Player Music Selection (jukebox: pick your ambient track)

**Status: READY TO IMPLEMENT**
**Priority:** Low-Medium — personalization / cozy-player feature; cheap (UI + pref over existing audio)
**Date:** 2026-05-30
**Lane:** Audio + HUD — code only. No `VillageSceneBuilder`, no bake by UI.
**Source:** owner — *"allow them to select custom music maybe from a selection."* Fits the cozy/personalization theme (home skins, pet room) — make the space *yours*, including its soundtrack.

---

## Reconcile — the audio system is BUILT; this is a selector on top

| Piece | State | Where |
|---|---|---|
| Music playback + crossfade + 2 sources | **BUILT** | `Assets/_Modules/Audio/AudioService.cs` — `PlayMusic(MusicTrack)`, `CurrentTrack` |
| Track enum | **BUILT** | `MusicTrack { None, Title, Village, Battle, Victory, Defeat, Dungeon }` |
| Clips loaded from Resources, wired at boot | **BUILT** | `AudioBootstrap.cs` (`TryAssignClip` per track) |
| Service seam | **BUILT** | `IAudioService.PlayMusic` (`CoreServices.Audio`) |
| Volume/settings persist | **EXISTS** | audio settings already persist (mixer + PlayerPrefs per AudioBootstrap) |

**So the work is:** a **selection UI** + a **persisted player preference** for which track plays in the
ambient/explore context, calling the existing `PlayMusic`. No new audio engine.

## What to build

1. **A selectable music set.** Curate the tracks the player may choose for ambient play — the existing
   `Village`/`Title`/`Dungeon` clips at minimum, plus room to add **dedicated jukebox tracks** (new
   clips dropped into Resources + `MusicTrack` entries, or a separate `jukeboxTracks[]` list so the
   selection isn't limited to the gameplay-state enum). Recommend a small `MusicChoice` data list
   (id, display name, clip) so the selection set is authorable/expandable without touching combat-state music.
2. **A selection UI** — a simple list/jukebox panel: track names, a play/preview, a checkmark on the
   chosen one. Code-built (no UXML — PIPELINE_STATE §8). Natural homes for it: **Settings**, and/or a
   **jukebox object in the Player Home** (WO-161 — the cozy space; flip your soundtrack where you nest).
3. **Persist the choice** — save the selected track id to `GameState`/PlayerPrefs (mirror how audio
   volume settings already persist); on load, the ambient context plays the chosen track via
   `CoreServices.Audio?.PlayMusic(...)` instead of the hard-coded default.
4. **Respect gameplay-state music.** Player choice governs the **ambient/explore/home** context only —
   **combat/victory/defeat still override** with their state tracks (you don't want your chill pick
   playing during a boss). When combat ends, return to the player's chosen ambient track. Make that
   precedence explicit so the selector never breaks the dramatic cues.

## Open questions for owner
- **"Custom" = curated selection, or player uploads?** Owner said "from a selection" — recommend a
  **curated in-game set** (licensing-safe, console-clean). True user-file uploads add platform/licensing
  complexity (mobile file access, rights) — flag as a separate, later consideration if wanted.
- **Where does it live?** Settings only, the Player Home jukebox (cozy tie-in), or both? (Recommend both
  — Settings for access, Home jukebox for flavor.)
- **Founder/cosmetic tracks?** Some tracks could be founder/premium or unlockable — ties to the
  cosmetic/founder stack (WO-161). Optional.

## Constraints (CLAUDE.md §5/§6/§9)
- Reuse `AudioService`/`IAudioService.PlayMusic` + `MusicTrack` + the AudioBootstrap clip-load — **do
  NOT build a new audio system.** Add a selection list + UI + a persisted pref only.
- Selection data (`MusicChoice` list) is fine in Audio/Core as pure data; UI code-built (no UXML).
- Persist via the existing settings/save path; `CoreServices.Audio?.` with `?.`. No new currency unless founder tracks are sold (then reuse the monetization stack).

## Acceptance criteria
1. Player can open a music-selection UI and **choose an ambient track** from a curated set; choice previews + shows as selected.
2. The chosen track plays in the **ambient/explore/home** context via the existing `PlayMusic`; **combat/victory/defeat state music still overrides** and returns to the chosen track after.
3. The choice **persists** across sessions (save/PlayerPrefs).
4. Selection set is **authorable/expandable** (add a clip + entry) without touching gameplay-state music.
5. Built entirely on the existing `AudioService` — no new audio engine; UI code-built; brace balance.

## Done checklist (CLAUDE.md §10)
- [ ] Curated selectable track set (authorable list); optional jukebox clips added
- [ ] Selection UI (code-built) in Settings and/or Home jukebox; preview + selected state
- [ ] Choice persists; ambient context plays it via PlayMusic
- [ ] Combat/state music precedence preserved (selector never breaks dramatic cues)
- [ ] No new audio system/currency; `?.` cross-module; brace balance
- [ ] `WORK_ORDER_162_player_music_selection.RESULT.md` when complete
