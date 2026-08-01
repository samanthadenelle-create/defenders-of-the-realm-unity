// =============================================================================
// KayKitNpcBody — WO-818: the ONE data-driven resolver for a structure's KayKit
// NPC body. The structures catalog authors repo.npcModel (a KayKit slug, owner-
// approved mapping table — creative pick is OWNER-ONLY, a swap is a one-word JSON
// retag). Both NPC injectors (BarracksNpcInjector drillmaster +
// CastleVendorNpcInjector vendors) call this FIRST; a null return means "use the
// legacy People prefab chain" (then the capsule placeholder) — never a blank NPC.
// -----------------------------------------------------------------------------
// Failure semantics (WO-818 acceptance criteria):
//   • row absent / npcModel not authored  -> quiet null (the People chain is the
//     designed fallback for un-mapped structures; no warn spam).
//   • npcModel AUTHORED but the load misses (typo'd slug / un-staged FBX)
//     -> exactly ONE FlowTrace.Warn naming slug + structure, then null so the
//     caller degrades to the People chain.
// Guard.Try wraps the catalog lookup + Resources.Load per §12 /
// docs/INSTRUMENTATION_STANDARD.md (no silent failures, one bad row never blanks
// a screen). Village -> Core only (CatalogRegistry lives in DeNelle.Core.Catalog).
// =============================================================================

using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>WO-818 — resolves a structure's data-driven KayKit NPC body (repo.npcModel).</summary>
    internal static class KayKitNpcBody
    {
        /// <summary>Resources folder the staged KayKit bodies live under (tracked, WO-818 phase 1).</summary>
        internal const string ResourcesRoot = "NPCs/KayKit/";

        /// <summary>
        /// Load the KayKit body the catalog authors for <paramref name="catalogId"/>
        /// (repo.npcModel). Null when the row/slug is absent (quiet — People chain is the
        /// authored fallback) or when an authored slug fails to load (exactly ONE
        /// FlowTrace.Warn, caller falls back — never a blank NPC).
        /// <paramref name="resolvedRes"/> = the Resources path actually loaded
        /// (for the caller's trace/verify messages); null whenever this returns null.
        /// </summary>
        internal static GameObject Load(string catalogId, string system, out string resolvedRes)
        {
            resolvedRes = null;
            if (string.IsNullOrEmpty(catalogId)) return null;

            string slug = null;
            Guard.Try(system, $"resolve npcModel for '{catalogId}'", () =>
            {
                var entry = CatalogRegistry.Get(catalogId);
                if (entry != null && entry.repo != null) slug = entry.repo.npcModel;
            });
            if (string.IsNullOrWhiteSpace(slug)) return null;   // not authored -> People chain, no warn

            string res = ResourcesRoot + slug;
            GameObject body = null;
            Guard.Try(system, $"load KayKit npc body '{res}'", () =>
            {
                body = Resources.Load<GameObject>(res);
            });
            if (body == null)
            {
                // Authored-but-broken slug: ONE Warn (captured by the F8 harness), then the
                // caller's People-chain fallback keeps the structure speaker visible.
                FlowTrace.Warn(system,
                    $"KayKit npc body '{slug}' for structure '{catalogId}' loads NULL from Resources/{res} " +
                    "- falling back to the People prefab chain (check repo.npcModel vs the staged Assets/Resources/NPCs/KayKit files).");
                return null;
            }
            resolvedRes = res;
            return body;
        }
    }
}
