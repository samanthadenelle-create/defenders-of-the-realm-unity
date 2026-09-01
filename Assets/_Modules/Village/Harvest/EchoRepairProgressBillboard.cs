using TMPro;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>Truthful world-space readout for the target currently being mended.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoRepairProgressBillboard : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _fill;
        private TextMeshProUGUI _label;
        private Transform _camera;
        private float _hideAt;

        public static void Show(GameObject target, float progress, string echoName)
        {
            if (target == null) return;
            var host = target;
            var view = host.GetComponent<EchoRepairProgressBillboard>();
            if (view == null) view = host.AddComponent<EchoRepairProgressBillboard>();
            view.EnsureBuilt();
            view._fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            view._label.text = (string.IsNullOrWhiteSpace(echoName) ? "Echo" : echoName) + " repairing";
            view._canvas.gameObject.SetActive(true);
            view._hideAt = Time.unscaledTime + 2.5f;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;
            var root = new GameObject("EchoRepairProgress", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, ResolveHeight(), 0f);
            root.transform.localScale = Vector3.one * 0.01f;
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260f, 62f);

            var bg = ElarionUiKit.AddImage(root.transform, "Track",
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.34f),
                new Color(0.03f, 0.025f, 0.05f, 0.9f));

            var fill = ElarionUiKit.AddImage(bg.transform, "Fill", Vector2.zero,
                new Vector2(0f, 1f), new Color(0.25f, 0.82f, 0.92f, 1f));
            _fill = fill.GetComponent<RectTransform>();

            _label = ElarionUiKit.Label(root.transform, "", 0.36f, 1f, Color.white,
                27, TextAlignmentOptions.Center, 0f, 1f, bold: true);
            _label.gameObject.name = "EchoName";
        }

        private float ResolveHeight()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            float top = transform.position.y + 2.5f;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) top = Mathf.Max(top, renderers[i].bounds.max.y);
            return Mathf.Clamp(top - transform.position.y + 0.7f, 2.5f, 12f);
        }

        private void LateUpdate()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;
            if (Time.unscaledTime > _hideAt) { _canvas.gameObject.SetActive(false); return; }
            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
            if (_camera != null)
                _canvas.transform.rotation = Quaternion.LookRotation(_canvas.transform.position - _camera.position);
        }
    }
}
