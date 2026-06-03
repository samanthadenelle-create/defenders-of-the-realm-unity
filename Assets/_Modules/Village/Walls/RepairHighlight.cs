// =============================================================================
// RepairHighlight — the in-world highlight marker for a repairable structure.
// -----------------------------------------------------------------------------
// Workstream B — player wall-repair mechanic. A pure-code, prefab-free marker so
// the scene-setup editor file has nothing extra to wire: WallRepairController
// spawns one of these per damaged structure (a soft "this is repairable" pulse)
// and one brighter instance for the actively-selected structure.
//
// Built entirely at runtime from Unity primitives + an unlit URP material so it
// needs no imported art. A thin floating ring of quads above the structure plus
// a soft ground disc; it pulses (scale + alpha) so damaged structures read at a
// glance while the player scans the village.
//
// Module isolation: DeNelle.Village only; no other-module / HUD coupling.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A runtime-built, prefab-free highlight marker that floats over a
    /// repairable village structure. Two intensities — a calm "repairable"
    /// pulse and a bright "selected" state — set through <see cref="SetSelected"/>.
    /// Spawned and pooled by <see cref="WallRepairController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RepairHighlight : MonoBehaviour
    {
        private static readonly Color RepairableColor = new Color(0.96f, 0.77f, 0.35f, 0.55f); // amber
        private static readonly Color SelectedColor = new Color(0.49f, 0.23f, 0.93f, 0.85f);   // violet

        private MeshRenderer _ringRenderer;
        private MeshRenderer _discRenderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // DEF-226: a small floating world-space label so the amber ground disc is
        // SELF-EXPLANATORY. Without it the marker reads as an unexplained green/gold
        // glow (the transparent amber disc seen over green grass) — a mobile playtester
        // saw exactly this and could not tell what it was. Code-built TextMesh billboard
        // (no UXML, so it renders in WebGL builds), mirroring BuildingInteractable's bubble.
        private TextMesh _label;
        private Transform _labelRoot;

        private bool _selected;
        private float _radius = 2f;
        private float _phase;

        /// <summary>
        /// Builds a new highlight marker GameObject and returns its component.
        /// The marker is parented under <paramref name="parent"/> (kept off the
        /// structure transform so it survives a structure swap) and starts in the
        /// calm "repairable" state.
        /// </summary>
        public static RepairHighlight Create(Transform parent)
        {
            var go = new GameObject("RepairHighlight");
            if (parent != null) go.transform.SetParent(parent, false);
            var hl = go.AddComponent<RepairHighlight>();
            hl.Build();
            return hl;
        }

        private void Build()
        {
            _mpb = new MaterialPropertyBlock();
            var mat = BuildMarkerMaterial();

            // Ground disc — a flat quad lying on the structure footprint.
            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Disc";
            DestroyCollider(disc);
            disc.transform.SetParent(transform, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            _discRenderer = disc.GetComponent<MeshRenderer>();
            _discRenderer.sharedMaterial = mat;
            _discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _discRenderer.receiveShadows = false;

            // Floating ring — a billboard-ish quad held above the structure.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Ring";
            DestroyCollider(ring);
            ring.transform.SetParent(transform, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            _ringRenderer = ring.GetComponent<MeshRenderer>();
            _ringRenderer.sharedMaterial = mat;
            _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;

            BuildLabel();
            ApplyColor();
        }

        // DEF-226: a camera-facing "Repair" text label floating above the disc so the
        // player instantly understands the marker. TextMesh + PromptBillboard (the same
        // WebGL-safe pattern BuildingInteractable uses); no UXML/UIDocument (which do not
        // render in player builds — PIPELINE_STATE.md §8).
        private void BuildLabel()
        {
            var root = new GameObject("RepairLabel");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            root.transform.localScale = Vector3.one * 0.5f;
            _labelRoot = root.transform;

            var tm = root.AddComponent<TextMesh>();
            tm.text = "Repair";
            tm.fontSize = 64;
            tm.characterSize = 0.18f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.92f, 0.62f, 1f);   // warm gold, matches the marker
            _label = tm;

            var billboard = root.AddComponent<PromptBillboard>();
            billboard.Camera = Camera.main;
        }

        private static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        /// <summary>
        /// Builds an unlit, transparent, additive-leaning material for the marker.
        /// Tries the URP unlit shader first, then the built-in unlit fallback so
        /// the marker still renders if the project is not on URP.
        /// </summary>
        private static Material BuildMarkerMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "RepairHighlightMat" };
            // Best-effort transparency setup — harmless if a property is absent.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        /// <summary>
        /// Positions and sizes the marker over <paramref name="target"/> from its
        /// renderer bounds, so one marker fits a small wall section or a large
        /// building alike.
        /// </summary>
        public void FitTo(RepairTarget target)
        {
            if (target == null || !target.IsValid) return;

            Vector3 center;
            float radius;
            if (target.TryGetWorldBounds(out var b))
            {
                center = new Vector3(b.center.x, b.min.y, b.center.z);
                radius = Mathf.Max(b.extents.x, b.extents.z) * 1.35f + 0.6f;
            }
            else
            {
                center = target.Transform != null ? target.Transform.position : Vector3.zero;
                radius = 2f;
            }

            radius = Mathf.Clamp(radius, 1f, 9f);
            _radius = radius;
            transform.position = center;

            // Lift the label so it clears the structure it sits over (taller props get a
            // higher label), reading as "this structure is repairable".
            if (_labelRoot != null)
            {
                float lift = (target.TryGetWorldBounds(out var lb) ? lb.size.y : 2f) + 0.8f;
                _labelRoot.localPosition = new Vector3(0f, Mathf.Clamp(lift, 1.6f, 7f), 0f);
            }

            ApplyScale(1f);
        }

        /// <summary>Switches the marker between the calm repairable and bright selected look.</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyColor();
        }

        /// <summary>Shows / hides the marker without destroying it (the controller pools markers).</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        private void Update()
        {
            // Gentle pulse so damaged structures catch the eye. The selected
            // marker pulses faster + wider to read as "this one".
            float speed = _selected ? 3.4f : 1.7f;
            float amp = _selected ? 0.14f : 0.08f;
            _phase += Time.deltaTime * speed;
            float pulse = 1f + Mathf.Sin(_phase) * amp;
            ApplyScale(pulse);

            // NO spin (owner 2026-05-27): the "ring" is a solid flat quad, so
            // spinning it around its normal read as a rotating yellow SQUARE, which
            // the owner mistook for an "under attack" cue. Hold it flat — the gentle
            // scale pulse is enough "life." "Under attack" is now its own red signal
            // (StructureAttackAlert); this amber marker only means "repairable".
            if (_ringRenderer != null)
                _ringRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void ApplyScale(float pulse)
        {
            float d = _radius * 2f * pulse;
            if (_discRenderer != null)
                _discRenderer.transform.localScale = new Vector3(d, d, 1f);
            if (_ringRenderer != null)
                _ringRenderer.transform.localScale = new Vector3(d * 0.82f, d * 0.82f, 1f);
        }

        private void ApplyColor()
        {
            Color c = _selected ? SelectedColor : RepairableColor;
            ApplyColorTo(_discRenderer, new Color(c.r, c.g, c.b, c.a * 0.45f));
            ApplyColorTo(_ringRenderer, c);

            // DEF-226: keep the label readable + in step with the marker's state — a calm
            // "Repair" affordance, a clearer call to action once the structure is picked.
            if (_label != null)
            {
                _label.text  = _selected ? "Repair?" : "Repair";
                _label.color = _selected
                    ? new Color(0.86f, 0.74f, 1f, 1f)   // violet, matches the selected marker
                    : new Color(1f, 0.92f, 0.62f, 1f);  // warm gold, matches the calm marker
            }
        }

        private void ApplyColorTo(MeshRenderer r, Color c)
        {
            if (r == null) return;
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c); // URP
            _mpb.SetColor(ColorId, c);     // built-in unlit
            r.SetPropertyBlock(_mpb);
        }
    }
}
