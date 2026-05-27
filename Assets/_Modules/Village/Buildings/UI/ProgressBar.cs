// =============================================================================
// ProgressBar — DEF-76 (Linear). A world-space fill bar that floats above a build
// site and tracks construction 0→1. namespace DeNelle.Village.UI.
// -----------------------------------------------------------------------------
// DEF-76 clarifies "world-space canvas + Billboard, facing the camera." CODEBASE
// RECONCILIATION: this project's UXML/UIDocument and uGUI-Canvas HUDs have a long
// history of rendering EMPTY in player builds (see the project memory), so — like
// the rest of GROUP 0 — the bar is built from code out of unlit primitive quads
// (a dark track + a green fill) rather than a uGUI Canvas. A Billboard component
// keeps it facing the camera. No text (no TMP dependency); the fill length IS the
// readout. SetProgress is driven by TowerConstruction; the component is created via
// the static Create() helper and never GetComponent'd per frame.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.UI
{
    /// <summary>World-space construction progress bar (two billboarded quads).</summary>
    public class ProgressBar : MonoBehaviour
    {
        private const float FullWidth = 1.2f;
        private const float Height    = 0.16f;

        private Transform _fill;

        /// <summary>Spawn a progress bar under <paramref name="parent"/> at a local offset.</summary>
        public static ProgressBar Create(Transform parent, Vector3 localOffset)
        {
            var root = new GameObject("ProgressBar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localOffset;

            var pb = root.AddComponent<ProgressBar>();
            root.AddComponent<Billboard>();
            pb.Build();
            return pb;
        }

        private void Build()
        {
            var bg = MakeQuad("BarTrack", new Color(0.05f, 0.05f, 0.06f, 1f));
            bg.transform.localScale = new Vector3(FullWidth, 0.20f, 1f);

            var fill = MakeQuad("BarFill", new Color(0.25f, 0.85f, 0.35f, 1f));
            _fill = fill.transform;
            SetProgress(0f);
        }

        private GameObject MakeQuad(string n, Color c)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = n;
            q.transform.SetParent(transform, false);

            var col = q.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = q.GetComponent<Renderer>();
            if (r != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                var m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                else m.color = c;
                r.sharedMaterial = m;
            }
            return q;
        }

        /// <summary>Set fill 0→1. Left-anchored so the bar grows rightward.</summary>
        public void SetProgress(float t)
        {
            if (_fill == null) return;
            t = Mathf.Clamp01(t);
            _fill.localScale = new Vector3(FullWidth * t, Height, 1f);
            // Keep the left edge fixed as the fill grows; sit just in front of the track.
            _fill.localPosition = new Vector3(-FullWidth * 0.5f + FullWidth * t * 0.5f, 0f, -0.01f);
        }
    }
}
