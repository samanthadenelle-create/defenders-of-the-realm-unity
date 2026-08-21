<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 63 — Hero / Pet Level-Up Celebration System

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small-Medium — new controller, VFX hook, event bus
**Depends on:** WO-50 (VFXManager), WO-58 (AuraController for pet burst)

---

## Goal

Levelling a hero or pet feels like a real achievement — vertical light beam,
orbiting golden particles, floating text, screen flash, and (for pets) an aura
intensity burst.

---

## 1. Create `LevelUpVFXController.cs`

**Path:** `Assets/_Modules/Progression/LevelUpVFXController.cs`

```csharp
using System.Collections;
using UnityEngine;
using TMPro;

public class LevelUpVFXController : MonoBehaviour
{
    public static LevelUpVFXController Instance { get; private set; }

    [Header("VFX")]
    [Tooltip("Assign the LevelUp_Celebration VFX prefab (or leave null to use VFXManager).")]
    public GameObject celebrationPrefab;

    [Header("Floating Text")]
    public GameObject levelUpTextPrefab;   // TMP label "Level Up!" with pop animation
    public float      textRiseSpeed = 1.8f;
    public float      textLifetime  = 1.4f;

    [Header("Screen Flash")]
    public float flashDuration = 0.18f;
    public Color flashColour   = new Color(1f, 0.95f, 0.5f, 0.55f);

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call from any system when a hero or pet gains a level.
    /// </summary>
    public void PlayLevelUp(Transform target, int newLevel, bool isPet = false)
    {
        Vector3 pos = target.position;

        // Main VFX.
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXType.LevelUp_Celebration, pos);
        else if (celebrationPrefab != null)
            Instantiate(celebrationPrefab, pos, Quaternion.identity);

        // Floating text.
        StartCoroutine(SpawnFloatingText(pos + Vector3.up * 1.8f, $"Level {newLevel}!"));

        // Screen flash.
        StartCoroutine(ScreenFlash());

        // Pet aura burst.
        if (isPet)
            target.GetComponent<AuraController>()?.OnLevelUp(newLevel);

        // Broadcast event so audio / UI can react.
        LevelUpEvents.RaiseLevelUp(target.gameObject, newLevel);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private IEnumerator SpawnFloatingText(Vector3 pos, string text)
    {
        if (levelUpTextPrefab == null) yield break;

        var go  = Instantiate(levelUpTextPrefab, pos, Quaternion.identity);
        var tmp = go.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = text;

        float elapsed = 0f;
        while (elapsed < textLifetime)
        {
            go.transform.position += Vector3.up * textRiseSpeed * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator ScreenFlash()
    {
        // Requires a full-screen quad UI element tagged "ScreenFlash".
        var flash = GameObject.FindWithTag("ScreenFlash");
        if (flash == null) yield break;

        var img = flash.GetComponent<UnityEngine.UI.Image>();
        if (img == null) yield break;

        img.color = flashColour;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            float t = elapsed / flashDuration;
            img.color = Color.Lerp(flashColour, Color.clear, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        img.color = Color.clear;
    }
}
```

---

## 2. Create `LevelUpEvents.cs` — event bus

**Path:** `Assets/_Modules/Progression/LevelUpEvents.cs`

```csharp
using System;
using UnityEngine;

/// <summary>
/// Static event bus for level-up — decouples VFX/Audio/UI from progression system.
/// </summary>
public static class LevelUpEvents
{
    /// <summary>Fired after any hero or pet gains a level.</summary>
    public static event Action<GameObject, int> OnLevelUp;

    public static void RaiseLevelUp(GameObject character, int newLevel) =>
        OnLevelUp?.Invoke(character, newLevel);
}
```

**Subscribe from AudioService:**
```csharp
private void OnEnable()  => LevelUpEvents.OnLevelUp += OnCharacterLevelUp;
private void OnDisable() => LevelUpEvents.OnLevelUp -= OnCharacterLevelUp;
private void OnCharacterLevelUp(GameObject go, int level)
    => PlaySfx(SfxId.LevelUp);
```

---

## 3. Hook into progression

Wherever XP thresholds are crossed (hero XP system / pet level-up):

```csharp
// For hero:
LevelUpVFXController.Instance?.PlayLevelUp(transform, newLevel, isPet: false);

// For pet:
LevelUpVFXController.Instance?.PlayLevelUp(transform, newLevel, isPet: true);
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Progression/LevelUpVFXController.cs` | **Create** |
| `Assets/_Modules/Progression/LevelUpEvents.cs` | **Create** |
| Hero XP / Pet XP system | **Edit** — call `PlayLevelUp` on threshold |
| `Assets/_Modules/Audio/AudioService.cs` | **Edit** — subscribe to `LevelUpEvents.OnLevelUp` |
| Persistent scene | **Edit** — add `LevelUpVFXController` + screen flash UI element |

---

## Acceptance Criteria

- [ ] Level-up fires a vertical light beam + orbiting particles at the character
- [ ] Floating "Level N!" text rises and fades over 1.4 s
- [ ] Screen briefly flashes gold
- [ ] Pet aura intensity spikes for 2 s after level-up (WO-58 burst)
- [ ] `LevelUpEvents.OnLevelUp` fires and audio system plays level-up chime
- [ ] Works in both Village and Dungeon scenes

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `LevelUpVFXController.cs:30` — celebration shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
