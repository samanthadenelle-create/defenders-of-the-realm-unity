# WORK ORDER 40 — Wave Imminent: Screen Flash + Haptic Vibration

**Status:** CLOSED (owner ruling 2026-08-09, felt-notated: flash partially present in-game — low-health flashing observed live; haptics never felt; remainder judged a bell-and-whistle not worth the performance weight on lower/mid-tier devices like Seeker)
**Date:** 2026-05-26
**Author:** Owner design spec — playtest feedback
**Priority:** High — no warning before enemies pour through the gate;
              player should feel the tension build in the final seconds,
              like Warcraft's horn blast when a major enemy respawns

---

## Owner Direction

> "Maybe flash screen and vibrate wave imminent like in warcraft when the
> main enemy respawns."

Three to five seconds before a wave launches, the screen edges blaze red,
the device vibrates twice (mobile), and an ominous low-pitched audio sting
plays. The compass rose arms all flash amber simultaneously. The moment
the countdown hits zero and enemies spawn, everything clears and the
normal battle HUD takes over.

---

## Existing Hooks

| System | Already exists | What's missing |
|---|---|---|
| `WaveManager.OnCountdownTick` | `WaveCountdownEvent` fires every frame with `float` seconds remaining | Nobody reads it to trigger alerts |
| `WaveManager.Phase` | `WavePhase.Countdown` / `WavePhase.Active` enum | No imminent-threshold listener |
| `VillageHud.uxml` | Full-screen `VisualElement` root | No edge-vignette overlay element |
| `VillageHudController` | Public setters pattern | No `SetWaveImminent(bool)` method |
| `ProceduralSfx` + `AbilityAudioBridge` | Synthesized audio | No danger-sting synthesis |
| `Handheld.Vibrate()` | Unity mobile API | Not called anywhere |
| `CompassDirectionBridge` (WO-39) | Pushes N/E/S/W flags to HUD | No all-arms flash mode |

---

## Design

### Alert sequence (countdown = 3 s remaining → 0 s)

```
t = 0.0 s  → Alert begins
            • Screen edges ignite (red vignette, opacity 0 → 0.6 over 0.4 s)
            • Compass all-arms pulse amber (overrides per-direction colour)
            • Danger sting audio plays (low descending tone, 0.7 s)
            • Haptic: double-pulse vibration (mobile only)

t = 0.0–3.0 s  → Vignette breathes (opacity 0.35 → 0.60 → 0.35, 1 Hz)

t = 3.0 s  → Wave spawns (WaveManager flips to Active)
            • Vignette fades out over 0.5 s
            • Compass arms revert to per-direction colours (WO-39 resumes)
```

### Visual — screen-edge vignette

A full-screen radial gradient overlay sitting atop every other HUD element.
The gradient is transparent at centre and opaque danger-red at the edges,
creating a classic "taking damage" alert used by virtually every action game.

```
centre: rgba(0,0,0,0)
edge:   rgba(200, 30, 30, 0.60)   ← #c81e1e at 60% alpha
```

Implemented as a `VisualElement` with a generated `background-image` (USS
`radial-gradient` is not available in UI Toolkit; use a `RenderTexture` or
a pre-baked sprite — see §2 below for the lightweight approach).

### Colour + symbol references

| Element | State | Value |
|---|---|---|
| Vignette | Imminent | `rgba(200,30,30,0.55)` — danger red |
| Vignette | Fading out | opacity lerp → 0 over 0.5 s |
| Compass arms | Imminent flash | Amber `#f5a623` (all four, overrides active/clear) |
| Compass arms | After wave starts | Revert to WO-39 per-direction colours |

---

## Implementation

### 1. New file: `WaveImminentDirector.cs`

Lives in `DeNelle.Village`. Listens to `WaveManager.OnCountdownTick` and
orchestrates the full alert: HUD vignette, compass flash, audio sting,
haptic feedback.

