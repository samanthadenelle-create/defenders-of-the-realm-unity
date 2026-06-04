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
        [SerializeField] private float _height = 2.3f;

        [Tooltip("World-unit width of the bubble panel.")]
        [SerializeField] private float _panelWidth = 1.8f;

        [Tooltip("World-unit height of the bubble panel.")]
        [SerializeField] private float _panelHeight = 0.7f;

        [Header("Style")]
        [Tooltip("Bubble panel fill colour (soft white speech-bubble).")]
        [SerializeField] private Color _panelColor = new Color(1f, 0.99f, 0.96f, 0.94f);

        [Tooltip("Bubble panel outline colour (subtle ink border).")]
        [SerializeField] private Color _outlineColor = new Color(0.14f, 0.11f, 0.09f, 0.85f);

        [Tooltip("Bubble text colour (dark ink).")]
        [SerializeField] private Color _textColor = new Color(0.10f, 0.08f, 0.06f, 1f);

        [Tooltip("Characters per line before the text wraps. Smaller = narrower bubble.")]
        [SerializeField] private int _wrapWidth = 22;

        [Tooltip("Character scale of the text — keep small so the bubble doesn't dominate.")]
        [SerializeField] private float _textScale = 0.07f;

        [Header("Auto-hide")]
        [Tooltip("Owner playtest 2026-06-03: a bubble shown while the Keeper stands still " +
                 "next to a villager lingered forever (it only hid when the Keeper walked out " +
                 "of the speak radius). Auto-hide the bubble this many seconds after it is shown " +
                 "so lines read and then clear. Re-showing a new line resets the timer. " +
                 "0 or less = never auto-hide (legacy behaviour).")]
        [SerializeField] private float _autoHideSeconds = 4.5f;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Transform _root;        // billboarded container for panel + text
        private TextMesh _text;
        private Renderer _panelRenderer;
        private Transform _outlineTransform;   // scale-tracked alongside the panel
        private Camera _faceCamera;
        private bool _visible;
        private bool _built;
        // TextMesh bounds aren't valid until it renders (a frame or two after .text is
        // set), so a same-frame ResizePanelToText reads stale bounds and the text spills
        // outside the panel (DEF-107). Re-measure for a few frames after Show to catch
        // the real glyph extents once they exist.
        private int _resizePending;
        // Unscaled time at which the currently-shown bubble should auto-hide. <= 0 = no timeout
        // armed (legacy / _autoHideSeconds disabled). Timestamp check — no per-frame alloc.
        private float _autoHideAt;

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

            // Auto-hide after the timeout so a bubble shown next to a stationary Keeper
            // clears instead of lingering forever (owner playtest 2026-06-03). Cheap
            // unscaled-time stamp compare — no per-frame allocation. Re-showing a new
            // line re-arms _autoHideAt in Show().
            if (_autoHideAt > 0f && Time.unscaledTime >= _autoHideAt)
            {
                Hide();
                return;
            }

            // Re-fit the panel once TextMesh bounds become valid (DEF-107) — a few
            // frames of re-measure catches the real glyph extents post-render.
            if (_resizePending > 0)
            {
                ResizePanelToText();
                _resizePending--;
            }

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
        // Owner 2026-05-20: only ONE bubble allowed on screen at a time. Two
        // overlapping bubbles look like UI glitches. Whoever called Show last
        // owns the slot; previous bubble force-hides.
        private static TownsfolkBubble s_activeBubble;

        public void Show(string speakerName, string line)
        {
            if (!_built) Build();

            string body = Wrap(line ?? string.Empty);
            _text.text = string.IsNullOrEmpty(speakerName)
                ? body
                : speakerName + "\n" + body;

            // Resize the panel to fit the actual text so long lines are never
            // clipped. TextMesh.GetComponent<Renderer>().bounds reflects the
            // rendered glyph extents once .text is assigned (world space, so
            // we convert back to panel-local units via _textScale).
            ResizePanelToText();
            _resizePending = 3;   // re-measure over the next frames once bounds are valid

            // Steal the active-bubble slot from whoever held it before.
            if (s_activeBubble != null && s_activeBubble != this)
                s_activeBubble.SetVisible(false);
            s_activeBubble = this;
            SetVisible(true);

            // (Re)arm the auto-hide timeout — a fresh line resets the clock.
            _autoHideAt = _autoHideSeconds > 0f
                ? Time.unscaledTime + _autoHideSeconds
                : 0f;
        }

        /// <summary>
        /// Measures the TextMesh renderer bounds and expands the bubble panel
        /// quads to fit, preserving a padding margin. World-space positioning
        /// is unchanged — only the quad scales (visual width/height) adjust.
        /// </summary>
        private void ResizePanelToText()
        {
            if (_text == null || _panelRenderer == null) return;

            // TextMesh reports bounds in world space relative to _root. Since
            // _root may be rotated (billboard), use localScale and the mesh
            // renderer's bounds size — both are reliable immediately after
            // assigning .text, with no layout frame needed.
            var textRenderer = _text.GetComponent<Renderer>();
            if (textRenderer == null) return;

            // The text GameObject is scaled by _textScale, so world-space
            // bounds are already in world-unit metres. Add horizontal and
            // vertical padding so the text doesn't butt up against the edges.
            const float PadX = 0.18f;
            const float PadY = 0.16f;

            float requiredWidth  = textRenderer.bounds.size.x + PadX * 2f;
            float requiredHeight = textRenderer.bounds.size.y + PadY * 2f;

            // Never shrink below the inspector-configured minimums so a single
            // short word still produces a visible bubble.
            float newWidth  = Mathf.Max(requiredWidth,  _panelWidth);
            float newHeight = Mathf.Max(requiredHeight, _panelHeight);

            // Rescale the panel quad (and the outline quad behind it).
            _panelRenderer.transform.localScale =
                new Vector3(newWidth, newHeight, 1f);

            if (_outlineTransform != null)
                _outlineTransform.localScale =
                    new Vector3(newWidth + 0.06f, newHeight + 0.06f, 1f);
        }

        /// <summary>Hides the bubble.</summary>
        public void Hide()
        {
            if (s_activeBubble == this) s_activeBubble = null;
            _autoHideAt = 0f;   // disarm so a later re-show isn't insta-hidden by a stale stamp
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

            float aspect = _panelWidth / _panelHeight;

            // Dark outline backing — slightly larger rounded-rect behind the
            // fill, gives the bubble its "ink border" silhouette.
            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outline.name = "BubbleOutline";
            DestroyImmediate(outline.GetComponent<Collider>());
            outline.transform.SetParent(_root, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            outline.transform.localScale = new Vector3(_panelWidth + 0.06f, _panelHeight + 0.06f, 1f);
            _outlineTransform = outline.transform;   // retained for ResizePanelToText
            ApplyRoundedMaterial(outline.GetComponent<Renderer>(), _outlineColor, aspect, 0.28f);

            // Fill panel — cream chat-bubble face with SDF rounded corners.
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "BubblePanel";
            DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(_root, false);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localScale = new Vector3(_panelWidth, _panelHeight, 1f);
            _panelRenderer = panel.GetComponent<Renderer>();
            ApplyRoundedMaterial(_panelRenderer, _panelColor, aspect, 0.28f);

            // Tail — proper triangle mesh pointing down at the speaker. Sits
            // at the bottom-centre of the bubble. Painted with the same fill
            // colour so it reads as part of the bubble silhouette.
            BuildTailMesh(_root, _panelHeight, _panelColor, _outlineColor);

            // The text — a TextMesh, slightly proud of the panel so it never
            // z-fights the quad. Smaller character size + tighter wrap so the
            // bubble reads as a chat balloon, not a billboard.
            var textGo = new GameObject("BubbleText");
            textGo.transform.SetParent(_root, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            textGo.transform.localScale = Vector3.one * _textScale;
            _text = textGo.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = _textColor;
            _text.fontSize = 96;
            _text.characterSize = 0.32f;
            _text.richText = false;

            // DEF-151 / owner playtest 2026-06-03 ("camera gets caught on word bubbles"):
            // the whole bubble hierarchy goes on "Ignore Raycast" (layer 2). The bubble is a
            // non-blocking visual — it must NEVER push the gameplay camera. The townsperson
            // root is on Default, which SmartMobileCamera's occlusion spherecast mask
            // (Default|Building|Tower) includes; a billboard quad swinging in front of the
            // camera would otherwise let the occlusion pass catch on it and pull the view in.
            // "Ignore Raycast" is excluded from that mask (it's a non-world-geometry layer), so
            // real walls/buildings/towers still block the camera (DEF-151 holds) while the
            // bubble never can. Quad colliders are already stripped above; this layer move is
            // the belt-and-braces guarantee even if a collider ever survives on a bubble part.
            SetLayerRecursively(_root.gameObject, IgnoreRaycastLayer);

            _built = true;
        }

        // "Ignore Raycast" is a Unity built-in layer (index 2) — always defined, and the one
        // layer SmartMobileCamera's collision mask is documented to exclude.
        private const int IgnoreRaycastLayer = 2;

        /// <summary>Sets <paramref name="go"/> and every descendant onto <paramref name="layer"/>.</summary>
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        /// <summary>Builds a flat unlit material for the outline + tail quads.</summary>
        private static void ApplyFlatMaterial(Renderer renderer, Color colour)
        {
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            if (shader == null) return;
            var mat = new Material(shader) { color = colour };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            renderer.sharedMaterial = mat;
        }

        /// <summary>
        /// Applies the rounded-rect SDF shader (DeNelle/UI/RoundedChatBubble)
        /// so the bubble panel renders with soft corners. Falls back to a flat
        /// material if the shader isn't compiled (build error / dev-only path).
        /// </summary>
        private static void ApplyRoundedMaterial(Renderer renderer, Color colour, float aspect, float radius)
        {
            if (renderer == null) return;
            Shader shader = Shader.Find("DeNelle/UI/RoundedChatBubble");
            if (shader == null)
            {
                ApplyFlatMaterial(renderer, colour);
                return;
            }
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Aspect", Mathf.Max(0.1f, aspect));
            mat.SetFloat("_Radius", Mathf.Clamp(radius, 0f, 0.5f));
            renderer.sharedMaterial = mat;
        }

        /// <summary>
        /// Builds a small downward-pointing triangle mesh as the bubble's tail.
        /// Fill triangle + slightly larger ink-coloured outline triangle behind
        /// it so the tail edge matches the bubble's outline.
        /// </summary>
        private static void BuildTailMesh(Transform parent, float panelHeight, Color fill, Color outline)
        {
            float w = panelHeight * 0.42f;
            float h = panelHeight * 0.50f;
            float yOffset = -panelHeight * 0.5f - h * 0.05f;

            // Outline tail — slightly larger triangle behind the fill.
            var outlineGo = MakeTriangleGO("BubbleTailOutline", w + 0.05f, h + 0.05f, outline);
            outlineGo.transform.SetParent(parent, false);
            outlineGo.transform.localPosition = new Vector3(-panelHeight * 0.4f, yOffset, 0.004f);

            // Fill tail — front.
            var fillGo = MakeTriangleGO("BubbleTailFill", w, h, fill);
            fillGo.transform.SetParent(parent, false);
            fillGo.transform.localPosition = new Vector3(-panelHeight * 0.4f, yOffset, 0.002f);
        }

        private static GameObject MakeTriangleGO(string name, float width, float height, Color colour)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            // Vertices: top-centre (apex of triangle is BOTTOM since tail
            // points DOWN), bottom-left, bottom-right.
            var mesh = new Mesh { name = "BubbleTail" };
            mesh.vertices = new[]
            {
                new Vector3(-width * 0.5f,  height * 0.5f, 0f), // upper-left
                new Vector3( width * 0.5f,  height * 0.5f, 0f), // upper-right
                new Vector3( 0f,           -height * 0.5f, 0f), // bottom point (the tip)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.uv = new[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0f),
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            ApplyFlatMaterial(mr, colour);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
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
