# WORK ORDER 83 — Wave Clear & Kill Combo Celebration System (Phase 2)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — one new script + extension of WO-60 KillComboTracker
**Depends on:** WO-60 (KillComboTracker, WaveManager), WO-61 (CameraShakeManager), WO-50 (VFXManager)

> **Supersedes WO-60** in its wave-clear celebration portion. `KillComboTracker`
> from WO-60 remains; `WaveCelebrationManager` here replaces the basic
> `WaveManager.CompleteWave()` celebration call.

---

## Goal

Winning a wave is the most important dopamine hit in the game. Make it feel
huge — bloom spike, screen flash, celebration VFX rain, floating "Wave X Cleared!"
text, brief slow-mo, and an immediate reward preview. Kill combos escalate
visually and reward the player before the wave is even over.

---

## 1. `WaveCelebrationManager.cs`

**Path:** `Assets/_Modules/Village/Wave/WaveCelebrationManager.cs`

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using TMPro;

public class WaveCelebrationManager : MonoBehaviour
{
    public static WaveCelebrationManager Instance { get; private set; }

    [Header("Screen Effects")]
    public Volume        postProcessVolume;
    public float         bloomPeakIntensity  = 6f;    // Spikes from base (e.g. 1.2) to this
    public float         bloomBaseline       = 1.2f;
    public float         bloomDuration       = 0.55f;
    public float         flashDuration       = 0.3f;
    public Color         flashColor          = new Color(1f, 0.95f, 0.7f, 0.7f);   // Warm gold

    [Header("Slow Motion")]
    public float         slowMoScale         = 0.28f;
    public float         slowMoDuration      = 0.9f;   // Real seconds

    [Header("Floating Text")]
    public GameObject    waveTextPrefab;    // World-space TMP with "Wave X Cleared!" text
    public Transform     textSpawnPoint;    // Centre of village / screen-space anchor

    [Header("Reward Rain")]
    public VFXType       celebrationVFX     = VFXType.WaveClear_Celebration;
    public int           celebrationBursts  = 3;       // Number of VFX spawns
    public float         burstSpread        = 4f;      // Radius around spawn point

    [Header("Mobile")]
    public bool          reducedOnMobile    = true;

    private Bloom    _bloom;
    private bool     _bloomAvailable;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (postProcessVolume != null &&
            postProcessVolume.profile.TryGet(out Bloom b))
        {
            _bloom          = b;
            _bloomAvailable = true;
        }
    }

    // ── Called by WaveManager.CompleteWave() ──────────────────────────────────

    public void PlayWaveClear(int waveNumber)
    {
        StartCoroutine(WaveClearRoutine(waveNumber));
    }

    private IEnumerator WaveClearRoutine(int waveNumber)
    {
        bool mobile = false;
#if UNITY_ANDROID || UNITY_IOS
        mobile = reducedOnMobile;
#endif

        // 1. Bloom spike
        if (_bloomAvailable)
            StartCoroutine(BloomSpike(mobile ? bloomPeakIntensity * 0.6f : bloomPeakIntensity));

        // 2. Screen flash
        StartCoroutine(ScreenFlash(mobile));

        // 3. Slow-mo dip
        StartCoroutine(SlowMoDip(mobile ? slowMoDuration * 0.6f : slowMoDuration));

        // 4. VFX rain bursts
        for (int i = 0; i < (mobile ? 2 : celebrationBursts); i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-burstSpread, burstSpread), 0f,
                Random.Range(-burstSpread, burstSpread));
            VFXManager.Instance?.Play(celebrationVFX,
                textSpawnPoint.position + offset + Vector3.up * 1.5f);
            yield return new WaitForSeconds(0.12f);
        }

        // 5. Floating "Wave X Cleared!" text
        SpawnWaveText(waveNumber);

        // 6. Camera shake
        CameraShakeManager.Instance?.Shake(mobile ? ShakeTier.Light : ShakeTier.Medium);

        // AudioService.Instance?.PlaySfx(SfxId.WaveClear);
    }

    // ── Sub-routines ──────────────────────────────────────────────────────────

    private IEnumerator BloomSpike(float peak)
    {
        if (!_bloomAvailable) yield break;

        float elapsed = 0f, half = bloomDuration * 0.4f;

        // Ramp up
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            _bloom.intensity.Override(Mathf.Lerp(bloomBaseline, peak, elapsed / half));
            yield return null;
        }

        // Decay
        elapsed = 0f;
        float decay = bloomDuration * 0.6f;
        while (elapsed < decay)
        {
            elapsed += Time.unscaledDeltaTime;
            _bloom.intensity.Override(Mathf.Lerp(peak, bloomBaseline, elapsed / decay));
            yield return null;
        }

        _bloom.intensity.Override(bloomBaseline);
    }

    private IEnumerator ScreenFlash(bool mobile)
    {
        // Simple full-screen quad approach — use a canvas Image overlay if preferred.
        // Here we spike the camera background clear color as a quick approximation.
        var cam = Camera.main;
        if (cam == null) yield break;

        Color orig  = cam.backgroundColor;
        float alpha = mobile ? flashColor.a * 0.5f : flashColor.a;
        cam.backgroundColor = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

        yield return new WaitForSecondsRealtime(flashDuration * 0.2f);

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cam.backgroundColor = Color.Lerp(
                new Color(flashColor.r, flashColor.g, flashColor.b, alpha),
                orig,
                elapsed / flashDuration);
            yield return null;
        }

        cam.backgroundColor = orig;
    }

    private IEnumerator SlowMoDip(float duration)
    {
        Time.timeScale = slowMoScale;
        yield return new WaitForSecondsRealtime(duration);
        // Ease back
        float elapsed = 0f, ease = 0.3f;
        while (elapsed < ease)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(slowMoScale, 1f, elapsed / ease);
            yield return null;
        }
        Time.timeScale = 1f;
    }

    private void SpawnWaveText(int waveNumber)
    {
        if (waveTextPrefab == null || textSpawnPoint == null) return;

        var obj = Instantiate(waveTextPrefab,
            textSpawnPoint.position + Vector3.up * 2.5f,
            Quaternion.identity);

        if (obj.TryGetComponent<TMP_Text>(out var tmp))
            tmp.text = $"Wave {waveNumber} Cleared!";

        Destroy(obj, 2.5f);
    }
}
```

---

## 2. Wire into `WaveManager`

```csharp
// At the end of WaveManager.CompleteWave():
WaveCelebrationManager.Instance?.PlayWaveClear(currentWave);
```

---

## 3. Enhanced `KillComboTracker` — escalating feedback (extends WO-60)

**Edit** `Assets/_Modules/Village/Wave/KillComboTracker.cs`.
Replace the tier thresholds and add bonus resource grants:

```csharp
// Constants — tune to taste
private const float ComboWindow    = 6f;
private const int   Tier1Threshold = 3;
private const int   Tier2Threshold = 5;
private const int   Tier3Threshold = 8;   // NEW