```csharp
/// <summary>
/// Plays a dramatic "wave imminent" alert in the final seconds of each
/// countdown. Drives:
///   • A red screen-edge vignette via VillageHudController.SetWaveImminent()
///   • An amber compass all-arms flash via SetCompassImminent()
///   • A synthesized danger sting via ProceduralSfx
///   • Double-pulse haptic feedback on mobile via Handheld.Vibrate()
///
/// Wired by VillageSceneBuilder alongside WaveClearDirector.
/// Uses the reflection bridge pattern for HUD (cross-asmdef).
/// </summary>
[DisallowMultipleComponent]
public sealed class WaveImminentDirector : MonoBehaviour
{
    [SerializeField] private WaveManager _waveManager;

    /// <summary>Seconds remaining when the alert fires.</summary>
    [SerializeField] private float _alertThreshold = 3f;

    private bool  _alertFired;      // guards one-shot per wave
    private bool  _alertActive;     // true while vignette is showing
    private float _vignetteAlpha;
    private float _vignetteDir = 1f;

    // Reflection bridge to VillageHudController
    private System.Reflection.MethodInfo _setImminent;
    private System.Reflection.MethodInfo _setCompassImminent;
    private Component _hudController;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Start()
    {
        if (_waveManager == null)
            _waveManager = FindObjectOfType<WaveManager>();
        if (_waveManager != null)
        {
            _waveManager.OnCountdownTick.AddListener(OnCountdownTick);
            _waveManager.OnWaveStarted.AddListener(OnWaveStarted);
        }
        ResolveHud();
    }

    private void OnDestroy()
    {
        if (_waveManager != null)
        {
            _waveManager.OnCountdownTick.RemoveListener(OnCountdownTick);
            _waveManager.OnWaveStarted.RemoveListener(OnWaveStarted);
        }
    }

    // ── Countdown listener ─────────────────────────────────────────────────

    private void OnCountdownTick(float secondsRemaining)
    {
        if (_alertFired) return;
        if (secondsRemaining > _alertThreshold) return;

        _alertFired  = true;
        _alertActive = true;
        _vignetteAlpha = 0f;
        _vignetteDir   = 1f;

        // 1. HUD vignette on
        PushImminent(true);

        // 2. Compass all-arms amber flash
        PushCompassImminent(true);

        // 3. Audio danger sting
        PlayDangerSting();

        // 4. Haptic (mobile only — no-op on desktop)
        TriggerHaptic();
    }

    private void OnWaveStarted(int waveId)
    {
        // Reset for next wave; start vignette fade-out.
        _alertFired = false;
        StartCoroutine(FadeOutVignette());
        PushCompassImminent(false);
    }

    // ── Per-frame vignette breath ──────────────────────────────────────────

    private void Update()
    {
        if (!_alertActive) return;

        // Breathe: 0.35 ↔ 0.60 at 1 Hz (matches compass pulse frequency)
        _vignetteAlpha += _vignetteDir * Time.unscaledDeltaTime * 0.5f;
        if (_vignetteAlpha >= 0.60f) { _vignetteAlpha = 0.60f; _vignetteDir = -1f; }
        if (_vignetteAlpha <= 0.35f) { _vignetteAlpha = 0.35f; _vignetteDir =  1f; }

        SetVignetteAlpha(_vignetteAlpha);
    }

    // ── Fade-out coroutine ─────────────────────────────────────────────────

    private System.Collections.IEnumerator FadeOutVignette()
    {
        _alertActive = false;
        float start = _vignetteAlpha;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            SetVignetteAlpha(Mathf.Lerp(start, 0f, t / 0.5f));
            yield return null;
        }
        SetVignetteAlpha(0f);
        PushImminent(false);
    }

    // ── Audio ──────────────────────────────────────────────────────────────

    private void PlayDangerSting()
    {
        // Synthesize a low descending danger tone via ProceduralSfx.
        // Wires through AbilityAudioBridge reflection path so no direct
        // asmdef dependency on the Audio module.
        System.Type bridgeType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            bridgeType = asm.GetType("DeNelle.Village.AbilityAudioBridge", false);
            if (bridgeType != null) break;
        }
        if (bridgeType == null) return;
        var playDanger = bridgeType.GetMethod("PlayDangerSting",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        playDanger?.Invoke(null, null);
    }

    // ── Haptic ─────────────────────────────────────────────────────────────

    private void TriggerHaptic()
    {
#if UNITY_IOS || UNITY_ANDROID
        StartCoroutine(DoublePulse());
#endif
    }

    private System.Collections.IEnumerator DoublePulse()
    {
        Handheld.Vibrate();
        yield return new WaitForSecondsRealtime(0.18f);
        Handheld.Vibrate();
    }

    // ── HUD bridge ─────────────────────────────────────────────────────────

    private void PushImminent(bool active)
    {
        _setImminent?.Invoke(_hudController, new object[] { active });
    }

    private void PushCompassImminent(bool active)
    {
        _setCompassImminent?.Invoke(_hudController, new object[] { active });
    }

    private void SetVignetteAlpha(float alpha)
    {
        // Called every frame during alert; uses a dedicated setter to avoid
        // re-boxing the bool repeatedly.
        var m = _hudController?.GetType()
                    .GetMethod("SetVignetteAlpha",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance);
        m?.Invoke(_hudController, new object[] { alpha });
    }

    private void ResolveHud()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("DeNelle.HUD.VillageHudController", false);
            if (t == null) continue;
            var inst = FindObjectOfType(t) as Component;
            if (inst == null) break;
            _hudController       = inst;
            _setImminent         = t.GetMethod("SetWaveImminent");
            _setCompassImminent  = t.GetMethod("SetCompassImminent");
            break;
        }
    }
}
```

