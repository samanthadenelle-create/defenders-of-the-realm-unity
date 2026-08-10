# WORK ORDER 41 — Cross-Assembly Interface Refactor: IVillageHud + IAudioService

**Status:** DONE (reconciled 2026-08-09 from the tree, NOT felt-verified — IVillageHud.cs + IAudioService.cs exist at the spec'd Core paths, CoreServices-resolved; CLAUDE.md §6)
**Date:** 2026-05-26
**Author:** Architecture pass — owner preference for correct methods over reflection shortcuts
**Priority:** High — prerequisite for WO-38/39/40 implementation; also cleans up
              existing `AbilityAudioBridge` and `WaveHudBridge` technical debt

---

## Problem

Several Village-side systems communicate with the HUD and Audio modules through
runtime reflection (`GetType().GetMethod(…).Invoke(…)`). This pattern was
introduced to avoid compile-time asmdef dependencies, but it:

- **Has no type safety** — a renamed method silently no-ops at runtime
- **Is expensive when called per-frame** — method lookup via reflection
  allocates and is 10–100× slower than a virtual dispatch
- **Is untestable** — `typeof(ReflectionCall)` cannot be mocked or verified
- **Spreads debt** — WO-38/39/40 all duplicated the pattern instead of
  fixing the root cause

The existing `IDamageable` interface in `DeNelle.Core.Combat` already
proves the correct pattern: a thin contract in Core that both gameplay
assemblies reference.

---

## Solution

Two new interfaces in `DeNelle.Core`, plus a `CoreServices` static registry
so Village and other modules can reach the live HUD and Audio service without
any reflection or direct asmdef cross-references.

```
DeNelle.Core
 └─ HUD/
 │   └─ IVillageHud.cs        ← new
 └─ Audio/
 │   └─ IAudioService.cs      ← new
 │   └─ MusicTrack.cs         ← move here from DeNelle.Audio
 └─ Services/
     └─ CoreServices.cs       ← new
```

**Call chain after this WO:**

```
WaveImminentDirector (Village)
  → CoreServices.Hud?.SetWaveImminent(true)
  → VillageHudController.SetWaveImminent(bool)   (direct virtual call)
```

```
AbilityAudioBridge (Village)
  → CoreServices.Audio?.PlaySfx(clip, vol)
  → AudioService.PlaySfx(AudioClip, float)       (direct virtual call)
```

No reflection. No boxing. No magic strings.

---

## 1. New: `IVillageHud` — `Assets/_Modules/Core/HUD/IVillageHud.cs`

Follows the `IDamageable` style: minimal contract, plain C# types only (no
Unity scene or HUD-module dependencies), documented with the module-isolation
rationale.

```csharp
// =============================================================================
// IVillageHud — cross-module HUD setter contract (DeNelle.Village → DeNelle.HUD).
// -----------------------------------------------------------------------------
// Module-isolation seam (port spec Part 2): DeNelle.Village must not reference
// DeNelle.HUD. Village systems that need to push data to the HUD talk only to
// this interface, which lives in DeNelle.Core (referenced by both modules).
//
// VillageHudController (DeNelle.HUD) implements IVillageHud and registers
// itself with CoreServices.RegisterHud() in Awake. Village systems resolve
// the live instance via CoreServices.Hud — no reflection, no direct reference.
//
// Design rule: no Unity-specific types beyond those already in DeNelle.Core.
// Only primitive data flows across the boundary; layout decisions stay in the
// HUD module.
// =============================================================================

namespace DeNelle.Core.HUD
{
    /// <summary>
    /// The typed contract that Village systems use to push data to the village HUD.
    /// Implemented by <c>DeNelle.HUD.VillageHudController</c>; resolved at runtime
    /// via <see cref="CoreServices.Hud"/>. Never call the concrete type directly
    /// from Village code — always use this interface.
    /// </summary>
    public interface IVillageHud
    {
        // ── Wave state ──────────────────────────────────────────────────────

        /// <summary>Update the wave-number label (1-based).</summary>
        void SetWave(int waveNumber);

        /// <summary>Update the countdown timer display (seconds remaining; 0 = wave active).</summary>
        void SetCountdown(float secondsRemaining);

        // ── Resource + HP bars ──────────────────────────────────────────────

        /// <summary>Update the Heart HP bar (0..1 normalised).</summary>
        void SetHeartHp(float normalisedHp);

        /// <summary>Update the crystal currency display.</summary>
        void SetCrystals(int amount);

        // ── Compass — attack direction indicator (WO-39) ────────────────────

        /// <summary>
        /// Lights up the N/E/S/W compass arms to show which gates currently
        /// have live enemies approaching. Called up to 4× per second by
        /// <c>CompassDirectionBridge</c>.
        /// </summary>
        void SetAttackDirections(bool north, bool east, bool south, bool west);

        // ── Wave-imminent alert (WO-40) ──────────────────────────────────────

        /// <summary>
        /// Triggers (or clears) the wave-imminent red vignette + compass flash.
        /// The HUD owns the animation; the caller only signals intent.
        /// Pass <c>true</c> when the countdown threshold is crossed;
        /// <c>false</c> when the wave spawns (<c>OnWaveStarted</c> fires).
        /// </summary>
        void SetWaveImminent(bool imminent);

        // ── Wave-clear celebration (WO-38) ───────────────────────────────────

        /// <summary>
        /// Slides in the wave-clear banner with the given stats and flavour line.
        /// <paramref name="flavourLine"/> is one of the five bible-voice strings
        /// from <c>WaveClearDirector.WaveFlavourLines</c>.
        /// </summary>
        void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine);

        /// <summary>Slides out the wave-clear banner (called 3.8 s after show).</summary>
        void HideWaveClearBanner();

        /// <summary>
        /// Displays the wall-repair prompt below the wave-clear banner.
        /// <paramref name="wallLabel"/> is the human-readable segment name,
        /// e.g. "North Gate Wall".
        /// </summary>
        void ShowRepairPrompt(string wallLabel, float damagePercent);
    }
}
```

