# WORK ORDER 241 — AlertIntelSystem (Watchtower → Raid Warning)

**Status: READY TO IMPLEMENT**
**Author:** UI + Owner (creative lane)
**WO Number:** 241
**Date:** 2026-06-02
**Protects:** claimed outposts AND the main village (Elarion)

---

## What it does

A single DDOL singleton that scales raid warning quality with Watchtower level.
Every Watchtower — whether at an outpost or inside the village walls — feeds into it.
Higher Watchtower = earlier warning, better direction, more accurate enemy count.

Without a Watchtower: no warning — raid hits cold.

---

## Intel tiers by Watchtower level

| Level | Radius | Warning lead | Detection quality | What the player sees |
|---|---|---|---|---|
| None | 0 | 0s | 0 | Ambushed — no warning |
| 1 | 55m | 10s | 60% | "Something is coming." |
| 2 | 90m | 18s | 82% | "Raid from the East — estimated 8–12 enemies." |
| 3 (max) | 140m | 30s | 96% | "Large force from the North — ~10 Hollow Ones, including a brute." |

---

## `AlertIntelSystem.cs`

```csharp
// Assets/_Modules/Village/Combat/AlertIntelSystem.cs
using UnityEngine;

namespace DeNelle.Village
{
    public sealed class AlertIntelSystem : MonoBehaviour
    {
        public static AlertIntelSystem Instance { get; private set; }

        [Header("Current intel level")]
        public float intelRadius       = 0f;
        public float warningLeadTime   = 0f;
        public float detectionQuality  = 0f;  // 0 = blind, 1 = perfect

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("AlertIntelSystem").AddComponent<AlertIntelSystem>();
        }

        /// <summary>
        /// Call when any Watchtower is built or upgraded (outpost or village).
        /// Pass the highest Watchtower level the player currently owns.
        /// </summary>
        public void UpdateFromWatchtower(int highestLevel)
        {
            switch (highestLevel)
            {
                case 0:
                    intelRadius = 0f; warningLeadTime = 0f; detectionQuality = 0f;
                    break;
                case 1:
                    intelRadius = 55f; warningLeadTime = 10f; detectionQuality = 0.60f;
                    break;
                case 2:
                    intelRadius = 90f; warningLeadTime = 18f; detectionQuality = 0.82f;
                    break;
                case 3:
                    intelRadius = 140f; warningLeadTime = 30f; detectionQuality = 0.96f;
                    break;
            }
            Debug.Log($"[AlertIntel] Level {highestLevel} → {warningLeadTime}s / {intelRadius}m / {detectionQuality:P0}");
        }

        /// <summary>
        /// Returns intel for an incoming raid. isMainCity = true gives a small accuracy bonus
        /// (the village watchtower is better maintained than a field outpost).
        /// Returns null if no Watchtower is built.
        /// </summary>
        public RaidIntel? GetRaidIntel(Vector3 raidSourcePos, bool isMainCity = false)
        {
            if (detectionQuality <= 0f) return null;   // no intel — ambushed

            float accuracy = Mathf.Min(1f, detectionQuality * (isMainCity ? 1.15f : 1f));
            bool  accurate = Random.value < accuracy;

            return new RaidIntel
            {
                warningTime          = warningLeadTime,
                estimatedEnemyCount  = accurate ? Random.Range(6, 14) : Random.Range(4, 20),
                direction            = GetCardinalDirection(raidSourcePos),
                quality              = detectionQuality,
                isMainCityTarget     = isMainCity
            };
        }

        private static string GetCardinalDirection(Vector3 raidSourcePos)
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) return "Unknown";

            Vector3 dir = (raidSourcePos - hero.transform.position).normalized;
            return Mathf.Abs(dir.z) > Mathf.Abs(dir.x)
                ? (dir.z > 0 ? "North" : "South")
                : (dir.x > 0 ? "East"  : "West");
        }
    }

    public struct RaidIntel
    {
        public float  warningTime;
        public int    estimatedEnemyCount;
        public string direction;
        public float  quality;
        public bool   isMainCityTarget;

        /// <summary>Formats a player-facing warning message based on intel quality.</summary>
        public string ToWarningMessage()
        {
            if (quality < 0.65f)
                return $"Something approaches from the {direction}.";
            if (quality < 0.90f)
                return $"Raid from the {direction} — estimated {estimatedEnemyCount} enemies.";
            string target = isMainCityTarget ? "Elarion" : "your outpost";
            return $"Force from the {direction} targeting {target} — ~{estimatedEnemyCount} strong.";
        }
    }
}
```

