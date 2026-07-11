// =============================================================================
// WaveDamageReport — post-wave structure-damage aggregation (F8-45 extension,
// owner directive 2026-07-11: "we need the damage report — if they did damage
// to collectors then those need to reduce economy").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Static, stateless MODEL-side helper: at wave clear it enumerates every
// damaged/destroyed structure the player cares about and emits a bounded,
// worst-first entry list. EndStateVM.FromWaveClear adapts the entries into
// SpoilRowVM rows (MVVM: the VM factory is the adapter; the View stays a dumb
// binder and reads no game state).
//
// Coverage (each through its EXISTING damage surface — no new damage model):
//   • WallSegment / Gate / Building — wrapped via RepairTarget (the proven
//     uniform repairable-structure view: DamageFraction + DisplayName), with
//     the crystal repair cost read from the LIVE WallRepairController.CostFor
//     when a controller exists (its _fullRepairCost/_minRepairCost are
//     serialized instance fields — the controller IS the cost authority, so we
//     never duplicate the constants; no controller => cost omitted, not faked).
//   • ResourceCollector — HpFraction damage + IsBroken + LastLootStolen (the
//     siege-raid steal), so the report shows the ECONOMY hit (accrual scales
//     with HP — see ResourceCollector.Accrue).
//   • Towers (Tower / DefenseTower / ArcaneTower): NOT reportable today —
//     verified 2026-07-11: IDamageableStructure exposes only IsAlive, all three
//     keep _hp/_maxHp private, and each Destroy(gameObject)s itself at 0 HP, so
//     a post-wave scan can see neither a damage fraction nor the corpse. Needs
//     a public HpFraction on those files (separate lane) before rows can exist.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village
{
    /// <summary>
    /// Enumerates damaged/destroyed village structures at wave clear into a
    /// bounded, worst-first list for the wave-results damage report.
    /// </summary>
    public static class WaveDamageReport
    {
        /// <summary>Row cap for the compact wave-results banner (worst-first; truncation is logged, never silent).</summary>
        public const int MaxRows = 8;

        /// <summary>One damaged structure, normalised for the report.</summary>
        public sealed class Entry
        {
            /// <summary>Player-facing structure name ("North Gate", "Lumbermill", ...).</summary>
            public string Name;
            /// <summary>Normalised damage 0..1 (1 = destroyed). Sort key, worst-first.</summary>
            public float DamageFraction;
            /// <summary>True when the structure is fully destroyed / broken.</summary>
            public bool Destroyed;
            /// <summary>Collectors only: pending resources stolen when the collector broke (0 = none).</summary>
            public int LootStolen;
            /// <summary>Crystal cost of a full repair; &lt; 0 = unknown (no WallRepairController alive to price it).</summary>
            public int RepairCost = -1;
            /// <summary>True for ResourceCollector rows — the label carries the production hit.</summary>
            public bool IsCollector;
        }

        /// <summary>
        /// Scans the scene and returns at most <see cref="MaxRows"/> damaged-structure
        /// entries, worst-first. Never throws (Guard-wrapped); an empty list means a
        /// clean wave (the banner stays today's compact clean form).
        /// </summary>
        public static List<Entry> Collect()
        {
            return Guard.Try("WaveClear", "damage report aggregation", () =>
            {
                var all = new List<Entry>();

                // Wall / Gate / Building through the uniform RepairTarget view; the live
                // repair controller (when present) prices each row — the ONE cost seam.
                var repair = Object.FindFirstObjectByType<WallRepairController>();
                AddRepairables<WallSegment>(all, repair);
                AddRepairables<Gate>(all, repair);
                AddRepairables<Building>(all, repair);

                // Resource collectors — the economy layer (accrual scales with HP).
                foreach (var c in ResourceCollectorRegistry.All)
                {
                    if (c == null) continue;
                    float frac = c.IsBroken ? 1f : 1f - c.HpFraction;
                    if (!c.IsBroken && frac <= 0.0001f) continue;   // pristine — no row
                    var def = ResourceBuildingProgression.Find(c.BuildingId);
                    all.Add(new Entry
                    {
                        Name = def != null && !string.IsNullOrEmpty(def.DisplayName)
                            ? def.DisplayName : c.BuildingId,
                        DamageFraction = frac,
                        Destroyed = c.IsBroken,
                        LootStolen = Mathf.RoundToInt(c.LastLootStolen),
                        IsCollector = true,
                        // Collector repair is its own free Repair() flow today — no crystal price.
                    });
                }

                // Worst-first, bounded — truncation is LOGGED, never silent.
                all.Sort((a, b) => b.DamageFraction.CompareTo(a.DamageFraction));
                int destroyed = 0;
                for (int i = 0; i < all.Count; i++)
                    if (all[i].Destroyed) destroyed++;
                int damaged = all.Count - destroyed;
                int shown = Mathf.Min(MaxRows, all.Count);
                if (all.Count > MaxRows)
                    all.RemoveRange(MaxRows, all.Count - MaxRows);
                FlowTrace.Step("WaveClear",
                    $"damage report: {damaged} damaged, {destroyed} destroyed, {shown} rows shown" +
                    (damaged + destroyed > shown ? $" (of {damaged + destroyed} — worst-first cap)" : ""));
                return all;
            }, new List<Entry>());
        }

        /// <summary>
        /// Adds one entry per damaged structure of type <typeparamref name="T"/>,
        /// wrapped through <see cref="RepairTarget.TryWrap"/> so name + damage come
        /// from the proven uniform surface (never re-branching on concrete types).
        /// </summary>
        private static void AddRepairables<T>(List<Entry> into, WallRepairController repair)
            where T : Component
        {
            foreach (var s in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var target = RepairTarget.TryWrap(s);
                if (target == null || !target.NeedsRepair) continue;
                float frac = target.DamageFraction;
                into.Add(new Entry
                {
                    Name = target.DisplayName,
                    DamageFraction = frac,
                    Destroyed = frac >= 0.999f,
                    RepairCost = repair != null ? repair.CostFor(target) : -1,
                });
            }
        }
    }
}
