// =============================================================================
// EchoWorkforceHud -- the self-contained Echo Workforce widget (ECHO_WORKFORCE_SPEC).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A SELF-CONTAINED, code-built uGUI overlay (NO UXML -- UXML does not render in
// player builds; PIPELINE_STATE S8). DELIBERATELY DISJOINT from VillageHudController
// (another agent may be touching the main HUD): this widget owns its OWN Canvas, so
// the two never collide. It shows:
//   - Echo count (e.g. "Echoes  2/4")
//   - silo fill % + a fill bar (silo / capacity)
//   - a "Dump All" button -> EchoService.DumpSilos()
// Mobile-friendly: anchored top-left under the safe zone, ScaleWithScreenSize, large
// tap target. Driven by EchoService (logic) via the Changed event -- a dumb view.
//
// Lives on the EchoService DDOL host (installed by EchoWorkforceBootstrap).
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DeNelle.Village
{
    /// <summary>Top-left Echo widget: count + silo fill + Dump All. Driven by EchoService.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoWorkforceHud : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _countLabel;
        private Text _siloLabel;
        private Image _fill;
        private Text _dumpLabel;

        private static readonly Color Dark  = new Color(0.06f, 0.07f, 0.10f, 0.82f);
        private static readonly Color Gold  = new Color(0.92f, 0.78f, 0.36f);
        private static readonly Color Green = new Color(0.40f, 0.78f, 0.45f);
        private static readonly Color BarBg = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BtnIdle = new Color(0.22f, 0.42f, 0.26f, 0.92f);

        // Top-left, below the typical resource bar / safe area. anchoredPosition is from
        // the top-left pivot (x right, y down).
        private static readonly Vector2 PanelPivot = new Vector2(0f, 1f);
        private static readonly Vector2 PanelPos   = new Vector2(170f, -150f);
        private static readonly Vector2 PanelSize  = new Vector2(280f, 118f);

        private void Start()
        {
            Build();
            Refresh();
            if (EchoService.Instance != null)
            {
                EchoService.Instance.Changed += Refresh;
                EchoService.Instance.EchoUnlocked += OnEchoUnlocked;
            }
        }

        private void OnDestroy()
        {
            if (EchoService.Instance != null)
            {
                EchoService.Instance.Changed -= Refresh;
                EchoService.Instance.EchoUnlocked -= OnEchoUnlocked;
            }
        }

        // -- build ----------------------------------------------------------------
        private void Build()
        {
            EnsureEventSystem();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 4500;   // above gameplay HUD, below the battle overlay (5000)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var panel = AddPanel(transform, PanelPivot, PanelPivot, PanelPos, PanelSize, Dark);

            _countLabel = AddText(panel.transform, "Echoes  1/4", 20, Gold, TextAnchor.UpperLeft);
            var cr = _countLabel.rectTransform;
            cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f); cr.anchoredPosition = new Vector2(0f, -8f);
            cr.sizeDelta = new Vector2(-20f, 26f);

            // Silo fill bar.
            var barBg = AddPanel(panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                 new Vector2(132f, -44f), new Vector2(256f, 18f), BarBg);
            _fill = AddImage(barBg.transform, Green);
            var fr = _fill.rectTransform; Stretch(fr); fr.offsetMin = new Vector2(2f, 2f); fr.offsetMax = new Vector2(-2f, -2f);
            _fill.type = Image.Type.Filled; _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = (int)Image.OriginHorizontal.Left; _fill.fillAmount = 0f;

            _siloLabel = AddText(panel.transform, "Silo  0%", 15, new Color(0.85f, 0.85f, 0.9f), TextAnchor.UpperLeft);
            var sr = _siloLabel.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f); sr.anchoredPosition = new Vector2(0f, -40f);
            sr.sizeDelta = new Vector2(-20f, 22f);

            // Dump All button (large tap target).
            var dumpPanel = AddPanel(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                                     new Vector2(0f, 26f), new Vector2(-20f, 40f), BtnIdle);
            var btn = dumpPanel.gameObject.AddComponent<Button>();
            btn.targetGraphic = dumpPanel;
            btn.onClick.AddListener(OnDumpTapped);
            _dumpLabel = AddText(dumpPanel.transform, "Dump All", 18, Color.white, TextAnchor.MiddleCenter);
            Stretch(_dumpLabel.rectTransform);
        }

        // -- view refresh (logic -> view) -----------------------------------------
        private void Refresh()
        {
            var svc = EchoService.Instance;
            if (svc == null) return;
            if (_countLabel != null) _countLabel.text = $"Echoes  {svc.EchoCount}/{svc.MaxEchoes}";
            if (_fill != null) _fill.fillAmount = svc.FillFraction;
            if (_siloLabel != null)
            {
                int pct = Mathf.RoundToInt(svc.FillFraction * 100f);
                _siloLabel.text = $"Silo  {pct}%   ({Mathf.FloorToInt((float)svc.Silo)})";
            }
        }

        private void OnDumpTapped()
        {
            int banked = EchoService.Instance != null ? EchoService.Instance.DumpSilos() : 0;
            if (_dumpLabel != null)
            {
                _dumpLabel.text = banked > 0 ? $"+{banked} banked!" : "Silo empty";
                CancelInvoke(nameof(ResetDumpLabel));
                Invoke(nameof(ResetDumpLabel), 1.5f);
            }
            Refresh();
        }

        private void ResetDumpLabel()
        {
            if (_dumpLabel != null) _dumpLabel.text = "Dump All";
        }

        private void OnEchoUnlocked(int newCount)
        {
            // Lightweight "New Echo joined!" toast on the count label.
            if (_countLabel != null)
            {
                _countLabel.text = "New Echo joined!";
                CancelInvoke(nameof(Refresh));
                Invoke(nameof(Refresh), 2.0f);
            }
        }

        // -- tiny uGUI builders (solid sprites, WebGL-safe) -----------------------
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }

        private static Image AddPanel(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        private static Image AddImage(Transform parent, Color col)
        {
            var go = new GameObject("Img");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            return img;
        }

        private static Text AddText(Transform parent, string s, int size, Color col, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = s; t.fontSize = size; t.color = col; t.alignment = anchor;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
