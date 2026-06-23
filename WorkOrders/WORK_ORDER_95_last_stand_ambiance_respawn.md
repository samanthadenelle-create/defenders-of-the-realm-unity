# WORK ORDER 95 — Last Stand Scene: Ambiance, Blaise Idle Pose, Enemy Respawn Loop

**Status:** BUG FIX — READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small–Medium — scene lighting, animator fix, respawn guard
**Observed:** Scene renders as a near-black void. Blaise stands in wrong idle
             pose ("Look at Pose" stub). Enemy respawns indefinitely until
             condition is met, creating an unintended infinite-combat loop.

---

## Bug 1 — Scene is a Black Void (No Ambiance)

### Root Cause

The Last Stand scene has no environment setup:
- No Directional Light (or it was deleted/disabled)
- Skybox is unassigned (renders black)
- Global Volume either absent or has zero intensity
- No scene backdrop — characters float in darkness

### Fix

#### Step 1 — Add Directional Light

In the Last Stand scene:
1. **GameObject → Light → Directional Light**
2. Settings:
   ```
   Rotation:       X=45, Y=30, Z=0
   Color:          #FFE8C0  (warm amber — dungeon torch feel)
   Intensity:      1.2
   Shadow Type:    Soft Shadows
   ```

#### Step 2 — Add fill light (optional but recommended)

Add a second Directional Light as a soft fill from below:
```
Rotation:   X=-30, Y=180, Z=0
Color:      #3040A0  (cool blue-purple underlight)
Intensity:  0.35
Shadows:    None
```

#### Step 3 — Skybox / Background Color

For a dungeon Last Stand feel, use a solid color background rather than a
skybox:
1. Select the Main Camera → `Clear Flags = Solid Color`
2. `Background Color = #0A060E` (very dark violet-black)

Or assign a dark gradient skybox material:
```
Window → Rendering → Lighting → Environment → Skybox Material
→ Assign: Assets/Materials/Skyboxes/DungeonNight.mat (create if needed)
```

Simple dark gradient skybox material (create as `DungeonNight.mat`):
```
Shader: Skybox/Gradient
Top Color:    #050308
Bottom Color: #1A0A2E
```

#### Step 4 — Point Lights near characters

Add two Point Lights for character illumination:
```
Light A (near Blaise):
    Position:   Blaise.transform.position + Vector3(0, 2, -1)
    Color:      #FFA050
    Intensity:  2.0
    Range:      6.0

Light B (near enemy spawn):
    Position:   enemy spawn point + Vector3(0, 2, -1)
    Color:      #FF3020  (enemy danger red)
    Intensity:  1.5
    Range:      5.0
```

#### Step 5 — Ambient light

```
Window → Rendering → Lighting → Environment
    Source:         Color
    Ambient Color:  #1A0A1A
    Intensity:      0.3
```

---

## Bug 2 — Blaise Wrong Idle Pose ("Look at Pose")

### Root Cause

Blaise's Animator Controller has its **default state** set to an empty "New
State" or a pose state rather than an Idle animation clip. Unity plays the
first frame of whatever the default state references — if it's a "LookAt"
or a bind pose with no clip, Blaise freezes in a T-pose or stares forward
rigidly.

### Fix

#### Step 1 — Find Blaise's Animator Controller

```
Select Blaise root GameObject → Inspector → Animator → Controller
Double-click Controller to open Animator window
```

#### Step 2 — Set correct default state

In the Animator window:
1. Right-click the **Idle** state (or create one if missing)
2. **Set as Layer Default State**
3. Confirm the Idle state has an animation clip assigned:
   ```
   State: Idle
   Motion: [assign: Blaise_Idle.anim or Humanoid_Idle.anim]
   Speed: 1.0
   ```

If no Idle clip exists, use a generic humanoid idle from the project's
animation library or Unity's built-in Humanoid avatars.

#### Step 3 — Verify transition from Entry

The Animator graph should be:
```
[Entry] → [Idle]   (default, no condition)
[Idle]  → [Attack] (trigger: "Attack")
[Idle]  → [Heal]   (trigger: "Heal")
[Any State] → [Death] (trigger: "Death")
```

