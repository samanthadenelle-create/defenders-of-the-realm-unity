# WORK ORDER 39 — Compass: Enemy Attack Direction Indicator

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-26
**Author:** Owner design spec — playtest feedback
**Priority:** High — player has no spatial awareness of which gate is under attack;
              all four `WaveSpawnPoint` cardinal directions are already in the scene,
              just not surfaced to the HUD

---

## Owner Direction

> "The compass should show a symbol for which direction enemy is attacking from."

A compass rose lives in the top-centre HUD (the VillageHudController comment
at line 300 already says "just under the wave timer / compass"). The four
N / E / S / W arms light up with a danger symbol whenever enemies from that
spawn point are alive on the field.

---

## Existing Hooks

| System | Already exists | What's missing |
|---|---|---|
| `WaveSpawnPoint.Direction` | "north" / "east" / "south" / "west" per spawn marker | Never read by the HUD |
| `WaveManager._liveEnemies` | `HashSet<Enemy>` of all live enemies on the field | No per-direction breakdown pushed to HUD |
| `WaveManager.OnWaveStarted` | `WaveNumberEvent` fired at wave start | Doesn't carry direction info |
| `VillageHudController` top-centre area | Wave countdown pill sits there | Compass widget not yet added |
| `VillageHud.uxml` | `wave-countdown-timer` at top-centre | No compass element |

---

## Design

### Visual layout

```
          ⚔  N  ⚔
    ⚔  W  ◈  E  ⚔
          ⚔  S  ⚔
```

- A small diamond/rose shape centred at the top of the HUD (below the wave pill)
- Each arm (N / E / S / W) shows a **skull symbol `☠`** when enemies from that
  direction are alive, or a **dim dot `·`** when clear
- Active arms **pulse** (opacity 1 → 0.5 → 1, 1 Hz) so they catch the eye
  without demanding it
- All four arms dim between waves (no active enemies)

### Colour coding

| State | Symbol | Color |
|---|---|---|
| Active (enemies alive) | `☠` | Danger red `#e84b4a` |
| Spawning this wave (enemies inbound but not yet alive) | `⚔` | Amber `#f5a623` |
| Clear (no enemies this direction) | `·` | Muted `#3a3050` |
| Between waves (all clear) | `·` | `#2a2040` |

---

## Implementation

### 1. Add compass to `VillageHud.uxml`

Insert after the `wave-countdown-timer` block:

```xml
<!-- Compass rose — N/E/S/W arms light up when enemies attack from that gate -->
<ui:VisualElement name="compass-rose" class="compass-rose" picking-mode="Ignore">
    <ui:VisualElement name="compass-row-n" class="compass-row compass-row--north">
        <ui:Label name="compass-n" text="·" class="compass-arm" />
    </ui:VisualElement>
    <ui:VisualElement name="compass-row-mid" class="compass-row compass-row--mid">
        <ui:Label name="compass-w" text="·" class="compass-arm" />
        <ui:Label name="compass-centre" text="◈" class="compass-centre" />
        <ui:Label name="compass-e" text="·" class="compass-arm" />
    </ui:VisualElement>
    <ui:VisualElement name="compass-row-s" class="compass-row compass-row--south">
        <ui:Label name="compass-s" text="·" class="compass-arm" />
    </ui:VisualElement>
</ui:VisualElement>
```

### 2. Add USS to `VillageHud.uss`

```uss
.compass-rose {
    position: absolute;
    top: 56px;          /* sits just below the wave-countdown-timer pill */
    left: 50%;
    translate: -50% 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0px;
}
.compass-row {
    display: flex;
    flex-direction: row;
    align-items: center;
    justify-content: center;
    gap: 4px;
}
.compass-arm {
    font-size: 18px;
    color: rgba(58, 48, 80, 1);    /* dim default */
    width: 22px;
    -unity-text-align: middle-center;
    transition-property: color;
    transition-duration: 0.2s;
}
.compass-arm--active {
    color: rgba(232, 75, 74, 1);   /* danger red */
}
.compass-arm--inbound {
    color: rgba(245, 166, 35, 1);  /* amber */
}
.compass-centre {
    font-size: 14px;
    color: rgba(100, 85, 130, 1);
    width: 22px;
    -unity-text-align: middle-center;
}
```

### 3. `VillageHudController` — bind compass + add setter

**Bind in `BindElements()`:**

