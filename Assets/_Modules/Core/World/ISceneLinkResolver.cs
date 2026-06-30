// =============================================================================
// ISceneLinkResolver — Core-defined contract for the data-driven scene-link
// resolver (WO1). Lives in DeNelle.Core so any module can request a crossing via
// CoreServices.SceneLinkResolver without referencing the Village implementation.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// The concrete host (DeNelle.Village.SceneLinkResolverHost) self-bootstraps,
// loads Resources/Data/scene-links.json, and registers itself via
// CoreServices.RegisterSceneLinkResolver. Callers MUST null-check
// (e.g. CoreServices.SceneLinkResolver?.TravelTo("castle_to_outerworld")).
// =============================================================================
using System.Collections.Generic;

namespace DeNelle.Core.World
{
    /// <summary>
    /// Routes the hero across the world graph from a data-driven catalog of
    /// SceneLink rows. Resolved through CoreServices.SceneLinkResolver.
    /// </summary>
    public interface ISceneLinkResolver
    {
        /// <summary>Looks up a link by id. Returns false (and a null link) if unknown.</summary>
        bool TryGetLink(string id, out SceneLink link);

        /// <summary>
        /// Loads the link's target scene (additive or single), finds the entry
        /// spawn, and warps the hero with NavMesh validation. No-op (logged) if the
        /// id is unknown or the target scene is not yet in Build Settings.
        /// </summary>
        void TravelTo(string linkId);

        /// <summary>All loaded links, in catalog order.</summary>
        IReadOnlyList<SceneLink> AllLinks { get; }
    }
}
