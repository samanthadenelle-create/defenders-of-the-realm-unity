# Defenders — Playtest Punch List (2026-05-27)

Living backlog from the owner's QA pass. Core loop is solid (hero moves, towers + scale,
NPCs, skill points, waves, breach -> Defend-the-Tower transition). Below = polish/bugs/
features by area, with diagnosis notes. Tackle one area per focused pass (investigate ->
batch-fix -> one rebuild).

## ✅ Fixed + pushed today
- Hero deletion on village load (HeroProgression/AttackTimingBonus singleton dedup `Destroy(gameObject)` -> `Destroy(this)` + takeover)
- Towers: BlastTower model + ~10x scale normalize; tower model swap
- NPCs: People pack (LFS, source-shrunk) + 2x size; controllers + prefabs
- Skill points working; bird's-eye Defend-the-Tower camera offset
- DungeonPortal NRE flood (frozen village); duplicate (F)-ring entrances removed
- Wave dev hotkey (N = ForceBeginNextWave); level-up popup tracks the hero
- **Breach -> Defend-the-Tower crash** (WaveManager:722 collection-modified)
- Village speech bubbles un-scaled after 2x NPC (#15) + death-logger quieted (74b7473)

## ✅ Verified this pass (smoke run, -bootScene Village, 38s)
- Project loads + plays clean: 0 exceptions/NREs over 1331 log lines
- Hero controllable, HUD bound, 4 People-pack NPCs placed, camera tracks hero
- WaveManager loop armed (wave 1); WO-48 TowerData loads from Resources (16x)
- Noise to clean up later (NOT errors): 16 Soft-Occlusion tree-shader warnings
  (art import), 33 [CAM DIAG] per-frame Debug.Log spam in VillageCamera.LateUpdate

## 🏘️ Village polish
- #14 Talent tree: clicking a node doesn't refresh buttons / new skill doesn't appear (wiring `Grant->Changed->Repaint` LOOKS intact -> likely unmet-prereq node silently fails, or skill-points-vs-wisdom mix-up — needs a live look)
- #13 Tower build: no particle VFX + an "invisible bar" slides across the screen
- #8 Sound/music toggle flips red<->green but no audio (likely no music actually playing)
- #10b Pets DO level (PetProgression, max 20, skill tree) but have NO level-up popup
- #12 Ranger stays in idle pose while moving (walk clip not firing on the swapped body)
- #3 SW main gate won't open / walk through (likely not a `Gate` component or no blocker)
- #4 map-edge: tightened to 50m stopgap (DONE); real fix = finish/texture the map

## 🔨 Handoff session (2026-05-27 PM) — pushed
- **ATB #6/#7 (partial):** Skills now casts the hero's best usable ability, Item
  uses a (now-seeded) Potion, Flee actually leaves the fight. Attack already
  worked. Targeting still auto-picks lowest-HP foe (no manual picker yet). (32928d8)
- **Dungeon endless loop:** DungeonStubEncounter re-fired on every scene return
  (hero respawns on the pad). Added arm-on-exit so it can't re-trigger until the
  hero leaves the radius. (32928d8)
- **#13 tower build feedback:** bigger/emissive progress bar + code dust VFX (this build)
- **Build unblocked:** removed a duplicate `Village/Scripts/DoorController.cs`
  (Animator-based) that collided with the canonical `Buildings/DoorController.cs`
  (rotation-based) → moved to `Backups/`. Compile was fully broken until this.
- **#8 sound — root-caused + fixed (needs owner's ears):** added an [AudioService]
  diagnostic → `music 'Village' clip='village' muted=False target=0.40 mixer=True
  group=False`. Music PLAYS, but the sources aren't routed through the mixer's Music
  group, so the ♪ toggle/volume (which drive the mixer) couldn't affect playback —
  the owner's "toggle does nothing sound-wise". Fixed SetVolume/SetMuted to also
  drive the sources directly. Confirm by ear. Long-term: fix the mixer-group routing
  (FindMatchingGroups("Music") returns nothing).
- **#14 talent tree — diagnosed:** TalentTreePanel grant→refresh wiring is intact;
  it spends WISDOM, not level-up SKILL POINTS. The owner's "added a skill point,
  nothing happens" is a currency mix-up between the two systems — needs owner.

## 🗼 Defend the Tower (PatriciaLight)
- Camera clips low / inside the geometry (CamOffset 0,12,-7 not landing right in-scene)
- Oversized world labels (giant "20", pet name-tags)

## 🏰 Dungeons
- #16 Folk's Granary NPC is a placeholder white pill (needs a real/better mesh)
- #18 Hero RESETS to dungeon start when a random spawn completes (movement/state bug)

## ⚔️ ATB (2D battle)
- #6/#7 No targeting; skills + items inert (attack works); kills loop forever (no win/exit)
- #17 Needs simple animation — slide-in + sword-swing / cast-spell

## 💾 Persistence (owner: "issue in and out" — likely TWO issues)
- Data not saved going INTO a scene, and/or not restored coming OUT
- Related to #18; clarify WHICH data (hero level/XP, towers, pets, currencies, talent/skill unlocks, cross-restart save) and which direction

## 🎬 Onboarding / UI
- Hero-select cards render "just images" (no card frames/text) — possibly USS not applying in build
- #11b XP progress bar (none exists; level-up shows a raw threshold number)
