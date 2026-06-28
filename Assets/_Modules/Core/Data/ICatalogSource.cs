// =============================================================================
// ICatalogSource — source-agnostic catalog seam (Tier-0 of
// docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md).
// -----------------------------------------------------------------------------
// Every canonical data file (gear/weapons/armor/accessories/talents/abilities/
// quests/...) is loaded through CanonicalJson. This interface abstracts WHERE
// that raw JSON text comes from so a future remote/DB source is a one-line swap
// (CanonicalJson.Source = new MyRemoteCatalogSource();) with NO call-site churn.
//
// The contract MATCHES CanonicalJson.Read exactly: given a StreamingAssets-
// relative logical path (e.g. "Data/Canonical/abilities.json") return the raw
// JSON text, or null when the catalog cannot be resolved. Implementations must
// be synchronous (callers expect a string back immediately) and must never
// throw — resolution failures return null (and self-report via FlowTrace/Guard).
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>Source-agnostic provider of canonical catalog JSON text.
    /// Default implementation is <see cref="LocalJsonCatalogSource"/> (local
    /// JSON from Resources first, StreamingAssets fallback). Swap the active
    /// source via <see cref="CanonicalJson.Source"/> to back the same catalogs
    /// with a remote/DB source without touching any call site.</summary>
    public interface ICatalogSource
    {
        /// <summary>Returns the raw JSON text for the logical catalog at
        /// <paramref name="relativePath"/> (StreamingAssets-relative, e.g.
        /// "Data/Canonical/abilities.json"), or null if it cannot be resolved.</summary>
        string Read(string relativePath);
    }
}