Remove or disconnect any "LookAt" / "New State" that is the current default.

#### Step 4 — Check Avatar mask

If Blaise uses a Humanoid avatar, confirm:
```
Animator → Avatar = BlaiseAvatar (or Humanoid)
Apply Root Motion = false (combat scene — use transform movement)
```

---

## Bug 3 — Enemy Respawns Infinitely (Respawn Loop)

### Root Cause

The Last Stand scene has a respawn condition that fires again immediately
after an enemy dies, before the victory check runs. This happens when:

1. `EnemyHealth.Die()` calls a respawn method before `BattleResultHandler.OnVictory()`
2. A `WaveManager` or spawn coroutine in the Last Stand scene re-queues enemies
   without checking whether the battle is already won
3. `_combatActive` is still true after the enemy dies, causing the AI to
   respawn the next wave

### Fix

#### Step 1 — Guard respawn behind combat-active flag

In whatever spawns the Last Stand enemy (likely `LastStandSceneController.cs`
or a spawn method in `ATBCombatManager`):

```csharp
public void SpawnNextEnemy()
{
    // Do not spawn if combat is over
    if (!_combatActive) return;
    if (_battleComplete) return;

    // ... existing spawn logic
}
```

#### Step 2 — Set `_battleComplete` on victory

In `EnemyHealth.Die()` for the Last Stand enemy:

```csharp
private void Die()
{
    _isDead = true;

    // Notify combat manager — this must run BEFORE any respawn trigger
    ATBCombatManager.Instance?.StopCombat();
    FindObjectOfType<BattleResultHandler>()?.OnVictory();

    // Pool return / death VFX AFTER victory notification
    VFXManager.Instance?.Play(VFXType.Death_EnemyExplosion, transform.position);
    ObjectSpawner.Instance?.ReturnToPool(_enemyData, gameObject);
}
```

#### Step 3 — Separate Last Stand enemy from WaveManager

The Last Stand scene should NOT use `WaveManager`. If it currently does,
add a scene-type guard:

```csharp
// In WaveManager.cs
private void Start()
{
    // Don't auto-start in Last Stand scene
    if (SceneManager.GetActiveScene().name == "LastStandScene") return;
    StartNextWave();
}
```

Or use a dedicated `LastStandCombatSequencer.cs` that owns exactly one
enemy at a time and listens to `BattleResultHandler` events.

---

## Scene Setup Checklist

| Step | Action |
|---|---|
| Directional Light | Add to scene, X=45 Y=30, color #FFE8C0, intensity 1.2 |
| Fill Light | Add secondary Directional Light, intensity 0.35, no shadows |
| Camera background | Clear Flags = Solid Color, #0A060E |
| Character Point Lights | Add near Blaise + near enemy spawn |
| Ambient light | Source=Color, #1A0A1A, intensity 0.3 |
| Blaise Animator | Set Idle state as default, assign Idle clip |
| Respawn guard | `if (!_combatActive) return` before SpawnNextEnemy |
| WaveManager guard | Skip auto-start in LastStand scene |

---

## Files to Create / Edit

| File | Action |
|---|---|
| Last Stand scene | **Edit** — add Directional Lights, Point Lights, ambient settings |
| Blaise Animator Controller | **Edit** — set Idle as default state, assign Idle clip |
| `LastStandSceneController.cs` | **Edit** — add `_battleComplete` guard to SpawnNextEnemy |
| `ATBCombatManager.cs` | **Edit** — `StopCombat()` sets `_combatActive = false` |
| `WaveManager.cs` | **Edit** — skip auto-start when scene is LastStand |

---

## Acceptance Criteria

- [ ] Last Stand scene has visible lighting — characters are clearly lit
- [ ] Camera background is dark (dungeon feel), not default Unity grey
- [ ] Blaise plays an Idle animation on combat scene load — no "frozen pose"
- [ ] Defeating the enemy does NOT immediately respawn a new enemy
- [ ] `ATBCombatManager._combatActive = false` after enemy dies
- [ ] After enemy dies, scene transitions to Village (via WO-94 BattleResultHandler)
- [ ] No `[WaveManager]` spawn calls trigger during Last Stand combat
