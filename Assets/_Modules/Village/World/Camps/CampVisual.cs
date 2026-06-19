// =============================================================================
// CampVisual - the code-built visual tell for a ClaimableCamp's stage.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Hostile -> red campfire ring (enemy-held). Cleared -> the fire goes out / a
// pale banner. Claimed -> a green claim banner. Pure code primitives so there is
// NO prefab hard-dependency; if a polyperfect prop were swapped in later, a
// missing prefab would LogWarning (never error) per CLAUDE.md.
// ASCII-only. Canon: village is Elarion.
//
// INSTRUMENTED per CLAUDE.md §12 (TGVRU): the campfire/banner primitive build is
// Guard'd; a SILENT-ABSENT visual (build threw, or a renderer never resolved) now
// self-reports via FlowTrace.Warn instead of leaving the camp with no on-screen tell.
// =============================================================================
using UnityEngine;
using DeNelle.Core.Diagnostics;   // TGVRU — FlowTrace/Guard on the camp visual build

namespace DeNelle.Village.World.Camps
{
    /// <summary>Primitive campfire/banner that recolors per <see cref="CampStage"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class CampVisual : MonoBehaviour
    {
        private ClaimableCamp _camp;
        private Renderer _fireRenderer;
        private Renderer _bannerRenderer;

        private static readonly Color HostileColor = new Color(0.85f, 0.25f, 0.10f); // red fire
        private static readonly Color ClearedColor = new Color(0.70f, 0.70f, 0.55f); // ash / pale
        private static readonly Color ClaimedColor = new Color(0.20f, 0.80f, 0.35f); // green banner

        public void Init(ClaimableCamp camp)
        {
            _camp = camp;
            BuildPrimitives();
            SetStage(camp != null ? camp.Stage : CampStage.Hostile);
        }

        private void BuildPrimitives()
        {
            // G — guard the primitive build so a throw logs (never silently swallows) and the
            // camp's stage tell can self-report if it never appeared.
            Guard.Try("Camp", "build camp visual primitives", () =>
            {
                // Campfire (low disc).
                var fire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fire.name = "Campfire";
                StripCollider(fire);
                fire.transform.SetParent(transform, false);
                fire.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);
                fire.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                _fireRenderer = fire.GetComponent<Renderer>();
                ApplyShader(_fireRenderer);

                // Banner pole + flag.
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pole.name = "Banner";
                StripCollider(pole);
                pole.transform.SetParent(transform, false);
                pole.transform.localScale = new Vector3(0.15f, 2.0f, 0.15f);
                pole.transform.localPosition = new Vector3(1.6f, 1.0f, 0f);
                _bannerRenderer = pole.GetComponent<Renderer>();
                ApplyShader(_bannerRenderer);
            });

            // V — a camp with NO renderer (build threw, or a primitive carried no Renderer) has no
            // on-screen tell of its Hostile/Cleared/Claimed stage. Self-report instead of a silent
            // blank — the owner's invisible-marker class. The camp logic still works; this is the
            // smoking gun for "the camp is there but I see nothing".
            if (_fireRenderer == null || _bannerRenderer == null)
                FlowTrace.Warn("Camp",
                    $"INVISIBLE CAMP TELL: '{name}' built no campfire/banner renderer " +
                    $"(fire={(_fireRenderer != null)}, banner={(_bannerRenderer != null)}) — " +
                    "camp stage has no visible marker (primitive build threw or carried no Renderer).");
        }

        public void SetStage(CampStage stage)
        {
            Color fireColor = stage == CampStage.Hostile ? HostileColor : ClearedColor;
            Color bannerColor = stage == CampStage.Claimed ? ClaimedColor : ClearedColor;
            Tint(_fireRenderer, fireColor);
            Tint(_bannerRenderer, bannerColor);
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private static void ApplyShader(Renderer r)
        {
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null) r.material = new Material(shader);
        }

        private static void Tint(Renderer r, Color c)
        {
            if (r != null && r.material != null) r.material.color = c;
        }
    }
}
