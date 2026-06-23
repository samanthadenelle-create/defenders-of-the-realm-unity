# WORK ORDER 52 — WeatherManager + Shooting Stars & Atmosphere

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Medium-High
**Scope:** Medium — new manager, two coroutine systems, particle wiring
**Depends on:** VFXManager (WO-50) recommended for pool fallback

---

## Goal

Add low-cost environmental atmosphere that makes the world feel alive —
shooting stars that streak across the night sky, scaleable rain, and hooks for
wind/fog expansion. All effects are pool-backed and disabled automatically on
low-performance devices via `PerformanceManager` (WO-51).

---

## 1. Create `WeatherManager.cs`

**Path:** `Assets/_Modules/Environment/WeatherManager.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    // ── Shooting Stars ────────────────────────────────────────────────────────
    [Header("Shooting Stars")]
    [Tooltip("Stretched particle + optional point light prefab.")]
    public GameObject shootingStarPrefab;
    [Tooltip("Empty Transform placed high in the sky (~200 units above village centre).")]
    public Transform  skySpawnPoint;
    public float  minInterval           = 12f;
    public float  maxInterval           = 35f;
    [Range(0f, 1f)] public float spawnChance = 0.7f;
    public int    maxActiveShootingStars = 3;

    // ── Rain ─────────────────────────────────────────────────────────────────
    [Header("Rain")]
    public ParticleSystem rainParticles;
    public ParticleSystem rainSplashParticles;
    [Range(0f, 1f)] public float rainIntensity = 0f;

    // ── Wind ─────────────────────────────────────────────────────────────────
    [Header("Wind (optional)")]
    [Tooltip("Assign to drive tree/grass shader wind parameter.")]
    public WindZone windZone;
    [Range(0f, 3f)] public float windStrength = 0f;

    // ── Performance ───────────────────────────────────────────────────────────
    [Header("Performance")]
    public bool enableOnMobile = true;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly List<GameObject> _activeStars = new List<GameObject>();
    private Coroutine _starRoutine;

    private bool WeatherEnabled =>
        enableOnMobile || !Application.isMobilePlatform;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (WeatherEnabled && shootingStarPrefab != null)
            _starRoutine = StartCoroutine(ShootingStarRoutine());
    }

    // ── Shooting Stars ────────────────────────────────────────────────────────

    private IEnumerator ShootingStarRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (Random.value <= spawnChance &&
                _activeStars.Count < maxActiveShootingStars)
            {
                SpawnShootingStar();
            }
        }
    }

    private void SpawnShootingStar()
    {
        if (skySpawnPoint == null || shootingStarPrefab == null) return;

        Vector3 spawnPos = skySpawnPoint.position + new Vector3(
            Random.Range(-50f, 50f),
            Random.Range(-8f,  12f),
            Random.Range(-40f, 40f));

        // Use VFXManager pool if available; fall back to Instantiate.
        GameObject star = VFXManager.Instance != null
            ? VFXManager.Instance.Play(VFXType.ShootingStar, spawnPos,
                                       Quaternion.identity)
            : Instantiate(shootingStarPrefab, spawnPos, Quaternion.identity);

        if (star == null) return;

        if (star.TryGetComponent<Rigidbody>(out var rb))
            rb.velocity = new Vector3(
                Random.Range(-18f, -8f), -42f, Random.Range(-12f, 12f));

        _activeStars.Add(star);
        StartCoroutine(ReturnShootingStar(star, 4.5f));
    }

    private IEnumerator ReturnShootingStar(GameObject star, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        if (star != null)
        {
            _activeStars.Remove(star);
            // Return to pool or destroy.
            if (VFXManager.Instance != null)
                star.SetActive(false);   // pool release handled by VFXAutoReturn
            else
                Destroy(star);
        }
    }

    // ── Rain ──────────────────────────────────────────────────────────────────

    public void SetRain(float intensity01)
    {
        rainIntensity = Mathf.Clamp01(intensity01);

        if (rainParticles != null)
        {
            var em = rainParticles.emission;
            em.rateOverTime = rainIntensity * 1200f;

            if (rainIntensity > 0f && !rainParticles.isPlaying)
                rainParticles.Play();
            else if (rainIntensity <= 0f && rainParticles.isPlaying)
                rainParticles.Stop();
        }

        if (rainSplashParticles != null)
        {
            var em = rainSplashParticles.emission;
            em.rateOverTime = rainIntensity * 450f;
        }

        // Drive wind slightly with rain intensity
        if (windZone != null)
            windZone.windMain = windStrength + rainIntensity * 1.5f;
    }

    public void ToggleRain(bool on) => SetRain(on ? 0.85f : 0f);

    // ── Wind ─────────────────────────────────────────────────────────────────

    public void SetWind(float strength)
    {
        windStrength = Mathf.Clamp(strength, 0f, 3f);
        if (windZone != null)
            windZone.windMain = windStrength + rainIntensity * 1.5f;
    }
}
```

> **`VFXType.ShootingStar`**: add this entry to the `VFXType` enum in
> `VFXManager.cs` (WO-50) and register the shooting star prefab in `VFXCatalog`.

---

## 2. Add `ShootingStar` to `VFXType` enum

**Edit** `Assets/_Modules/VFX/VFXManager.cs` — append to the `VFXType` enum:

```csharp
ShootingStar,
```

And add a matching entry to `VFXCatalog` pointing at the shooting star prefab.

---

## 3. Create the Shooting Star prefab

1. Create a new empty GameObject: `ShootingStar`.
2. Add a `ParticleSystem` — stretch alignment, bright white/blue colour,
   trail enabled, short lifetime (0.8–1.5 s).
3. Add a `Rigidbody` (Use Gravity = false, Is Kinematic = false) — velocity is
   set by `WeatherManager.SpawnShootingStar()`.
4. Optionally add a `Light` component (point, intensity 2, range 8, warm white)
   and animate its intensity with a curve over the particle lifetime.
5. Save as a prefab: `Assets/Resources/VFX/ShootingStar.prefab`.
6. Assign to `WeatherManager.shootingStarPrefab` in Inspector.

---

## 4. Scene wiring

1. Create an empty scene root object `WeatherManager`.
2. Add the `WeatherManager` component.
3. Create an empty child `SkySpawnPoint` positioned ~200 units above the village.
4. Assign a `WindZone` if one exists in the scene, else skip.
5. To trigger rain from code (e.g. wave start):
   ```csharp
   WeatherManager.Instance.ToggleRain(true);
   // or
   WeatherManager.Instance.SetRain(0.4f); // light drizzle
   ```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Environment/WeatherManager.cs` | **Create** |
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add `ShootingStar` to `VFXType` enum |
| `Assets/Resources/VFX/VFXCatalog.asset` | **Edit** — add ShootingStar entry |
| `Assets/Resources/VFX/ShootingStar.prefab` | **Create** — particle + Rigidbody |

---

## Acceptance Criteria

- [ ] Shooting stars spawn at random intervals and streak across the sky
- [ ] No more than `maxActiveShootingStars` are alive at once
- [ ] Stars are returned to pool (or destroyed) after 4.5 s — no accumulation in Hierarchy
- [ ] `SetRain(0.85f)` produces visible rain and splash particles
- [ ] `SetRain(0f)` cleanly stops both particle systems
- [ ] Wind zone reacts when rain is active
- [ ] `WeatherEnabled = false` on mobile devices with `enableOnMobile = false` skips all effects
- [ ] No shooting stars during daytime if a `DayNightCycle` system is present and exposes `IsNight`
