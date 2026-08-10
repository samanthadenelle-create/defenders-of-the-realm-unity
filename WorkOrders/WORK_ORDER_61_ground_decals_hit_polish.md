# WORK ORDER 61 — Ground Decals, Hit Reactions & Final Polish

**Status:** CLOSED — SUPERSEDED by WO-84 (owner-approved sweep 2026-08-09: WO-84 hit-reactions RESULT exists; decals residue unverified)
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — CameraShakeManager + HitStop + DecalSpawner + footstep dust
**Depends on:** WO-50 (VFXManager)

---

## Goal

Every hit and death feels weighty. Ground decals mark impacts, hit stop
telegraphs powerful strikes, and subtle footstep dust and floating particles
make the world feel alive even during calm moments.

---

## 1. Create `CameraShakeManager.cs`

**Path:** `Assets/_Modules/Camera/CameraShakeManager.cs`

```csharp
using System.Collections;
using UnityEngine;

public enum ShakeTier { Light, Medium, Heavy }

public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance { get; private set; }

    [Header("Shake Profiles")]
    public float lightMagnitude  = 0.06f;  public float lightDuration  = 0.12f;
    public float mediumMagnitude = 0.14f;  public float mediumDuration = 0.22f;
    public float heavyMagnitude  = 0.28f;  public float heavyDuration  = 0.38f;

    private Vector3 _originPos;
    private Coroutine _current;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Shake(ShakeTier tier, float durationOverride = -1f)
    {
        float mag = tier == ShakeTier.Heavy  ? heavyMagnitude
                  : tier == ShakeTier.Medium ? mediumMagnitude
                  :                            lightMagnitude;
        float dur = durationOverride > 0 ? durationOverride
                  : tier == ShakeTier.Heavy  ? heavyDuration
                  : tier == ShakeTier.Medium ? mediumDuration
                  :                            lightDuration;

        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(ShakeRoutine(mag, dur));
    }

    private IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        _originPos = Camera.main.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float damped   = magnitude * (1f - progress);   // decay over time
            Camera.main.transform.localPosition = _originPos +
                (Vector3)Random.insideUnitCircle * damped;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Camera.main.transform.localPosition = _originPos;
        _current = null;
    }
}
```

---

## 2. Create `HitStopManager.cs`

**Path:** `Assets/_Modules/Combat/HitStopManager.cs`

```csharp
using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Briefly freeze time to telegraph a powerful hit.
    /// duration = 0.08–0.15 s recommended.
    /// </summary>
    public void TriggerHitStop(float duration = 0.1f)
    {
        // Respect quality settings — skip if disabled.
        if (MobileQualitySettings.Current != null &&
            !MobileQualitySettings.Current.enableHitStop)
            return;

        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}
```

**Usage:**
```csharp
// On heavy hit (knight ground slam, tower cannonball):
HitStopManager.Instance?.TriggerHitStop(0.12f);
CameraShakeManager.Instance?.Shake(ShakeTier.Medium);
```

---

## 3. Create `DecalSpawner.cs`

**Path:** `Assets/_Modules/Combat/DecalSpawner.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns fading ground decals on big impacts.
/// Uses Unity's built-in Decal Projector (URP) if available,
/// otherwise falls back to a simple quad.
/// </summary>
public class DecalSpawner : MonoBehaviour
{
    public static DecalSpawner Instance { get; private set; }

    [Header("Decal Prefabs")]
    public GameObject scorchDecalPrefab;
    public GameObject iceDecalPrefab;
    public GameObject crackDecalPrefab;

    [Header("Pool")]
    public int poolSize  = 12;
    public float fadeTime = 4f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SpawnScorch(Vector3 position)  => Spawn(scorchDecalPrefab, position);
    public void SpawnIce(Vector3 position)     => Spawn(iceDecalPrefab, position);
    public void SpawnCrack(Vector3 position)   => Spawn(crackDecalPrefab, position);

    private void Spawn(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        var decal = Instantiate(prefab, position, Quaternion.Euler(90f, 0f, 0f));
        StartCoroutine(FadeAndDestroy(decal, fadeTime));
    }

    private IEnumerator FadeAndDestroy(GameObject decal, float duration)
    {
        yield return new WaitForSeconds(duration * 0.6f);  // solid for 60% of life

        float elapsed = 0f;
        float fadeDur = duration * 0.4f;
        var renderers = decal.GetComponentsInChildren<Renderer>();

        while (elapsed < fadeDur)
        {
            float alpha = 1f - (elapsed / fadeDur);
            foreach (var r in renderers)
            {
                var c = r.material.color;
                r.material.color = new Color(c.r, c.g, c.b, alpha);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(decal);
    }
}
```

**Usage:**
```csharp
// On fire explosion impact:
DecalSpawner.Instance?.SpawnScorch(hitPosition);
// On tower cannonball:
DecalSpawner.Instance?.SpawnCrack(hitPosition);
```

---

## 4. Footstep dust

Add a `FootstepDustController.cs` to hero and enemy prefabs:

```csharp
using UnityEngine;

public class FootstepDustController : MonoBehaviour
{
    public ParticleSystem dustParticles;
    public float minSpeed = 0.5f;

    private Rigidbody _rb;
    private NavMeshAgent _agent;

    private void Awake()
    {
        _rb    = GetComponent<Rigidbody>();
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        float speed = _agent != null ? _agent.velocity.magnitude
                    : _rb    != null ? _rb.velocity.magnitude
                    : 0f;

        if (dustParticles == null) return;

        if (speed > minSpeed && !dustParticles.isPlaying)
            dustParticles.Play();
        else if (speed <= minSpeed && dustParticles.isPlaying)
            dustParticles.Stop();
    }
}
```

---

## 5. Critical hits & healing

```csharp
// In damage system — on crit:
VFXManager.Instance?.Play(VFXType.CriticalHit, hitPosition);
CameraShakeManager.Instance?.Shake(ShakeTier.Light);
HitStopManager.Instance?.TriggerHitStop(0.08f);

// On heal:
VFXManager.Instance?.Play(VFXType.Impact_Heal, targetPosition);
```

Add to `VFXType` enum: `CriticalHit`.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Camera/CameraShakeManager.cs` | **Create** |
| `Assets/_Modules/Combat/HitStopManager.cs` | **Create** |
| `Assets/_Modules/Combat/DecalSpawner.cs` | **Create** |
| `Assets/_Modules/Combat/FootstepDustController.cs` | **Create** |
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add `CriticalHit` to enum |
| All character prefabs | **Edit** — add `FootstepDustController`, wire dust PS |
| Damage system (wherever crits resolve) | **Edit** — add CritHit VFX + HitStop |

---

## Acceptance Criteria

- [ ] Scorch decal appears on ground after fire impact and fades over 4 s
- [ ] Heavy hits produce 0.1 s time slowdown + medium camera shake
- [ ] Hero and enemies leave subtle dust footprints while moving
- [ ] Critical hits show a yellow flash + extra particles
- [ ] Healing shows a rising light column
- [ ] `enableHitStop = false` in quality settings correctly skips hit stop
- [ ] Camera returns to exact origin position after every shake