---

## 2. New: `IAudioService` — `Assets/_Modules/Core/Audio/IAudioService.cs`

```csharp
// =============================================================================
// IAudioService — cross-module audio contract (Village / HUD → DeNelle.Audio).
// -----------------------------------------------------------------------------
// Follows the IDamageable pattern. AudioService (DeNelle.Audio) implements this
// and registers with CoreServices.RegisterAudio() in Awake. Any module that
// needs to play SFX or switch music resolves via CoreServices.Audio.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Audio
{
    /// <summary>
    /// The typed contract for playing SFX and switching music tracks.
    /// Implemented by <c>DeNelle.Audio.AudioService</c>; resolved at runtime
    /// via <see cref="CoreServices.Audio"/>.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Plays a one-shot SFX clip at the given volume (0..1).
        /// Fire-and-forget — the AudioService owns the AudioSource pool.
        /// </summary>
        void PlaySfx(AudioClip clip, float volume);

        /// <summary>
        /// Crossfades to the given music track. The AudioService applies
        /// the durations and volumes specified in <c>audio-mix-spec.md</c>.
        /// </summary>
        void PlayMusic(MusicTrack track);
    }
}
```

---

## 3. Move `MusicTrack` — `Assets/_Modules/Core/Audio/MusicTrack.cs`

`MusicTrack` is currently defined in `DeNelle.Audio`. It has no audio-specific
dependencies — it is a pure data enum like `HeroClass` or `Difficulty`. Move it
to `DeNelle.Core.Audio` so `IAudioService` can reference it without creating a
circular dependency.

```csharp
namespace DeNelle.Core.Audio
{
    /// <summary>
    /// Named music states the AudioService crossfades between.
    /// Mirrors <c>audio-mix-spec.md</c>.
    /// </summary>
    public enum MusicTrack
    {
        /// <summary>Calm ambient music during the build/explore phase.</summary>
        Village = 0,

        /// <summary>Driving combat music during an active wave.</summary>
        Battle = 1,

        /// <summary>
        /// Short victory sting after a wave is cleared — 3.8 s before
        /// crossfading back to Village.
        /// </summary>
        Victory = 2,

        /// <summary>Tense atmospheric music inside the Healer's Cottage dungeon.</summary>
        Dungeon = 3,
    }
}
```

In `DeNelle.Audio.AudioService`, update the `using` / namespace from wherever
`MusicTrack` was declared to `using DeNelle.Core.Audio;`. All call sites that
already use `MusicTrack.Victory` / `.Battle` / `.Village` continue to compile
without change.

---

## 4. New: `CoreServices` — `Assets/_Modules/Core/Services/CoreServices.cs`

