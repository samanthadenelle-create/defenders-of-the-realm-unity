# WORK ORDER 91 — Fix Wave Countdown "9" Rendering at World Scale

**Status:** CLOSED — SUPERSEDED by WO-186 (owner-approved sweep 2026-08-09: WO-186 owns the wave-countdown surface)
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small — replace world-space TextMesh with screen-space Canvas overlay
**Observed:** Screenshot — a large "9" numeral renders in world space at the
             centre of the village, visible at massive scale above the scene.

---

## Root Cause

The wave countdown is driven by a **TextMeshPro** or legacy `TextMesh` component
on a world-space GameObject rather than a screen-space Canvas. When the camera
is zoomed out for the village view, the text renders enormous.

---

## Fix

### Step 1 — Find the countdown GameObject

Search:
```
grep -r "countdown\|CountDown\|waveTimer\|WaveTimer\|countdownText" Assets/ --include="*.cs" -l
```

Likely scripts: `WaveManager.cs`, `WaveCountdownUI.cs`, `WaveTimerDisplay.cs`.
Likely scene object: a 3D TextMesh or TextMeshPro with tag "WaveTimer" or
parented to an empty GO named "CountdownText" or "WaveNumber".

---

### Step 2 — Move to screen-space Canvas

1. In the scene, **delete** the world-space text GameObject (or disable its
   Renderer — keep the script reference if needed).
2. On the main HUD Canvas (the one holding the hero HP bar and wave number),
   add a **TextMeshProUGUI** child named `WaveCountdownText`.

Recommended placement: top-centre of the screen, just below "Wave 3".

```
HUD Canvas
├── WaveNumberText          ← existing "Wave 3" label
├── WaveCountdownText       ← NEW — countdown overlay (large, centred, fades in/out)
├── ...
```

Settings for `WaveCountdownText`:
```
Font Size:      120
Alignment:      Centre / Middle
Color:          White, alpha controlled by script
Anchor:         Centre-top stretch
Rect:           Full width, height ~200
```

---

### Step 3 — `WaveCountdownUI.cs`

**Path:** `Assets/_Modules/Village/UI/WaveCountdownUI.cs`

```csharp
using UnityEngine;
using TMPro;
using System.Collections;

public class WaveCountdownUI : MonoBehaviour
{
    public static WaveCountdownUI Instance { get; private set; }

    [Header("References")]
    public TextMeshProUGUI countdownText;    // Assign the screen-space TMP

    [Header("Timing")]
    public float countdownDuration = 10f;   // Seconds of countdown before wave starts
    public float fadeInTime        = 0.2f;
    public float fadeOutTime       = 0.4f;
    public float scalePopAmount    = 1.25f; // Scale punch on each number change

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    // ── Called by WaveManager before wave starts ──────────────────────────────

    public void StartCountdown(float seconds, System.Action onComplete)
    {
        StartCoroutine(CountdownRoutine(seconds, onComplete));
    }

    private IEnumerator CountdownRoutine(float seconds, System.Action onComplete)
    {
        countdownText.gameObject.SetActive(true);

        int displayed = Mathf.CeilToInt(seconds);
        while (displayed > 0)
        {
            // Update text
            countdownText.text = displayed.ToString();

            // Scale pop
            countdownText.transform.localScale = Vector3.one * scalePopAmount;
            float elapsed = 0f;
            while (elapsed < 0.9f)
            {
                elapsed += Time.deltaTime;
                countdownText.transform.localScale = Vector3.Lerp(
                    Vector3.one * scalePopAmount, Vector3.one, elapsed / 0.9f);
                yield return null;
            }

            displayed--;
            yield return new WaitForSeconds(1f - 0.9f);
        }

        // "GO!" flash
        countdownText.text = "GO!";
        countdownText.transform.localScale = Vector3.one * scalePopAmount;
        yield return new WaitForSeconds(0.6f);

        // Fade out
        float fadeElapsed = 0f;
        Color c = countdownText.color;
        while (fadeElapsed < fadeOutTime)
        {
            fadeElapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, fadeElapsed / fadeOutTime);
            countdownText.color = c;
            yield return null;
        }

        countdownText.gameObject.SetActive(false);
        c.a = 1f;
        countdownText.color = c;

        onComplete?.Invoke();
    }
}
```

---

### Step 4 — Update `WaveManager` to call the countdown

Replace any existing world-space number update with:

```csharp
// Before starting the wave spawn coroutine:
WaveCountdownUI.Instance?.StartCountdown(
    wave.prewaveDelay,
    () => StartCoroutine(SpawnWave(wave)));
```

If `wave.prewaveDelay` is ≤ 0, skip the countdown and spawn immediately.

---

## Files to Create / Edit

| File | Action |
|---|---|
| World-space countdown TextMesh GO in scene | **Delete** (or disable Renderer) |
| HUD Canvas in Village scene | **Edit** — add `WaveCountdownText` TMP child |
| `Assets/_Modules/Village/UI/WaveCountdownUI.cs` | **Create** |
| `WaveManager.cs` | **Edit** — replace direct text update with `WaveCountdownUI.Instance?.StartCountdown(...)` |

---

## Acceptance Criteria

- [ ] The large "9" (or any countdown number) no longer appears floating in world space
- [ ] Countdown renders as a screen-space overlay at the top-centre of the HUD
- [ ] Numbers count down from N → 1 → "GO!" with a scale pop on each change
- [ ] "GO!" displays for 0.6 s then fades out
- [ ] After countdown completes, the wave spawn coroutine fires
- [ ] Countdown text is invisible when no wave is counting down
- [ ] Text is legible on mobile at all quality tiers (test at screen width 375 pt)
