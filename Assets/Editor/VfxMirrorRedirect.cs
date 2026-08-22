// =============================================================================
// VfxMirrorRedirect - resolves a GITIGNORED pack prefab path to the TRACKED
// mirror of that same prefab, when one has already been built and committed.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// THE DEFECT THIS CORRECTS - MEASURED, NOT INFERRED.
//
// On 2026-08-16 (e65b549ff, "mirror pass + catalog regen outputs") three prefab
// mirror builders ran and COMMITTED tracked, self-contained copies of three pack
// prefabs:
//
//   Assets/Hovl Studio/Magic circles/.../Magic circle dark star.prefab
//        -> Assets/Resources/VFX/Portal/PortalCircleDarkStar.prefab
//   Assets/Hovl Studio/Map track markers VFX/.../Marker 2 Pointer Loop.prefab
//        -> Assets/Resources/VFX/UI/TalentNodePointer.prefab
//   Assets/UnityTechnologies/ParticlePack/.../Prefabs/BigExplosion.prefab
//        -> Assets/Resources/VFX/Status/BigExplosion.prefab
//
// The SAME commit then regenerated HovlVfxCatalog.asset - and every one of those
// rows was written pointing at the PACK prefab, not at the mirror that had just
// been made for it. The mirror was built and then not used. Measured effect
// (VfxResourceSelfContainmentRegression, 2026-08-20): HovlVfxCatalog exposure
// into gitignored art roots GREW 689 -> 702, tripping the ratchet.
//
// The cause is structural, not a typo: the catalog is generated from a path
// table (HovlVfxCatalogGenerator.Map) plus the owner's verbatim tag overlay
// (VfxManualPicks.json). BOTH record the path the owner picked in the VFX Caster
// - which is the PACK path, because that is where she browsed the art. Nothing
// in the generate step knew a tracked twin existed, so every regenerate re-wrote
// the pack GUID and quietly undid the mirror pass.
//
// THE FIX, and why it lives at GENERATE time.
//
// This is the same shape as the IsLoop rule in HovlVfxCatalogGenerator: derive
// it during Build() so a corrected catalog CANNOT be silently undone by the next
// regenerate. A redirect applied by hand to the .asset would survive exactly one
// regen. Applied here, every present AND future owner pick that lands on a pack
// prefab with a committed mirror resolves to the mirror automatically.
//
// THIS IS NOT A CREATIVE SUBSTITUTION (owner rule: CLI maps owner tags verbatim,
// never picks or substitutes an effect). A mirror is an AssetDatabase.CopyAsset
// of the owner's own pick with its art re-pointed at Assets/Resources/VFX/_Shared/
// - the same effect, byte for byte, and the only copy of it that renders at all
// on a machine without the pack. Redirecting is what makes her pick SHIP; leaving
// the pack path is what makes it resolve to nothing on a fresh clone.
//
// SINGLE HOME OF THE PAIRS. This file DECLARES NO PATHS OF ITS OWN. It reads the
// source->mirror pairs off the builders that create them (PortalCircleVfxMirror,
// TalentPointerVfxMirror, StatusVfxMirrors.Mirrors, and - WO-887 surface half -
// SurfaceImpactVfxMirrors.Mirrors), for
// the reason CLAUDE.md keeps re-learning at project scale: a second, hand-copied
// table is how a tool and its consumer come to disagree while both report
// success. Add a mirror to a builder and this resolver picks it up for free.
//
// The gitignored-art-root test is likewise NOT re-derived - it comes from
// VfxResourceSelfContainmentRegression, the single home of that rule.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Maps a gitignored pack prefab path to its committed tracked mirror, when one
    /// exists on disk. Editor-only, read-only, allocation-cheap.
    /// </summary>
    public static class VfxMirrorRedirect
    {
        private const string FlowSys = "VfxMirrorRedirect";

        private static Dictionary<string, string> _map;

        /// <summary>
        /// source pack path -> tracked mirror path, gathered from the mirror builders
        /// themselves. Pairs whose source is null (hand-staged, already-tracked copies)
        /// are skipped: there is no pack path to redirect FROM.
        /// </summary>
        public static Dictionary<string, string> Pairs()
        {
            if (_map != null) return _map;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Add(map, PortalCircleVfxMirror.SrcPath,  PortalCircleVfxMirror.DstPath);
            Add(map, TalentPointerVfxMirror.SrcPath, TalentPointerVfxMirror.DstPath);

            var status = StatusVfxMirrors.Mirrors;
            for (int i = 0; i < status.Length; i++)
                Add(map, status[i].src, status[i].dst);

            // WO-887 surface half: the owner's five 2026-08-21 surface-impact tags. Her
            // VfxManualPicks rows point at the gitignored Particle Pack, so without this
            // declaration all five resolve to NOTHING on a fresh clone / the laptop / CI.
            // Declaring the pairs is the entire wiring - SurfaceImpactVfxMirrors owns the
            // paths and this file still declares none of its own.
            var surfaces = SurfaceImpactVfxMirrors.Mirrors;
            for (int i = 0; i < surfaces.Length; i++)
                Add(map, surfaces[i].src, surfaces[i].dst);

            _map = map;
            return _map;
        }

        private static void Add(Dictionary<string, string> map, string src, string dst)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) return;
            map[src] = dst;
        }

        /// <summary>Drop the cached pair map (a builder may have added a mirror this session).</summary>
        public static void Invalidate() { _map = null; }

        /// <summary>
        /// The path that should actually be wired for <paramref name="assetPath"/>.
        ///
        /// Returns the tracked mirror when ALL of these hold, and the input unchanged
        /// otherwise - a redirect is never guessed:
        ///   * the path resolves inside a gitignored art root (the rule's single home,
        ///     VfxResourceSelfContainmentRegression.IsInGitignoredArtRoot), and
        ///   * a builder declares a mirror for exactly this source path, and
        ///   * that mirror is actually LOADABLE right now (committed and imported).
        /// The third test matters: a mirror named by a builder but never run leaves no
        /// file on disk, and redirecting onto a missing asset would turn a pack-only
        /// row into a null row - trading a fresh-clone break for a break everywhere.
        /// </summary>
        public static string Resolve(string assetPath)
        {
            string redirected;
            return TryResolve(assetPath, out redirected, out _) ? redirected : assetPath;
        }

        /// <summary>Resolve, reporting whether a redirect happened and why/why not.</summary>
        public static bool TryResolve(string assetPath, out string mirrorPath, out string detail)
        {
            mirrorPath = assetPath;
            detail = "not a gitignored pack path";

            if (string.IsNullOrEmpty(assetPath)) { detail = "empty path"; return false; }

            if (!Regression.VfxResourceSelfContainmentRegression.IsInGitignoredArtRoot(assetPath))
                return false;

            string dst;
            if (!Pairs().TryGetValue(assetPath, out dst))
            {
                detail = "gitignored, but NO tracked mirror is declared for it - this row " +
                         "still resolves to nothing on a fresh clone";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) == null)
            {
                detail = "a mirror is declared at '" + dst + "' but nothing loads there - " +
                         "the builder that makes it has not been run/committed; keeping the pack path";
                FlowTrace.Warn(FlowSys, "declared mirror missing on disk: " + dst + " (source " + assetPath + ")");
                return false;
            }

            mirrorPath = dst;
            detail = "redirected to the committed tracked mirror";
            return true;
        }
    }
}
