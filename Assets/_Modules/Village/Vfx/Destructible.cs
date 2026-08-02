// =============================================================================
// Destructible - the ONE owner of a destructible entity's VFX lifecycle (WO-753).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner ruling 2026-07-19 (memory `destroyed-items-no-rebuild-full-cost-and-vfx-cleanup`):
//   1. When destroyed, an item's VFX are torn down WITH it - no orphans. (Root of the
//      live "two random VFX in the castle" / "i see a vfx but no tower" bug: leftover
//      effects from destroyed/removed structures whose VFX were never cleaned up.)
//   3. Architecture: ONE owner per concern (ARCHITECTURE_PRINCIPLES 2b) - a single
//      component owns the death lifecycle: on death it tears ALL of the entity's VFX
//      down (pool-return via the VFXManager/VfxPool handle Stop, plus stop+disable any
//      arcane aura, plus destroy standalone effect roots). Composed onto towers/buildings
//      so cleanup lives in ONE place instead of being scattered across reactive polls.
//
// WHY THIS EXISTS (the gap it closes): before WO-753 the arcane aura was kept from
// orphaning only by a throttled liveness POLL inside ArcaneAura.Update and a second
// poll inside StructureDamageVisuals.Evaluate. Those are reactive (a 0.5s / 0.3s window)
// and miss the genuine-removal paths that have NO steady poll on them - build-mode
// delete, StructureFactory reskin-replace, additive scene teardown. This component is
// the structural catch-all: its OnDestroy tears the VFX down on EVERY removal path, and
// NotifyBroken tears them down synchronously at the break moment, in one owner.
//
// COMPOSE: the destructible's own Awake calls Destructible.Ensure(gameObject) (Building,
// DefenseTower, ArcaneTower). The death path calls Destructible.For(go)?.NotifyBroken(...).
//
// NOT a damage model: it never reads/writes HP and never decides DESTROYED-vs-DAMAGED -
// the structure owns that (IsDestroyed / IsBroken). This owns only the VFX teardown.
// Null-safe + idempotent throughout; instrumented [Flow:Destroy] per CLAUDE.md section 12.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;     // WO-753: drop the destroyed structure's persisted BaseLayout record
using DeNelle.Core.Catalog;   // WO-753: resolve the destroyed structure's display name for the rebuild prompt
using DeNelle.Core.UI;        // WO-753: ElarionUiKit.ShowToast — the rebuild prompt (Point 4)

namespace DeNelle.Village
{
    /// <summary>
    /// The single owner of a destructible entity's VFX teardown. Composed onto a
    /// tower/building; on death (<see cref="NotifyBroken"/>) or on any removal
    /// (<see cref="OnDestroy"/>) it returns every held loop handle to its pool, stops
    /// and disables any <see cref="ArcaneAura"/> in the hierarchy, and destroys any
    /// registered standalone effect root - so no VFX can outlive the structure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Destructible : MonoBehaviour
    {
        // Pooled loop/aura handles this entity owns (Stop -> pool-return on death).
        private readonly List<VFXHandle> _handles = new List<VFXHandle>();
        // Standalone (non-pooled) effect roots to Destroy on death.
        private readonly List<GameObject> _vfxRoots = new List<GameObject>();
        // OnDestroy latch - never re-run teardown after the object is already gone.
        private bool _removed;

        /// <summary>Idempotently attach a <see cref="Destructible"/> to <paramref name="root"/>
        /// (the single seam the destructible spawn/Awake paths call). Returns the component.</summary>
        public static Destructible Ensure(GameObject root)
        {
            if (root == null) return null;
            var d = root.GetComponent<Destructible>();
            if (d == null) d = root.AddComponent<Destructible>();
            return d;
        }

        /// <summary>The <see cref="Destructible"/> on <paramref name="root"/>, or null. Use at the
        /// death site: <c>Destructible.For(gameObject)?.NotifyBroken("...")</c>.</summary>
        public static Destructible For(GameObject root)
            => root != null ? root.GetComponent<Destructible>() : null;

        /// <summary>Register a pooled loop/aura handle so it is Stop()'d (pool-returned) on death.
        /// Null-safe. (ArcaneAura is auto-discovered - this is for any other held loop a caller owns.)</summary>
        public void RegisterHandle(VFXHandle handle)
        {
            if (handle == null) return;
            _handles.Add(handle);
        }

        /// <summary>Register a standalone (non-pooled) effect GameObject to Destroy on death. Null-safe.</summary>
        public void RegisterVfxRoot(GameObject go)
        {
            if (go == null) return;
            _vfxRoots.Add(go);
        }

        /// <summary>
        /// Tear down EVERY VFX this entity owns, in one place: pool-return each held loop handle,
        /// stop+disable any <see cref="ArcaneAura"/> under this root, and destroy each registered
        /// standalone effect root. Idempotent - safe to call at the break moment AND again from
        /// <see cref="OnDestroy"/>. Returns how many effects were torn down (for the trace).
        /// </summary>
        public int TeardownVfx(string reason)
        {
            int torn = 0;

            // 1. Pooled loop/aura handles owned directly by a caller -> immediate pool-return.
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i];
                if (h != null && h.IsAlive) { h.Stop(immediate: true); torn++; }
            }
            _handles.Clear();

