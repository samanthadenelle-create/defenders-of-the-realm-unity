// =============================================================================
// BuildingSign — a reusable world-space name label that floats over a building.
// -----------------------------------------------------------------------------
// The "upgrade district" buildings (Sawmill, Armorer, Crystal Mine ...) each want
// a small floating nameplate so the player can read what a building is from the
// over-the-shoulder camera. This is the building twin of TownsfolkBubble — same
// self-building, billboarded, no-UGUI design — re-homed for a static structure
// rather than a wandering villager.
//
// ── Why a TextMesh, not a UGUI Canvas ──
// TownsfolkBubble (this module's sibling) deliberately avoids a world-space UGUI
// Canvas so the DeNelle.Village asmdef never has to pull in the separate
// UnityEngine.UI assembly. BuildingSign follows the same idiom: a single 3D
// TextMesh on a billboarded container, built once in Awake, low-alloc. The font
// is the build-safe builtin LegacyRuntime.ttf so it renders in a player build
// with no font asset to ship.
//
// The MAIN SESSION attaches this to each building during scene placement and
// calls Configure(label) once with the resolved display name
// (VillageStrings.BuildingName(def)). The sign builds itself, billboards to the
// active camera in LateUpdate, and re-resolves Camera.main lazily so a camera
// that spawns after the sign still gets picked up.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A self-building world-space name label that floats just above a building
    /// and billboards to the active camera. Built in code (a 3D
    /// <see cref="TextMesh"/> on a billboarded container) — no prefab, no UGUI
    /// Canvas, no UXML. Call <see cref="Configure"/> once with the building's
    /// player-facing display name.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingSign : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Local-space height above this transform the label sits at — " +
                 "clears the building roof.")]
        [SerializeField] private float _height = 4.5f;

        [Tooltip("Character size of the label — keep small/legible on mobile.")]
        [SerializeField] private float _characterSize = 0.18f;

        [Tooltip("TextMesh font size (the rasterized resolution of the glyphs).")]
        [SerializeField] private int _fontSize = 84;

        [Header("Style")]
        [Tooltip("Label text colour (warm parchment ink, readable on outdoor terrain).")]
        [SerializeField] private Color _textColor = new Color(0.98f, 0.96f, 0.90f, 1f);

        [Tooltip("Drop-shadow / outline colour behind the text for legibility.")]
        [SerializeField] private Color _shadowColor = new Color(0.06f, 0.05f, 0.04f, 0.85f);

        // ── Runtime ──────────────────────────────────────────────────────────

        private Transform _root;        // billboarded container for the text + shadow
        private TextMesh _text;
        private TextMesh _shadow;
        private Camera _faceCamera;
        private bool _built;

        /// <summary>The label currently displayed (empty until configured).</summary>
        private string _label = string.Empty;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            Build();
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            // Re-resolve the active camera lazily so a camera that spawns after
            // this sign (additive scene load, hero camera built later) is still
            // picked up. Camera.main is cached once we have a live one.
            if (_faceCamera == null) _faceCamera = Camera.main;
            if (_faceCamera == null) return;

            // Billboard: face the label flat toward the camera so it stays
            // readable under the over-the-shoulder / tilted village view.
            Vector3 toCam = _root.position - _faceCamera.transform.position;
            if (toCam.sqrMagnitude > 0.0001f)
                _root.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Sets the sign's label. Call once with the building's resolved,
        /// player-facing display name (e.g. <c>VillageStrings.BuildingName(def)</c>
        /// -> "Sawmill"). A null / empty label hides the sign. Cheap to call again
        /// — it just retargets the existing TextMeshes, no re-allocation.
        /// </summary>
        public void Configure(string label)
        {
            if (!_built) Build();

            _label = label ?? string.Empty;
            if (_text != null) _text.text = _label;
            if (_shadow != null) _shadow.text = _label;

            bool visible = !string.IsNullOrEmpty(_label);
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        // ── Construction ─────────────────────────────────────────────────────

        /// <summary>Builds the billboarded container + label TextMesh (+ shadow) once.</summary>
        private void Build()
        {
            if (_built) return;

            _root = new GameObject("SignRoot").transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, _height, 0f);

            // Build-safe builtin font — ships with the player, no font asset
            // dependency (the BattleHUD / TownsfolkBubble lesson: don't rely on a
            // serialized font reference in a code-built UI).
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Shadow first (slightly behind + offset) so the main text reads over it.
            _shadow = MakeText("SignShadow", font, _shadowColor);
            _shadow.transform.localPosition = new Vector3(0.02f, -0.02f, 0.01f);

            // Main label, proud of the shadow.
            _text = MakeText("SignText", font, _textColor);
            _text.transform.localPosition = new Vector3(0f, 0f, 0f);

            // Hidden until Configure() gives it a label.
            _root.gameObject.SetActive(false);
            _built = true;
        }

        /// <summary>Creates a centred TextMesh child under the billboard root.</summary>
        private TextMesh MakeText(string name, Font font, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localScale = Vector3.one;

            var tm = go.AddComponent<TextMesh>();
            if (font != null)
            {
                tm.font = font;
                // The TextMesh's MeshRenderer must use the font's material or
                // glyphs render as solid quads.
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;
            }
            tm.text = _label;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = colour;
            tm.fontSize = _fontSize;
            tm.characterSize = _characterSize;
            tm.richText = false;
            return tm;
        }
    }
}