### 2. `VillageHud.uxml` — add vignette overlay element

Insert as the **last child** of the root `VisualElement` so it renders on
top of everything (UI Toolkit paints children in order; last = topmost):

```xml
<!-- Wave-imminent vignette — full-screen red edge flash.
     Opacity driven each frame by WaveImminentDirector.          -->
<ui:VisualElement name="wave-vignette" class="wave-vignette"
                  picking-mode="Ignore" />
```

### 3. `VillageHud.uss` — vignette styles

UI Toolkit does not support radial-gradient in USS as of Unity 6. Use a
pre-baked `vignette.png` sprite (see §3a) or the shader approach (§3b).

**Recommended: §3a (sprite)**

```uss
.wave-vignette {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;

    /* vignette.png: 64×64 RGBA, transparent centre, opaque red edges.
       Unity scales it to fill the screen.                          */
    background-image: url("project://database/Assets/_Modules/HUD/vignette.png");
    -unity-background-scale-mode: stretch-to-fill;

    opacity: 0;       /* hidden by default; driven by C# each frame */
    display: flex;
    /* pointer events already blocked by picking-mode="Ignore"      */
}
```

**§3a — create `vignette.png`**

A 64×64 RGBA PNG with a radial gradient: centre pixel fully transparent
(`rgba(0,0,0,0)`), edge pixels danger-red (`rgba(200,30,30,255)`).
Create once with any paint tool or the editor script in §3c.

**§3c — one-time editor script to generate the sprite**

```csharp
// Assets/Editor/VignetteTextureGen.cs — run via Tools > Generate Vignette
using UnityEditor;
using UnityEngine;

public static class VignetteTextureGen
{
    [MenuItem("Tools/Generate Vignette")]
    public static void Generate()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 centre = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist  = centre.magnitude;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), centre);
            float t    = Mathf.Clamp01(dist / maxDist);
            // Ease the falloff: more transparent at centre, steeper near edge
            t = t * t;
            tex.SetPixel(x, y, new Color(0.78f, 0.12f, 0.12f, t));
        }
        tex.Apply();

        string path = "Assets/_Modules/HUD/vignette.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        Debug.Log("[VignetteTextureGen] Saved vignette to " + path);
    }
}
```

