# WORK ORDER 38 — Wave Complete: Victory Audio, Celebration Banner, Auto-Repair

**Status:** CLOSED — SUPERSEDED by WO-83 (owner-approved sweep 2026-08-09: WO-83 RESULT covers celebration + combo; auto-repair residue unverified)
**Date:** 2026-05-26
**Author:** Owner design spec — playtest feedback
**Priority:** High — wave clear currently has zero feedback; the player has no
              sense of accomplishment, no prompt to tend repairs, and the
              `victory.mp3` clip that exists in AudioService is never played

---

## Owner Direction

> "On success of completing wave there was an audio file to play. Please see in
> notes and connect on wave complete with an on-screen visual. Creative about how
> the wave was repelled — please make sure everyone is safe and tend to any repairs
> if needed. Should feel like an accomplishment."

Three deliverables:

1. **Victory audio** — `victory.mp3` crossfades in on wave clear (already wired
   in `AudioService`; never triggered from `WaveManager`)
2. **Wave-cleared celebration banner** — on-screen visual in the bible voice /
   lantern palette; reads as earned accomplishment, not a game-show pop-up
3. **Post-wave auto-repair check** — after the banner, automatically surface any
   damaged walls with a repair prompt so the player tends to Avalon before the
   next wave

---

## Existing hooks to wire

| System | Already exists | Missing |
|---|---|---|
| `WaveManager.OnWaveCleared` | `WaveNumberEvent` fired in `CompleteWave()` | Nobody listens to it |
| `AudioService.PlayMusic(MusicTrack.Victory)` | `_victoryClip` serialized; `PlayMusic()` wired | Never called on wave clear |
| `audio-mix-spec §3` | `battle → victory`: hard cut + 200ms fade-in | Not triggered |
| `VillageHudController` | Repair prompt display path | Not called post-wave |
| `WallRepairController` | Finds + prices wall damage | Not called post-wave |

---

## Fix — Part 1: Play Victory Music

### New file: `Assets/_Modules/Village/Waves/WaveClearDirector.cs`

This MonoBehaviour listens to `WaveManager.OnWaveCleared` and coordinates the
three post-wave beats in sequence: audio → banner → repair check.

```csharp
/// <summary>
/// Orchestrates the wave-clear sequence:
///   Beat 1 (0 s)   — victory.mp3 plays (hard cut per audio-mix-spec §3).
///   Beat 2 (0.2 s) — WaveClearBanner slides in with flavour text.
///   Beat 3 (3.8 s) — Banner fades; auto-repair scan runs if walls are damaged.
///   Beat 4 (4.5 s) — Village music resumes at 0.4 mix volume.
///
/// Wired by VillageSceneBuilder (or the integrator) — attaches this component
/// to the village root, sets HUD + WallRepairController refs.
/// </summary>
[DisallowMultipleComponent]
public sealed class WaveClearDirector : MonoBehaviour
{
    [SerializeField] private WaveManager            _waveManager;
    [SerializeField] private VillageHudController   _hud;

    private void Start()
    {
        if (_waveManager == null)
            _waveManager = FindObjectOfType<WaveManager>();
        if (_waveManager != null)
            _waveManager.OnWaveCleared.AddListener(OnWaveCleared);
    }

    private void OnDestroy()
    {
        if (_waveManager != null)
            _waveManager.OnWaveCleared.RemoveListener(OnWaveCleared);
    }

    private void OnWaveCleared(int waveNumber)
    {
        StartCoroutine(WaveClearSequence(waveNumber));
    }

    private System.Collections.IEnumerator WaveClearSequence(int waveNumber)
    {
        // Beat 1 — Victory audio (hard cut into victory.mp3).
        PlayVictoryMusic();

        // Beat 2 (0.2 s delay — let the first bar of victory.mp3 hit).
        yield return new WaitForSeconds(0.2f);
        ShowClearBanner(waveNumber);
        SpawnHeartCelebrationVfx();

        // Beat 3 (3.8 s dwell — banner stays while the sting plays).
        yield return new WaitForSeconds(3.8f);
        HideClearBanner();

        // Beat 4 — Repair check, then return to village music.
        yield return new WaitForSeconds(0.4f);
        RunRepairCheck();

        yield return new WaitForSeconds(1.2f);
        PlayVillageMusic();  // crossfade back from victory → village
    }
}
```

**Audio calls** (reflection bridge to `DeNelle.Audio.AudioService`, same pattern
as `AbilityAudioBridge`):