---

## Wiring

### On Watchtower build/upgrade (in `OutpostBuildPanel` or `BuildingUpgradePanel`):

```csharp
// After building or upgrading any Watchtower — pass the player's current highest level:
int highestLevel = GetHighestWatchtowerLevel();   // scan all Player-owned buildings
AlertIntelSystem.Instance?.UpdateFromWatchtower(highestLevel);
```

### For outpost raids (in `TribeManager` or `ClaimableNode`):

```csharp
// Before a raid spawns — check for intel:
var intel = AlertIntelSystem.Instance?.GetRaidIntel(raidSourcePos, isMainCity: false);
if (intel.HasValue)
{
    // Show warning to player — use intel.Value.warningTime as the delay before enemies arrive
    // Display intel.Value.ToWarningMessage() as HUD notification
    yield return new WaitForSeconds(intel.Value.warningTime);
}
// Raid spawns here (enemies attack regardless — intel just gives warning, not prevention)
```

### For main village wave attacks (in `WaveManager`):

```csharp
var intel = AlertIntelSystem.Instance?.GetRaidIntel(spawnPoint.position, isMainCity: true);
if (intel.HasValue)
    HudController?.ShowRaidWarning(intel.Value.ToWarningMessage(), intel.Value.warningTime);
```

---

## Watchtower JSON (updated — level-tiered)

```json
{
  "upgrades": [
    {
      "id": "watchtower_l1",
      "title": "Wooden Lookout",
      "description": "Basic watch post. Something's coming — you'll know.",
      "woodCost": 60, "stoneCost": 0, "ironCost": 20, "crystalCost": 0,
      "boosts": [{ "stat": "WatchtowerLevel", "value": 1, "type": "set" }]
    },
    {
      "id": "watchtower_l2",
      "title": "Reinforced Tower",
      "description": "Signal lanterns. Direction and rough count added to warnings.",
      "woodCost": 80, "stoneCost": 60, "ironCost": 40, "crystalCost": 0,
      "boosts": [{ "stat": "WatchtowerLevel", "value": 2, "type": "set" }]
    },
    {
      "id": "watchtower_l3",
      "title": "Grand Stone Watchtower",
      "description": "Near-perfect foreknowledge. Composition, count, direction — 30 seconds ahead.",
      "woodCost": 120, "stoneCost": 100, "ironCost": 80, "crystalCost": 40,
      "boosts": [{ "stat": "WatchtowerLevel", "value": 3, "type": "set" }]
    }
  ]
}
```

---

## `RaidWarningUI.cs` (code-built — no UXML)

