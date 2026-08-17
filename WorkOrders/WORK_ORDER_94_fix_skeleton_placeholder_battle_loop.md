<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 94 — Fix Skeleton Enemy: Purple Capsule Placeholder + Battle Loop Bug

**Status:** BUG FIX — READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Small — assign Skeleton prefab + fix post-battle navigation
**Observed:** Skeleton enemy renders as purple URP default capsule. After defeating
             it, the Last Stand battle restarts the same fight instead of advancing.

---

## Bug 1 — Skeleton Renders as Purple Capsule

### Root Cause

`EnemyData` ScriptableObject for the Skeleton has no prefab assigned (or
points to a missing/renamed prefab), so `ObjectSpawner` falls back to the
default primitive capsule with no material — Unity URP renders this magenta/
purple.

### Fix

#### Step 1 — Locate EnemyData asset

Search for the Skeleton enemy's data asset:
```
Find: Assets/_Data/Enemies/Skeleton.asset   (or similar name)
Grep: "Skeleton" Assets/_Data/Enemies/ --include="*.asset"
```

Open the asset in the Inspector. Confirm:
- `prefab` field → should reference a Skeleton prefab, not null
- If null: assign `Assets/_Modules/Village/Enemies/Prefabs/Skeleton.prefab`
  (or the actual prefab path)

#### Step 2 — Confirm prefab has a material

On the Skeleton prefab:
1. Select the root (or mesh child)
2. Confirm `MeshRenderer.materials[0]` is assigned — not empty/None
3. If the material is missing, assign a placeholder from `Assets/Materials/`
   or create a basic URP Lit material (gray, metallic=0.3)

#### Step 3 — Verify ObjectSpawner registration

In `ObjectSpawner.cs`, `GetOrCreatePool(ItemData data)` uses `data.prefab`.
If `data.prefab == null`, add a guard with a clear error:

```csharp
private ObjectPool<GameObject> GetOrCreatePool(ItemData data)
{
    if (data == null)
    {
        Debug.LogError("[ObjectSpawner] ItemData is null.");
        return null;
    }
    if (data.prefab == null)
    {
        Debug.LogError($"[ObjectSpawner] Prefab is null on ItemData '{data.name}'. " +
            "Assign a prefab in the Inspector.", data);
        return null;
    }
    // ... existing pool creation
}
```

Also guard the `Spawn()` call:
```csharp
public GameObject Spawn(ItemData data, Vector3 position, Quaternion rotation = default)
{
    var pool = GetOrCreatePool(data);
    if (pool == null) return null;
    // ...
}
```

---

## Bug 2 — Battle Loops Back to Same Fight

### Root Cause

After defeating the enemy in Last Stand, the victory handler does not
advance state — it either restarts the same wave or calls
`SceneManager.LoadScene` on the same scene. Common causes:

1. `BattleResultHandler.OnVictory()` (or equivalent) calls
   `SceneManager.LoadScene(SceneManager.GetActiveScene().name)` — reloads
   same scene.
2. The enemy wave index is not incremented before the scene reloads.
3. `LastStandSceneController` has no post-victory navigation wired.

### Fix

#### Step 1 — Find the post-battle handler

```
grep -r "OnVictory\|BattleResult\|combatVictory\|LoadScene\|SceneManager" \
    Assets/ --include="*.cs" -l
```

Likely files: `BattleResultHandler.cs`, `LastStandSceneController.cs`,
`ATBCombatManager.cs`.

#### Step 2 — Canonical post-battle flow

Create or update `BattleResultHandler.cs`:

```csharp
// BattleResultHandler.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleResultHandler : MonoBehaviour
{
    [Header("Scene Names")]
    public string villageSceneName    = "VillageScene";
    public string gameOverSceneName   = "GameOverScene";

    [Header("Timing")]
    public float victoryDisplayTime   = 2.0f;  // Show "Victory!" before transitioning
    public float defeatDisplayTime    = 2.5f;

    // ── Called by ATBCombatManager when enemy HP hits 0 ──────────────────

    public void OnVictory()
    {
        Debug.Log("[BattleResult] Victory — returning to village.");
        StartCoroutine(VictoryRoutine());
    }

    public void OnDefeat()
    {
        Debug.Log("[BattleResult] Defeat — loading game over.");
        StartCoroutine(DefeatRoutine());
    }

    private System.Collections.IEnumerator VictoryRoutine()
    {
        // Optional: show victory panel here
        yield return new WaitForSeconds(victoryDisplayTime);

        // Clear combat state so ATB doesn't carry over
        ATBCombatManager.Instance?.StopCombat();

        // Return to village — NOT reload of current scene
        SceneManager.LoadScene(villageSceneName);
    }

    private System.Collections.IEnumerator DefeatRoutine()
    {
        yield return new WaitForSeconds(defeatDisplayTime);
        ATBCombatManager.Instance?.StopCombat();
        SceneManager.LoadScene(gameOverSceneName);
    }
}
```

#### Step 3 — Add `StopCombat()` to `ATBCombatManager`

```csharp
// Add to ATBCombatManager.cs
public void StopCombat()
{
    _combatActive = false;
    StopAllCoroutines();
}
```

#### Step 4 — Wire `OnVictory()` from enemy death

In `EnemyHealth.Die()` (or wherever the Last Stand enemy is defeated):

```csharp
private void Die()
{
    // ... existing death logic ...

    // Notify battle result handler
    FindObjectOfType<BattleResultHandler>()?.OnVictory();
}
```

Or, if `ATBCombatManager` tracks enemy HP, call from there:

```csharp
// In ATBCombatManager — wherever enemy HP reaches 0
if (_currentEnemyHealth != null && _currentEnemyHealth.currentHealth <= 0)
{
    _combatActive = false;
    FindObjectOfType<BattleResultHandler>()?.OnVictory();
}
```

#### Step 5 — Scene setup

Add `BattleResultHandler` component to the Last Stand scene root GameObject.
Set `villageSceneName` to match the exact name in Build Settings.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Data/Enemies/Skeleton.asset` | **Edit** — assign `prefab` field |
| `Assets/_Modules/Village/Enemies/Prefabs/Skeleton.prefab` | **Edit** — assign material if missing |
| `Assets/_Modules/Village/Enemies/ObjectSpawner.cs` | **Edit** — add null guard on `data.prefab` |
| `Assets/_Modules/LastStand/BattleResultHandler.cs` | **Create** — post-battle navigation |
| `Assets/_Modules/LastStand/ATBCombatManager.cs` | **Edit** — add `StopCombat()`, call `BattleResultHandler.OnVictory()` |
| `EnemyHealth.cs` | **Edit** — call `BattleResultHandler.OnVictory()` in `Die()` |
| Last Stand scene root | **Edit** — add `BattleResultHandler` component, set scene names |

---

## Acceptance Criteria

- [ ] Skeleton enemy renders with its correct mesh and material (no purple capsule)
- [ ] No `[ObjectSpawner] Prefab is null` error in Console
- [ ] Defeating the Skeleton enemy transitions to the Village scene (not loop)
- [ ] `ATBCombatManager._combatActive` is false after battle ends
- [ ] Dying as hero transitions to Game Over / respawn screen (not loop)
- [ ] `BattleResultHandler.OnVictory()` logs "[BattleResult] Victory" in Console
- [ ] Scene transition has a brief display window (≥1.5s) before loading
