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

        // WO-973: these were 4.4 x 1.7 with a 34-char wrap and a hardcoded 0.16 text
        // scale — ~2.4x the shipped village bubble (DeNelle.Village.TownsfolkBubble:
        // 1.8 x 0.7, wrap 22, textScale 0.07) on every axis at once. That comparison is
        // authored-value against authored-value, so it does not depend on the camera
        // that was frozen during the run which found this. Matched to the shipped
        // sibling's numbers; the glyph metrics below are matched too, so the two
        // bubbles now read at the same size.
        [Tooltip("World-unit width of the bubble panel.")]
        [SerializeField] private float _panelWidth = 1.8f;

        [Tooltip("World-unit height of the bubble panel.")]
        [SerializeField] private float _panelHeight = 0.7f;

        [Header("Style")]
        [Tooltip("Bubble panel fill colour (warm parchment).")]
        [SerializeField] private Color _panelColor = new Color(0.953f, 0.918f, 0.835f, 0.96f);

        [Tooltip("Bubble text colour (dark ink).")]
        [SerializeField] private Color _textColor = new Color(0.149f, 0.122f, 0.094f, 1f);

        [Tooltip("Characters per line before the text wraps. Smaller = narrower bubble. " +
                 "Wrap and panel size are ONE knob, not two: the wrap sets the line length, " +
                 "the line length sets the text bounds, and ResizePanelToText grows the quad " +
                 "to those bounds. Tune them together.")]
        [SerializeField] private int _wrapWidth = 22;

        [Tooltip("Character scale of the text — keep small so the bubble doesn't dominate " +
                 "the frame. WO-973: this used to be a hardcoded Vector3.one * 0.16f in " +
                 "Build(), which meant it could not be tuned from the scene at all (the " +
                 "scene serialises _panelWidth/_panelHeight/_wrapWidth but could not serialise " +
                 "a literal). Serialised so the bake carries it like every other number.")]
        [SerializeField] private float _textScale = 0.07f;

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
            bool settledThisFrame = false;
            if (_resizePending > 0)
            {
                ResizePanelToText();
                _resizePending--;
                settledThisFrame = _resizePending == 0;
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

            // WO-973: measure AFTER the final resize AND after the billboard, so the
            // numbers describe the frame the player actually sees. Earlier emit points
            // (Bryn.Configure, or Show) all report a pre-settle size — the panel is
            // still at its authored minimum and Camera.main may not be seated yet.
            if (settledThisFrame) TraceSettledGeometry();
        }

        // ── Legibility instrumentation (WO-973) ──────────────────────────────

        /// <summary>
        /// Emits the ONE line that can tell a healthy bubble from an unreadable one.
        /// The old <c>bubble=ok</c> asserted construction and said nothing about
        /// legibility — it printed green next to a card covering 60 % of the screen.
        /// This prints the MEASURED world span, the resulting SCREEN span as a
        /// fraction of the viewport, how far off the camera's image plane the
        /// billboard actually sits, and whether the parent chain is shearing the
        /// quad — so the broken case reads differently from the healthy one.
        /// Never stripped (§12); flag off via <c>FlowTrace.Enabled</c> if it ever
        /// gets noisy.
        /// </summary>
        private void TraceSettledGeometry()
        {
            Guard.Try("Dungeon", "WandererBubble.TraceSettledGeometry", () =>
            {
                if (_panelRenderer == null || _root == null) return;

                Transform panelT = _panelRenderer.transform;

                // ── World span: the panel quad plus the glyphs, encapsulated. ──
                Bounds world = _panelRenderer.bounds;
                var textRenderer = _text != null ? _text.GetComponent<Renderer>() : null;
                if (textRenderer != null) world.Encapsulate(textRenderer.bounds);

                Vector3 panelScale = panelT.lossyScale;
                Vector3 parentScale = transform.lossyScale;

                // HYPOTHESIS A — inherited NON-UNIFORM scale. A rotated child under a
                // non-uniformly scaled parent is genuinely SHEARED (the transform stops
                // being a similarity), and the quad renders as a parallelogram no matter
                // what the camera does. shear=1.00 rules this out; anything else is it.
                float maxP = Mathf.Max(parentScale.x, Mathf.Max(parentScale.y, parentScale.z));
                float minP = Mathf.Min(parentScale.x, Mathf.Min(parentScale.y, parentScale.z));
                float shear = minP > 0.0001f ? maxP / minP : -1f;

                // HYPOTHESIS B — billboard obliquity. LookRotation(toCam) aims the quad at
                // the camera's POSITION, not at its image PLANE, so the quad is oblique to
                // the view by the off-axis angle and foreshortens into a trapezoid. The
                // bigger the card and the nearer the camera, the more visible that is.
                // offAxis=0 deg means plane-aligned (no perspective skew possible).
                float offAxis = -1f, distToCam = -1f;
                float wFrac = -1f, hFrac = -1f, areaFrac = -1f;
                int cornersOffscreen = -1, cornersBehind = -1;

                Camera cam = _faceCamera != null ? _faceCamera : Camera.main;
                if (cam == null)
                {
                    FlowTrace.Warn("Dungeon",
                        $"WandererBubble.settled on '{name}': NO CAMERA — cannot measure screen span. " +
                        $"render=TextMesh+Quad (no Canvas) worldSpan={world.size.x:F2}x{world.size.y:F2}m " +
                        $"panelScale={panelScale.x:F2}x{panelScale.y:F2} parentScale={parentScale.x:F2},{parentScale.y:F2},{parentScale.z:F2} " +
                        $"shear={shear:F2} wrap={_wrapWidth} textScale={_textScale:F3} lines={CountLines()}");
                    return;
                }

                distToCam = Vector3.Distance(cam.transform.position, _root.position);
                offAxis = Vector3.Angle(_root.forward, cam.transform.forward);

                // ── Screen span: project the quad's four corners. ──
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                cornersOffscreen = 0;
                cornersBehind = 0;
                float pw = Mathf.Max(1, cam.pixelWidth);
                float ph = Mathf.Max(1, cam.pixelHeight);
                for (int i = 0; i < 4; i++)
                {
                    var local = new Vector3((i == 0 || i == 3) ? -0.5f : 0.5f,
                                            (i < 2) ? 0.5f : -0.5f, 0f);
                    Vector3 sp = cam.WorldToScreenPoint(panelT.TransformPoint(local));
                    if (sp.z <= 0f) cornersBehind++;
                    if (sp.x < 0f || sp.x > pw || sp.y < 0f || sp.y > ph) cornersOffscreen++;
                    if (sp.x < minX) minX = sp.x;
                    if (sp.x > maxX) maxX = sp.x;
                    if (sp.y < minY) minY = sp.y;
                    if (sp.y > maxY) maxY = sp.y;
                }
                wFrac = (maxX - minX) / pw;
                hFrac = (maxY - minY) / ph;
                areaFrac = wFrac * hFrac;

                string body =
                    $"WandererBubble.settled on '{name}': render=TextMesh+Quad (no Canvas) " +
                    $"worldSpan={world.size.x:F2}x{world.size.y:F2}m " +
                    $"panelScale={panelScale.x:F2}x{panelScale.y:F2} parentScale={parentScale.x:F2},{parentScale.y:F2},{parentScale.z:F2} " +
                    $"shear={shear:F2} wrap={_wrapWidth} textScale={_textScale:F3} lines={CountLines()} " +
                    $"distToCam={distToCam:F2}m billboardOffAxis={offAxis:F1}deg " +
                    $"screenSpan={wFrac * 100f:F0}%x{hFrac * 100f:F0}% area={areaFrac * 100f:F0}% " +
                    $"cornersOffscreen={cornersOffscreen}/4 cornersBehindCam={cornersBehind}/4";

                // The line has to READ differently when it is broken, or it is the same
                // hollow "ok" this WO exists to kill. A card eating a third of the frame,
                // or with a corner past the viewport edge (that is how the text was
                // clipped mid-word), is a READABILITY defect and says so.
                bool oversize = areaFrac > 0.35f;
                bool clipped = cornersOffscreen > 0 || cornersBehind > 0;
                if (oversize || clipped)
                {
                    FlowTrace.Warn("Dungeon",
                        body + " — UNREADABLE: " +
                        (oversize ? $"covers {areaFrac * 100f:F0}% of frame (>35% budget); " : "") +
                        (clipped ? "part of the panel is outside the viewport, so the line is cut off; " : "") +
                        "shear!=1.00 means an inherited non-uniform parent scale; " +
                        "shear==1.00 with a large billboardOffAxis means perspective foreshortening " +
                        "of an oversized card.");
                }
                else
                {
                    FlowTrace.Step("Dungeon", body + " — legible.");
                }
            });
        }

        /// <summary>Line count of the wrapped body — the driver behind panel height.</summary>
        private int CountLines()
        {
            if (_text == null || string.IsNullOrEmpty(_text.text)) return 0;
            int n = 1;
            foreach (char c in _text.text) if (c == '\n') n++;
            return n;
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

            // WO-973: Show was entirely untraced — there was no line at all saying the
            // bubble had been asked to speak. This one deliberately says PRE-SETTLE: the
            // panel is still at its authored minimum for up to 3 more frames, so it is
            // NOT the legibility measurement. TraceSettledGeometry is (LateUpdate, on
            // the frame _resizePending hits 0).
            FlowTrace.Step("Dungeon",
                $"WandererBubble.Show on '{name}': chars={(_text.text != null ? _text.text.Length : 0)} " +
                $"lines={CountLines()} wrap={_wrapWidth} textScale={_textScale:F3} " +
                $"authoredPanel={_panelWidth:F2}x{_panelHeight:F2}m — PRE-SETTLE, " +
                "measured size follows in WandererBubble.settled.");
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
            textGo.transform.localScale = Vector3.one * _textScale;
            _text = textGo.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = _textColor;
            // WO-973: glyph world size scales as localScale * characterSize * fontSize.
            // This was 0.16 * 0.5 * 64 = 5.12 against the shipped village bubble's
            // 0.07 * 0.32 * 96 = 2.15 — 2.4x. Matched to the village metrics exactly.
            _text.fontSize = 96;
            _text.characterSize = 0.32f;
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
