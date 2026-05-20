// =============================================================================
// HeroBodySwapper — at scene start, swap the hero's placeholder Wizard body
// for the FBX matching the player's chosen HeroClass (Knight / Ranger / Mage).
// -----------------------------------------------------------------------------
// VillageSceneBuilder bakes the scene with a Wizard placeholder (since the
// builder runs at edit time and doesn't see runtime state). This component
// runs at runtime, reads GameStateService.State.HeroClass, and if the choice
// isn't Mage it loads the matching FBX from Resources/Heroes/<slug>.fbx,
// destroys the old "HeroBody" child, and instantiates the new mesh in its
// place. Idempotent — only runs once per scene.
//
// Resources/Heroes/<slug>.fbx is the canonical pickup path because Resources
// is auto-included in player builds (no Addressables wiring needed for the
// hand-imported Tripo FBXs).
// =============================================================================

using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeroBodySwapper : MonoBehaviour
    {
        private const float TargetHeightMeters = 2.0f;

        private void Start()
        {
            HeroClass cls = ResolveHeroClass();
            string slug = SlugFor(cls);
            if (slug == null) return;             // Mage = default body, no swap.

            var prefab = Resources.Load<GameObject>("Heroes/" + slug);
            if (prefab == null)
            {
                Debug.LogWarning("[HeroBodySwapper] Resources/Heroes/" + slug +
                                 ".fbx not found — keeping placeholder body.");
                return;
            }

            // Remove the old body if one exists.
            var old = transform.Find("HeroBody");
            if (old != null) Destroy(old.gameObject);

            var body = Instantiate(prefab, transform);
            body.name = "HeroBody";
            body.transform.localPosition = Vector3.zero;
            // Tripo FBXs export with their forward along -Z (model faces the
            // camera in the title pose). The hero root rotates to face the
            // move direction, so without this 180° correction the wizard's
            // BACK turns toward the press direction. Owner ask 2026-05-20:
            // "can they turn left when going left?"
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            NormalizeHeight(body, TargetHeightMeters);

            StripRigidbodies(body);
            StripColliders(body);
            RetargetMaterialsToUrp(body);
            Debug.Log("[HeroBodySwapper] Swapped hero body to " + slug + ".fbx");
        }

        private static HeroClass ResolveHeroClass()
        {
            var svc = GameStateService.Instance;
            if (svc == null) return HeroClass.Mage;
            var opt = svc.State?.HeroClass.ToNullable();
            return opt ?? HeroClass.Mage;
        }

        /// <summary>
        /// Tripo FBXs come in with Phong materials that URP can't render —
        /// the mesh shows as a transparent magenta ghost. Walk the renderers,
        /// pull each material's main texture if any, and wrap them in URP/Lit
        /// so the body lands with its proper texture in the player build.
        /// </summary>
        private static void RetargetMaterialsToUrp(GameObject body)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (litShader == null) return;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    // Already URP-compatible? Skip.
                    if (src.shader != null && src.shader.name != null &&
                        src.shader.name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
                        continue;
                    Texture tex = null;
                    if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                    if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                    Color col = src.HasProperty("_Color") ? src.color : Color.white;

                    var newMat = new Material(litShader);
                    newMat.name = (src.name ?? "Tripo") + " (URP)";
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", col);
                    if (newMat.HasProperty("_Color"))     newMat.SetColor("_Color", col);
                    if (tex != null)
                    {
                        if (newMat.HasProperty("_BaseMap")) newMat.SetTexture("_BaseMap", tex);
                        if (newMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", tex);
                    }
                    if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", 0.15f);
                    if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic", 0f);
                    mats[i] = newMat;
                }
                r.sharedMaterials = mats;
            }
        }

        private static string SlugFor(HeroClass cls) => cls switch
        {
            HeroClass.Knight => "Knight",
            HeroClass.Ranger => "Ranger",
            _ => null,    // Mage uses the scene-builder's Wizard placeholder
        };

        private static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.01f) return;
            float scale = targetHeight / b.size.y;
            go.transform.localScale *= scale;
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (c != null) Destroy(c);
        }

        private static void StripRigidbodies(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) Destroy(rb);
        }
    }
}
