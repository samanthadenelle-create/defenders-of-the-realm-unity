// =============================================================================
// WandererBubble — Bryn's world-space speech bubble (Week 6 integration).
// -----------------------------------------------------------------------------
// Port spec Part 5 Week 6: "World-space speech bubble (UGUI)" for the dungeon
// NPC. This is the concrete IWandererBubble the scene builder attaches to Bryn
// and assigns to her _bubbleBehaviour seam — Bryn herself stays HUD-free and
// talks to the bubble only through the interface.
//
// ── Why TextMesh, not a UGUI Canvas ──
// The whole v2 Unity port renders UI through UI Toolkit (UnityEngine.UIElements,
// a core module — no asmdef reference). A UGUI world-space Canvas would pull the
// separate UnityEngine.UI assembly into the DeNelle.Dungeons asmdef. A
// world-space speech bubble is a tiny, self-contained piece of 3D text, so this
// uses UnityEngine.TextMesh + a MeshRenderer panel — both core UnityEngine, no
// new assembly reference. The bubble billboards to the active camera so it
// always faces the player under the isometric tilt. (Decision flagged for
// unity-decisions.md — this file does not edit that log per the task brief.)
//
// The bubble builds its own panel + text at runtime in Awake(), so the scene
// builder only needs to AddComponent it (by reflection) and hand it to Bryn.
// No prefab, no .meta hand-authoring.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A self-building world-space speech bubble for Bryn the Wanderer — a
    /// billboarded panel + 3D text implementing <see cref="IWandererBubble"/>.
    /// The dungeon scene builder attaches this to Bryn and assigns it to her
    /// <c>_bubbleBehaviour</c> field.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WandererBubble : MonoBehaviour, IWandererBubble
    {
        [Header("Layout")]
        [Tooltip("Local-space height above the bubble's own transform the panel " +
                 "sits at — clears the NPC's head.")]
        [SerializeField] private float _height = 2.6f;

        [Tooltip("World-unit width of the bubble panel.")]
        [SerializeField] private float _panelWidth = 4.4f;

        [Tooltip("World-unit height of the bubble panel.")]
        [SerializeField] private float _panelHeight = 1.7f;

        [Header("Style")]
        [Tooltip("Bubble panel fill colour (warm parchment).")]
        [SerializeField] private Color _panelColor = new Color(0.953f, 0.918f, 0.835f, 0.96f);

        [Tooltip("Bubble text colour (dark ink).")]
        [SerializeField] private Color _textColor = new Color(0.149f, 0.122f, 0.094f, 1f);

        [Tooltip("Characters per line before the text wraps.")]
        [SerializeField] private int _wrapWidth = 34;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Transform _root;        // billboarded container for panel + text
        private TextMesh _text;
        private Renderer _panelRenderer;
        private Camera _faceCamera;
        private bool _visible;
        private bool _built;
        // TextMesh bounds aren't valid until it renders (a frame or two after .text is
        // set), so a same-frame ResizePanelToText reads stale bounds and the text spills
        // outside the panel (DEF-107). Re-measure for a few frames after Show to catch
        // the real glyph extents once they exist.
        private int _resizePending;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_visible || _root == null) return;

            // Re-fit the panel once TextMesh bounds become valid (DEF-107) — a few
            // frames of re-measure catches the real glyph extents post-render.
            if (_resizePending > 0)
            {
                ResizePanelToText();
                _resizePending--;
            }

            // Billboard the bubble to the active camera so it stays readable
            // under the dungeon's fixed isometric tilt.
            if (_faceCamera == null) _faceCamera = Camera.main;
            if (_faceCamera != null)
            {
                Vector3 toCam = _root.position - _faceCamera.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    _root.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            }
        }

        // ── IWandererBubble ──────────────────────────────────────────────────

        /// <summary>
        /// Shows the bubble with <paramref name="line"/> as the spoken words.
        /// <paramref name="speakerName"/> is prepended as a quiet attribution.
        /// </summary>
        public void Show(string speakerName, string line)
        {
            if (!_built) Build();

            string body = Wrap(line ?? string.Empty);
            _text.text = string.IsNullOrEmpty(speakerName)
                ? body
                : speakerName + "\n" + body;

            // Resize the panel to fit the actual text so long lines (Bryn speaks the
            // game's longest bubble) are never clipped outside the parchment. The
            // TextMesh renderer's world-space bounds reflect the rendered glyph extents
            // once .text is assigned; re-measure over the next frames until they settle.
            ResizePanelToText();
            _resizePending = 3;

            SetVisible(true);
        }

        /// <summary>
        /// Measures the TextMesh renderer bounds and expands the bubble panel quad
        /// to fit, preserving a padding margin. World-space positioning is unchanged
        /// — only the quad's scale (visual width/height) adjusts.
        /// </summary>
        private void ResizePanelToText()
        {
            if (_text == null || _panelRenderer == null) return;

            // TextMesh reports bounds in world space relative to _root. Since _root
            // may be rotated (billboard), use the mesh renderer's bounds size — the
            // text GameObject is already scaled, so world-space bounds are in metres.
            var textRenderer = _text.GetComponent<Renderer>();
            if (textRenderer == null) return;

            // Add horizontal and vertical padding so the text doesn't butt up against
            // the parchment edges.
            const float PadX = 0.4f;
            const float PadY = 0.35f;

            float requiredWidth  = textRenderer.bounds.size.x + PadX * 2f;
            float requiredHeight = textRenderer.bounds.size.y + PadY * 2f;

            // Never shrink below the inspector-configured minimums so a single short
            // word still produces a visible bubble.
            float newWidth  = Mathf.Max(requiredWidth,  _panelWidth);
            float newHeight = Mathf.Max(requiredHeight, _panelHeight);

            _panelRenderer.transform.localScale = new Vector3(newWidth, newHeight, 1f);
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
            // The quad's box collider would intercept the hero's tap-to-move
            // raycast — a speech bubble is decoration, never a collider.
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
            if (shader == null)
            {
                // RENDER-VERIFY: no shader resolved means the quad keeps Unity's default
                // material and renders as a MAGENTA "shader-missing" panel — a glaring
                // visual break. Self-report instead of silently shipping a pink bubble.
                FlowTrace.Fail("Dungeon",
                    "WandererBubble.ApplyPanelMaterial: no Unlit shader found " +
                    "(URP/Unlit, Unlit/Color, Sprites/Default all missing) — the bubble panel " +
                    "will render MAGENTA (shader-missing). Ensure an unlit shader is in the build.");
                return;
            }
            var mat = new Material(shader) { color = _panelColor };
            // URP/Unlit exposes _BaseColor; Unlit/Color uses the legacy color.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _panelColor);
            _panelRenderer.sharedMaterial = mat;
            FlowTrace.Step("Dungeon",
                $"WandererBubble.ApplyPanelMaterial: panel material set via shader '{shader.name}'.");
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