```csharp
// =============================================================================
// CoreServices — lightweight runtime service registry for DeNelle.Core.
// -----------------------------------------------------------------------------
// Provides a typed, non-reflective access point for the two cross-module
// singletons that Village systems need but cannot reference directly:
//   • IVillageHud  (DeNelle.HUD.VillageHudController)
//   • IAudioService (DeNelle.Audio.AudioService)
//
// Implementors call Register*() in MonoBehaviour.Awake() and Unregister*() in
// OnDestroy(). Consumers call CoreServices.Hud?.Method() — the null-conditional
// operator makes every call safely no-op when the service is absent (e.g.
// during unit tests or before the scene is fully loaded).
//
// This follows the same pattern as GameStateService.Instance: a Core-resident
// static that any module can reach, populated at runtime by the concrete class
// in the owning module.
// =============================================================================

using DeNelle.Core.Audio;
using DeNelle.Core.HUD;

namespace DeNelle.Core.Services
{
    /// <summary>
    /// Runtime registry for cross-module singleton services.
    /// Populated by the owning MonoBehaviours in their Awake; read by any
    /// module that needs the service at runtime.
    /// </summary>
    public static class CoreServices
    {
        // ── Village HUD ─────────────────────────────────────────────────────

        /// <summary>
        /// The live <see cref="IVillageHud"/> implementation, or <c>null</c>
        /// when the HUD scene object has not yet registered (or has been destroyed).
        /// </summary>
        public static IVillageHud Hud { get; private set; }

        /// <summary>
        /// Called by <c>VillageHudController.Awake()</c> to register itself.
        /// Only one HUD instance is expected; a second registration logs a warning.
        /// </summary>
        public static void RegisterHud(IVillageHud hud)
        {
            if (Hud != null && !ReferenceEquals(Hud, hud))
                UnityEngine.Debug.LogWarning("[CoreServices] RegisterHud: replacing an existing IVillageHud registration. Was a second HUD controller created?");
            Hud = hud;
        }

        /// <summary>Called by <c>VillageHudController.OnDestroy()</c>.</summary>
        public static void UnregisterHud(IVillageHud hud)
        {
            if (ReferenceEquals(Hud, hud)) Hud = null;
        }

        // ── Audio ────────────────────────────────────────────────────────────

        /// <summary>
        /// The live <see cref="IAudioService"/> implementation, or <c>null</c>
        /// when AudioService has not yet registered.
        /// </summary>
        public static IAudioService Audio { get; private set; }

        /// <summary>Called by <c>AudioService.Awake()</c>.</summary>
        public static void RegisterAudio(IAudioService audio)
        {
            if (Audio != null && !ReferenceEquals(Audio, audio))
                UnityEngine.Debug.LogWarning("[CoreServices] RegisterAudio: replacing an existing IAudioService registration.");
            Audio = audio;
        }

        /// <summary>Called by <c>AudioService.OnDestroy()</c>.</summary>
        public static void UnregisterAudio(IAudioService audio)
        {
            if (ReferenceEquals(Audio, audio)) Audio = null;
        }
    }
}
```

---

## 5. `VillageHudController.cs` — implement `IVillageHud` + register

