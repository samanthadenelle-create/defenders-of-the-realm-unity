# WO-372: Battle Music System — State Machine & Track Integration

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P0 (0.5–1 day — audio import + state machine wiring)  
**Priority:** HIGH (core audio experience, completes battle feel)  
**Lane:** 9 VFX/Audio

---

## Overview

Integrate **4 Suno-generated battle music tracks** into a dynamic state machine that responds to gameplay events.

**Tracks provided:**
1. `Overworld battle 1.mp3` — General combat (medium intensity)
2. `Overworld battle 2.mp3` — Intense/alternate battle (high pressure)
3. `Overworld Victory.mp3` — Post-wave celebration
4. `Overworld Boss Fight.mp3` — Boss/arena battle

**Goal:** Music transitions smoothly based on game state (exploration → alert → battle → intense → victory).

---

## Acceptance Criteria

- [ ] All 4 audio files imported to `Assets/Audio/Music/Battle/`
- [ ] Audio clips configured for looping (battle) and one-shot (victory)
- [ ] BattleMusicManager script created and wired to WaveManager
- [ ] State machine transitions smoothly (crossfade, no pops/clicks)
- [ ] Correct music plays at correct time (combat trigger, intensity escalation, victory)
- [ ] Volume mixing balanced (music + SFX don't conflict)
- [ ] Music loops seamlessly (no gap at loop point)
- [ ] Boss theme triggers on boss waves
- [ ] Victory theme plays on wave clear
- [ ] Music pauses/stops on scene transition
- [ ] Can toggle music volume separately from SFX

---

## Audio Import Setup

### File Organization

```
Assets/Audio/Music/
├── Battle/
│   ├── Overworld_Battle_1.mp3
│   ├── Overworld_Battle_2.mp3
│   ├── Overworld_Victory.mp3
│   └── Overworld_Boss_Fight.mp3
└── (future: exploration, hub, etc.)
```

### Audio Clip Settings

**For battle tracks (looping):**
- Load Type: Compressed in Memory
- Compression Format: Vorbis
- Quality: 70–80 (balance quality/size)
- **Loop:** ✓ Checked
- 3D Audio: Off (music is global, not spatial)

**For victory track (one-shot):**
- Load Type: Compressed in Memory
- Compression Format: Vorbis
- Quality: 70–80
- **Loop:** ✗ Unchecked (plays once)
- 3D Audio: Off

---

## BattleMusicManager Script

### Enum: Music States

```csharp
public enum BattleMusicState
{
    None,           // No music
    Exploration,    // Village idle (overworld theme, if exists)
    Alert,          // Enemies nearby, heightened tension
    Combat,         // Active battle (Overworld_Battle_1)
    Intense,        // High pressure (Overworld_Battle_2)
    Victory,        // Wave cleared (Overworld_Victory)
    BossBattle,     // Boss encounter (Overworld_Boss_Fight)
}
```

### Core Class

```csharp
using DeNelle.Core;
using UnityEngine;
using System.Collections;

public sealed class BattleMusicManager : MonoBehaviour
{
    [SerializeField] private AudioSource _musicSource;
    [SerializeField] private float _crossfadeDuration = 1.5f;
    
    // Audio clips
    [SerializeField] private AudioClip _combatTheme;      // Overworld_Battle_1
    [SerializeField] private AudioClip _intenseTheme;     // Overworld_Battle_2
    [SerializeField] private AudioClip _victoryTheme;     // Overworld_Victory
    [SerializeField] private AudioClip _bossTheme;        // Overworld_Boss_Fight
    
    private BattleMusicState _currentState = BattleMusicState.None;
    private Coroutine _crossfadeCoroutine;

    public void TransitionTo(BattleMusicState newState)
    {
        if (_currentState == newState) return;  // Already in this state

        if (_crossfadeCoroutine != null)
            StopCoroutine(_crossfadeCoroutine);

        AudioClip targetClip = GetClipForState(newState);
        bool shouldLoop = (newState != BattleMusicState.Victory);

        _crossfadeCoroutine = StartCoroutine(Crossfade(targetClip, shouldLoop));
        _currentState = newState;

        Debug.Log($"[BattleMusic] Transitioning to {newState}");
    }

    private IEnumerator Crossfade(AudioClip targetClip, bool loop)
    {
        // Fade out current music
        float elapsed = 0f;
        float startVolume = _musicSource.volume;

        while (elapsed < _crossfadeDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (_crossfadeDuration * 0.5f));
            yield return null;
        }

        // Switch clip
        _musicSource.clip = targetClip;
        _musicSource.loop = loop;
        _musicSource.Play();

        // Fade in new music
        elapsed = 0f;
        while (elapsed < _crossfadeDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(0f, startVolume, elapsed / (_crossfadeDuration * 0.5f));
            yield return null;
        }

        _musicSource.volume = startVolume;
    }

    private AudioClip GetClipForState(BattleMusicState state) => state switch
    {
        BattleMusicState.Combat => _combatTheme,
        BattleMusicState.Intense => _intenseTheme,
        BattleMusicState.Victory => _victoryTheme,
        BattleMusicState.BossBattle => _bossTheme,
        _ => null,
    };

    public void Stop()
    {
        if (_crossfadeCoroutine != null)
            StopCoroutine(_crossfadeCoroutine);
        
        StartCoroutine(FadeOutAndStop());
    }

    private IEnumerator FadeOutAndStop()
    {
        float elapsed = 0f;
        float startVolume = _musicSource.volume;

        while (elapsed < _crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / _crossfadeDuration);
            yield return null;
        }

        _musicSource.Stop();
        _currentState = BattleMusicState.None;
    }
}
```

---

## Integration with WaveManager

### Hook Points

**In WaveManager.cs:**

```csharp
public class WaveManager : MonoBehaviour
{
    private BattleMusicManager _musicManager;

    private void Start()
    {
        _musicManager = GetComponent<BattleMusicManager>();
        // OR: _musicManager = FindObjectOfType<BattleMusicManager>();
    }

    public void BeginWave(int waveNumber)
    {
        // Detect if this is a boss wave
        if (IsBossWave(waveNumber))
        {
            _musicManager.TransitionTo(BattleMusicState.BossBattle);
        }
        else
        {
            _musicManager.TransitionTo(BattleMusicState.Combat);
        }

        // ... rest of wave start logic
    }

    public void OnEnemyCountHigh(int currentEnemyCount)
    {
        // If many enemies are active, escalate to intense theme
        if (currentEnemyCount >= 5 && _currentMusicState == BattleMusicState.Combat)
        {
            _musicManager.TransitionTo(BattleMusicState.Intense);
        }
    }

    public void OnWaveVictory()
    {
        _musicManager.TransitionTo(BattleMusicState.Victory);
        // Victory plays once, then stops
        // (or loops back to exploration—depends on design)
    }

    public void OnWaveEnd()
    {
        // After victory music plays, return to calm state
        // (optional—depends on game flow)
        _musicManager.TransitionTo(BattleMusicState.None);
    }

    private bool IsBossWave(int waveNumber)
    {
        // Define boss wave logic (e.g., final wave, specific tier)
        return waveNumber >= _totalWaves - 1;  // Last wave is boss
    }
}
```

---

## Music State Flow

```
┌─────────────────────────────────────────────────────┐
│  Game Start                                          │
│  → No music (or exploration theme if exists)        │
└─────────────────┬───────────────────────────────────┘
                  │
         ┌────────▼────────┐
         │ Enemies Spotted │
         └────────┬────────┘
                  │
         ┌────────▼──────────────────┐
         │ Overworld Battle 1         │  (Combat)
         │ (Medium intensity, loops)  │
         └────────┬──────────────────┘
                  │
      ┌───────────┴────────────┐
      │                        │
  ┌───▼────────────────┐    ┌─▼──────────────────┐
  │ Many Enemies Active │    │ Boss Detected      │
  │ → Battle 2 (Intense)│    │ → Boss Fight Theme │
  └───┬────────────────┘    └─┬──────────────────┘
      │                       │
      └───────────┬───────────┘
              ┌───▼─────────────────┐
              │ All Enemies Defeated│
              └───┬─────────────────┘
                  │
          ┌───────▼──────────────┐
          │ Victory Theme         │  (One-shot)
          │ Plays → Fades to None │
          └───────┬──────────────┘
                  │
          ┌───────▼──────────────┐
          │ Back to Exploration   │
          │ (or idle music)       │
          └──────────────────────┘
```

---

## Setup Checklist

### 1. Audio Import
- [ ] Copy 4 MP3 files to `Assets/Audio/Music/Battle/`
- [ ] Rename files (use underscores, not spaces)
- [ ] Import settings: Vorbis compression, 70–80 quality
- [ ] Loop enabled on battle tracks, disabled on victory

### 2. Audio Source Setup
- [ ] Create empty GameObject: `MusicManager`
- [ ] Add AudioSource component
- [ ] Set volume to 0.7 (leaves headroom for SFX + ambience)
- [ ] Disable "Play On Awake"

### 3. BattleMusicManager Script
- [ ] Create script (code above)
- [ ] Assign to `MusicManager` GameObject
- [ ] Drag audio clips into Inspector slots
- [ ] Set crossfade duration to 1.5s (adjustable)

### 4. WaveManager Integration
- [ ] Get reference to BattleMusicManager (Start method)
- [ ] Call `TransitionTo()` on wave start (detect boss)
- [ ] Call `TransitionTo(Intense)` on high enemy count
- [ ] Call `TransitionTo(Victory)` on wave clear
- [ ] Call `Stop()` on scene transition

### 5. Testing
- [ ] Start game, trigger wave → Combat music plays
- [ ] Many enemies → Transitions to Intense (smooth crossfade)
- [ ] Clear wave → Victory plays once, then stops
- [ ] Boss wave → Boss theme plays (distinct and epic)
- [ ] No pops/clicks on transitions
- [ ] Music volume doesn't drown SFX

---

## Volume Mixing

**Master levels:**
- Music: 0.7 (primary layer)
- SFX: 0.8 (prominent but not louder than music)
- Ambience: 0.3 (background, barely noticeable)

**Use AudioMixer for easy tweaking:**

```csharp
public class AudioMixerController : MonoBehaviour
{
    [SerializeField] private AudioMixer _audioMixer;

    public void SetMusicVolume(float value)  // 0–1
    {
        // AudioMixer uses dB scale: value * 80 - 80
        _audioMixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(value, 0.001f)) * 20);
    }

    public void SetSFXVolume(float value)
    {
        _audioMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(value, 0.001f)) * 20);
    }
}
```

---

## Crossfade Tuning

Current: **1.5 seconds** (smooth but not sluggish)

- **Faster (0.5–1s):** Feels snappy, urgent (for high-intensity escalations)
- **Slower (2–3s):** Feels gradual, cinematic (for exploration → combat)

Adjust `_crossfadeDuration` in Inspector per transition if needed.

---

## What NOT to Do

- Don't manually play clips (use TransitionTo only)
- Don't overlap music tracks (stop old before playing new)
- Don't use 3D audio for music (keep it global)
- Don't compress music too much (quality loss)

---

## Testing Scenarios

| Scenario | Expected Behavior |
|---|---|
| Start game | No music (or exploration theme) |
| First wave starts | Combat theme crossfades in |
| 5+ enemies active | Crossfade to Intense theme |
| Boss wave detected | Crossfade to Boss theme (distinct) |
| All enemies defeated | Crossfade to Victory (one-shot) |
| Victory ends | Music fades out, silence |
| Next wave starts | Combat theme resumes |
| Scene transition | Music stops cleanly (no tail) |

---

## Performance Notes

**MP3 size estimate:**
- Each track: ~2–4 MB
- Total: ~8–16 MB (negligible)

**Audio streaming:**
- Set to "Streaming" if files >5 MB
- Recommend "Compressed in Memory" for responsiveness

---

## Future Enhancements

- [ ] Exploration/hub music (calm background theme)
- [ ] Alert state music (enemies nearby but not fighting yet)
- [ ] Dynamic intensity based on enemy count (fade between themes)
- [ ] Seasonal/variant battle themes
- [ ] Procedural music transitions (algorithmic mixing)

---

## Acceptance Sign-Off

- [ ] All 4 music tracks integrated
- [ ] State machine wired to WaveManager
- [ ] Transitions smooth and responsive
- [ ] Boss theme distinct and epic
- [ ] Victory theme celebratory
- [ ] Volume mixing balanced (music + SFX work together)
- [ ] No audio glitches or pops
- [ ] Works in WebGL build
- [ ] Ready for live gameplay testing

---

## Dependencies

**Requires:** WaveManager, AudioSource, AudioMixer (optional but recommended)  
**Unblocks:** Complete audio layer (music + SFX from WO-371)  
**Parallel:** None (final audio integration step)

---

## Notes for CLI

1. Import audio clips with correct settings (Vorbis, loop enabled/disabled)
2. Brace balance check NOT needed (pure audio integration, no C# logic changes)
3. Test in WebGL (audio can behave differently on web)
4. Verify crossfade loop point (check for gaps or pops at loop boundary)
