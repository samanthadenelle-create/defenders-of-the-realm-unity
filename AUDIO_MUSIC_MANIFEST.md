# Battle Music Audio Manifest

**Generated:** 2026-06-08  
**Source:** Suno AI Music Generation  
**Status:** Ready for Integration

---

## Audio Tracks (4 files)

### 1. Overworld Battle 1 (`Overworld battle 1.mp3`)
- **Purpose:** General overworld combat
- **Mood:** Heroic, grounded, medium intensity
- **Duration:** ~2 minutes
- **BPM:** 130–140 (driving, relentless)
- **Key:** D minor (tension)
- **Loop:** Yes (combat loops until victory or escalation)
- **Integration:** WaveManager → `BattleMusicState.Combat`

### 2. Overworld Battle 2 (`Overworld battle 2.mp3`)
- **Purpose:** High-intensity combat escalation
- **Mood:** Desperate, adrenaline-fueled, urgent
- **Duration:** ~2 minutes
- **BPM:** 145–160 (fast and relentless)
- **Key:** E minor (darker, more urgent)
- **Loop:** Yes (escalated battle loops)
- **Integration:** WaveManager → `BattleMusicState.Intense` (triggers when enemy count ≥ 5)

### 3. Overworld Victory (`Overworld Victory.mp3`)
- **Purpose:** Post-wave victory celebration
- **Mood:** Triumphant, earned, weary but proud
- **Duration:** ~1–1.5 minutes
- **BPM:** 100–110 (slower, stately)
- **Key:** D major (bright, victorious)
- **Loop:** No (one-shot, plays once and ends)
- **Integration:** WaveManager → `BattleMusicState.Victory` (on wave clear)

### 4. Overworld Boss Fight (`Overworld Boss Fight.mp3`)
- **Purpose:** Boss/arena battle (epic endgame encounter)
- **Mood:** Mythic, grand, grounded, confrontational
- **Duration:** ~3–4 minutes
- **BPM:** 135–150 (powerful, inexorable)
- **Key:** E minor or C minor (dark, powerful)
- **Loop:** Yes (boss battle loops until victory/defeat)
- **Integration:** WaveManager → `BattleMusicState.BossBattle` (on boss wave detection)

---

## Import Instructions

### File Organization

Copy all 4 MP3 files to this location:
```
Assets/Audio/Music/Battle/
├── Overworld_Battle_1.mp3
├── Overworld_Battle_2.mp3
├── Overworld_Victory.mp3
└── Overworld_Boss_Fight.mp3
```

**Note:** Rename files to use underscores (no spaces, for scripting consistency).

### Unity Import Settings

**For all files:**

| Setting | Value | Notes |
|---|---|---|
| Load Type | Compressed in Memory | Fast playback, acceptable size |
| Compression Format | Vorbis | Best quality/size balance |
| Quality | 70–80 | ~128 kbps bitrate equivalent |
| Mono/Stereo | Stereo | Preserve spatial depth |
| Sample Rate | 44100 Hz | Standard for game audio |

**Loop configuration:**

| File | Loop | Notes |
|---|---|---|
| Overworld_Battle_1.mp3 | ✓ Enabled | Loops during active combat |
| Overworld_Battle_2.mp3 | ✓ Enabled | Loops during escalated combat |
| Overworld_Victory.mp3 | ✗ Disabled | Plays once, one-shot |
| Overworld_Boss_Fight.mp3 | ✓ Enabled | Loops during boss encounter |

---

## State Machine Flow

### Combat Progression

```
Wave Starts
    ↓
Overworld_Battle_1 (Combat)
    ↓
[Enemy count ≥ 5 OR time > 30s]
    ↓
Crossfade → Overworld_Battle_2 (Intense)
    ↓
[All enemies defeated]
    ↓
Crossfade → Overworld_Victory (one-shot)
    ↓
Victory ends → Silence/Exploration (if exists)
```

### Boss Battle Flow

```
Boss Wave Detected
    ↓
Crossfade → Overworld_Boss_Fight (BossBattle)
    ↓
[Boss defeated]
    ↓
Crossfade → Overworld_Victory
    ↓
Victory ends
```

---

## Volume Levels

**Master Mix:**
- Music: 0.7 (primary layer)
- SFX: 0.8 (from WO-371, combat feedback)
- Ambience: 0.3 (background, if added later)