```csharp
// Add to existing using block:
using DeNelle.Core.HUD;
using DeNelle.Core.Services;

// Change class declaration:
public sealed class VillageHudController : MonoBehaviour, IVillageHud
{
    // ── CoreServices registration ──────────────────────────────────────────

    private void Awake()
    {
        CoreServices.RegisterHud(this);
    }

    private void OnDestroy()
    {
        CoreServices.UnregisterHud(this);
    }

    // ── IVillageHud setters — implement each in full ───────────────────────

    public void SetWave(int waveNumber) { /* existing implementation */ }
    public void SetCountdown(float secondsRemaining) { /* existing implementation */ }
    public void SetHeartHp(float normalisedHp) { /* existing implementation */ }
    public void SetCrystals(int amount) { /* existing implementation */ }

    // SetAttackDirections — from WO-39 (add as new, not via reflection):
    public void SetAttackDirections(bool north, bool east, bool south, bool west)
    {
        SetCompassArm(_compassN, north);
        SetCompassArm(_compassE, east);
        SetCompassArm(_compassS, south);
        SetCompassArm(_compassW, west);
    }

    // SetWaveImminent — from WO-40. The HUD owns the animation; no external
    // per-frame calls. The breathing and compass flash are driven by Update().
    private bool _waveImminent;
    private float _vignetteAlpha;
    private float _vignetteDir = 1f;
    private bool _vignetteFadingOut;
    private float _vignetteFadeTimer;

    public void SetWaveImminent(bool imminent)
    {
        if (imminent == _waveImminent) return;
        _waveImminent = imminent;

        if (imminent)
        {
            _vignetteAlpha = 0f;
            _vignetteDir   = 1f;
            _vignetteFadingOut = false;
            SetCompassImminent(true);
        }
        else
        {
            // Trigger a fade-out; Update() will complete it.
            _vignetteFadingOut = true;
            _vignetteFadeTimer = 0f;
            SetCompassImminent(false);
        }
    }

    // ShowWaveClearBanner / HideWaveClearBanner / ShowRepairPrompt
    // — from WO-38; implement per that WO's UXML/USS spec, but call these
    // directly from WaveClearDirector via the interface (not reflection).
    public void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine)
    {
        // Slide-in animation + populate labels per WO-38.
        // _waveClearBanner.RemoveFromClassList("wave-clear-banner--hidden");
        // _waveClearTitle.text = $"Wave {waveNumber} Repelled";
        // _waveClearFlavour.text = flavourLine;
        // _waveClearStats.text = $"{enemiesDefeated} enemies defeated";
    }

    public void HideWaveClearBanner()
    {
        // _waveClearBanner.AddToClassList("wave-clear-banner--hidden");
    }

    public void ShowRepairPrompt(string wallLabel, float damagePercent)
    {
        // Per WO-38 repair-prompt spec.
    }

    // ── Vignette + compass animation — driven inside Update() ─────────────
    // (No external per-frame calls. CoreServices consumers only call
    //  SetWaveImminent(bool); animation is private to this class.)

    private void Update()
    {
        // Existing toast hide timer + compass arm pulse (WO-39) …

        if (_waveImminent && !_vignetteFadingOut)
        {
            // Breathe between 35% and 60% at 1 Hz.
            _vignetteAlpha += _vignetteDir * Time.unscaledDeltaTime * 0.5f;
            if (_vignetteAlpha >= 0.60f) { _vignetteAlpha = 0.60f; _vignetteDir = -1f; }
            if (_vignetteAlpha <= 0.35f) { _vignetteAlpha = 0.35f; _vignetteDir =  1f; }
            ApplyVignetteAlpha(_vignetteAlpha);
        }
        else if (_vignetteFadingOut)
        {
            // Fade out over 0.5 s then clear.
            _vignetteFadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_vignetteFadeTimer / 0.5f);
            ApplyVignetteAlpha(Mathf.Lerp(_vignetteAlpha, 0f, t));
            if (t >= 1f)
            {
                _vignetteFadingOut = false;
                _waveImminent = false;
                ApplyVignetteAlpha(0f);
            }
        }

        // Compass imminent: double-speed amber pulse (2 Hz).
        if (_compassImminent)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) * 0.25f) + 0.75f;
            PulseCompassArm(_compassN, pulse, forceActive: true);
            PulseCompassArm(_compassE, pulse, forceActive: true);
            PulseCompassArm(_compassS, pulse, forceActive: true);
            PulseCompassArm(_compassW, pulse, forceActive: true);
        }
        else
        {
            // Regular 1 Hz pulse for active arms (WO-39).
            float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f) * 0.25f) + 0.75f;
            PulseCompassArm(_compassN, pulse);
            PulseCompassArm(_compassE, pulse);
            PulseCompassArm(_compassS, pulse);
            PulseCompassArm(_compassW, pulse);
        }
    }

    // Private helper — not part of IVillageHud (the interface only expresses
    // intent; the animation implementation is the HUD's own concern).
    private void ApplyVignetteAlpha(float alpha)
    {
        if (_vignetteEl == null) return;
        _vignetteEl.style.opacity = alpha;
    }
}
```

---

## 6. `AudioService.cs` — implement `IAudioService` + register

```csharp
// Add to existing using block:
using DeNelle.Core.Audio;
using DeNelle.Core.Services;

// Change class declaration:
public sealed class AudioService : MonoBehaviour, IAudioService
{
    private void Awake()
    {
        // Existing singleton guard …
        CoreServices.RegisterAudio(this);
    }

    private void OnDestroy()
    {
        CoreServices.UnregisterAudio(this);
    }

    // Implement IAudioService — map to existing internal methods:
    public void PlaySfx(AudioClip clip, float volume)
    {
        // Existing PlaySfx logic (pool / AudioSource.PlayOneShot).
    }

    public void PlayMusic(MusicTrack track)
    {
        // Existing PlayMusic logic (crossfade to the track's clip).
        // MusicTrack is now DeNelle.Core.Audio.MusicTrack — update the
        // switch/enum references accordingly.
    }
}
```

---

