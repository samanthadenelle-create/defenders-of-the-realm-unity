# WORK ORDER 106 — XP / Level Progress HUD + Gear Screen

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High — player feedback, core progression feel
**Scope:** Small–Medium — two UI components, no gameplay changes
**Depends on:** HeroProgression.cs (built), ProgressionManager.cs (built), VillageHudController (built)

---

## Goal

Two surfaces for player progression visibility:

1. **Persistent XP bar** — always-on strip at the bottom of the village HUD
   showing the fill percentage to the next level. Pulses on XP gain.

2. **Gear screen** — tapping the existing gear icon (⚙) opens a panel showing
   level number, current XP, XP needed, a large progress bar, and lifetime XP.

Both read from `HeroProgression` events — zero polling, zero gameplay coupling.

---

## Existing hooks (do NOT recreate)

- `HeroProgression.OnXPChanged(float currentXp, float xpNeeded)` — fires on every XP gain
- `HeroProgression.OnLevelUp(int newLevel)` — fires on level-up
- `HeroProgression.Level` — current level (int)
- `HeroProgression.LifetimeXp` (or `_lifetimeXp` field — check actual property name)
- Gear icon button: find via `UXML name="settings-button"` or similar in VillageHud.uxml

---

## 1. XP Bar — `XPBarController.cs`

**Path:** `Assets/_Modules/HUD/XPBarController.cs`
**Assembly:** `DeNelle.HUD`

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    /// <summary>
    /// Drives a persistent XP strip at the bottom of the village HUD.
    /// Subscribes to HeroProgression events via reflection (HUD cannot ref Village).
    /// </summary>
    public class XPBarController : MonoBehaviour
    {
        [Header("UI Elements (assigned by code from UIDocument)")]
        private VisualElement _xpFill;
        private Label _xpLabel;
        private Label _levelLabel;

        [Header("Animation")]
        [SerializeField] private float _fillLerpSpeed = 4f;
        [SerializeField] private float _pulseDuration  = 0.35f;

        private float _targetFill;   // 0..1
        private float _currentFill;
        private bool  _pulsing;

        private void Start()
        {
            // Find the HUD UIDocument elements
            var doc = GetComponent<UIDocument>() ?? FindObjectOfType<UIDocument>();
            if (doc?.rootVisualElement == null) return;

            _xpFill   = doc.rootVisualElement.Q<VisualElement>("xp-fill");
            _xpLabel  = doc.rootVisualElement.Q<Label>("xp-label");
            _levelLabel = doc.rootVisualElement.Q<Label>("xp-level-label");

            // Subscribe via reflection — HUD cannot reference DeNelle.Village
            HookHeroProgression();
        }

        private void HookHeroProgression()
        {
            var heroGo = GameObject.FindWithTag("HeroTarget") ?? GameObject.FindWithTag("Player");
            if (heroGo == null) return;

            var prog = heroGo.GetComponent("HeroProgression");
            if (prog == null) return;

            var type = prog.GetType();

            // OnXPChanged(float currentXp, float xpNeeded)
            var onXpChanged = type.GetEvent("OnXPChanged");
            onXpChanged?.AddEventHandler(prog,
                System.Delegate.CreateDelegate(
                    onXpChanged.EventHandlerType, this,
                    nameof(OnXPChanged)));

            // OnLevelUp(int newLevel)
            var onLevelUp = type.GetEvent("OnLevelUp");
            onLevelUp?.AddEventHandler(prog,
                System.Delegate.CreateDelegate(
                    onLevelUp.EventHandlerType, this,
                    nameof(OnLevelUp)));

            // Seed with current values
            var levelProp = type.GetProperty("Level");
            var xpProp    = type.GetProperty("CurrentXp");
            var needProp  = type.GetProperty("XpToNextLevel");
            if (levelProp != null) SetLevel((int)levelProp.GetValue(prog));
            if (xpProp != null && needProp != null)
                OnXPChanged((float)xpProp.GetValue(prog), (float)needProp.GetValue(prog));
        }

        // Called by HeroProgression.OnXPChanged
        public void OnXPChanged(float currentXp, float xpNeeded)
        {
            _targetFill = xpNeeded > 0f ? Mathf.Clamp01(currentXp / xpNeeded) : 0f;
            if (_xpLabel != null)
                _xpLabel.text = $"{Mathf.FloorToInt(currentXp):N0} / {Mathf.FloorToInt(xpNeeded):N0} XP";
            if (!_pulsing) StartCoroutine(PulseRoutine());
        }

        // Called by HeroProgression.OnLevelUp
        public void OnLevelUp(int newLevel)
        {
            SetLevel(newLevel);
            _targetFill = 0f;  // Reset bar on level-up
        }

        private void SetLevel(int level)
        {
            if (_levelLabel != null) _levelLabel.text = $"Lv. {level}";
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentFill, _targetFill)) return;
            _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * _fillLerpSpeed);
            if (_xpFill != null)
                _xpFill.style.width = Length.Percent(_currentFill * 100f);
        }

        private IEnumerator PulseRoutine()
        {
            _pulsing = true;
            if (_xpFill != null) _xpFill.AddToClassList("xp-fill--pulse");
            yield return new WaitForSeconds(_pulseDuration);
            if (_xpFill != null) _xpFill.RemoveFromClassList("xp-fill--pulse");
            _pulsing = false;
        }
    }
}
```

**USS classes needed** (add to VillageHud.uss or a new XPBar.uss):
```css
#xp-bar-root {
    height: 8px;
    background-color: rgba(0,0,0,0.5);
    border-radius: 4px;
    margin: 0 16px 4px 16px;
}
#xp-fill {
    height: 100%;
    background-color: #7B5EA7;  /* Elarion purple */
    border-radius: 4px;
    transition-property: width;
    transition-duration: 0.2s;
}
.xp-fill--pulse {
    background-color: #B08AFF;
}
#xp-level-label {
    font-size: 11px;
    color: #C8B8E8;
}
#xp-label {
    font-size: 10px;
    color: rgba(200,184,232,0.7);
}
```

**UXML to add** (bottom of `VillageHud.uxml`, inside the root):
```xml
<VisualElement name="xp-bar-root">
    <VisualElement name="xp-fill" />
