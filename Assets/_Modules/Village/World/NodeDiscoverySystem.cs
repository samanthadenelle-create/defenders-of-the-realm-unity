// =============================================================================
// NodeDiscoverySystem — fog-of-war-lite discovery for outer-world nodes (DEF-189 / WO-244).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// THE FEATURE (lightweight, runtime, code-built, WebGL-safe):
//   Outer-world nodes (MineNode reserves / Settlements / ClaimableCamps) start
//   UNDISCOVERED — their visuals are dimmed/obscured until the hero (tag "Player")
//   walks within RevealRadius. On first reveal the node "lights up" (renderers
//   restore + a short fade-in) and a brief code-built toast announces the find.
//   Once discovered a node STAYS discovered, persisted to PlayerPrefs so it
//   survives a reload — no minimap/map dependency.
//
// ISOLATION (CLAUDE.md §9 — no parallel node system):
//   * Reuses the EXISTING node instances. It does NOT spawn or own nodes; it polls
//     them (MineNode via FindObjectsByType, Settlement.All, CampSystem.Camps) and
//     only reads their public transforms / renderers. No existing file is edited.
//   * Village -> Core only. Cross-module reads are null-conditional.
//   * Self-bootstraps via RuntimeInitializeOnLoadMethod (AfterSceneLoad) — NO scene
//     edit, NO prefab hard-dependency. No-ops cleanly when no nodes exist.
//
// PERSISTENCE: a stable per-node key (type + rounded world position) -> "1" in
// PlayerPrefs. Position-keyed (not name-keyed) so it is stable across sessions and
// independent of how the node was placed (scene builder vs runtime bootstrap).
//
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village.World
{
    /// <summary>
    /// DEF-189: hides/dims undiscovered outer-world nodes until the hero gets near,
    /// then reveals them (fade-in + a short toast) and remembers the discovery in
    /// PlayerPrefs. Self-bootstrapping; reuses the existing node instances.
    /// </summary>
    public sealed class NodeDiscoverySystem : MonoBehaviour
    {
        public static NodeDiscoverySystem Instance { get; private set; }

        // ── Tunables (code-only; no SO authoring) ────────────────────────────
        [Tooltip("How close (metres) the hero must come for an undiscovered node to reveal.")]
        public float RevealRadius = 24f;

        [Tooltip("Seconds the reveal fade-in takes when a node is first discovered.")]
        public float FadeInSeconds = 0.6f;

        [Tooltip("How dim an undiscovered node renders (0 = fully hidden, 1 = full bright). " +
                 "A faint silhouette reads better than a hard cut, so default is a low dim.")]
        [Range(0f, 1f)] public float UndiscoveredDim = 0.18f;

        [Tooltip("Seconds the 'discovered' toast stays on screen.")]
        public float ToastSeconds = 2.5f;

        [Tooltip("Seconds between node re-scans (new nodes spawn at runtime — camps/settlements).")]
        public float RescanInterval = 1.0f;

        // PlayerPrefs key prefix — value "1" once discovered. Position-keyed.
        private const string PrefKeyPrefix = "dotr-node-discovered-";

        // ── Tracked node entry ───────────────────────────────────────────────
        private sealed class Entry
        {
            public Transform Node;          // the node root we track + measure distance to
            public int TrackedId;           // GetInstanceID() captured at track time (for prune)
            public string Key;              // stable PlayerPrefs key
            public bool Discovered;         // revealed this session (or restored from prefs)
            public Renderer[] Renderers;    // the visuals we dim / restore
            public Color[] BaseColors;      // captured full-bright colors to fade back to
            public float FadeT;             // 0..1 reveal fade progress (1 = done)
        }

        private readonly List<Entry> _entries = new List<Entry>();
        // Set of node-instance ids we've already wrapped, so a re-scan never double-tracks one.
        private readonly HashSet<int> _tracked = new HashSet<int>();

        private Transform _hero;
        private float _rescanTimer;

        // Toast UI (code-built, build-safe — no UXML, no EventSystem).
        private Canvas _canvas;
        private Text _toastLabel;
        private float _toastTimer;

        // =====================================================================
        // Self-bootstrap (no scene edit). Runs after every scene load; idempotent.
        // =====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("NodeDiscoverySystem");
            go.AddComponent<NodeDiscoverySystem>();
            Object.DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildToastCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Periodic re-scan: nodes can spawn after scene load (camps, settlements,
            // pet-harvest nodes). Cheap — just wraps any node we haven't tracked yet.
            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = Mathf.Max(0.25f, RescanInterval);
                Rescan();
            }

            EnsureHero();
            TickReveal();
            TickToast();
        }

        // =====================================================================
        // Scan: collect the existing node instances (no new node system). Each is
        // wrapped once; new ones picked up on the next re-scan.
        // =====================================================================
        private void Rescan()
        {
            // MineNodes (scene-placed reserves + runtime bootstrap nodes).
#if UNITY_2023_1_OR_NEWER
            var mines = Object.FindObjectsByType<MineNode>(FindObjectsSortMode.None);
#else
            var mines = Object.FindObjectsOfType<MineNode>();
#endif
            if (mines != null)
                foreach (var m in mines)
                    if (m != null) TrackNode(m.transform, "mine");

            // Player-built settlements (own static registry).
            var settlements = Settlement.All;
            if (settlements != null)
                for (int i = 0; i < settlements.Count; i++)
                {
                    var s = settlements[i];
                    if (s != null) TrackNode(s.transform, "settle");
                }

            // Claimable camps (the camp loop registry; inert/empty when the feature is dark).
            var camps = DeNelle.Village.World.Camps.CampSystem.Camps;
            if (camps != null)
                for (int i = 0; i < camps.Count; i++)
                {
                    var c = camps[i];
                    if (c != null) TrackNode(c.transform, "camp");
                }
        }

        private void TrackNode(Transform node, string kind)
        {
            if (node == null) return;
            int id = node.GetInstanceID();
            if (_tracked.Contains(id)) return;
            _tracked.Add(id);

            var renderers = node.GetComponentsInChildren<Renderer>(true);
            // A node with no renderers (e.g. a bare logic node) is still "discoverable"
            // for persistence, but there is nothing to dim — track it without visuals.
            var colors = new Color[renderers != null ? renderers.Length : 0];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = SafeColor(renderers[i]);

            var entry = new Entry
            {
                Node = node,
                TrackedId = id,
                Key = MakeKey(kind, node.position),
                Renderers = renderers,
                BaseColors = colors,
            };

            entry.Discovered = PlayerPrefs.GetInt(entry.Key, 0) == 1;
            entry.FadeT = entry.Discovered ? 1f : 0f;

            // Already-discovered nodes render at full bright immediately (no re-toast).
            // Fresh nodes start dimmed/obscured.
            ApplyDim(entry, entry.Discovered ? 1f : UndiscoveredDim);

            _entries.Add(entry);
        }

        // =====================================================================
        // Reveal loop: measure hero distance, flip undiscovered -> discovered on
        // proximity, drive the fade-in, prune dead entries.
        // =====================================================================
        private void TickReveal()
        {
            float revealSqr = RevealRadius * RevealRadius;
            bool heroValid = _hero != null;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e == null || e.Node == null)
                {
                    // Node despawned (mined-empty reserve, razed settlement) — drop it.
                    if (e != null) _tracked.Remove(e.TrackedId);
                    _entries.RemoveAt(i);
                    continue;
                }

                if (!e.Discovered)
                {
                    if (heroValid)
                    {
                        float sqr = (e.Node.position - _hero.position).sqrMagnitude;
                        if (sqr <= revealSqr)
                            Discover(e);
                    }
                    continue;
                }

                // Drive the reveal fade-in to completion.
                if (e.FadeT < 1f)
                {
                    float step = FadeInSeconds > 0f ? Time.deltaTime / FadeInSeconds : 1f;
                    e.FadeT = Mathf.Clamp01(e.FadeT + step);
                    ApplyDim(e, Mathf.Lerp(UndiscoveredDim, 1f, e.FadeT));
                }
            }
        }

        private void Discover(Entry e)
        {
            if (e == null || e.Discovered) return;
            e.Discovered = true;
            e.FadeT = 0f;   // begin fade from dim -> full
            PlayerPrefs.SetInt(e.Key, 1);
            PlayerPrefs.Save();

            ShowToast("New site discovered!");
            Debug.Log($"[NodeDiscovery] Revealed node '{(e.Node != null ? e.Node.name : "?")}' (key {e.Key}).");
        }

        // =====================================================================
        // Visual dim/restore. We scale each renderer's material color toward black
        // for the obscured look, and restore the captured base color on reveal. No
        // shader assumptions — works on URP Lit (_BaseColor) and legacy (.color).
        // =====================================================================
        private static void ApplyDim(Entry e, float brightness)
        {
            if (e == null || e.Renderers == null) return;
            brightness = Mathf.Clamp01(brightness);
            for (int i = 0; i < e.Renderers.Length; i++)
            {
                var r = e.Renderers[i];
                if (r == null) continue;
                Color baseCol = (e.BaseColors != null && i < e.BaseColors.Length)
                    ? e.BaseColors[i] : Color.white;
                Color dimmed = new Color(baseCol.r * brightness, baseCol.g * brightness,
                                         baseCol.b * brightness, baseCol.a);
                // Toggle the renderer off entirely when fully hidden to also drop its
                // label/footprint cheaply; on otherwise.
                bool visible = brightness > 0.001f;
                if (r.enabled != visible) r.enabled = visible;
                SetColor(r, dimmed);
            }
        }

        private static Color SafeColor(Renderer r)
        {
            if (r == null) return Color.white;
            var m = r.sharedMaterial;
            if (m == null) return Color.white;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.color;
            return Color.white;
        }

        private static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            // Use the instanced material so we don't stomp a shared asset across nodes.
            var m = r.material;
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.color = c;
        }

        // =====================================================================
        // Code-built toast (screen-space overlay; build-safe LegacyRuntime font).
        // =====================================================================
        private void BuildToastCanvas()
        {
            var go = new GameObject("NodeDiscoveryCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 29000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var panelGo = new GameObject("Toast");
            panelGo.transform.SetParent(_canvas.transform, false);
            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0.06f, 0.09f, 0.14f, 0.85f);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.82f);
            rt.anchorMax = new Vector2(0.5f, 0.82f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 56f);
            rt.anchoredPosition = Vector2.zero;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(panelGo.transform, false);
            _toastLabel = lblGo.AddComponent<Text>();
            _toastLabel.font = BuiltinFont();
            _toastLabel.alignment = TextAnchor.MiddleCenter;
            _toastLabel.color = new Color(1f, 0.9f, 0.6f);
            _toastLabel.fontSize = 24;
            _toastLabel.fontStyle = FontStyle.Bold;
            _toastLabel.text = "";
            var lrt = _toastLabel.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

            panelGo.SetActive(false);
        }

        private void ShowToast(string text)
        {
            if (_toastLabel == null) return;
            _toastLabel.text = text;
            _toastTimer = ToastSeconds;
            var panel = _toastLabel.transform.parent;
            if (panel != null) panel.gameObject.SetActive(true);
        }

        private void TickToast()
        {
            if (_toastTimer <= 0f) return;
            _toastTimer -= Time.deltaTime;

            // Fade the toast out over its last second.
            if (_toastLabel != null)
            {
                float a = Mathf.Clamp01(_toastTimer);
                var c = _toastLabel.color; c.a = a; _toastLabel.color = c;
                var panel = _toastLabel.transform.parent;
                var img = panel != null ? panel.GetComponent<Image>() : null;
                if (img != null) { var ic = img.color; ic.a = 0.85f * a; img.color = ic; }
            }

            if (_toastTimer <= 0f)
            {
                var panel = _toastLabel != null ? _toastLabel.transform.parent : null;
                if (panel != null) panel.gameObject.SetActive(false);
            }
        }

        private static Font BuiltinFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // =====================================================================
        // Helpers.
        // =====================================================================
        private void EnsureHero()
        {
            if (_hero != null) return;
            var p = SafeFindWithTag("Player");
            _hero = p != null ? p.transform : null;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        // Stable position-keyed id (rounded to the metre) so the discovery survives a
        // reload regardless of how/when the node was placed.
        private static string MakeKey(string kind, Vector3 pos) =>
            PrefKeyPrefix + kind + "-" +
            Mathf.RoundToInt(pos.x) + "_" +
            Mathf.RoundToInt(pos.y) + "_" +
            Mathf.RoundToInt(pos.z);
    }
}
