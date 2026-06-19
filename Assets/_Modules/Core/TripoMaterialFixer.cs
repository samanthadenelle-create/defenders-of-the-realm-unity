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
using DeNelle.Core.Diagnostics;

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

        private bool _hasEmissionOverride;
        private Color _emissionOverride = Color.black;
        private const float EmissionOverrideIntensity = 0.30f; // owner: "very minimal"

        /// <summary>
        /// Owner 2026-05-25: the pets' "aura / light beams" was bright emission
        /// preserved from their source materials. Replace it with a MINIMAL,
        /// affinity-coloured glow instead (fire red / ice white / aether violet).
        /// When set, this overrides any source emission on every rebuilt material.
        /// </summary>
        public void SetEmissionOverride(Color color)
        {
            _emissionOverride = color;
            _hasEmissionOverride = true;
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

            using var _t = FlowTrace.Enter("TripoMatFix", $"Run on '{gameObject.name}'");

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                // V/T: the fixer failing to find its target shader = magenta/error everywhere it
                // is attached (heroes/pets/buildings). This is a HARD fail, not a warn — roll it
                // up to the break-log so a run self-reports the entire fixer was a no-op.
                FlowTrace.Fail("TripoMatFix",
                    $"URP/Lit shader NOT FOUND — TripoMaterialFixer on '{gameObject.name}' cannot rebuild any " +
                    "material; every mesh it covers stays on its (likely Phong/error) source shader = magenta/pink. " +
                    "URP pipeline asset missing or shader stripped from the build.");
                return;
            }

            Texture2D fallbackTex = null;
            if (!string.IsNullOrEmpty(_fallbackTextureName))
                fallbackTex = Resources.Load<Texture2D>(_fallbackTextureName);
            if (!string.IsNullOrEmpty(_fallbackTextureName) && fallbackTex == null)
                FlowTrace.Warn("TripoMatFix",
                    $"'{gameObject.name}': fallback texture '{_fallbackTextureName}' did not load from Resources — " +
                    "rebuilt materials will fall back to tint/source only.");
            FlowTrace.Step("TripoMatFix",
                $"{gameObject.name}: fallbackPath='{_fallbackTextureName}', loaded={fallbackTex != null}, tintActive={_hasFallbackTint}");

            int renderers = 0, slotsRebuilt = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                renderers++;
                // G: guard the per-material loop so ONE bad source material logs + is skipped,
                // never aborting the rebuild of the rest of this renderer's slots (Guard.TryEach
                // LogErrors the bad index via [Flow:TripoMatFix] -> break-log, then carries on).
                var rr = r;
                var matsRef = mats;
                Guard.TryEach("TripoMatFix", $"rebuild slots on '{rr.name}'", System.Linq.Enumerable.Range(0, matsRef.Length), i =>
                {
                    var src = matsRef[i];
                    // WO-34 (2026-05-25): ALWAYS rebuild — do NOT skip already-URP
                    // materials. The Tripo importer extracts materials AS URP, but
                    // those extracted URP mats render washed-out/grey (buildings
                    // were grey in ~90% of runs because their BAKED fixer had
                    // _forceRebuild=false and skipped them). Rebuilding every
                    // material as a clean URP/Lit from its real basecolor + maps is
                    // what makes colour reliable. Normal + emission are preserved
                    // below, so this is non-destructive for materials that already
                    // rendered correctly (no regression on working models).

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
                    // Preserve the normal map always (non-destructive).
                    if (src != null && src.HasProperty("_BumpMap"))
                    {
                        Texture nrm = src.GetTexture("_BumpMap");
                        if (nrm != null && newMat.HasProperty("_BumpMap"))
                        {
                            newMat.SetTexture("_BumpMap", nrm);
                            newMat.EnableKeyword("_NORMALMAP");
                        }
                    }
                    // Emission: a minimal affinity glow when overridden (pets), else
                    // preserve the source emission (buildings keep their lit windows).
                    if (_hasEmissionOverride)
                    {
                        if (newMat.HasProperty("_EmissionColor"))
                            newMat.SetColor("_EmissionColor", _emissionOverride * EmissionOverrideIntensity);
                        newMat.EnableKeyword("_EMISSION");
                        newMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    else if (src != null)
                    {
                        Texture em = src.HasProperty("_EmissionMap") ? src.GetTexture("_EmissionMap") : null;
                        Color emc = src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.black;
                        if (em != null || emc.maxColorComponent > 0.01f)
                        {
                            if (em != null && newMat.HasProperty("_EmissionMap")) newMat.SetTexture("_EmissionMap", em);
                            if (newMat.HasProperty("_EmissionColor")) newMat.SetColor("_EmissionColor", emc);
                            newMat.EnableKeyword("_EMISSION");
                            newMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        }
                    }
                    if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", _smoothness);
                    if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic", _metallic);
                    matsRef[i] = newMat;
                    slotsRebuilt++;
                });
                r.sharedMaterials = matsRef;
            }

            FlowTrace.Step("TripoMatFix",
                $"{gameObject.name}: rebuilt {slotsRebuilt} slot(s) across {renderers} renderer(s).");

            // V: post-rebuild VERIFY — every renderer this fixer covers must now be on a URP/Lit
            // shader (the result of `new Material(lit)`), NOT a Hidden/InternalError/Standard/legacy
            // shader (the exact magenta/pink/grey symptom). If ANY slot is still on a non-URP/error
            // shader, the rebuild did not take for it — FlowTrace.Fail so the run self-reports
            // instead of leaving the owner to spot magenta on a model.
            VerifyAllRenderersUrp();
        }

        // V (TGVRU): assert every renderer slot under this fixer ended on a URP shader after Run().
        // A slot still on Hidden/InternalErrorShader (magenta), Standard/Legacy (grey/pink under URP),
        // or a null shader means the rebuild silently failed for it — roll it up to the break-log.
        // Pure read-only inspection; always runs (control-flow safety, not behind a render check).
        private void VerifyAllRenderersUrp()
        {
            int checkedSlots = 0, broken = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    checkedSlots++;
                    string sn = (m != null && m.shader != null) ? m.shader.name : null;
                    bool isUrp = !string.IsNullOrEmpty(sn) &&
                                 (sn.StartsWith("Universal Render Pipeline/") || sn.StartsWith("URP/"));
                    bool isError = !string.IsNullOrEmpty(sn) &&
                                   (sn.Contains("InternalErrorShader") || sn.Contains("Hidden/"));
                    if (isUrp && !isError) continue;

                    broken++;
                    FlowTrace.Fail("TripoMatFix",
                        $"VERIFY FAILED on '{gameObject.name}' renderer '{r.name}' slot {i}: shader='{sn ?? "<null>"}' " +
                        "is NOT a URP shader after rebuild (magenta/pink/grey risk) — the URP/Lit rebuild did not take " +
                        "for this slot. Mesh will render as the error/legacy fallback.");
                }
            }
            if (broken == 0)
                FlowTrace.Step("TripoMatFix",
                    $"{gameObject.name}: VERIFY OK — all {checkedSlots} slot(s) on a URP shader (no magenta/error).");
        }
    }
}