</VisualElement>
<VisualElement name="xp-bar-labels" style="flex-direction: row; justify-content: space-between; margin: 0 16px;">
    <Label name="xp-level-label" text="Lv. 1" />
    <Label name="xp-label" text="0 / 200 XP" />
</VisualElement>
```

---

## 2. Gear Screen — `PlayerProgressPanel.cs`

**Path:** `Assets/_Modules/HUD/PlayerProgressPanel.cs`
**Assembly:** `DeNelle.HUD`

Opens/closes when the gear icon is tapped. Shows full progression detail.

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    public class PlayerProgressPanel : MonoBehaviour
    {
        private VisualElement _panel;
        private Label  _levelBig;
        private Label  _xpDetail;
        private Label  _lifetimeXp;
        private VisualElement _detailFill;
        private bool   _open;

        private void Start()
        {
            var doc = GetComponent<UIDocument>() ?? FindObjectOfType<UIDocument>();
            if (doc?.rootVisualElement == null) return;

            _panel      = doc.rootVisualElement.Q<VisualElement>("progress-panel");
            _levelBig   = doc.rootVisualElement.Q<Label>("progress-level");
            _xpDetail   = doc.rootVisualElement.Q<Label>("progress-xp-detail");
            _lifetimeXp = doc.rootVisualElement.Q<Label>("progress-lifetime");
            _detailFill = doc.rootVisualElement.Q<VisualElement>("progress-fill");

            // Wire gear button
            var gear = doc.rootVisualElement.Q<Button>("settings-button");
            gear?.RegisterCallback<ClickEvent>(_ => Toggle());

            // Close button inside panel
            var close = doc.rootVisualElement.Q<Button>("progress-close");
            close?.RegisterCallback<ClickEvent>(_ => Close());

            // Start hidden
            _panel?.AddToClassList("progress-panel--hidden");
        }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void Open()
        {
            RefreshData();
            _panel?.RemoveFromClassList("progress-panel--hidden");
            _open = true;
        }

        public void Close()
        {
            _panel?.AddToClassList("progress-panel--hidden");
            _open = false;
        }

        private void RefreshData()
        {
            var heroGo = GameObject.FindWithTag("HeroTarget") ?? GameObject.FindWithTag("Player");
            if (heroGo == null) return;
            var prog = heroGo.GetComponent("HeroProgression");
            if (prog == null) return;
            var type = prog.GetType();

            int   level      = (int)(type.GetProperty("Level")?.GetValue(prog) ?? 1);
            float currentXp  = (float)(type.GetProperty("CurrentXp")?.GetValue(prog) ?? 0f);
            float xpNeeded   = (float)(type.GetProperty("XpToNextLevel")?.GetValue(prog) ?? 200f);
            float lifetimeXp = (float)(type.GetProperty("LifetimeXp")?.GetValue(prog) ?? 0f);

            if (_levelBig   != null) _levelBig.text   = $"Level {level}";
            if (_xpDetail   != null) _xpDetail.text   = $"{currentXp:N0} / {xpNeeded:N0} XP to next level";
            if (_lifetimeXp != null) _lifetimeXp.text = $"Total XP earned: {lifetimeXp:N0}";
            if (_detailFill != null)
                _detailFill.style.width = Length.Percent(xpNeeded > 0f ? (currentXp / xpNeeded * 100f) : 0f);
        }
    }
}
```