Run **Tools > Generate Vignette** once, then the USS `background-image`
URL will resolve at runtime.

### 4. `VillageHudController.cs` — new setters

**Add fields + setters:**

```csharp
// ── Wave-imminent vignette ──────────────────────────────────────────────
private const string VignetteElName = "wave-vignette";
private VisualElement _vignetteEl;

// In BindElements():
_vignetteEl = _root.Q<VisualElement>(VignetteElName);
if (_vignetteEl != null) _vignetteEl.style.opacity = 0f;

/// <summary>
/// Shows or hides the wave-imminent vignette overlay.
/// Called by WaveImminentDirector.
/// </summary>
public void SetWaveImminent(bool active)
{
    if (_vignetteEl == null) return;
    if (!active) _vignetteEl.style.opacity = 0f;
    // Opacity while active is driven each frame via SetVignetteAlpha.
}

/// <summary>
/// Sets the vignette overlay opacity directly. Called each frame
/// during the alert; alpha 0 = hidden, 0.6 = full danger glow.
/// </summary>
public void SetVignetteAlpha(float alpha)
{
    if (_vignetteEl == null) return;
    _vignetteEl.style.opacity = alpha;
}

// ── Compass all-arms imminent flash ────────────────────────────────────

private bool _compassImminent;

/// <summary>
/// When true, all four compass arms flash amber (overriding per-direction
/// colours) to signal the wave is about to launch.
/// Called by WaveImminentDirector; reverted on wave start.
/// </summary>
public void SetCompassImminent(bool active)
{
    _compassImminent = active;
    Label[] arms = { _compassN, _compassE, _compassS, _compassW };
    foreach (var arm in arms)
    {
        if (arm == null) continue;
        if (active)
        {
            arm.text = "⚔";
            arm.EnableInClassList("compass-arm--active",  false);
            arm.EnableInClassList("compass-arm--inbound", true);
        }
        else
        {
            // Revert to dim — SetAttackDirections() will re-apply correct state.
            arm.text = "·";
            arm.EnableInClassList("compass-arm--inbound", false);
        }
    }
}
```

**Update `Update()` — pulse imminent arms at double speed:**

```csharp
private void Update()
{
    // ... existing toast hide timer + compass pulse ...

    if (_compassImminent)
    {
        // Double-speed amber pulse (2 Hz) during imminent phase.
        float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) * 0.25f) + 0.75f;
        PulseCompassArm(_compassN, pulse, forceActive: true);
        PulseCompassArm(_compassE, pulse, forceActive: true);
        PulseCompassArm(_compassS, pulse, forceActive: true);
        PulseCompassArm(_compassW, pulse, forceActive: true);
    }
}

// Update PulseCompassArm to accept a forceActive override:
private static void PulseCompassArm(Label arm, float alpha, bool forceActive = false)
{
    if (arm == null) return;
    if (!forceActive && !arm.ClassListContains("compass-arm--active") &&
        !arm.ClassListContains("compass-arm--inbound")) return;
    arm.style.opacity = alpha;
}
```

### 5. `AbilityAudioBridge.cs` — add `PlayDangerSting()`

```csharp
/// <summary>
/// Plays the wave-imminent danger sting synthesized by ProceduralSfx.
/// Called by WaveImminentDirector via reflection (cross-asmdef safe).
/// </summary>
public static void PlayDangerSting()
{
    Resolve();
    if (s_instanceProp == null || s_playSfx == null) return;
    object inst = s_instanceProp.GetValue(null);
    if (inst == null) return;
    AudioClip sting = ProceduralSfx.DangerSting();
    if (sting == null) return;
    try { s_playSfx.Invoke(inst, new object[] { sting, 0.65f }); }
    catch { /* best-effort */ }
}
```

### 6. `ProceduralSfx.cs` — add `DangerSting()`