// In RegisterKill():
_comboKills++;

if (_comboKills == Tier1Threshold)
{
    VFXManager.Instance?.Play(VFXType.Combo_Tier1, _heroTransform.position);
    CameraShakeManager.Instance?.Shake(ShakeTier.Light);
    ShowComboText("COMBO!");
    // AudioService.Instance?.PlaySfx(SfxId.Combo1);
}
else if (_comboKills == Tier2Threshold)
{
    VFXManager.Instance?.Play(VFXType.Combo_Tier2, _heroTransform.position);
    CameraShakeManager.Instance?.Shake(ShakeTier.Medium);
    ShowComboText("RAMPAGE!");
    MonetizationManager.Instance?.AddShards(25);   // Bonus Aether
    // AudioService.Instance?.PlaySfx(SfxId.Combo2);
}
else if (_comboKills >= Tier3Threshold)
{
    VFXManager.Instance?.Play(VFXType.Combo_Tier2, _heroTransform.position + Vector3.up);
    CameraShakeManager.Instance?.Shake(ShakeTier.Heavy);
    ShowComboText("UNSTOPPABLE!");
    MonetizationManager.Instance?.AddShards(60);
    // AudioService.Instance?.PlaySfx(SfxId.Combo3);
}
```

Add `ShowComboText(string)` that instantiates a world-space TMP above the hero
and fades it out over 1.2 s (same floating text pattern as level-up).

---

## 4. New VFXType entries (add to enum if not present)

```csharp
// In VFXType enum:
WaveClear_Celebration,
Combo_Tier1,
Combo_Tier2,
// Combo_Tier3 can reuse Combo_Tier2 with a bigger prefab or scale override
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Wave/WaveCelebrationManager.cs` | **Create** |
| `Assets/_Modules/Village/Wave/KillComboTracker.cs` | **Edit** — Tier3, bonus Aether, `ShowComboText()` |
| `Assets/_Modules/Village/Wave/WaveManager.cs` | **Edit** — call `WaveCelebrationManager.Instance?.PlayWaveClear(wave)` |
| Scene root | **Edit** — add `WaveCelebrationManager` GO, wire `postProcessVolume`, `textSpawnPoint` |
| `WaveTextPrefab` | **Create** — world-space canvas with TMP |

---

## Acceptance Criteria

- [ ] Wave clear triggers bloom spike, screen flash, slow-mo dip, VFX rain, floating text
- [ ] "Wave X Cleared!" text displays the correct wave number
- [ ] Slow-mo restores to 1× within 1.2 s (real time) after trigger
- [ ] 3 kills in 6 s → Tier1 VFX + shake + "COMBO!" text
- [ ] 5 kills in 6 s → Tier2 VFX + Medium shake + "RAMPAGE!" + 25 Aether
- [ ] 8+ kills → Heavy shake + "UNSTOPPABLE!" + 60 Aether
- [ ] Combo timer resets correctly when the window expires
- [ ] Mobile: bloom and flash at 60% intensity; still feels satisfying