## 7. `AbilityAudioBridge.cs` — replace reflection with `CoreServices.Audio`

**Before (reflection — DELETE):**
```csharp
private static void Resolve() { /* AppDomain scan + MethodInfo cache */ }
public static void PlayForKind(AbilityEffect kind) { Resolve(); … s_playSfx.Invoke(…); }
```

**After (interface — REPLACE WITH):**
```csharp
using DeNelle.Core.Services;

public static class AbilityAudioBridge
{
    public static void PlayForKind(AbilityEffect kind)
    {
        AudioClip clip = ProceduralSfx.ForKind(kind);
        if (clip == null) return;
        CoreServices.Audio?.PlaySfx(clip, VolumeFor(kind));
    }

    public static void PlayForClassAndKind(string heroClass, AbilityEffect kind)
    {
        AudioClip clip = ProceduralSfx.ForClassAndKind(heroClass, kind);
        if (clip == null) return;
        CoreServices.Audio?.PlaySfx(clip, VolumeFor(kind));
    }

    public static void PlayDangerSting()
    {
        AudioClip clip = ProceduralSfx.DangerSting();
        if (clip == null) return;
        CoreServices.Audio?.PlaySfx(clip, 0.65f);
    }

    private static float VolumeFor(AbilityEffect k) { /* unchanged */ }
}
```

All private `s_resolved`, `s_instanceProp`, `s_playSfx` fields and the `Resolve()`
method are removed entirely.

---

## 8. Village bridge MonoBehaviours — replace reflection with `CoreServices.Hud`

All three bridges introduced in WO-38/39/40 follow the same pattern.
The reflection-based `ResolveHud()` / `_hudController` / `_setDirections` fields
are replaced with a single line at each call site:

### `CompassDirectionBridge.cs` (WO-39 — correct implementation)

```csharp
using DeNelle.Core.Services;

// DELETE: private System.Reflection.MethodInfo _setDirections;
// DELETE: private Component _hudController;
// DELETE: private void ResolveHud() { … }

private void PushDirections()
{
    if (_waveManager == null) return;
    var dirs = GetDirectionFlags();
    // Direct interface call — no reflection:
    CoreServices.Hud?.SetAttackDirections(dirs[0], dirs[1], dirs[2], dirs[3]);
}
```

### `WaveClearDirector.cs` (WO-38 — correct implementation)

```csharp
using DeNelle.Core.Audio;
using DeNelle.Core.Services;

// On wave clear:
CoreServices.Audio?.PlayMusic(MusicTrack.Victory);
CoreServices.Hud?.ShowWaveClearBanner(waveNumber, enemyCount, flavourLine);

// 3.8 s later:
CoreServices.Hud?.HideWaveClearBanner();

// 4.5 s later, if wall damage found:
CoreServices.Hud?.ShowRepairPrompt(wallLabel, damagePercent);

// 4.5 s later, village music:
CoreServices.Audio?.PlayMusic(MusicTrack.Village);
```

### `WaveImminentDirector.cs` (WO-40 — correct implementation)

```csharp
using DeNelle.Core.Services;

// DELETE: private System.Reflection.MethodInfo _setImminent;
// DELETE: private System.Reflection.MethodInfo _setCompassImminent;
// DELETE: private Component _hudController;
// DELETE: private void ResolveHud() { … }
// DELETE: private void SetVignetteAlpha(float alpha) { … }  ← driven by VillageHudController.Update now

// On alert fire:
CoreServices.Hud?.SetWaveImminent(true);
CoreServices.Audio?.PlaySfx(ProceduralSfx.DangerSting(), 0.65f);
// (haptic unchanged)

// On wave start (OnWaveStarted listener):
CoreServices.Hud?.SetWaveImminent(false);
```

### `WaveHudBridge.cs` (existing — update in place)

The existing `WaveHudBridge` stores a `_hud` Component reference and calls
it reflectively. Replace its internals:

```csharp
using DeNelle.Core.Services;

// DELETE all reflection fields and Resolve() method.
// DELETE _hud serialized field (no longer needed — CoreServices provides it).

// In OnCountdownTick listener:
CoreServices.Hud?.SetCountdown(seconds);

// In OnWaveStarted listener:
CoreServices.Hud?.SetWave(waveNumber);
```

Also remove `_hud` from `VillageSceneBuilder.WireWaveHudBridge()` — the
`SetObjectField(so, "_hud", ...)` call is no longer needed since the bridge
no longer holds the reference.

---

## 9. `VillageSceneBuilder.cs` — clean up bridge wiring

