// =============================================================================
// TripoMaterialFixer — runtime fix for FBX meshes that import with legacy
// Phong/Standard materials URP can't render.
// -----------------------------------------------------------------------------
// Owner question 2026-05-20: "why do colors not show on models?"
//
// Root cause: every Tripo AI-generated FBX (Wizard, Knight, Ranger, fairy,
// dragon, fox, castle ballast tower) ships with FbxSurfacePhong materials.
// Unity 6 URP can't render Phong shaders — the mesh appears as a transparent
// pink ghost or a magenta error.
//
// This component walks every Renderer in its hierarchy on Awake and rebuilds
// each material under "Universal Render Pipeline/Lit", carrying the texture
// across (preferring _MainTex, then _BaseMap). The optional fallbackTextureName
// loads from Resources/<name> when the source material has no texture bound
// (Tripo's .fbm-folder textures sometimes don't auto-link on import).
//
// Drop this MonoBehaviour onto any GameObject whose FBX renders wrong —
// works for castle arch, pets, hero meshes, anything.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core
{
    [DisallowMultipleComponent]
    public sealed class TripoMaterialFixer : MonoBehaviour
    {
        [SerializeField] private string _fallbackTextureName;
        [SerializeField] private Color _fallbackTint = Color.white;
        [SerializeField] private bool _hasFallbackTint;
        [SerializeField] private float _smoothness = 0.15f;
        [SerializeField] private float _metallic = 0f;
        private bool _ran;

        public void SetFallbackTexture(string resourcesPath) => _fallbackTextureName = resourcesPath;

        /// <summary>
        /// Forces a solid fallback colour on every material rebuilt by this
        /// fixer. Use when the Tripo FBX's embedded textures don't extract
        /// (the player build sees no _MainTex / _BaseMap on the source) and
        /// the mesh would otherwise render solid white. Owner direction
        /// 2026-05-20: pets / heroes show white in the player despite the
        /// fixer — wire each model's species tint as a safety net.
        /// </summary>
        public void SetFallbackTint(Color tint)
        {
            _fallbackTint = tint;
            _hasFallbackTint = true;
        }

        private void Awake() => Run();

        private void Run()
        {
            if (_ran) return;
            _ran = true;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogWarning("[TripoMaterialFixer] URP/Lit shader not found — skipping.");
                return;
            }

            Texture2D fallbackTex = null;
            if (!string.IsNullOrEmpty(_fallbackTextureName))
                fallbackTex = Resources.Load<Texture2D>(_fallbackTextureName);
            Debug.Log($"[TripoMaterialFixer] {gameObject.name}: fallbackPath='{_fallbackTextureName}', loaded={fallbackTex != null}, tintActive={_hasFallbackTint}");

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    // Already URP — skip.
                    if (src != null && src.shader != null && src.shader.name != null &&
                        src.shader.name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
                        continue;

                    Texture tex = null;
                    Color col = Color.white;
                    if (src != null)
                    {
                        if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                        if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                        if (src.HasProperty("_Color")) col = src.color;
                    }
                    if (tex == null && fallbackTex != null) tex = fallbackTex;
                    // Owner 2026-05-20 ("still grey"): the fallback tint was
                    // only applied when tex == null, but Tripo's source
                    // material often has a _MainTex reference pointing at a
                    // broken/embedded texture URP renders as white. Apply the
                    // tint whenever it's been set — when a real texture also
                    // resolves the tint just multiplies (mild colour push).
                    if (_hasFallbackTint) col = _fallbackTint;

                    var newMat = new Material(lit);
                    newMat.name = (src != null && src.name != null ? src.name : "Tripo") + " (URP)";
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", col);
                    if (newMat.HasProperty("_Color"))     newMat.SetColor("_Color", col);
                    if (tex != null)
                    {
                        if (newMat.HasProperty("_BaseMap")) newMat.SetTexture("_BaseMap", tex);
                        if (newMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", tex);
                    }
                    if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", _smoothness);
                    if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic", _metallic);
                    mats[i] = newMat;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
