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

            // Remove the old body if one exists, but FIRST snapshot the
            // Animator's runtimeAnimatorController so the new body keeps the
            // Walk / Cast states. Tripo FBXs import without an Animator on
            // some configs — when null we still try to assign the controller
            // to a freshly-added Animator on the new body root.
            RuntimeAnimatorController controllerSnapshot = null;
            var old = transform.Find("HeroBody");
            if (old != null)
            {
                var oldAnim = old.GetComponentInChildren<Animator>();
                if (oldAnim != null) controllerSnapshot = oldAnim.runtimeAnimatorController;
                Destroy(old.gameObject);
            }

            var body = Instantiate(prefab, transform);
            body.name = "HeroBody";
            body.transform.localPosition = Vector3.zero;
            // Tripo FBXs export with their forward along -Z (model faces the
            // camera in the title pose), so they need a 180° correction to
            // align with the hero root's facing direction. KayKit FBXs ship
            // with Unity-standard +Z forward and need no rotation.
            float yaw = NeedsForwardFlip(cls) ? 180f : 0f;
            body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            NormalizeHeight(body, TargetHeightMeters);

            StripRigidbodies(body);
            StripColliders(body);
            RetargetMaterialsToUrp(body);
            // Owner ask 2026-05-20 "when hero moves can it call walk
            // animation". The snapshot-controller from the old wizard body
            // has Idle / Walk / Cast states keyed on the Speed float that
            // HeroLocomotion already drives. Reattach it to the new body
            // so movement plays the walk animation; add an Animator if the
            // FBX imported without one.
            if (controllerSnapshot != null)
            {
                var anim = body.GetComponentInChildren<Animator>();
                if (anim == null) anim = body.AddComponent<Animator>();
                anim.runtimeAnimatorController = controllerSnapshot;
                anim.applyRootMotion = false;
                // Force HeroLocomotion to re-cache its Animator reference —
                // its private _animator field still points at the destroyed
                // old animator. Reflection write because the field is
                // private (and reflection here is cheap — runs once per
                // scene load).
                var locos = GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var mb in locos)
                {
                    if (mb == null) continue;
                    if (mb.GetType().Name != "HeroLocomotion") continue;
                    var f = mb.GetType().GetField("_animator",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    f?.SetValue(mb, anim);
                }
            }
            // Owner 2026-05-20: Tripo's Send To Unity feature extracted the
            // Knight basecolor PNG. Copied into Resources/Textures/Knight.png
            // so the runtime can apply the real texture rather than the
            // stale class tint. Same hook works for Ranger if/when its
            // Send To Unity export lands.
            ApplyExtractedTexture(body, cls);
            // Safety net: if Tripo's embedded textures didn't extract, paint
            // the body with a class tint so the mesh isn't solid white.
            ApplyClassTint(body, cls);
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
            // Mage keeps the scene-builder's Wizard placeholder body — the
            // placeholder IS the Tripo Wizard FBX, so no runtime swap needed.
            _ => null,
        };

        // All three heroes are Tripo AI exports (-Z forward) — they need a
        // 180° yaw correction so they face the move direction instead of the
        // camera. Owner direction 2026-05-20: use the paid-for Tripo models
        // throughout; KayKit Adventurers swap was rolled back.
        private static bool NeedsForwardFlip(HeroClass cls) => true;

        /// <summary>
        /// Loads a per-class basecolor PNG out of Resources/Textures/
        /// (extracted via Tripo's Send-To-Unity flow on the owner's side)
        /// and assigns it to every URP material on the body. When this
        /// returns true the subsequent <see cref="ApplyClassTint"/> is a
        /// no-op for that material (it preserves textures).
        /// </summary>
        private static void ApplyExtractedTexture(GameObject body, HeroClass cls)
        {
            string texPath = cls switch
            {
                HeroClass.Knight => "Textures/Knight",
                HeroClass.Ranger => "Textures/Ranger",
                _ => null,    // Mage uses the scene Wizard placeholder body
            };
            if (string.IsNullOrEmpty(texPath)) return;
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex == null) return;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    // White base colour so the texture's actual colours show
                    // through (otherwise the class tint would multiply on top
                    // and dirty the basecolor).
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color", Color.white);
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// If the URP material on the body still ended up white (Tripo's
        /// embedded textures sometimes don't survive import), paint each
        /// material with a class tint so the body reads coloured anyway.
        /// Skipped when a real diffuse texture is already bound (e.g. the
        /// extracted basecolor PNG from Tripo's Send-To-Unity flow).
        /// </summary>
        private static void ApplyClassTint(GameObject body, HeroClass cls)
        {
            Color tint = cls switch
            {
                HeroClass.Knight => new Color(0.78f, 0.80f, 0.86f),   // steel
                HeroClass.Ranger => new Color(0.62f, 0.55f, 0.85f),   // alien-violet
                _                => new Color(0.60f, 0.45f, 0.85f),   // mage fallback
            };
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    // Only override when the material clearly has no diffuse
                    // texture — preserve real textures when Unity DID extract.
                    Texture tex = null;
                    if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
                    if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex != null) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color", tint);
                }
                r.sharedMaterials = mats;
            }
        }

        private static void NormalizeHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.01f) return;
            float scale = targetHeight / b.size.y;
            go.transform.localScale *= scale;

            // Owner 2026-05-20 ("archer appears halfway under surface"): the
            // Tripo Ranger FBX pivots near the mesh centre, so after scaling
            // the lower half drops below the hero root's Y=0. Recompute the
            // post-scale bounds and lift the body so the feet land at y=0.
            Bounds b2 = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b2.Encapsulate(renderers[i].bounds);
            float feetOffset = b2.min.y - go.transform.position.y;
            if (feetOffset < 0f)
                go.transform.localPosition -= new Vector3(0f, feetOffset, 0f);
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
