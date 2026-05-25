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
        [SerializeField] private bool _forceRebuild;
        private bool _ran;

        /// <summary>
        /// Rebuild EVERY material (even ones already on a URP shader) as a plain
        /// URP/Lit carrying the source's basecolor. Use when the auto-extracted
        /// Tripo materials are URP but render wrong (e.g. a vertex-colour shader
        /// painting a rainbow patchwork) — a plain URP/Lit with the same _BaseMap
        /// shows the real texture instead.
        /// </summary>
        public void ForceRebuildAll() => _forceRebuild = true;

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

        // Start (not Awake): callers like PetDeployer add this component and
        // THEN set the fallback texture name + tint on the next line. Awake
        // fires synchronously inside AddComponent, so the setters would land
        // too late and Run() would build URP materials with no diffuse and
        // no tint — the symptom Samantha hit 2026-05-24 (pets invisible,
        // labels only). Start defers Run() to the next frame so the setter
        // calls have already landed.
        private void Start() => Run();

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
                    // Already URP — skip (unless force-rebuilding: the extracted
                    // Tripo URP materials can still render wrong, so rebuild them
                    // as plain URP/Lit from their basecolor).
                    if (!_forceRebuild && src != null && src.shader != null && src.shader.name != null &&
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
                    // Tripo Phong materials sometimes export _Color as
                    // transparent black (0,0,0,0) — that'd render the rebuilt
                    // URP material as invisible. Treat near-zero alpha or
                    // near-black as "no useful source colour" and default to
                    // white so the texture or fallback tint controls the look.
                    if (col.a < 0.05f || (col.r + col.g + col.b) < 0.05f)
                        col = Color.white;
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
