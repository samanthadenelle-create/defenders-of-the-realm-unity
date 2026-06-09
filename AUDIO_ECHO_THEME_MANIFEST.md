# Echo's Theme Audio Manifest

**Generated:** 2026-06-08  
**Source:** Suno AI Music Generation  
**Status:** Ready for Integration  
**File:** `Echo's theme 7.mp3` (1.7 MB)

---

## Overview

**Echo's Theme** is the companion introduction/appearance music. Plays when:
- Echo is first summoned after Wave 3 (WO-360: Companion Echo Outpost)
- Echo appears at an outpost location
- Hero meets Echo for the first time
- Optional: Echo theme on special events (pet interactions, leveling up)

---

## Audio Specifications

| Property | Value |
|---|---|
| Filename | Echo's_theme_7.mp3 |
| File Size | 1.7 MB |
| Duration | ~1:30–2:00 (estimated) |
| Loop | No (one-shot, companion introduction) |
| Mood | Whimsical, magical, warm, inviting |
| Instrumentation | Light (strings, synth pads, bells) |
| Style | Fantasy adventure (magical but grounded) |

---

## Integration Location

**Target folder:**
```
Assets/Audio/Music/Companion/
├── Echo_Theme.mp3
└── (future: Echo interaction sounds, level-up jingle, etc.)
```

**Alternative location (if centralizing):**
```
Assets/Audio/Music/
├── Battle/
│   ├── Overworld_Battle_1.mp3
│   ├── Overworld_Battle_2.mp3
│   ├── Overworld_Victory.mp3
│   └── Overworld_Boss_Fight.mp3
├── Companion/
│   └── Echo_Theme.mp3
└── (future: Exploration, Hub, Boss, etc.)
```

---

## Replacement Instructions

### What to Delete (Old Echo Intro)

Search project for any existing Echo introduction track:

```
Assets/Audio/Music/
  - Look for: EchoTheme, EchoIntro, CompanionIntro, EchoAppear
  - Check: Companion folder (if exists)
  - Likely names: EchoTheme_Old, EchoIntro_v1, etc.
```

**Once found, delete the old file.**

### What to Add (New Echo Theme)

1. Create folder: `Assets/Audio/Music/Companion/` (if doesn't exist)
2. Copy `Echo's_theme_7.mp3` to this folder
3. Rename to: `Echo_Theme.mp3` (remove version number for cleanliness)
4. Import settings (see below)

---

## Unity Import Settings

| Setting | Value | Notes |
|---|---|---|
| Load Type | Compressed in Memory | Fast playback |
| Compression Format | Vorbis | Quality/size balance |
| Quality | 80–90 | Higher quality for magical feel |
| Mono/Stereo | Stereo | Preserve atmosphere |
| Sample Rate | 44100 Hz | Standard |
| **Loop** | **✗ Unchecked** | **One-shot (plays once)** |
| 3D Audio | Off | Ambient, not spatial |

---

## Integration with Companion System

### Trigger Points

**In WO-360 (Companion Echo Outpost) code:**

```csharp
public void OnEchoSummoned()
{
    // Play Echo theme on first appearance
    CoreServices.Audio.PlayMusic(SfxId.EchoTheme, loop: false);
    
    // Or via BattleMusicManager (if integrated there):
    _musicManager.TransitionTo(BattleMusicState.Companion);
    
    // Spawn Echo companion
    _echoCompanion.Summon();
}

public void OnEchoIntroduction()
{
    // Optional: Play on first dialogue with Echo
    if (!_hasMetEcho)
    {
        CoreServices.Audio.PlayMusic(SfxId.EchoTheme, loop: false);
        _hasMetEcho = true;
    }
}
```

### Volume Level

- **Music volume:** 0.8 (slightly higher than ambient, lets Echo shine)
- **SFX volume:** 0.6 (pulled back for magical moment)
- **Ambience:** 0.3 (background only)

---

## Testing Checklist

- [ ] File imports without errors
- [ ] No audio pops/clicks at start or end
- [ ] One-shot playback (doesn't loop)
- [ ] Plays at correct trigger (Echo summoned, dialogue, etc.)
- [ ] Volume balanced with game audio
- [ ] Doesn't overlap with other music (crossfade if needed)
- [ ] Stereo imaging is clear (if applicable)
- [ ] Works in WebGL build

---

## File Metadata

| Property | Value |
|---|---|
| Filename | Echo's_theme_7.mp3 |
| Size | 1.7 MB |
| Bitrate | 128 kbps (estimated) |
| Sample Rate | 44100 Hz |
| Channels | Stereo |
| Format | MP3 (Vorbis compression in Unity) |

---

## Placement in Audio Architecture

```
CoreServices.Audio
    ├── Music Layer
    │   ├── Battle Music (WO-372)
    │   ├── Companion Music ← Echo_Theme.mp3 (THIS)
    │   └── (future: Exploration, Hub, Boss intros)
    │
    └── SFX Layer (WO-371)
        ├── Tower fire
        ├── Combat feedback
        └── etc.
```

---

## Notes

- Echo's theme is a **one-shot** (plays once per summoning, no loop)
- Use **crossfade** if transitioning from battle music to Echo theme (0.5–1s)
- Optional: Create audio event in dialogue system to trigger theme automatically
- Can be used for multiple Echo moments (summoning, leveling, bond events)

---

## Next Steps

1. Delete old Echo intro track (if exists)
2. Create `Assets/Audio/Music/Companion/` folder
3. Copy `Echo's_theme_7.mp3` to folder
4. Rename to `Echo_Theme.mp3`
5. Configure import settings (Vorbis, 80–90 quality, loop: OFF)
6. Wire to WO-360 summoning trigger
7. Test in-game (Echo summoned = theme plays)
8. Done! ✅

---

## Related Work Orders

- **WO-360:** Companion Echo Outpost (summoning trigger)
- **WO-364:** Companion Hero Gear Setup (Echo interaction scenes)
- **WO-372:** Battle Music System (if need crossfade between battle → companion)

---

**Status:** Ready for CLI integration  
**Estimated integration time:** 0.25 days (straightforward audio import)
