// =============================================================================
// SurfaceImpactMirrorSet - the SINGLE declaration of WO-887's five surface-impact
// source -> tracked-mirror pairs.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// WHY THE TABLE LIVES ON THE REGRESSION SIDE, WHICH LOOKS BACKWARDS AT FIRST.
//
// Three surfaces have to agree on these ten paths:
//   * SurfaceImpactVfxMirrors  (DeNelle.Editor)          - builds and repairs them
//   * VfxMirrorRedirect        (DeNelle.Editor)          - redirects the owner's tags onto them
//   * SurfaceImpactVfxRegression (DeNelle.EditorRegression) - proves they are repaired
//
// The assembly graph is ONE-WAY: DeNelle.Editor references DeNelle.EditorRegression
// and NOT the reverse (see both .asmdef files). So a table declared in the builder
// is invisible to its own gate, and the only way to give the gate the paths would be
// to hand-copy them into it. This project has re-learned at scale what that costs -
// the stale WO number block, the retired dependency table, the R2 push+verify pair
// that drifted between two chains while both reported success. A second copy is how
// a tool and its gate come to disagree.
//
// So the declaration is placed where BOTH can reach it, which the graph makes the
// regression assembly. It is exactly the inversion VfxLoopFlagRegression already
// uses: the shared loop-vs-burst DERIVATION lives there and the builders call INTO
// it, for the same reason. This file holds data only - no AssetDatabase, no
// UnityEditor calls, no behaviour.
//
// The catalog KEY for each surface is NOT here. That lives once, in
// DeNelle.Village.HitSurfaceVfx, next to the resolution that chooses it.
// =============================================================================

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// WO-887 surface half: the owner's 2026-08-21 surface-impact tags, as
    /// (gitignored pack source, committed tracked mirror) pairs.
    /// </summary>
    public static class SurfaceImpactMirrorSet
    {
        private const string Src =
            "Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Prefabs/";
        private const string Dst = "Assets/Resources/VFX/Impact/";

        /// <summary>
        /// Every recipe in this set carries exactly this many particle layers - MEASURED on
        /// all five sources 2026-08-22 (5 GameObjects, 4 ParticleSystems, 1 MeshFilter,
        /// 1 MeshRenderer, 1 SphereCollider, 0 MonoBehaviours; the one GameObject without a
        /// ParticleSystem is the ROOT). Declared so a pack reimport that silently trimmed a
        /// layer hard-fails instead of shipping a splatter with no debris, which would still
        /// look like an impact.
        /// </summary>
        public const int RequiredLayers = 4;

        /// <summary>source (gitignored pack) -> dest (tracked Resources mirror).</summary>
        public static readonly (string src, string dst)[] Pairs =
        {
            (Src + "FleshImpacts.prefab", Dst + "FleshImpacts.prefab"),   // PP_FleshImpacts
            (Src + "MetalImpacts.prefab", Dst + "MetalImpacts.prefab"),   // PP_MetalImpacts
            (Src + "StoneImpacts.prefab", Dst + "StoneImpacts.prefab"),   // PP_StoneImpacts
            (Src + "WoodImpacts.prefab",  Dst + "WoodImpacts.prefab"),    // PP_WoodImpacts
            (Src + "SandImpacts.prefab",  Dst + "SandImpacts.prefab"),    // PP_SandImpacts
        };
    }
}