```csharp
private static void PlayVictoryMusic()
{
    InvokeAudioService("PlayMusic", MusicTrackEnum("Victory"));
}
private static void PlayVillageMusic()
{
    InvokeAudioService("PlayMusic", MusicTrackEnum("Village"));
}
```

Use the reflection bridge (no cross-asmdef reference) OR add
`WaveClearDirector.cs` to the `DeNelle.Village` asmdef which already holds a
reference to `DeNelle.Audio` via `AudioBootstrap`. If `DeNelle.Village` already
references `DeNelle.Audio`, call `AudioService.Instance.PlayMusic(MusicTrack.Victory)`
directly. Check `Village.asmdef` for the existing reference.

---

## Fix — Part 2: Wave Clear Celebration Banner

### `WaveClearBanner.cs` — UI Toolkit overlay, built in code

The banner builds directly into the HUD root at runtime (no new UXML required).
It displays for ~4 seconds then slides off to the right.

**Visual design (bible voice / lantern palette)**:

```
┌─────────────────────────────────────────────────────┐
│                                                     │
│    ⚔  WAVE 3 REPELLED  ⚔                          │
│                                                     │
│  "The Hollow Ones are driven back from the gates.  │
│   Avalon holds — for now."                          │
│                                                     │
│  Enemies defeated: 12   Crystals earned: +45       │
│                                                     │
└─────────────────────────────────────────────────────┘
```

Tone rules:
- Title: `"WAVE {N} REPELLED"` — plain military dispatch, not gamey
- Flavour line: rotated per wave from the `WaveFlavourLines` table below
- Stats: enemies defeated + crystals earned (read from `GameState`)
- Colors: warm amber `#f5a623` title + `#e8d8b0` body text on dark `#1a1a12` background at 92% opacity
- Entry: slides in from the right edge (translate X: +100% → 0) over 0.3 s
- Exit: fades out opacity 1 → 0 over 0.4 s

```csharp
private static readonly string[] WaveFlavourLines = new[]
{
    // Wave 1
    "The Hollow Ones do not tire. But tonight, Avalon holds.",
    // Wave 2
    "They came in silence and left in ruin. Tend the Heart.",
    // Wave 3
    "Three waves broken against the walls. The dark grows patient.",
    // Wave 4
    "The lanterns still burn. That is enough.",
    // Wave 5+
    "The gates held. See to the walls before the next tide comes.",
};

private static string FlavourFor(int waveNumber)
    => WaveFlavourLines[Mathf.Clamp(waveNumber - 1, 0, WaveFlavourLines.Length - 1)];
```

**USS for the banner** (add to `VillageHud.uss`):

```uss
.wave-clear-banner {
    position: absolute;
    bottom: 22%;
    left: 10%;
    right: 10%;
    background-color: rgba(26, 26, 18, 0.92);
    border-radius: 12px;
    padding: 24px 32px;
    border-width: 1px;
    border-color: rgba(245, 166, 35, 0.6);
}
.wave-clear-title {
    font-size: 28px;
    color: rgb(245, 166, 35);
    -unity-font-style: bold;
    -unity-text-align: middle-center;
    margin-bottom: 8px;
}
.wave-clear-flavour {
    font-size: 15px;
    color: rgb(232, 216, 176);
    -unity-text-align: middle-center;
    white-space: normal;
    margin-bottom: 12px;
}
.wave-clear-stats {
    font-size: 13px;
    color: rgb(170, 160, 130);
    -unity-text-align: middle-center;
}
```

**Implementation in `WaveClearDirector.ShowClearBanner()`**:

```csharp
private void ShowClearBanner(int waveNumber)
{
    var hud = _hud?.GetComponent<UIDocument>();
    if (hud?.rootVisualElement == null) return;
    var root = hud.rootVisualElement;

    // Remove any leftover banner from a previous wave.
    root.Q("wave-clear-banner")?.RemoveFromHierarchy();

    var banner = new VisualElement { name = "wave-clear-banner" };
    banner.AddToClassList("wave-clear-banner");

    var title = new Label($"⚔  WAVE {waveNumber} REPELLED  ⚔");
    title.AddToClassList("wave-clear-title");

    var flavour = new Label(FlavourFor(waveNumber));
    flavour.AddToClassList("wave-clear-flavour");

    // Crystal + enemy stats from GameState.
    int crystals = GameStateService.Instance?.State?.Resources?.Crystals ?? 0;
    var stats = new Label($"Crystals held: ◆ {crystals}");
    stats.AddToClassList("wave-clear-stats");

    banner.Add(title);
    banner.Add(flavour);
    banner.Add(stats);
    root.Add(banner);

    // Slide-in animation: translate right → centre.
    banner.style.translate = new Translate(Length.Percent(110f), 0f, 0f);
    banner.schedule.Execute(() =>
    {
        banner.style.transitionProperty = new StyleList<StylePropertyName>(
            new List<StylePropertyName> { new StylePropertyName("translate") });
        banner.style.transitionDuration = new StyleList<TimeValue>(
            new List<TimeValue> { new TimeValue(300, TimeUnit.Millisecond) });
        banner.style.translate = new Translate(0f, 0f, 0f);
    }).ExecuteLater(16);  // one frame delay so the initial position applies first
}

private void HideClearBanner()
{
    var hud = _hud?.GetComponent<UIDocument>();
    var banner = hud?.rootVisualElement?.Q("wave-clear-banner");
    if (banner == null) return;

    // Fade out opacity.
    banner.style.transitionProperty = new StyleList<StylePropertyName>(
        new List<StylePropertyName> { new StylePropertyName("opacity") });
    banner.style.transitionDuration = new StyleList<TimeValue>(
        new List<TimeValue> { new TimeValue(400, TimeUnit.Millisecond) });
    banner.style.opacity = 0f;
    banner.schedule.Execute(() => banner.RemoveFromHierarchy()).ExecuteLater(450);
}
```

---

## Fix — Part 3: Heart Celebration VFX

A brief warm-amber light pulse + rising particle column at the Heart (`Elarion`)
after each wave — signals that the Heart is safe and the village endures.

```csharp
private void SpawnHeartCelebrationVfx()
{
    // Find the Heart object via HeartController.
    var heart = FindObjectOfType<HeartController>();
    if (heart == null) return;

    Vector3 pos = heart.transform.position;

    // Rising column of warm amber particles (like candle sparks ascending).
    var host = new GameObject("WaveClearVFX");
    host.transform.position = pos;

    var col = host.AddComponent<ParticleSystem>();
    var m = col.main;
    m.loop = false; m.duration = 2.0f;
    m.startLifetime = 1.8f;
    m.startSpeed = 0f;
    m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
    m.gravityModifier = -0.06f;
    m.maxParticles = 60;
    m.simulationSpace = ParticleSystemSimulationSpace.World;
    m.stopAction = ParticleSystemStopAction.None;

    var em = col.emission; em.rateOverTime = 25f;

    var sh = col.shape; sh.enabled = true;
    sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.8f;

    var vel = col.velocityOverLifetime; vel.enabled = true;
    vel.space = ParticleSystemSimulationSpace.World;
    vel.x = new ParticleSystem.MinMaxCurve(0f);
    vel.y = new ParticleSystem.MinMaxCurve(2.2f);
    vel.z = new ParticleSystem.MinMaxCurve(0f);

    // Amber → warm white COL.
    var colLife = col.colorOverLifetime; colLife.enabled = true;
    var g = new Gradient();
    Color amber = new Color(1f, 0.72f, 0.12f);
    Color warmWhite = new Color(1f, 0.92f, 0.7f);
    g.SetKeys(
        new[] { new GradientColorKey(amber, 0f), new GradientColorKey(warmWhite, 0.5f), new GradientColorKey(amber, 1f) },
        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
    colLife.color = new ParticleSystem.MinMaxGradient(g);

    // Warm point-light flash at the Heart base.
    var lightGo = new GameObject("CelebLight");
    lightGo.transform.SetParent(host.transform, false);
    lightGo.transform.position = pos + Vector3.up * 0.5f;
    var l = lightGo.AddComponent<Light>();
    l.type = LightType.Point; l.color = amber;
    l.intensity = 5f; l.range = 8f; l.shadows = LightShadows.None;
    lightGo.AddComponent<VfxLightFade>().FadeTime = 1.8f;

    col.Play();
    Destroy(host, 2.8f);
}
```

`VfxLightFade` is already defined in `AbilityVfxKit.cs` — reuse it directly.

---

## Fix — Part 4: Post-Wave Auto-Repair Check

After the banner fades, `WaveClearDirector` calls `WallRepairController` via
reflection to check for and surface wall damage:

```csharp
private void RunRepairCheck()
{
    // Reflection bridge — same pattern as BuildMenu.InvokeRepairNearestWall().
    System.Type t = null;
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
    {
        t = asm.GetType("DeNelle.Village.WallRepairController", false);
        if (t != null) break;
    }
    if (t == null) return;

    var ctrl = FindObjectOfType(t) as Component;
    if (ctrl == null) return;

    // CheckAndShowRepairPrompt: if any wall is below 100% HP, surfaces the
    // repair prompt via HUD exactly like pressing "Repair Wall" from BuildMenu.
    // Add this method to WallRepairController (see §5 below).
    var check = t.GetMethod("CheckAndShowRepairPrompt");
    check?.Invoke(ctrl, null);
}
```

### New method on `WallRepairController`: `CheckAndShowRepairPrompt()`

```csharp
/// <summary>
/// Post-wave repair pass: if any wall segment is below full HP, automatically
/// selects the most-damaged one and surfaces the repair prompt via the HUD.
/// Called by WaveClearDirector after the wave-clear banner fades.
/// If all walls are at full HP, logs "All walls are holding — no repair needed."
/// and returns without showing a prompt.
/// </summary>
public void CheckAndShowRepairPrompt()
{
    var damaged = FindMostDamagedWall();
    if (damaged == null)
    {
        Debug.Log("[WallRepairController] All walls holding — no repair needed after wave clear.");
        return;
    }
    // Re-use existing ShowRepairPrompt flow (the same path as the player
    // manually selecting a wall and pressing F).
    SelectWall(damaged);
    // The existing ShowRepairPrompt call in WallRepairController will push to
    // the HUD via the already-wired VillageHudController.ShowRepairPrompt path.
}

private WallSegment FindMostDamagedWall()
{
    WallSegment worst = null;
    float worstFraction = 1f;
    foreach (var w in FindObjectsOfType<WallSegment>())
    {
        if (w == null || w.MaxHp <= 0) continue;
        float frac = (float)w.CurrentHp / w.MaxHp;
        if (frac < worstFraction) { worstFraction = frac; worst = w; }
    }
    return (worstFraction < 1f) ? worst : null;
}
```

---

## Wiring in `VillageSceneBuilder`

Add `WaveClearDirector` to the village scene root:

```csharp
// VillageSceneBuilder.BuildVillageRoot() or BuildHero() or equivalent:
var director = root.AddComponent<WaveClearDirector>();
// WaveManager and VillageHudController are discovered at runtime by FindObjectOfType
// if not set — no hard serialized ref needed at bake time.
```

---

## Files to Edit / Create

| File | Change |
|---|---|
| `Assets/_Modules/Village/Waves/WaveClearDirector.cs` | **New** — orchestrates the full wave-clear sequence (audio → banner → VFX → repair) |
| `Assets/_Modules/Village/Buildings/UI/WallRepairController.cs` | Add `CheckAndShowRepairPrompt()` + `FindMostDamagedWall()` |
| `Assets/_Modules/HUD/VillageHud.uss` | Add `.wave-clear-banner`, `.wave-clear-title`, `.wave-clear-flavour`, `.wave-clear-stats` classes |
| `Assets/Editor/VillageSceneBuilder.cs` | Add `WaveClearDirector` component to village root in `BuildExterior()` or village root setup |

---

## Audio Spec Cross-Reference

Per `docs/audio-mix-spec.md §3`:
- `battle → victory`: hard cut + 200ms fade-in on victory ✓ (the `WaveClearDirector` calls `PlayMusic(Victory)` which uses the `AudioService` crossfade logic — 200ms fade-in is the `victory` track's configured `fadeInMs`)
- `victory → village`: crossfade 1000ms ✓ (called at Beat 4, 4.5 s after the wave clear)
- Victory volume: **0.7** (already in `AudioService` registry), no loop ✓

---

## Acceptance Criteria

- [ ] Wave clears → `victory.mp3` plays within 0.2 s (hard cut, no fade delay)
- [ ] Wave-clear banner appears within 0.5 s of wave end, displays wave number + flavour line + crystal count
- [ ] Banner flavour text is in the bible voice (quiet, earned, not gamey)
- [ ] Amber particle celebration column rises from the Heart for ~2 s
- [ ] Banner auto-hides after ~4 s
- [ ] Village music (`village.mp3`) resumes after ~5.5 s total
- [ ] If any wall is below full HP after the wave, the repair prompt appears automatically
- [ ] If all walls are at full HP, no repair prompt appears (logs "All walls holding")
- [ ] No regressions: existing Build button + Start Wave button still work
- [ ] No scene re-bake required