            // 2. Any arcane aura in the hierarchy - stop the loop (pool-return) + disable the
            //    component so its OnEnable can't re-acquire over a dead shell. Symmetric with
            //    repair (StructureDamageVisuals re-enables it when the structure stands again).
            var auras = GetComponentsInChildren<ArcaneAura>(true);
            for (int i = 0; i < auras.Length; i++)
            {
                if (auras[i] == null) continue;
                auras[i].StopAndDisable();
                torn++;
            }

            // 3. Standalone (non-pooled) effect roots -> destroy so nothing lingers in the world.
            for (int i = 0; i < _vfxRoots.Count; i++)
            {
                var go = _vfxRoots[i];
                if (go != null) { Destroy(go); torn++; }
            }
            _vfxRoots.Clear();

            FlowTrace.Step("Destroy",
                $"[Flow:Destroy] Destructible.TeardownVfx '{name}' ({reason}): tore down {torn} VFX " +
                "(handles/auras/roots) - no orphaned effect left behind.");
            return torn;
        }

        /// <summary>
        /// Death hook - the entity was DESTROYED by enemies. In ONE owner, this now performs the
        /// full owner ruling 2026-07-19 (memory `destroyed-items-no-rebuild-full-cost-and-vfx-cleanup`,
        /// WO-753): tear the VFX down (ruling 1 - no orphans), despawn the bound vendor NPC (ruling 2),
        /// REMOVE the object + free its grid cell + drop its persisted record so it is truly GONE
        /// (ruling: destroyed = LOST, no in-place respawn), and PROMPT the player to rebuild fresh at
        /// FULL cost (ruling 4).
        ///
        /// WO-672 RECONCILIATION (deliberate): this DELIBERATELY SUPERSEDES the WO-672 "persistent
        /// inoperable shell" design (a broken tower/building used to stay in-world awaiting Repair()).
        /// The owner ruling wins - a destroyed structure is removed here and returns only via a
        /// full-cost build-mode placement. The callers still set <c>_broken</c>/fire their Destroyed
        /// event before this returns; because Unity defers Destroy(gameObject) to end-of-frame, those
        /// post-call lines still run against a live object.
        ///
        /// Called ONLY from the genuine death paths (Building/DefenseTower/ArcaneTower at hp 0). The
        /// removal is death-specific and lives here, NOT in <see cref="OnDestroy"/> (which also fires
        /// on controlled rebuild / scene-unload and must never drop a persisted record).
        /// </summary>
        public void NotifyBroken(string reason)
        {
            using var _ = FlowTrace.Enter("Destroy", $"[Flow:Destroy] NotifyBroken '{name}' ({reason})");

            // 1. VFX first - no orphaned effect can outlive the structure (the original WO-753 concern).
            TeardownVfx(reason);

            var placed = GetComponent<PlacedStructure>();

            // 2. Despawn the bound vendor NPC (Point 2). Read the seat marker's Vendor ref and destroy
            //    it. PROPER Unity-null check (never `?.` on a UnityEngine.Object - fake-null slips past
            //    the null-conditional). The injector's idempotent poll re-anchors a FRESH vendor only
            //    when a NEW building of that id is placed, so clearing the ref here is enough.
            var seat = GetComponent<VendorSeatMarker>();
            if (seat != null && seat.Vendor != null)
            {
                FlowTrace.Step("Destroy",
                    $"[Flow:Destroy] despawning bound vendor '{seat.Vendor.name}' with destroyed structure '{name}'.");
                Destroy(seat.Vendor);
                seat.Vendor = null;
            }

            // 3. Truly GONE: free the footprint, forget it from the loader's live set, and drop its
            //    persisted BaseLayout record so it will NOT respawn on the next base-layout replay.
            //    Mirrors the sell path (BuildModeController.SellSelected) minus the refund. Guarded by
            //    a PlacedStructure (a scene-seed default carries none - it is still destroyed below).
            if (placed != null)
            {
                PlacementGrid.Instance?.Free(placed.gridCell, placed.footprint);
                BaseLayoutLoader.Instance?.Forget(placed);
                RemovePersistedLayoutRecord(placed.itemId, placed.gridCell);
                BurnFreeBuild(placed.itemId);  // WO-753 (owner F8 2026-07-30): rebuild is NEVER free.
                // WO-843 - destruction now mirrors the SELL path's singleton notify (deferred one
                // frame so the dying object no longer counts as placed): the WO-819 baked twin
                // resurfaces immediately (not on the next hub load) and the build card's memoized
                // "Built" state refreshes to BUILDABLE-at-full-cost.
                StructureSingletonBootstrap.NotifyRemovedDeferred(placed.itemId);
                OfferRebuild(placed.itemId);   // Point 4 - prompt to rebuild at full cost.
            }

            // 4. Remove the object itself (Point 1). Deferred to end-of-frame by Unity.
            FlowTrace.Step("Destroy",
                $"[Flow:Destroy] destroying structure '{name}' ({reason}) - destroyed = lost; rebuild fresh at full cost.");
            Destroy(gameObject);
        }

        /// <summary>
        /// Drop the persisted <see cref="GameState.BaseLayout"/> record matching
        /// (<paramref name="itemId"/>, <paramref name="cell"/>) so a destroyed structure does NOT
        /// respawn on the next base-layout replay (ruling: destroyed = lost). Mirrors
        /// BuildModeController.RemoveLayoutEntry (which is private); kept local so removal works even
        /// when Build Mode is not active (e.g. an enemy raid tears the structure down mid-wave).
        /// </summary>
        /// <summary>
        /// WO-753 enforcement (owner F8 2026-07-30: "after being destroyed the cathedral of
        /// magic was free to build - should of had a cost"). The free-build policy
        /// (BuildModeController.FreeBuildAvailable) grants the FIRST placement of each
        /// distinct id free - but a BAKED structure (the hub Cathedral, Default-Town rows,
        /// FoundingKit grants) was never player-placed, so its id was never burned in
        /// GameState.FreeBuildsUsed, and destroying it made the rebuild read as a fresh
        /// first placement -> FREE. That defeats the owner's ruling: destroyed = build
        /// fresh at FULL COST, no exceptions. So destruction itself burns the id into the
        /// ledger; idempotent, null-safe, persisted with the next Save (the same commit
        /// cadence as the removed layout record above).
        /// </summary>
        private static void BurnFreeBuild(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            var gs = DeNelle.Core.State.GameStateService.Instance;
            var state = gs != null ? gs.State : null;
            if (state == null)
            {
                FlowTrace.Warn("Destroy",
                    $"[Flow:Destroy] cannot burn free-build for '{itemId}' - no GameState; a rebuild may read as free.");
                return;
            }
            if (state.FreeBuildsUsed == null)
                state.FreeBuildsUsed = new System.Collections.Generic.List<string>();
            bool already = state.FreeBuildsUsed.Exists(
                x => string.Equals(x, itemId, System.StringComparison.OrdinalIgnoreCase));
            if (!already)
            {
                state.FreeBuildsUsed.Add(itemId);
                FlowTrace.Step("Destroy",
                    $"[Flow:Destroy] free-build BURNED for destroyed '{itemId}' - any rebuild now charges full cost (WO-753).");
            }
        }

        private static void RemovePersistedLayoutRecord(string itemId, Vector2Int cell)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var layout = state != null ? state.BaseLayout : null;
            if (layout == null || string.IsNullOrEmpty(itemId)) return;
            for (int i = layout.Count - 1; i >= 0; i--)
            {
                if (layout[i].itemId == itemId && layout[i].cellX == cell.x && layout[i].cellZ == cell.y)
                {
                    layout.RemoveAt(i);
                    FlowTrace.Step("Destroy",
                        $"[Flow:Destroy] dropped persisted BaseLayout record '{itemId}' cell=({cell.x},{cell.y}) - no respawn.");
                    return;
                }
            }
        }

        /// <summary>
        /// Point 4 (basic) - surface a short prompt that the structure is gone and must be rebuilt at
        /// FULL cost. Fires the shared non-blocking transient toast + a [Flow:Destroy] hook. The
        /// INTERACTIVE "rebuild now?" confirm (which would EnterBuildMode + BuildModeController.ArmById
        /// (itemId) to charge the full EffectiveCostFor) is DEFERRED to its own UI pass: the kit toast
        /// is non-blocking (cannot carry a tap) and auto-entering Build Mode mid-wave is disruptive.
        /// The arm path is ready for that pass: BuildModeController.EnsureExists().ArmById(itemId).
        /// </summary>
        private static void OfferRebuild(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            var entry = CatalogRegistry.Get(itemId);
            string label = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : itemId;

            // WO-843 coherence (owner F8 seq 618: "says destroyed but its clearly still
            // here"): when a WO-819 baked twin will stand in for the destroyed structure,
            // SAY SO - the toast used to claim it was simply gone while the old building
            // visibly stood, which read as a broken destroy. Twin presence = catalog
            // repo.bakedTwins + the WO-834 surfacing gate.
            bool twinStandsIn = StructureSingleton.IsSingleton(itemId)
                && entry?.repo?.bakedTwins != null && entry.repo.bakedTwins.Length > 0
                && StructureSingleton.MayBakedTwinSurface(itemId);

            FlowTrace.Step("Destroy",
                $"[Flow:Destroy] rebuild-prompt for destroyed '{label}' (id='{itemId}') - rebuild costs full price (no repair)"
                + (twinStandsIn ? "; the old baked building stands in for it (WO-819)." : "."));
            ElarionUiKit.ShowToast(
                twinStandsIn
                    ? $"Your {label} was destroyed - the old village {label} stands in for it. Rebuild your own at full cost from Build mode."
                    : $"Your {label} was destroyed - rebuild it at full cost from Build mode.",
                ElarionUiKit.ToastTone.Danger);
        }

        /// <summary>
        /// Catch-all: ANY path that removes this object (build-mode delete, StructureFactory
        /// reskin-replace, additive scene teardown) tears the VFX down here - a pooled loop is
        /// returned to its pool rather than destroyed-with-the-parent (pool shrink) or left
        /// orphaned, and no aura outlives the structure. This is the structural guarantee the
        /// reactive polls could not give.
        /// </summary>
        private void OnDestroy()
        {
            if (_removed) return;
            _removed = true;
            TeardownVfx("OnDestroy");
        }
    }
}
