// =============================================================================
// RemoteCatalogSource - WO-1331. The ICatalogSource that finally gets assigned to
// CanonicalJson.Source.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// IT IS A DECORATOR, NOT A REPLACEMENT, AND THAT IS THE WHOLE SAFETY ARGUMENT.
// It owns no loading of its own. For one catalog it either has a VALIDATED remote
// override (RemoteCatalogOverrides.TryGet) or it does not; when it does not - the
// normal case, and the only case with no database row - it delegates verbatim to
// the inner LocalJsonCatalogSource, so the resolved text is the SAME STRING the
// game would have got with this file absent.
//
// -----------------------------------------------------------------------------
// WITH THE FLAG OFF THIS TYPE IS NEVER CONSTRUCTED AND NEVER INSTALLED.
// -----------------------------------------------------------------------------
// RemoteCatalogService.Install() returns before touching CanonicalJson.Source
// when the seam is disarmed, so a default build does not merely behave like today
// - it runs the IDENTICAL code path, with CanonicalJson.Source still holding the
// LocalJsonCatalogSource its own field initializer gave it. That is why the
// flag-off claim is provable by reading, not only by testing.
//
// ASCII only. Instrumentation: FlowTrace tag "CatalogRemote". Never strip it.
// =============================================================================

using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    /// <summary>
    /// <see cref="ICatalogSource"/> that answers from a validated remote override when one
    /// stands for that exact catalog, and otherwise delegates to the inner (compiled/local)
    /// source. Synchronous, never throws, and returns null only where the inner source would.
    /// </summary>
    public sealed class RemoteCatalogSource : ICatalogSource
    {
        private readonly ICatalogSource _inner;

        /// <summary>The source every non-overridden catalog falls through to. Never null.</summary>
        public ICatalogSource Inner => _inner;

        /// <param name="inner">The fall-through source. Null is replaced with a fresh
        /// <see cref="LocalJsonCatalogSource"/> so this type can never be the reason a
        /// catalog fails to resolve.</param>
        public RemoteCatalogSource(ICatalogSource inner)
        {
            _inner = inner ?? new LocalJsonCatalogSource();
        }

        /// <inheritdoc/>
        public string Read(string relativePath)
        {
            if (RemoteCatalogOverrides.TryGet(relativePath, out string overridden))
            {
                // Warn, not Step: an overridden catalog is NOT the shipping catalog, and a
                // capture must never let that read as ordinary narration (CLAUDE.md 12).
                FlowTrace.Throttle(RemoteCatalogOverrides.Sys, "serve:" + relativePath, 60f,
                    "resolve '" + relativePath + "' <- REMOTE OVERRIDE (" + overridden.Length +
                    " chars, generation=" + RemoteCatalogOverrides.Generation + ", provenance=" +
                    RemoteCatalogOverrides.TableProvenance + "). This catalog is NOT the compiled " +
                    "one this build shipped with.");
                return overridden;
            }

            // The ordinary path, and the ONLY path with no database row: byte-for-byte
            // what CanonicalJson resolved before this seam existed.
            return _inner.Read(relativePath);
        }
    }
}