```csharp
// Compass arms
private const string CompassNName = "compass-n";
private const string CompassEName = "compass-e";
private const string CompassSName = "compass-s";
private const string CompassWName = "compass-w";

private Label _compassN, _compassE, _compassS, _compassW;

// In BindElements():
_compassN = _root.Q<Label>(CompassNName);
_compassE = _root.Q<Label>(CompassEName);
_compassS = _root.Q<Label>(CompassSName);
_compassW = _root.Q<Label>(CompassWName);
```

**New public setter:**

```csharp
/// <summary>
/// Updates the compass rose to show which cardinal directions have active
/// enemies. Called each frame (or on enemy spawn/death) by the integrator.
/// Each flag: true = enemies alive from that direction, false = clear.
/// </summary>
public void SetAttackDirections(bool north, bool east, bool south, bool west)
{
    SetCompassArm(_compassN, north);
    SetCompassArm(_compassE, east);
    SetCompassArm(_compassS, south);
    SetCompassArm(_compassW, west);
}

private static readonly string[] CompassSymbolActive  = { "☠" };
private static readonly string[] CompassSymbolClear   = { "·" };

private void SetCompassArm(Label arm, bool active)
{
    if (arm == null) return;
    arm.text = active ? "☠" : "·";
    arm.EnableInClassList("compass-arm--active", active);
}
```

### 4. New file: `CompassDirectionBridge.cs`

This MonoBehaviour polls `WaveManager` for live enemy positions each frame and
pushes direction flags to the HUD. Lives in `DeNelle.Village`.

```csharp
/// <summary>
/// Reads live enemy positions from WaveManager each frame and pushes
/// N/E/S/W attack direction flags to VillageHudController.SetAttackDirections.
/// An enemy is "from the north" when its spawn point's Direction == "north"
/// OR (fallback) when its world Z position > HeartZ + threshold.
///
/// Wired by VillageSceneBuilder — attach to the village root alongside
/// WaveManager. Uses the reflection bridge for HUD (cross-asmdef).
/// </summary>
[DisallowMultipleComponent]
public sealed class CompassDirectionBridge : MonoBehaviour
{
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private float       _updateInterval = 0.25f;  // 4 fps — fast enough

    private float _timer;
    // Reflection bridge to VillageHudController.SetAttackDirections
    private System.Reflection.MethodInfo _setDirections;
    private Component _hudController;

    private void Start()
    {
        if (_waveManager == null)
            _waveManager = FindObjectOfType<WaveManager>();
        ResolveHud();
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = _updateInterval;
        PushDirections();
    }

    private void PushDirections()
    {
        if (_waveManager == null || _hudController == null || _setDirections == null) return;

        // Ask WaveManager for its live enemies via the public property.
        // WaveManager exposes LiveEnemyCount; we need per-direction counts.
        // Walk the live enemy set via the new WaveManager.GetLiveEnemiesByDirection()
        // method added in §5 below.
        var dirs = GetDirectionFlags();
        _setDirections.Invoke(_hudController, new object[]
            { dirs[0], dirs[1], dirs[2], dirs[3] });
    }

    // Returns [north, east, south, west] bool flags.
    private bool[] GetDirectionFlags()
    {
        var flags = new bool[4]; // N=0 E=1 S=2 W=3

        // Strategy 1: ask WaveManager for spawn-point-tagged live enemies.
        // (WaveManager.GetActiveDirections() added in §5.)
        System.Type wmt = _waveManager.GetType();
        var m = wmt.GetMethod("GetActiveDirections");
        if (m != null)
        {
            var result = m.Invoke(_waveManager, null) as string[];
            if (result != null)
            {
                foreach (string d in result)
                {
                    if (d == "north") flags[0] = true;
                    else if (d == "east")  flags[1] = true;
                    else if (d == "south") flags[2] = true;
                    else if (d == "west")  flags[3] = true;
                }
                return flags;
            }
        }

        // Strategy 2 (fallback): classify by world position relative to Heart.
        var heart = FindObjectOfType<HeartController>();
        if (heart == null) return flags;
        Vector3 centre = heart.transform.position;
        const float Threshold = 8f;

        foreach (var enemy in FindObjectsOfType<Enemy>())
        {
            if (enemy == null) continue;
            Vector3 d = enemy.transform.position - centre;
            // Cardinal quadrant: whichever axis dominates.
            if (Mathf.Abs(d.z) >= Mathf.Abs(d.x))
            {
                if (d.z >  Threshold) flags[0] = true; // North
                if (d.z < -Threshold) flags[2] = true; // South
            }
            else
            {
                if (d.x >  Threshold) flags[1] = true; // East
                if (d.x < -Threshold) flags[3] = true; // West
            }
        }
        return flags;
    }

    private void ResolveHud()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("DeNelle.HUD.VillageHudController", false);
            if (t == null) continue;
            var inst = FindObjectOfType(t) as Component;
            if (inst == null) break;
            _hudController  = inst;
            _setDirections  = t.GetMethod("SetAttackDirections");
            break;
        }
    }
}
```