**Individual tracks:**
- Battle 1: 0.7 (default level)
- Battle 2: 0.75 (slightly louder for intensity)
- Victory: 0.8 (celebratory peak)
- Boss Fight: 0.8 (epic scale)

All tracks should be normalized to roughly the same loudness before import.

---

## Crossfade Settings

**Transition duration:** 1.5 seconds (smooth but responsive)

| Transition | Duration | Fade Curve | Notes |
|---|---|---|---|
| Combat → Intense | 1.5s | Linear | Player feels escalation |
| Any → Victory | 2.0s | Linear | Celebratory, slightly slower |
| Combat → Boss | 3.0s | Linear | Mythic build-up, slower start |
| Music → Silence | 1.5s | Linear | Clean stop on scene change |

---

## Integration Hooks

**WaveManager calls:**

```csharp
// On wave start
_musicManager.TransitionTo(IsBossWave ? BattleMusicState.BossBattle : BattleMusicState.Combat);

// On high enemy count (≥5 enemies)
_musicManager.TransitionTo(BattleMusicState.Intense);

// On wave victory
_musicManager.TransitionTo(BattleMusicState.Victory);

// On scene transition
_musicManager.Stop();
```

---

## Audio Quality Verification

**Before shipping, check:**

- [ ] All 4 files import without errors
- [ ] Loop points are seamless (no clicks/pops at loop boundary)
- [ ] Crossfades don't create gaps or overlaps
- [ ] Victory track plays once and stops (not looping)
- [ ] Boss theme is distinct from general battle
- [ ] No audio dropouts or stuttering on WebGL
- [ ] Music volume doesn't drown SFX (test with WO-371)
- [ ] Transitions trigger at correct game events

---

## Testing Checklist

### In-Game Testing

- [ ] Start game → No music initially
- [ ] Wave 1 starts → Overworld_Battle_1 begins
- [ ] Observe enemy count → At 5+ enemies, transitions to Overworld_Battle_2
- [ ] Clear wave → Victory music plays (one-shot, celebratory)
- [ ] Wave 2 starts → Combat resumes (Battle_1 or Battle_2 depending on intensity)
- [ ] Trigger boss wave → Boss theme plays (distinct, epic)
- [ ] Boss defeated → Victory music plays
- [ ] Scene transition → Music stops cleanly

### Audio Quality

- [ ] No pops/clicks at transitions
- [ ] Loop points are seamless
- [ ] Stereo imaging is clear (if applicable)
- [ ] No audio sync issues with game events
- [ ] Volume levels are consistent

---

## File Metadata

| Filename | Duration | Size | Bitrate | Sample Rate |
|---|---|---|---|---|
| Overworld_Battle_1.mp3 | ~2:00 | ~2.5 MB | 128 kbps | 44100 Hz |
| Overworld_Battle_2.mp3 | ~2:00 | ~2.5 MB | 128 kbps | 44100 Hz |
| Overworld_Victory.mp3 | ~1:30 | ~2.0 MB | 128 kbps | 44100 Hz |
| Overworld_Boss_Fight.mp3 | ~3:30 | ~4.5 MB | 128 kbps | 44100 Hz |
| **Total** | **~9:00** | **~11.5 MB** | — | — |

---

## Notes

- All tracks generated by Suno AI using prompts from `SUNO_BATTLE_MUSIC_PROMPTS.md`
- Music is medieval fantasy style, low-poly game aesthetic (Kingdom Come: Deliverance vibes)
- No synths or modern instruments (authentic medieval + fantasy feel)
- Designed to loop seamlessly and transition smoothly between intensity levels
- Victory theme is one-shot (plays once per wave clear, no loop)

---

## Next Steps

1. Copy all 4 MP3 files to `Assets/Audio/Music/Battle/`
2. Configure import settings per this manifest
3. Create BattleMusicManager script (see WO-372)
4. Wire to WaveManager events
5. Test in-game for correct transitions and timing
6. Adjust crossfade duration or volume if needed
7. Verify no audio glitches in WebGL build
8. Done! ✅

---

## Contact / Feedback

If any track needs regeneration or tweaking:
1. Return to SUNO_BATTLE_MUSIC_PROMPTS.md
2. Adjust prompt parameters (BPM, mood, instrumentation)
3. Regenerate on Suno
4. Replace in project

All tracks were generated with care to match the game's aesthetic and gameplay flow. Ready for production!