```csharp
// Assets/_Modules/Village/Combat/RaidWarningUI.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    public sealed class RaidWarningUI : MonoBehaviour
    {
        public static RaidWarningUI Instance { get; private set; }

        private Canvas      _canvas;
        private GameObject  _panel;
        private Text        _headerText;
        private Text        _detailsText;
        private Text        _timerText;
        private Button      _ackBtn;
        private Coroutine   _timerCoroutine;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCanvas();
            Hide();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("RaidWarningUI").AddComponent<RaidWarningUI>();
        }

        public void Show(RaidIntel intel, string targetName)
        {
            _panel.SetActive(true);

            _headerText.text  = intel.isMainCityTarget
                ? "THE VILLAGE IS UNDER ATTACK!"
                : "RAID INCOMING!";

            _detailsText.text = $"{intel.direction}  ·  ~{intel.estimatedEnemyCount} enemies  ·  {targetName}";

            if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
            _timerCoroutine = StartCoroutine(CountDown(intel.warningTime));

            CancelInvoke(nameof(Hide));
            Invoke(nameof(Hide), intel.warningTime + 8f);
        }

        private IEnumerator CountDown(float total)
        {
            float remaining = total;
            while (remaining > 0f)
            {
                _timerText.text = $"Arriving in {Mathf.CeilToInt(remaining)}s";
                remaining -= Time.deltaTime;
                yield return null;
            }
            _timerText.text = "ARRIVING NOW!";
        }

        private void Hide()
        {
            _panel?.SetActive(false);
            CancelInvoke(nameof(Hide));
        }

        // ── canvas builder ───────────────────────────────────────────────────
        private void BuildCanvas()
        {
            _canvas             = gameObject.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("RaidPanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);

            var bg   = _panel.AddComponent<Image>();
            bg.color = new Color(0.55f, 0.05f, 0.05f, 0.92f);   // deep red

            var pr   = _panel.GetComponent<RectTransform>();
            pr.sizeDelta        = new Vector2(400f, 130f);
            pr.anchoredPosition = new Vector2(0f, 160f);         // top-centre
            pr.anchorMin        = new Vector2(0.5f, 0.5f);
            pr.anchorMax        = new Vector2(0.5f, 0.5f);

            _headerText  = MakeLabel("RAID INCOMING!", 20, new Vector2(0f,  42f), Color.white);
            _detailsText = MakeLabel("",               13, new Vector2(0f,  14f), new Color(1f, 0.85f, 0.7f));
            _timerText   = MakeLabel("",               12, new Vector2(0f, -10f), new Color(1f, 0.7f, 0.7f));

            var btnGo  = new GameObject("AckBtn", typeof(RectTransform));
            btnGo.transform.SetParent(_panel.transform, false);
            btnGo.AddComponent<Image>().color = new Color(0.7f, 0.1f, 0.1f);
            _ackBtn    = btnGo.AddComponent<Button>();
            _ackBtn.onClick.AddListener(Hide);
            var br     = btnGo.GetComponent<RectTransform>();
            br.sizeDelta        = new Vector2(80f, 28f);
            br.anchoredPosition = new Vector2(0f, -45f);
            br.anchorMin        = new Vector2(0.5f, 0.5f);
            br.anchorMax        = new Vector2(0.5f, 0.5f);
            MakeLabel("Got it", 12, Vector2.zero, Color.white).transform.SetParent(btnGo.transform, false);
        }

        private Text MakeLabel(string txt, int size, Vector2 pos, Color col)
        {
            var go  = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
            var t   = go.AddComponent<Text>();
            t.text  = txt; t.fontSize = size; t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            t.font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var r   = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(380f, 28f);
            r.anchoredPosition = pos;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            return t;
        }
    }
}
```

**Trigger pattern** (in `TribeManager` or `ClaimableNode.TriggerRaid()`):
```csharp
var intel = AlertIntelSystem.Instance?.GetRaidIntel(raidSource, isMainCity);
if (intel.HasValue)
{
    RaidWarningUI.Instance?.Show(intel.Value, targetName);
    yield return new WaitForSeconds(intel.Value.warningTime);
}
// enemies spawn here
```

---

## Acceptance criteria

- [ ] No Watchtower → raids arrive with zero warning
- [ ] Level 1 Watchtower → "Something approaches" 10s before raid spawns
- [ ] Level 3 Watchtower → full composition + direction + 30s warning
- [ ] Village wave attacks also route through `AlertIntelSystem`
- [ ] `ToWarningMessage()` returns quality-appropriate text
- [ ] Brace balance passed

## What NOT to touch
- `WaveManager` wave spawn logic — add a thin `GetRaidIntel` call before spawn, nothing else
- `TribeManager` raid AI — same: add intel check, don't change raid behaviour
- No UXML / UIDocument
