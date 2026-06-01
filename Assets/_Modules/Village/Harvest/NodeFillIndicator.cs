// =============================================================================
// NodeFillIndicator — code-built world-space harvest bar over a MineNode (WO-117).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A small floating bar above a node showing how much resource is left to mine
// (MineNode.ExtractFraction) and whether a worker is actively collecting. Built
// ENTIRELY in C# (uGUI world-space Canvas + Image) — no UXML / UIDocument, which
// do not render in builds (PIPELINE_STATE.md §8, CLAUDE.md memory).
//
// States:
//   collecting → bright fill, billboarded toward the camera
//   idle/ready → dim fill (resource present, no worker)
//   depleted   → empty + grey (waiting to respawn)
//
// Attach to a MineNode (or call Attach() to add one at runtime). Self-contained:
// it reads the node's public read-only seam only — it never banks or mutates.
// =============================================================================
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class NodeFillIndicator : MonoBehaviour
    {
        [Tooltip("The node this bar reflects. Auto-found on the same GameObject if unset.")]
        public MineNode Node;

        [Tooltip("Height above the node the bar floats at.")]
        public float HeightOffset = 2.4f;

        [Tooltip("Bar width / height in world units.")]
        public Vector2 BarSize = new Vector2(1.6f, 0.22f);

        private Canvas _canvas;
        private RectTransform _fillRect;
        private Image _fillImg;
        private Transform _cam;

        private static readonly Color CollectingColor = new Color(0.30f, 0.85f, 0.45f, 1f);
        private static readonly Color IdleColor       = new Color(0.55f, 0.70f, 0.85f, 0.7f);
        private static readonly Color DepletedColor   = new Color(0.45f, 0.45f, 0.45f, 0.6f);

        /// <summary>Add (or reuse) a fill indicator on a node's GameObject.</summary>
        public static NodeFillIndicator Attach(MineNode node)
        {
            if (node == null) return null;
            var existing = node.GetComponent<NodeFillIndicator>();
            if (existing != null) return existing;
            var ind = node.gameObject.AddComponent<NodeFillIndicator>();
            ind.Node = node;
            return ind;
        }

        private void Awake()
        {
            if (Node == null) Node = GetComponent<MineNode>();
        }

        private void Start()
        {
            BuildUi();
            var c = Camera.main;
            _cam = c != null ? c.transform : null;
        }

        private void BuildUi()
        {
            // World-space canvas anchored above the node.
            var canvasGo = new GameObject("FillBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, HeightOffset, 0f);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var crt = _canvas.GetComponent<RectTransform>();
            crt.sizeDelta = BarSize;
            canvasGo.transform.localScale = Vector3.one * 1f;

            // Background track.
            var bgGo = new GameObject("Track");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.55f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill (left-anchored, width scaled by fraction).
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            _fillImg = fillGo.AddComponent<Image>();
            _fillImg.color = IdleColor;
            _fillRect = fillGo.GetComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 1f);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
            _fillRect.sizeDelta = new Vector2(BarSize.x, 0f);
        }

        private void LateUpdate()
        {
            if (Node == null || _canvas == null) return;

            float frac = Mathf.Clamp01(Node.ExtractFraction);
            if (_fillRect != null)
                _fillRect.sizeDelta = new Vector2(BarSize.x * frac, 0f);

            if (_fillImg != null)
            {
                if (Node.IsDepleted)               _fillImg.color = DepletedColor;
                else if (Node.IsClaimedByWorker)   _fillImg.color = CollectingColor;
                else                               _fillImg.color = IdleColor;
            }

            // Billboard toward the camera.
            if (_cam == null)
            {
                var c = Camera.main;
                _cam = c != null ? c.transform : null;
            }
            if (_cam != null)
                _canvas.transform.rotation =
                    Quaternion.LookRotation(_canvas.transform.position - _cam.position);
        }
    }
}
