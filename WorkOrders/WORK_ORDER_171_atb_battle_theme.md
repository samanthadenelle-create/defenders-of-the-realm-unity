<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 171 — Music: ATB Battle Themes (3) + Overworld Themes (2)

**Status: READY TO IMPLEMENT — quick win**
**Priority:** Low-effort / High-feel — real battle music for ATB fights.
**Date:** 2026-05-31
**Lane:** Audio — drop-in asset swap + a tiny rotation hook. No VillageSceneBuilder; no bake.
**Source:** owner provided **THREE** battle tracks — *"add as the songs played in background for regular
ATB fights."* Track 1 is the first/default; 2 and 3 join the rotation.

---

## What
The owner provided **3 battle tracks** to play as background music for regular ATB battles
(`MusicTrack.Battle`). The audio system already loads `MusicTrack.Battle` from
`Assets/Audio/Resources/battle.mp3` via `AudioBootstrap.TryAssignClip(service, MusicTrack.Battle, "battle")`.
**Track 1 is the drop-in default** (zero code). Tracks 2 + 3 make a **rotating/random battle pool** (a
small additive hook).

## The assets (staged by UI)
- **`Assets/Audio/Resources/battle_theme_NEW.mp3`** (track 1, 3.53 MB) — the default/first.
- **`Assets/Audio/Resources/battle_theme2_NEW.mp3`** (track 2, 2.88 MB).
- **`Assets/Audio/Resources/battle_theme3_NEW.mp3`** (track 3, 4.43 MB).
- Current placeholder to replace: `Assets/Audio/Resources/battle.mp3`.

## CLI does the swap (clean)
1. **Track 1 → the default:** rename `battle_theme_NEW.mp3` → `battle.mp3` (overwrite) so the existing
   `Resources.Load("battle")` path serves it with **no code edit**. (Recommended over repointing.)
2. **Tracks 2 + 3 → the rotation pool:** import as `battle2.mp3` / `battle3.mp3` (clean names). Then a
   **small additive hook**: when an ATB battle starts, `PlayMusic(Battle)` picks **randomly (or rotates)**
   among `battle` / `battle2` / `battle3` instead of always the one clip. Simplest: AudioService holds a
   **list of battle clips** and `PlayMusic(Battle)` selects one (random, or sequential each battle). Keep
   it tiny — a clip array + a pick, not a new system. (Pairs with the WO-162 jukebox, which can later let
   players *choose* among these.)
3. **`.meta` for all three** in-editor: AudioClip, **streaming/compressed** load type (these are 3–4 MB
   music files), **looping** on the music source so each sustains a long battle.
4. Verify in-editor: enter ATB battles → a battle theme plays, loops, and **varies across battles** (the
   3-track pool); routes through the **Music** mixer group (volume-controllable — note WO-163 mixer-param
   fix may be needed for the slider to work).
5. Delete the `_NEW` staging files after renaming (no orphans).

## Notes
- These are the **first 3** battle tracks; the music-selection jukebox (WO-162) can later let players pick
  among them. For now: a rotating/random pool so battles aren't monotonous.
- Plays in the **battle** context; combat-state precedence already handled by AudioService.
- Mostly asset swap + a tiny clip-pool hook — engine untouched. Commit all 3 clips + their `.meta`.

## ⊕ ALSO: Overworld theme music (owner 2026-05-31) — needs a NEW MusicTrack
Owner provided **2 overworld theme tracks** (`world.mp3`, `mainworld1.mp3`) for the **open world**
(OuterWorld — exploration, distinct from the Village). **The `MusicTrack` enum has NO overworld entry**
(it's `{ Village=0, Battle=1, Victory=2, Dungeon=3 }`), so:
1. **Add a `MusicTrack.Overworld`** (append to the enum — stable order, don't renumber existing).
2. **Import the 2 tracks** as `world.mp3` / `mainworld1.mp3` (compressed/streaming, looping).
3. **Play `Overworld` when the player is in the OuterWorld** (the WorldSceneLoader's OuterWorld scene /
   when `ZoneManager` reports a non-Village region) — same rotating-pool approach as battle (2 tracks →
   pick/rotate per visit), and **fall back to / hand off from Village music** when entering/leaving the world.
   - AudioBootstrap: `TryAssignClip(service, MusicTrack.Overworld, "world")` (+ the 2nd in the pool).
   - Staged: `Assets/Audio/Resources/world_theme_NEW.mp3`, `mainworld1_NEW.mp3` → rename to `world.mp3` /
     `mainworld1.mp3` (or `overworld1/2`), set `.meta`, add to the Overworld clip pool.
4. **Context transitions:** Village→Overworld (cross the gate / enter the world) swaps Village music →
   Overworld music; entering a battle still overrides with Battle; back to Overworld after. Combat-state
   precedence already handled — just add the Overworld ambient context.

## Acceptance criteria
0b. **Overworld theme:** `MusicTrack.Overworld` added; the 2 overworld tracks play (rotating) when the player is in the open world, hand off cleanly from/to Village music, and Battle still overrides during fights.
1. Regular ATB battles play one of the **3 owner battle themes** as background music (loops, sustained).
2. Track 1 serves via the existing `MusicTrack.Battle` / `"battle"` path; tracks 2+3 added to a **rotating/random battle pool** so the music varies across battles.
3. Routes through the Music mixer group; volume-controllable (note WO-163 mixer-param dependency).
4. All 3 `.meta` correct (AudioClip, compressed/streaming, loop); no orphan `_NEW` files; clips + metas committed.

## Done checklist
- [ ] Track 1 → `battle.mp3` (default); tracks 2+3 → `battle2/3.mp3`
- [ ] AudioService picks from a battle-clip pool on `PlayMusic(Battle)` (random/rotate) — varies per battle
- [ ] All 3 `.meta` set (compressed/streaming, loop); play + loop + vary in ATB battles; route to Music mixer
- [ ] `_NEW` staging files cleaned; clips + metas committed
- [ ] `WORK_ORDER_171_atb_battle_theme.RESULT.md` when complete
