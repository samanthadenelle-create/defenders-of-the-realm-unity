// =============================================================================
// GateIntelHud (DEF-152) — "Gate crossing" intel so leaving Elarion isn't a blind
// commit. When the hero nears a village gate, a small code-built label appears at
// screen-bottom-centre naming the region just beyond the gate and its threat:
//   "North -> Ashwood  -  Threat 20"
// Hidden when the hero is not near any gate.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// Minimal, isolated, flag-guarded. Self-bootstraps ONLY in the Village scene
// (mirrors BuildingSignInjector / CampPromptUI). Proximity is a cheap throttled
// distance check against CACHED gate transforms (found by name once per scene
// load — NOT FindObjectsOfType per frame). Region beyond a gate is resolved by
// sampling a point a few metres OUTSIDE the gate (gate position pushed away from
// the village centre) and passing it to the shared classifier
// DeNelle.Core.World.ZoneManager (GetZone + ZoneAt + ThreatLevel).
//
// Build-safe: code-built uGUI, LegacyRuntime.ttf font, NO UXML, NO EventSystem,
// NO System.Reflection. Null-guarded throughout. ASCII-only. Canon: Elarion.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeNelle.Core.World;

namespace DeNelle.Village.World
{
    /// <summary>Shows a brief region+threat hint when the hero nears a village gate.</summary>
    public sealed class GateIntelHud : MonoBehaviour
    {
        /// <summary>Master switch — ships ON. Set false to disable the whole feature.</summary>
        public static bool Enabled = true;

        public static GateIntelHud Instance { get; private set; }

        private const string TargetScene = "Village2";

        // The gate objects VillageSceneBuilder.Walls.cs names. Cached once per scene
        // load so the per-frame path never touches Find/FindObjectsOfType.
        private static readonly string[] GateNames =
        {
            "Gate-North-Main",
            "Gate-South-Main",
            "Gate-East-Side",
            "Gate-West-Side",
        };

        [Tooltip("Hero must be within this many metres of a gate to see its intel.")]
        public float ShowRange = 9f;

        [Tooltip("How far OUTSIDE the gate to sample the region beyond (metres).")]
        public float SamplePush = 8f;

        [Tooltip("Seconds between proximity re-evaluations (throttled, not per-frame).")]
        public float Interval = 0.2f;

        private readonly List<Transform> _gates = new List<Transform>();
        private Transform _hero;
        private float _nextCheck;

        private Canvas _canvas;
        private RectTransform _panelRect;
        private Text _label;
        private Transform _shownGate;   // gate the label currently describes (null = hidden)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Enabled) return;                 // ships dark if disabled
            if (Instance != null) return;
            if (SceneManager.GetActiveScene().name != TargetScene) return;
            new GameObject("GateIntelHud").AddComponent<GateIntelHud>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            BuildCanvas();
            CacheGates();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Re-cache gates + drop hero ref whenever a scene loads; hide if we left
            // the Village (gates only live there).
            _hero = null;
            CacheGates();
            if (scene.name != TargetScene) Hide();
        }

        // =====================================================================
        // Cached gate discovery (once per scene load — never per frame).
        // =====================================================================
        private void CacheGates()
        {
            _gates.Clear();
            if (SceneManager.GetActiveScene().name != TargetScene) return;
            for (int i = 0; i < GateNames.Length; i++)
            {
                var go = GameObject.Find(GateNames[i]);
                if (go != null) _gates.Add(go.transform);
            }
        }

        // =====================================================================
        // Throttled proximity check.
        // =====================================================================
        private void Update()
        {
            if (!Enabled) { Hide(); return; }
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + Mathf.Max(0.05f, Interval);

            if (_gates.Count == 0) { Hide(); return; }

            EnsureHero();
            if (_hero == null) { Hide(); return; }

            Transform near = FindNearestGate();
            if (near == null) { Hide(); return; }

            Show(near);
        }

        private void EnsureHero()
        {
            if (_hero != null) return;
            var p = GameObject.FindWithTag("Player");
            if (p == null)
            {
                // "HeroTarget" may be undefined (FindWithTag throws on an undefined tag).
                var ht = SafeFindWithTag("HeroTarget");
                if (ht != null) p = ht;
            }
            _hero = p != null ? p.transform : null;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        private Transform FindNearestGate()
        {
            Transform best = null;
            float bestSqr = ShowRange * ShowRange;
            Vector3 hp = _hero.position;
            for (int i = 0; i < _gates.Count; i++)
            {
                var g = _gates[i];
                if (g == null) continue;
                float sqr = (g.position - hp).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = g; }
            }
            return best;
        }

        // =====================================================================
        // Region resolution — sample a point just OUTSIDE the gate and classify it
        // with the shared DeNelle.Core.World.ZoneManager.
        // =====================================================================
        private static Vector3 SampleBeyond(Transform gate, float push)
        {
            // Village sits at world origin; "outside" is radially away from centre.
            Vector3 p = gate.position;
            Vector3 outward = new Vector3(p.x, 0f, p.z);
            if (outward.sqrMagnitude < 0.0001f)
                outward = gate.forward;          // gate exactly at centre (shouldn't happen)
            outward.Normalize();
            return new Vector3(p.x, 0f, p.z) + outward * Mathf.Max(0f, push);
        }

        private void Show(Transform gate)
        {
            if (_panelRect == null) return;

            // Only rebuild the text when the targeted gate changes (cheap steady state).
            if (gate != _shownGate)
            {
                _shownGate = gate;
                Vector3 sample = SampleBeyond(gate, SamplePush);
                RegionZone zone = ZoneManager.ZoneAt(sample);
                int threat = ZoneManager.ThreatLevel(sample);
                string cardinal = zone != null ? zone.Cardinal : "";
                string name = zone != null ? zone.DisplayName : "Unknown";

                if (_label != null)
                    _label.text = string.IsNullOrEmpty(cardinal)
                        ? name + "   -   Threat " + threat
                        : cardinal + " -> " + name + "   -   Threat " + threat;
            }

            if (!_panelRect.gameObject.activeSelf)
                _panelRect.gameObject.SetActive(true);
        }

        private void Hide()
        {
            _shownGate = null;
            if (_panelRect != null && _panelRect.gameObject.activeSelf)
                _panelRect.gameObject.SetActive(false);
        }

        // =====================================================================
        // Code-built uGUI (build-safe font, no EventSystem) — a single label panel
        // anchored bottom-centre.
        // =====================================================================
        private void BuildCanvas()
        {
            var go = new GameObject("GateIntelCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 29000;        // below CampPromptUI (30000), above HUD
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var panelGo = new GameObject("GateIntelPanel");
            panelGo.transform.SetParent(_canvas.transform, false);
            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0.05f, 0.06f, 0.10f, 0.82f);
            _panelRect = img.rectTransform;
            // Bottom-centre strip.
            _panelRect.anchorMin = new Vector2(0.5f, 0f);
            _panelRect.anchorMax = new Vector2(0.5f, 0f);
            _panelRect.pivot = new Vector2(0.5f, 0f);
            _panelRect.sizeDelta = new Vector2(460f, 56f);
            _panelRect.anchoredPosition = new Vector2(0f, 96f);

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(panelGo.transform, false);
            _label = lblGo.AddComponent<Text>();
            _label.font = BuiltinFont();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(1f, 0.90f, 0.66f);
            _label.fontSize = 24;
            _label.fontStyle = FontStyle.Bold;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.text = "";
            var lrt = _label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            _panelRect.gameObject.SetActive(false);
        }

        private static Font BuiltinFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
