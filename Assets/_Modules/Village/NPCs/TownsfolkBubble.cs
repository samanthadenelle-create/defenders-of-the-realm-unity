// =============================================================================
// TownsfolkBubble — an ambient villager's world-space speech bubble (D).
// -----------------------------------------------------------------------------
// The engage-on-approach word bubble for the village's ambient townsfolk. It is
// the DeNelle.Village twin of the dungeon's WandererBubble (Bryn's bubble) —
// same self-building, billboarded TextMesh-on-a-quad design.
//
// ── Why a new class, not a reuse of WandererBubble ──
// WandererBubble lives in the DeNelle.Dungeons assembly. The module-isolation
// rule (a gameplay module references only DeNelle.Core / DeNelle.Data, never
// another gameplay module) forbids DeNelle.Village taking a dependency on
// DeNelle.Dungeons. So the *design* of WandererBubble is reused verbatim — a
// self-building world-space panel + 3D text that billboards to the camera —
// re-homed in DeNelle.Village. No prefab, no UGUI Canvas (which would pull the
// separate UnityEngine.UI assembly in); just core UnityEngine types.
//
// The bubble builds its own panel + text in Awake(), so the scene builder only
// needs to AddComponent it (by reflection) and call Show / Hide via AmbientNPC.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A self-building world-space speech bubble for an ambient villager — a
    /// billboarded parchment panel + 3D text. <see cref="AmbientNPC"/> drives it
    /// directly through <see cref="Show"/> / <see cref="Hide"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownsfolkBubble : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Local-space height above this transform the panel sits at — " +
                 "clears the villager's head.")]
        [SerializeField] private float _height = 2.5f;

        [Tooltip("World-unit width of the bubble panel.")]
        [SerializeField] private float _panelWidth = 4.2f;

        [Tooltip("World-unit height of the bubble panel.")]
        [SerializeField] private float _panelHeight = 1.6f;

        [Header("Style")]
        [Tooltip("Bubble panel fill colour (warm parchment).")]
        [SerializeField] private Color _panelColor = new Color(0.972f, 0.949f, 0.886f, 0.97f);

        [Tooltip("Bubble text colour (dark ink).")]
        [SerializeField] private Color _textColor = new Color(0.157f, 0.129f, 0.102f, 1f);

        [Tooltip("Characters per line before the text wraps.")]
        [SerializeField] private int _wrapWidth = 32;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Transform _root;        // billboarded container for panel + text
        private TextMesh _text;
        private Renderer _panelRenderer;
        private Camera _faceCamera;
        private bool _visible;
        private bool _built;

        /// <summary>True while the bubble is currently shown.</summary>
        public bool IsVisible => _visible;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_visible || _root == null) return;

            // Billboard the bubble to the active camera so it stays readable
            // under the village's overhead-ish tilt.
            if (_faceCamera == null) _faceCamera = Camera.main;
            if (_faceCamera != null)
            {
                Vector3 toCam = _root.position - _faceCamera.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    _root.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Shows the bubble with <paramref name="line"/> as the spoken words.
        /// <paramref name="speakerName"/>, when given, is prepended as a quiet
        /// attribution line.
        /// </summary>
        public void Show(string speakerName, string line)
        {
            if (!_built) Build();

            string body = Wrap(line ?? string.Empty);
            _text.text = string.IsNullOrEmpty(speakerName)
                ? body
                : speakerName + "\n" + body;

            SetVisible(true);
        }

        /// <summary>Hides the bubble.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        // ── Construction ─────────────────────────────────────────────────────

        /// <summary>Builds the panel + 3D text under a billboarded container.</summary>
        private void Build()
        {
            if (_built) return;

            _root = new GameObject("BubbleRoot").transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, _height, 0f);

            // The panel — a quad with an unlit-ish tinted material.
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "BubblePanel";
            // A speech bubble is decoration — its quad collider must never
            // intercept a hero tap-to-move raycast or an ability sweep.
            var quadCollider = panel.GetComponent<Collider>();
            if (quadCollider != null) Destroy(quadCollider);
            panel.transform.SetParent(_root, false);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localScale = new Vector3(_panelWidth, _panelHeight, 1f);
            _panelRenderer = panel.GetComponent<Renderer>();
            ApplyPanelMaterial();

            // The text — a TextMesh, slightly proud of the panel so it never
            // z-fights the quad.
            var textGo = new GameObject("BubbleText");
            textGo.transform.SetParent(_root, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            textGo.transform.localScale = Vector3.one * 0.16f;
            _text = textGo.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = _textColor;
            _text.fontSize = 64;
            _text.characterSize = 0.5f;
            _text.richText = false;

            _built = true;
        }

        /// <summary>Tints the bubble panel — a flat parchment fill.</summary>
        private void ApplyPanelMaterial()
        {
            if (_panelRenderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            if (shader == null) return;
            var mat = new Material(shader) { color = _panelColor };
            // URP/Unlit exposes _BaseColor; Unlit/Color uses the legacy color.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _panelColor);
            _panelRenderer.sharedMaterial = mat;
        }

        /// <summary>Shows / hides the whole bubble container.</summary>
        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Hard-wraps <paramref name="line"/> at roughly <see cref="_wrapWidth"/>
        /// characters on word boundaries — TextMesh has no auto-wrap.
        /// </summary>
        private string Wrap(string line)
        {
            if (string.IsNullOrEmpty(line) || _wrapWidth <= 0) return line;

            var sb = new System.Text.StringBuilder(line.Length + 8);
            int lineLen = 0;
            foreach (string word in line.Split(' '))
            {
                if (lineLen > 0 && lineLen + 1 + word.Length > _wrapWidth)
                {
                    sb.Append('\n');
                    lineLen = 0;
                }
                else if (lineLen > 0)
                {
                    sb.Append(' ');
                    lineLen += 1;
                }
                sb.Append(word);
                lineLen += word.Length;
            }
            return sb.ToString();
        }
    }
}
