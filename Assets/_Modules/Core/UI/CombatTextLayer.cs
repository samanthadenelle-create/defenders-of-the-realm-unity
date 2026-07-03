// =============================================================================
// CombatText / CombatTextLayer — pooled, capped, NON-STACKING combat stamps.
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 §1.8 — Kit-team-owned, single writer.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// ROOT CAUSE THIS REPLACES (named in the architecture doc §0, felt-verified):
// PlayerAttackController.OnParrySuccess (:383) + the riposte swing (:633) call
// DamageNumberSpawner.SpawnLabel (:140) — pooled but UNCAPPED, UN-DEDUPED
// world-space TextMesh at 1.4-1.6x scale; every parried hit inside the 0.25s
// parry window spawned another overlapping giant label. P3 swaps those two call
// sites to CombatText.Show(...); damage NUMBERS stay on DamageNumberSpawner
// (feel-tuned, out of scope §3.5).
//
// THE CONTRACT (§1.8):
//   - ONE screen-space layer; POOL OF 6, hard cap (oldest is recycled).
//   - PER-KIND DEDUPE, 0.5s window: a repeat refreshes the live stamp and bumps
//     an "xN" counter ("PARRY! x3") instead of spawning.
//   - Font size CAPPED (<=44 reference px; stamp role font — Acme).
//   - Auto-expire ~0.9s with an eased rise + fade.
//   - Obsidian styling: stamp font, dark outline, kind-tinted.
// MOBILE LENS: pool entries pre-built once; Update mutates alpha/position only;
// label strings rebuilt only on Show/dedupe-bump (never per frame).
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>What a combat stamp announces — drives tint + dedupe bucketing (§1.8).</summary>
    public enum CombatTextKind { Parry, Riposte, Block, Status }

    /// <summary>
    /// The one static entry point for combat text stamps. Safe from any call site:
    /// lazily builds its screen-space layer, never throws, no-ops outside play mode.
    /// </summary>
    public static class CombatText
    {
        /// <summary>Show a stamp for <paramref name="kind"/> near <paramref name="worldPos"/>.
        /// A repeat of the same kind inside the 0.5s window refreshes + counts ("PARRY! x3")
        /// instead of stacking a new label — the §1.8 anti-spam law.</summary>
        public static void Show(CombatTextKind kind, string text, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            var layer = CombatTextLayer.Instance;
            if (layer != null) layer.Push(kind, text, worldPos);
        }
    }

    /// <summary>The pooled screen-space combat-text layer (see file header for the contract).</summary>
    public sealed class CombatTextLayer : MonoBehaviour
    {
        private const int PoolSize = 6;            // hard cap (§1.8)
        private const float DedupeWindow = 0.5f;   // per-kind repeat window
        private const float Lifetime = 0.9f;       // auto-expire
        private const float RisePx = 90f;          // eased rise distance (reference px)
        private const float MaxFontSize = 44f;     // size cap (§1.8)

        private sealed class Entry
        {
            public GameObject go;
            public RectTransform rect;
            public TextMeshProUGUI label;
            public CombatTextKind kind;
            public bool live;
            public float age;
            public int count;
            public string baseText;
            public Vector2 basePos;
        }

        private static CombatTextLayer _instance;
        private readonly Entry[] _pool = new Entry[PoolSize];
        private RectTransform _canvasRect;
        private Canvas _canvas;

        /// <summary>The lazily-built singleton layer (null only when construction failed).</summary>
        public static CombatTextLayer Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    _instance = Guard.Try("UI", "build CombatTextLayer", () =>
                    {
                        var go = new GameObject("CombatTextLayer");
                        DontDestroyOnLoad(go);
                        return go.AddComponent<CombatTextLayer>();
                    }, null);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            // Own overlay canvas above the battle HUD, below modals.
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            // NO GraphicRaycaster — the layer is purely decorative and must never eat taps.
            _canvasRect = (RectTransform)transform;

            for (int i = 0; i < PoolSize; i++)
            {
                var e = new Entry();
                e.go = new GameObject("Stamp" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
                e.go.transform.SetParent(transform, false);
                e.rect = (RectTransform)e.go.transform;
                e.rect.sizeDelta = new Vector2(560f, 90f);
                e.label = e.go.GetComponent<TextMeshProUGUI>();
                ElarionUiKit.EnsureFont(e.label, ElarionUiKit.FontRole.Stamp);   // Acme, fallback chain intact
                e.label.fontSize = MaxFontSize;
                e.label.enableAutoSizing = false;                                // the cap is the cap
                e.label.alignment = TextAlignmentOptions.Center;
                e.label.fontStyle = FontStyles.Bold;
                e.label.outlineColor = new Color32(8, 8, 12, 230);               // dark outline (legibility)
                e.label.outlineWidth = 0.22f;
                e.label.raycastTarget = false;
                e.go.SetActive(false);
                _pool[i] = e;
            }
        }

        private static Color TintFor(CombatTextKind kind)
        {
            switch (kind)
            {
                case CombatTextKind.Parry:   return new Color(0.95f, 0.82f, 0.35f, 1f); // gilt
                case CombatTextKind.Riposte: return new Color(0.95f, 0.52f, 0.22f, 1f); // ember
                case CombatTextKind.Block:   return new Color(0.72f, 0.78f, 0.86f, 1f); // steel
                default:                     return new Color(0.72f, 0.55f, 0.92f, 1f); // status violet
            }
        }

        /// <summary>Place/refresh a stamp (called via <see cref="CombatText.Show"/>).</summary>
        public void Push(CombatTextKind kind, string text, Vector3 worldPos)
        {
            // 1) Dedupe: a live same-kind stamp inside the window refreshes + counts, never stacks.
            for (int i = 0; i < PoolSize; i++)
            {
                var e = _pool[i];
                if (!e.live || e.kind != kind || e.age > DedupeWindow) continue;
                e.count++;
                e.age = 0f;                                          // refresh the lifetime
                e.label.text = e.baseText + " x" + e.count;          // "PARRY! x3"
                FlowTrace.Throttle("UI", "combattext-dedupe", 1f,
                    "CombatText dedupe: " + kind + " x" + e.count);
                return;
            }

            // 2) Take a free slot; hard cap — recycle the OLDEST live stamp when full.
            Entry take = null;
            float oldest = -1f;
            for (int i = 0; i < PoolSize; i++)
            {
                var e = _pool[i];
                if (!e.live) { take = e; break; }
                if (e.age > oldest) { oldest = e.age; take = e; }
            }
            if (take == null) return;   // unreachable (pool non-empty) — belt and braces

            take.live = true;
            take.kind = kind;
            take.age = 0f;
            take.count = 1;
            take.baseText = string.IsNullOrEmpty(text) ? kind.ToString().ToUpperInvariant() + "!" : text;
            take.label.text = take.baseText;
            take.label.color = TintFor(kind);
            take.basePos = ScreenAnchor(worldPos);
            take.rect.anchoredPosition = take.basePos;
            take.rect.localScale = Vector3.one;
            take.go.SetActive(true);
        }

        /// <summary>World position → canvas-local anchored position (centre-anchored), with a fallback
        /// to upper-centre-screen when no camera exists.</summary>
        private Vector2 ScreenAnchor(Vector3 worldPos)
        {
            var cam = Camera.main;
            Vector2 screen = cam != null
                ? (Vector2)cam.WorldToScreenPoint(worldPos)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.62f);
            // Behind the camera → keep it readable at upper-centre.
            if (cam != null && cam.WorldToScreenPoint(worldPos).z < 0f)
                screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.62f);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out local);
            return local;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                var e = _pool[i];
                if (!e.live) continue;
                e.age += dt;
                float k = Mathf.Clamp01(e.age / Lifetime);
                float ease = 1f - (1f - k) * (1f - k);               // quad ease-out rise
                e.rect.anchoredPosition = e.basePos + new Vector2(0f, RisePx * ease);
                var c = e.label.color;
                c.a = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;      // hold, then eased fade
                e.label.color = c;
                if (k >= 1f)
                {
                    e.live = false;
                    e.go.SetActive(false);
                }
            }
        }
    }
}
