// =============================================================================
// StoreStockService — OFFLINE-FIRST store stock (WO-429).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// WO-429 ("Store stock from Neon DB, offline-first"). The store has ALWAYS sourced
// its BUY stock from the LOCAL baked catalog (GearCatalog -> weapons.json/armor.json
// under Resources/StreamingAssets). That offline-first guarantee is NON-NEGOTIABLE:
// the shop must show stock with NO network. Serving stock from a remote DB (Neon,
// via a backend GET) is a deliberate REFRESH layer ON TOP of the local default —
// never a hard dependency.
//
// CONTRACT (read before touching):
//   • LOCAL catalog is the authoritative DEFAULT source of truth. It is read every
//     time and is what the BUY tab renders when there is no network / no provider /
//     a failed fetch. The store is NEVER blocked or emptied on a remote miss.
//   • An optional remote provider (IStoreStockProvider) may supply an OVERRIDE
//     snapshot. When present + non-empty, it MERGES on top of local (replace/add by
//     id; ids the remote does not mention keep their local def). The remote half is
//     a SEAM only — see IStoreStockProvider below. There is NO network call or DB
//     connection string in this client (SECURITY: Neon creds live ONLY in the
//     backend; client -> HTTPS -> backend -> Neon). The remote half is FLAGGED as
//     needing a backend GET endpoint + a Unity-side UnityWebRequest provider that
//     implements IStoreStockProvider and calls StoreStockService.SetRemoteProvider.
//
// §12 instrumentation: FlowTrace.Step at load-local -> remote-fetch -> merge ->
// final-stock-count so a headless capture pinpoints exactly where stock comes from.
// Every remote read is wrapped in Guard.Try (no silent catch): a bad provider logs
// via FlowTrace.Fail and is skipped, falling back to local stock.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// SEAM for a future server-provided stock refresh. A Unity-side implementation
    /// (e.g. a UnityWebRequest GET to a backend `/api/store/items`, mirroring
    /// GameStateService's auth-header pattern) would deserialize the response into
    /// <see cref="WeaponDef"/> / <see cref="ArmorDef"/> lists and return them here.
    ///
    /// IMPORTANT (WO-429 SECURITY): the implementer must NOT embed a Neon connection
    /// string or any DB credential in the client. The only legitimate channel is
    /// client -> HTTPS -> backend -> Neon. Until that backend GET endpoint + provider
    /// exist, NO provider is registered and the store runs purely from local stock —
    /// which is the correct, shipping, offline-first behaviour.
    ///
    /// Implementations MUST be non-throwing for the consumer's purposes (the service
    /// still Guard.Try-wraps every call): an offline / failed fetch returns
    /// <c>false</c> (or null lists) and the local default stands.
    /// </summary>
    public interface IStoreStockProvider
    {
        /// <summary>
        /// True when this provider currently has a remote snapshot to contribute.
        /// False (offline / not-yet-fetched / failed) means "use local only".
        /// </summary>
        bool HasRemoteStock { get; }

        /// <summary>
        /// The remote weapon overrides (by id), or null/empty when none. Returned
        /// defs MERGE on top of the local catalog: a matching id REPLACES the local
        /// def; a new id is ADDED. Never used to DELETE local stock — offline-first
        /// means local is always at least the floor.
        /// </summary>
        IReadOnlyList<WeaponDef> RemoteWeapons();

        /// <summary>The remote armor overrides (by id). Same merge semantics as <see cref="RemoteWeapons"/>.</summary>
        IReadOnlyList<ArmorDef> RemoteArmors();
    }

    /// <summary>
    /// Offline-first store-stock resolver. The store reads its BUY stock through this
    /// instead of <see cref="GearCatalog"/> directly, so the (optional) remote refresh
    /// layer is applied in ONE place. With no provider registered (the shipping
    /// default) it returns the local catalog verbatim — fully offline.
    /// </summary>
    public static class StoreStockService
    {
        private const string Sys = "StoreStock";

        // The remote refresh provider. NULL by default (offline-first). A future
        // backend-backed provider registers itself here; until then the store is
        // 100% local. Never holds any credential — see IStoreStockProvider.
        private static IStoreStockProvider _remote;

        /// <summary>
        /// Register (or clear, with null) the remote refresh provider. Called by a
        /// future backend-backed provider once a safe HTTPS channel + GET endpoint
        /// exist. Registering a provider NEVER changes the offline-first guarantee:
        /// on a failed/empty fetch the local catalog still stands.
        /// </summary>
        public static void SetRemoteProvider(IStoreStockProvider provider)
        {
            _remote = provider;
            FlowTrace.Step(Sys, provider == null
                ? "remote provider cleared — store runs LOCAL-only (offline-first default)."
                : "remote refresh provider registered — local stock remains the floor.");
        }

        /// <summary>True when a remote refresh provider is registered AND it currently has stock.</summary>
        public static bool HasRemote => _remote != null && SafeHasRemoteStock();

        /// <summary>
        /// The resolved BUY weapon stock: local catalog as the authoritative default,
        /// with any remote overrides merged on top. Always non-null and never empties
        /// the local floor.
        /// </summary>
        public static IReadOnlyList<WeaponDef> Weapons()
        {
            // 1) load-local — the authoritative offline default.
            var local = GearCatalog.AllWeapons();
            FlowTrace.Step(Sys, $"load-local weapons: {local.Count}");

            // 2) remote-fetch (optional) — Guard.Try so a bad provider logs + is skipped.
            IReadOnlyList<WeaponDef> remote = null;
            if (_remote != null && SafeHasRemoteStock())
            {
                Guard.Try(Sys, "remote-fetch weapons", () =>
                {
                    remote = _remote.RemoteWeapons();
                    FlowTrace.Step(Sys, $"remote-fetch weapons: {(remote == null ? 0 : remote.Count)}");
                });
            }

            // 3) merge — local is the floor; remote replaces/adds by id.
            var merged = MergeWeapons(local, remote);

            // 4) final-stock-count.
            FlowTrace.Step(Sys, $"final weapon stock: {merged.Count} (remote={(remote != null)})");
            return merged;
        }

        /// <summary>
        /// The resolved BUY armor stock: local catalog default + remote overrides
        /// merged on top. Always non-null; never empties the local floor.
        /// </summary>
        public static IReadOnlyList<ArmorDef> Armors()
        {
            // 1) load-local — the authoritative offline default.
            var local = GearCatalog.AllArmors();
            FlowTrace.Step(Sys, $"load-local armor: {local.Count}");

            // 2) remote-fetch (optional) — Guard.Try so a bad provider logs + is skipped.
            IReadOnlyList<ArmorDef> remote = null;
            if (_remote != null && SafeHasRemoteStock())
            {
                Guard.Try(Sys, "remote-fetch armor", () =>
                {
                    remote = _remote.RemoteArmors();
                    FlowTrace.Step(Sys, $"remote-fetch armor: {(remote == null ? 0 : remote.Count)}");
                });
            }

            // 3) merge — local is the floor; remote replaces/adds by id.
            var merged = MergeArmors(local, remote);

            // 4) final-stock-count.
            FlowTrace.Step(Sys, $"final armor stock: {merged.Count} (remote={(remote != null)})");
            return merged;
        }

        // ── Merge: local floor, remote override by id (case-insensitive) ──────────

        private static List<WeaponDef> MergeWeapons(IReadOnlyList<WeaponDef> local, IReadOnlyList<WeaponDef> remote)
        {
            var byId = new Dictionary<string, WeaponDef>(System.StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            if (local != null)
                foreach (var w in local)
                    if (w != null && !string.IsNullOrEmpty(w.id) && !byId.ContainsKey(w.id))
                    { byId[w.id] = w; order.Add(w.id); }

            if (remote != null)
                foreach (var w in remote)
                {
                    if (w == null || string.IsNullOrEmpty(w.id)) continue;
                    if (!byId.ContainsKey(w.id)) order.Add(w.id);   // remote-new id appended
                    byId[w.id] = w;                                 // remote replaces local def
                }

            var result = new List<WeaponDef>(order.Count);
            foreach (var id in order) result.Add(byId[id]);
            return result;
        }

        private static List<ArmorDef> MergeArmors(IReadOnlyList<ArmorDef> local, IReadOnlyList<ArmorDef> remote)
        {
            var byId = new Dictionary<string, ArmorDef>(System.StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            if (local != null)
                foreach (var a in local)
                    if (a != null && !string.IsNullOrEmpty(a.id) && !byId.ContainsKey(a.id))
                    { byId[a.id] = a; order.Add(a.id); }

            if (remote != null)
                foreach (var a in remote)
                {
                    if (a == null || string.IsNullOrEmpty(a.id)) continue;
                    if (!byId.ContainsKey(a.id)) order.Add(a.id);   // remote-new id appended
                    byId[a.id] = a;                                 // remote replaces local def
                }

            var result = new List<ArmorDef>(order.Count);
            foreach (var id in order) result.Add(byId[id]);
            return result;
        }

        // A provider that throws from HasRemoteStock must NOT take the store down:
        // Guard.Try-log and treat it as "no remote" so local stock stands.
        private static bool SafeHasRemoteStock()
        {
            bool has = false;
            Guard.Try(Sys, "remote HasRemoteStock", () => { has = _remote.HasRemoteStock; });
            return has;
        }
    }
}