### 5. `WaveManager` — add `GetActiveDirections()`

```csharp
/// <summary>
/// Returns the cardinal direction strings of spawn points that currently
/// have live enemies on the field. CompassDirectionBridge calls this each
/// update interval to drive the HUD compass.
/// </summary>
public string[] GetActiveDirections()
{
    var dirs = new System.Collections.Generic.HashSet<string>();
    foreach (var e in _liveEnemies)
    {
        if (e == null) continue;
        // Enemy carries a reference to its spawn point set in SpawnEnemy().
        // Add _spawnDirection field to Enemy (§6).
        if (!string.IsNullOrEmpty(e.SpawnDirection))
            dirs.Add(e.SpawnDirection);
    }
    return new string[dirs.Count];  // populated from HashSet
}
```

### 6. `Enemy.cs` — add `SpawnDirection` field

```csharp
// Enemy.cs — add:
/// <summary>Cardinal direction of the spawn point this enemy came from.
/// Set by WaveManager when the enemy is instantiated.</summary>
public string SpawnDirection { get; private set; }

public void SetSpawnDirection(string direction) => SpawnDirection = direction;
```

In `WaveManager` where enemies are spawned, add:
```csharp
enemy.SetSpawnDirection(spawnPoint.Direction);
```

### 7. Wire in `VillageSceneBuilder`

```csharp
// In VillageSceneBuilder — add alongside WaveClearDirector:
root.AddComponent<CompassDirectionBridge>();
```

---

## Pulse Animation (active arms)

UI Toolkit `transition` handles the color change. For the pulse, drive opacity
via a coroutine in `VillageHudController`:

```csharp
// In VillageHudController — pulse active arms:
private void Update()
{
    // ... existing toast hide timer ...

    // Pulse active compass arms at 1 Hz.
    float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f) * 0.25f) + 0.75f;
    PulseCompassArm(_compassN, pulse);
    PulseCompassArm(_compassE, pulse);
    PulseCompassArm(_compassS, pulse);
    PulseCompassArm(_compassW, pulse);
}

private static void PulseCompassArm(Label arm, float alpha)
{
    if (arm == null) return;
    if (!arm.ClassListContains("compass-arm--active")) return;
    arm.style.opacity = alpha;
}
```

---

## Files to Edit / Create

| File | Change |
|---|---|
| `Assets/_Modules/HUD/VillageHud.uxml` | Add `compass-rose` element block after `wave-countdown-timer` |
| `Assets/_Modules/HUD/VillageHud.uss` | Add `.compass-rose`, `.compass-arm`, `.compass-arm--active`, `.compass-centre` |
| `Assets/_Modules/HUD/VillageHudController.cs` | Bind compass labels; add `SetAttackDirections(bool,bool,bool,bool)`; add pulse in `Update()` |
| `Assets/_Modules/Village/Waves/WaveManager.cs` | Add `GetActiveDirections()` using `_liveEnemies` + enemy spawn direction |
| `Assets/_Modules/Village/Enemies/Enemy.cs` | Add `SpawnDirection` property + `SetSpawnDirection()` |
| `Assets/_Modules/Village/Waves/CompassDirectionBridge.cs` | **New** — polls WaveManager, pushes direction flags to HUD via reflection |
| `Assets/Editor/VillageSceneBuilder.cs` | Add `CompassDirectionBridge` component to village root |

---

## Acceptance Criteria

- [ ] Between waves — all four compass arms show dim `·` dots
- [ ] When wave starts from the north gate — N arm lights up red `☠` and pulses
- [ ] When enemies from multiple gates are alive — all active arms light up simultaneously
- [ ] When last enemy from a direction dies — that arm returns to dim `·` within 0.25 s
- [ ] Compass sits cleanly below the wave countdown pill, above the START WAVE button
- [ ] No scene re-bake required — `CompassDirectionBridge` is a runtime component