```csharp
private static AudioClip s_dangerSting;

/// <summary>
/// Synthesizes (once) a low descending danger sting:
/// 80 Hz → 40 Hz over 0.7 s with a heavy noise layer — ominous,
/// non-musical, unmistakably "something bad is coming."
/// Prefer drop-in: Resources/Sfx/danger_sting.wav if present.
/// </summary>
public static AudioClip DangerSting()
{
    if (s_dangerSting != null) return s_dangerSting;
    s_dangerSting = Resources.Load<AudioClip>("Sfx/danger_sting")
                 ?? Synthesize("sfx_danger_sting",
                        dur:   0.70f,
                        f0:    80f,
                        f1:    38f,    // descend to near-bass rumble
                        noise: 0.60f,
                        amp:   0.80f);
    return s_dangerSting;
}
```

**Drop-in path**: `Assets/Resources/Sfx/danger_sting.wav`
Recommended source: a short 0.5–0.8 s low horn stab, war drum impact, or
sub-bass pulse from freesound.org (CC0). The synthesized fallback sounds
like a low rumbling thud, which is functional but less dramatic.

### 7. Wire in `VillageSceneBuilder.cs`

```csharp
// In VillageSceneBuilder — add alongside WaveClearDirector and CompassDirectionBridge:
root.AddComponent<WaveImminentDirector>();
```

---

## Alert Threshold Tuning

`_alertThreshold` is a serialized float (default `3f` seconds). Inspector-
adjustable so the designer can tune the feel without a recompile:

- `3 s` — tight, punchy (recommended for Hard difficulty pacing)
- `5 s` — gives more build-up, good for first wave tutorial
- `0 s` — effectively disables the alert (useful in debug/QA)

If per-wave tuning is desired later, `WaveDef` can carry an
`ImminentThreshold` field; `WaveImminentDirector` reads it from
`WaveManager` alongside the countdown.

---

## Files to Edit / Create

| File | Change |
|---|---|
| `Assets/_Modules/Village/Waves/WaveImminentDirector.cs` | **New** — alert director; listens to `OnCountdownTick` + `OnWaveStarted` |
| `Assets/_Modules/HUD/VillageHud.uxml` | Add `wave-vignette` VisualElement as last child of root |
| `Assets/_Modules/HUD/VillageHud.uss` | Add `.wave-vignette` class |
| `Assets/_Modules/HUD/vignette.png` | **New** — 64×64 radial-gradient sprite (generate via editor script) |
| `Assets/Editor/VignetteTextureGen.cs` | **New** — one-time vignette sprite generator |
| `Assets/_Modules/HUD/VillageHudController.cs` | Bind `wave-vignette`; add `SetWaveImminent()`, `SetVignetteAlpha()`, `SetCompassImminent()`; update `PulseCompassArm()` |
| `Assets/_Modules/Audio/AbilityAudioBridge.cs` | Add `PlayDangerSting()` static method |
| `Assets/_Modules/Audio/ProceduralSfx.cs` | Add `DangerSting()` synthesis + drop-in load |
| `Assets/Editor/VillageSceneBuilder.cs` | Add `WaveImminentDirector` to village root |

---

## Acceptance Criteria

- [ ] At `_alertThreshold` seconds remaining (default 3 s), screen edges ignite red
- [ ] Vignette breathes (35%–60% opacity) at 1 Hz for the duration of the alert
- [ ] All four compass arms switch to amber `⚔` and pulse at 2 Hz during alert
- [ ] Danger sting audio plays once at alert start (low rumble / horn stab)
- [ ] On iOS/Android: double-pulse vibration fires at alert start
- [ ] When wave spawns (`OnWaveStarted` fires): vignette fades out over 0.5 s
- [ ] Compass arms revert to WO-39 per-direction colours after vignette fades
- [ ] Between-wave period (no countdown): vignette fully hidden, no pulse
- [ ] No alert fires during the very first game load (before `BeginLoop()`)
- [ ] `_alertThreshold = 0` disables the effect entirely (QA/debug use)
- [ ] No scene re-bake required — `WaveImminentDirector` is a runtime component