Remove the `SetObjectField(so, "_hud", ...)` line from `WireWaveHudBridge()`.
The method still needs to exist to add the `WaveHudBridge` component if absent,
and wire `_wave` to the `WaveManager` — only the HUD reference wiring is removed.

---

## Files to Edit / Create

| File | Change |
|---|---|
| `Assets/_Modules/Core/HUD/IVillageHud.cs` | **New** — cross-module HUD contract |
| `Assets/_Modules/Core/Audio/IAudioService.cs` | **New** — cross-module audio contract |
| `Assets/_Modules/Core/Audio/MusicTrack.cs` | **New** — move MusicTrack enum here from DeNelle.Audio |
| `Assets/_Modules/Core/Services/CoreServices.cs` | **New** — static service registry |
| `Assets/_Modules/HUD/VillageHudController.cs` | Implement `IVillageHud`; register with `CoreServices`; move vignette + compass animation into `Update()` |
| `Assets/_Modules/Audio/AudioService.cs` | Implement `IAudioService`; register with `CoreServices`; update `MusicTrack` namespace reference |
| `Assets/_Modules/Village/Hero/AbilityAudioBridge.cs` | Delete reflection fields + `Resolve()`; replace with `CoreServices.Audio?.PlaySfx(…)` |
| `Assets/_Modules/Village/Waves/WaveHudBridge.cs` | Delete `_hud` field + reflection; replace with `CoreServices.Hud?.SetCountdown(…)` / `SetWave(…)` |
| `Assets/_Modules/Village/Waves/CompassDirectionBridge.cs` | Delete `ResolveHud()` + reflection fields; replace with `CoreServices.Hud?.SetAttackDirections(…)` |
| `Assets/_Modules/Village/Waves/WaveClearDirector.cs` | Delete `ResolveHud()` + reflection fields; replace with `CoreServices.Hud?.ShowWaveClearBanner(…)` and `CoreServices.Audio?.PlayMusic(…)` |
| `Assets/_Modules/Village/Waves/WaveImminentDirector.cs` | Delete `ResolveHud()` + reflection fields + `SetVignetteAlpha()`; replace with `CoreServices.Hud?.SetWaveImminent(…)` |
| `Assets/Editor/VillageSceneBuilder.cs` | Remove `SetObjectField(so, "_hud", ...)` from `WireWaveHudBridge()` |

---

## Implementation Order

This WO should be implemented before WO-38, WO-39, and WO-40 — or concurrently
with them, using this spec as the authoritative guide for those bridges. The
reflection patterns described in those earlier WOs are **superseded** by this spec
wherever they conflict.

**Suggested order within this WO:**
1. Add `MusicTrack.cs` to Core → update `AudioService` namespace reference → confirm compile
2. Add `IAudioService.cs` + `CoreServices.cs` → implement in `AudioService` → confirm compile
3. Update `AbilityAudioBridge` (simplest consumer) → confirm no regressions
4. Add `IVillageHud.cs` → implement in `VillageHudController` + register → confirm compile
5. Update `WaveHudBridge` → remove HUD wiring from `VillageSceneBuilder` → confirm wave timer still works
6. Implement `CompassDirectionBridge`, `WaveClearDirector`, `WaveImminentDirector` using `CoreServices.Hud`

---

## Acceptance Criteria

- [ ] `IVillageHud` and `IAudioService` compile in `DeNelle.Core` with no Unity scene dependencies
- [ ] `CoreServices.Hud` is non-null from the frame `VillageHudController.Awake()` runs
- [ ] `CoreServices.Audio` is non-null from the frame `AudioService.Awake()` runs
- [ ] `AbilityAudioBridge` contains zero `System.Reflection` references
- [ ] `WaveHudBridge` contains zero `System.Reflection` references
- [ ] `CompassDirectionBridge` contains zero `System.Reflection` references
- [ ] `WaveClearDirector` contains zero `System.Reflection` references
- [ ] `WaveImminentDirector` contains zero `System.Reflection` references
- [ ] Wave countdown timer still updates in-game after `WaveHudBridge` refactor
- [ ] Compass arms still light up per-direction after `CompassDirectionBridge` refactor
- [ ] Vignette animation is entirely internal to `VillageHudController.Update()` — no external per-frame calls
- [ ] `MusicTrack` references throughout the codebase resolve to `DeNelle.Core.Audio.MusicTrack`
- [ ] All unit tests in `DeNelle.Core.Tests` still pass
- [ ] No scene re-bake required