**UXML panel** (inside VillageHud.uxml root, starts hidden):
```xml
<VisualElement name="progress-panel" class="progress-panel--hidden">
    <Label name="progress-level" text="Level 1" />
    <VisualElement name="progress-bar-bg">
        <VisualElement name="progress-fill" />
    </VisualElement>
    <Label name="progress-xp-detail" text="0 / 200 XP to next level" />
    <Label name="progress-lifetime" text="Total XP earned: 0" />
    <Button name="progress-close" text="Close" />
</VisualElement>
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/HUD/XPBarController.cs` | **Create** |
| `Assets/_Modules/HUD/PlayerProgressPanel.cs` | **Create** |
| `Assets/_Modules/HUD/UI/VillageHud.uxml` | **Edit** — add xp-bar-root + progress-panel elements |
| `Assets/_Modules/HUD/UI/VillageHud.uss` (or XPBar.uss) | **Edit** — add XP bar + panel styles |

**Do NOT touch:**
- HeroProgression.cs
- ProgressionManager.cs
- VillageSceneBuilder.cs
- Any .unity scene file

---

## Property names to verify

Before implementing, grep HeroProgression.cs for the exact property names:
```
grep -n "public.*Level\|public.*Xp\|public.*XP\|public.*Lifetime" Assets/_Modules/Village/Hero/HeroProgression.cs
```
Match exactly — the reflection calls depend on correct names.

---

## Acceptance Criteria

- [ ] XP bar visible at bottom of village HUD at all times
- [ ] Fill updates smoothly (lerp) as XP is gained
- [ ] Bar pulses briefly on each XP gain
- [ ] Level label reads "Lv. X" and updates on level-up
- [ ] Tapping gear icon opens progress panel
- [ ] Panel shows level, XP / XP needed, large progress bar, lifetime XP
- [ ] Close button dismisses panel
- [ ] No polling — driven entirely by OnXPChanged / OnLevelUp events
- [ ] Works in builds (no UXML — if UXML unavailable, implement as IMGUI fallback)
