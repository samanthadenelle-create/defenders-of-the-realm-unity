// =============================================================================
// VfxMirrorPairSet - the SINGLE declaration of every VFX source -> tracked-mirror
// pair in the project, placed where BOTH the builders and their gates can read it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// WHY THIS FILE EXISTS - the gap between two correct assertions.
//
// A "mirror" is an AssetDatabase.CopyAsset of a GITIGNORED pack prefab into a
// tracked Resources path, with its art re-pointed at Assets/Resources/VFX/_Shared/
// and (for the surface set) its demo geometry / colliders / loop flags stripped.
// Four builders make them:
//
//   PortalCircleVfxMirror        1 pair
//   TalentPointerVfxMirror       1 pair
//   StatusVfxMirrors            13 rows (3 with a pack source; 10 hand-staged)
//   SurfaceImpactVfxMirrors      5 pairs  (declared in SurfaceImpactMirrorSet)
//
// On 2026-08-21 the five PP_*Impacts catalog rows still pointed at the UNREPAIRED
// PACK prefabs while their repaired mirrors sat committed on disk beside them. The
// mirrors had been built; HovlVfxCatalogGenerator.Generate had not been re-run, so
// VfxMirrorRedirect never got the chance to redirect the rows. Every one of those
// pack prefabs is IsLoop, and HitSurface.cs:221 plays its key fire-and-forget with
// the returned VFXHandle DISCARDED, which per VFXManager.Hovl.cs:399-422 burns one
// of the 20 global loop slots PERMANENTLY. The owner's session shows the result:
// "active loops 24/24 (cap hit)" 21 times, never recovering.
//
// ⚠ TWO ORACLES BOTH PASSED GREEN WHILE THAT SHIPPED, AND NEITHER WAS WRONG:
//   * VfxLoopFlagRegression compares a row's stored IsLoop against THE PREFAB THAT
//     ROW POINTS AT. Row and prefab were both the pack copy, both said loop. Green.
//   * SurfaceImpactVfxRegression asserts THE MIRROR is one-shot. It was. Green.
//   NEITHER ASSERTED THAT THE ROW POINTS AT THE MIRROR. The bug lived exactly in
//   the gap between two correct assertions, which is why the fix is a THIRD
//   assertion over the JOIN of the two - VfxLoopFlagRegression.CheckRowsResolveToMirrors -
//   and not a tightening of either one.
//
// WHY THE DECLARATION LIVES ON THE REGRESSION SIDE, WHICH LOOKS BACKWARDS.
//
// The assembly graph is ONE-WAY: DeNelle.Editor references DeNelle.EditorRegression
// and NOT the reverse (see both .asmdef files). A table declared in a builder is
// therefore INVISIBLE to the gate that judges it, and the only way to give the gate
// the paths would be to hand-copy them - a second copy is precisely how a tool and
// its gate come to disagree while both report success (CLAUDE.md §2 stale WO block,
// §5 retired dependency table, §16 the R2 push+verify pair that drifted between two
// chains). So the declaration sits where both can reach it. This is the same
// inversion SurfaceImpactMirrorSet and VfxLoopFlagRegression's shared derivation
// already use, and this file AGGREGATES that set rather than restating it.
//
// This file holds DATA ONLY - no AssetDatabase, no UnityEditor calls, no behaviour -
// so it stays readable from any editor assembly and cannot develop opinions.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// Every declared (gitignored pack source -> committed tracked mirror) pair, from
    /// all four mirror builders. The builders read their paths back OUT of here.
    /// </summary>
    public static class VfxMirrorPairSet
    {
        // ── Portal circle (owner pick 2026-08-16, "use this rotated for the portals") ──

        public const string PortalSrc =
            "Assets/Hovl Studio/Magic circles/Prefabs/Magic circle dark star.prefab";

        /// <summary>Runtime loads this as Resources "VFX/Portal/PortalCircleDarkStar"
        /// (DungeonWorldPortalSpawner) - keep the two in lockstep.</summary>
        public const string PortalDst = "Assets/Resources/VFX/Portal/PortalCircleDarkStar.prefab";

        // ── Talent node pointer (owner pick 2026-08-16, "for nodes") ──────────────────

        public const string TalentSrc =
            "Assets/Hovl Studio/Map track markers VFX/Prefabs/Marker 2 Pointer Loop.prefab";

        /// <summary>Runtime loads this as Resources "VFX/UI/TalentNodePointer"
        /// (TalentNodeVfxRig.PointerResourcePath) - keep the two in lockstep.</summary>
        public const string TalentDst = "Assets/Resources/VFX/UI/TalentNodePointer.prefab";

        // ── Status / aura / buff family ───────────────────────────────────────────────
        //
        // A NULL source means the dest is expected to ALREADY be on disk (hand-staged
        // copies of git-TRACKED packs) and only needs the self-containment verify. Those
        // rows have no pack path to redirect FROM, and the mirror-join check below skips
        // them for the same reason: there is no source for a catalog row to wrongly hold.

        public static readonly (string src, string dst)[] StatusPairs =
        {
            ("Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/BigExplosion.prefab",
             "Assets/Resources/VFX/Status/BigExplosion.prefab"),
            (null, "Assets/Resources/VFX/Status/Aura_acceleration.prefab"),
            (null, "Assets/Resources/VFX/Status/Aura_slowdown.prefab"),
            (null, "Assets/Resources/VFX/Status/backlight_health_drop.prefab"),
            (null, "Assets/Resources/VFX/Status/top_down_ice_circle.prefab"),
            (null, "Assets/Resources/VFX/Status/Character_status_sleep.prefab"),
            (null, "Assets/Resources/VFX/Status/Hit_light.prefab"),
            (null, "Assets/Resources/VFX/Markers/Marker8_SafeZoneLoop.prefab"),
            // Arcane crown (owner pick): Lana is git-TRACKED - hand-staged plain copy, verify-only.
            (null, "Assets/Resources/VFX/Aura/top_down_bomb_rainbow.prefab"),
            // Owner tag 2026-08-16 verbatim: ParticlePack FireFlies -> "Tree of Life Aura".
            // Pack is GITIGNORED; its deps (FireFly.mat etc.) are already in _Shared from the
            // 08-06 pass, so the art-mirror pass re-links this copy onto them. NOTE for the
            // verifier: mirroring the PREFAB does not by itself rebind the VFX catalog row -
            // the WO-1025 audit delta (treeHandle=live) is the proof the key resolves.
            ("Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab",
             "Assets/Resources/VFX/Aura/FireFlies.prefab"),
            // Owner pick 2026-08-16: "Buff_Light.prefab -> Knight Shield Buff or something".
            // Spells Pack is GITIGNORED - dependency mirror required (Casting_Fire class).
            ("Assets/Spells Pack/Particles/Prefabs/Buffs/Buff_Light.prefab",
             "Assets/Resources/VFX/Buffs/Buff_Light.prefab"),
            // Owner pick 2026-08-16: Lana starfall -> "Special Ability Mage cast" (tracked, plain copy).
            (null, "Assets/Resources/VFX/Aura/top_down_starfall_line_blue.prefab"),
        };

        // ── The join: every pair, from every builder ─────────────────────────────────

        /// <summary>
        /// EVERY declared pair in the project, source-first. Rows with a null/empty source
        /// are INCLUDED here (callers that only care about redirectable pairs filter them,
        /// exactly as VfxMirrorRedirect.Add already does) so this stays the honest full list
        /// rather than a filtered view somebody later mistakes for the whole set.
        /// </summary>
        public static IEnumerable<(string src, string dst)> AllPairs()
        {
            yield return (PortalSrc, PortalDst);
            yield return (TalentSrc, TalentDst);

            for (int i = 0; i < StatusPairs.Length; i++)
                yield return StatusPairs[i];

            // WO-887 surface half - the owner's five 2026-08-21 surface-impact tags. Kept in
            // its own file because SurfaceImpactVfxRegression's layer-count contract lives
            // beside it; aggregated (never restated) here.
            var surfaces = SurfaceImpactMirrorSet.Pairs;
            for (int i = 0; i < surfaces.Length; i++)
                yield return surfaces[i];
        }

        private static Dictionary<string, string> _bySource;

        /// <summary>
        /// source path -> mirror path, for every pair that HAS a source. Case-insensitive
        /// because Unity asset paths on Windows round-trip through a case-insensitive
        /// filesystem and a case-only mismatch would silently make a lookup miss - which
        /// would read as "this row is fine", the one wrong answer.
        /// </summary>
        public static Dictionary<string, string> BySource()
        {
            if (_bySource != null) return _bySource;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (src, dst) in AllPairs())
            {
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) continue;
                map[src] = dst;
            }

            _bySource = map;
            return _bySource;
        }

        /// <summary>
        /// True when <paramref name="assetPath"/> is a declared MIRROR SOURCE, handing back
        /// the mirror that should be wired instead. Pure lookup - it asks nothing of the
        /// filesystem, so the caller decides what "the mirror is not on disk" means.
        /// </summary>
        public static bool TryMirrorForSource(string assetPath, out string mirrorPath)
        {
            mirrorPath = null;
            if (string.IsNullOrEmpty(assetPath)) return false;
            return BySource().TryGetValue(assetPath, out mirrorPath);
        }

        /// <summary>Total declared pairs, and how many of those carry a pack source.</summary>
        public static void Count(out int total, out int withSource)
        {
            total = 0; withSource = 0;
            foreach (var (src, dst) in AllPairs())
            {
                total++;
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst)) withSource++;
            }
        }
    }
}
