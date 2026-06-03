// =============================================================================
// ItemDropSystem - feature flag + self-bootstrapping listener for the
// "enemy/boss dies -> roll a loot drop -> drop feeds crafting of consumables"
// loop (ISOLATED parallel lane: gear/crafting + item drops + consumables).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// HARD ISOLATION CONTRACT (this whole lane's reason to exist, mirrors CampSystem):
//   * Touches NO existing file. Every type here is NEW. It only REFERENCES
//     existing PUBLIC, read-only APIs:
//       - DeNelle.Village.Enemy.Died        (subscribe-only kill -> drop roll)
//       - DeNelle.Village.DragonBoss.Died   (subscribe-only boss -> drop roll)
//       - DeNelle.Village.Crafting.VillageInventory (Add ingredients, public)
//   * SHIPS DARK. Enabled defaults to FALSE -> the bootstrap returns immediately,
//     subscribes to nothing, rolls nothing. The module is fully inert in the
//     build until someone flips the flag (see "HOW TO ENABLE").
//
// HOW TO ENABLE (pick one):
//   1. Code: set ItemDropSystem.Enabled = true BEFORE first AfterSceneLoad (e.g.
//      a dev MonoBehaviour Awake, or a debug console). Then play a wave.
//   2. Editor/dev: add the scripting define DOTR_ITEM_DROPS to default-enable it
//      (DefaultEnabled below ORs the flag on) without editing this file.
//   3. Runtime toggle: call ItemDropSystem.Enable() / ItemDropSystem.Disable().
//
// WHAT IT DOES WHEN ON: a single hidden host watches the active scene for any
// Enemy / DragonBoss, subscribes to each one's Died event ONCE, and on death
// rolls the LootTableCatalog for that enemy kind. Rolled materials are deposited
// into the existing VillageInventory (the same larder the Workshop crafts from).
// Consumable RECIPES (potions, tent kits, food) are content in the canonical
// JSON and craft through the existing VillageInventory.TryCraft path - this lane
// adds the SOURCES (drops) and the consumable USE stub (ConsumableUseService),
// it does not replace the crafting station.
//
// Canon: the village is Elarion (never Avalon). ASCII runtime strings only.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Items
{
    /// <summary>
    /// Static feature flag + the AfterSceneLoad self-bootstrap that, ONLY when
    /// <see cref="Enabled"/>, attaches a hidden watcher which subscribes to every
    /// enemy/boss Died event and rolls drops into the village larder. Inert when off.
    /// </summary>
    public static class ItemDropSystem
    {
        // ---------------------------------------------------------------------
        // Feature flag - DEFAULT OFF. The module does nothing until this is true.
        // ---------------------------------------------------------------------

        /// <summary>Compile-time default. OFF unless DOTR_ITEM_DROPS is defined so the
        /// feature ships dark in the grant/village build.</summary>
        private const bool DefaultEnabled =
#if DOTR_ITEM_DROPS
            true;
#else
            false;
#endif

        private static bool _enabled = DefaultEnabled;

        /// <summary>Master switch. When false the whole drop loop is inert: no
        /// watcher, no subscriptions, no rolls. Set this BEFORE the gameplay scene
        /// loads (or call <see cref="Enable"/> then <see cref="StartNow"/>).</summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>Turn the feature on at runtime (then call <see cref="StartNow"/>
        /// or load a gameplay scene to begin watching for kills).</summary>
        public static void Enable() => _enabled = true;

        /// <summary>Turn the feature off at runtime (stops new rolls; existing
        /// drops already in the larder are untouched).</summary>
        public static void Disable() => _enabled = false;

        // ---------------------------------------------------------------------
        // Tunables (code-only; no SO authoring needed for the dark feature).
        // ---------------------------------------------------------------------

        /// <summary>How often the watcher rescans the scene for new, not-yet-subscribed
        /// enemies (seconds). Cheap: a single FindObjectsByType pass.</summary>
        public const float DefaultScanInterval = 1.5f;

        /// <summary>When true, a kill spawns a WORLD pickup mote at the death point that
        /// the hero must walk over to collect (the "fight -> harvest" feel). When false,
        /// rolled drops deposit straight into the larder (the original instant path).
        /// Default true so the vertical slice has a tangible pickup; still fully dark
        /// behind <see cref="Enabled"/>.</summary>
        public static bool UseWorldPickups { get; set; } = true;

        private static bool _started;
        private static GameObject _host;

        // ---------------------------------------------------------------------
        // Self-bootstrap. Runs after EVERY scene load; returns instantly when the
        // feature is off (zero footprint) so the build is unaffected.
        // ---------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Enabled) return;             // SHIPS DARK: do nothing at all.
            StartNow();
        }

        /// <summary>
        /// Spin up the hidden drop watcher now (idempotent). Honors <see cref="Enabled"/>.
        /// Safe to call from a dev toggle after flipping <see cref="Enabled"/> at runtime.
        /// </summary>
        public static void StartNow()
        {
            if (!Enabled) return;
            if (_started && _host != null) return;

            _host = new GameObject("ItemDropSystem (watcher)");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<ItemDropWatcher>().Configure(DefaultScanInterval);

            _started = true;
            Debug.Log("[ItemDropSystem] Drop watcher started (feature ENABLED).");
        }

        /// <summary>
        /// Roll a drop for a slain enemy of <paramref name="lootTableId"/> and deposit
        /// the materials into the village larder. Public so the watcher (or a dev test)
        /// can invoke it directly. No-op when disabled or when no table/inventory exists.
        /// </summary>
        public static void RollAndDeposit(string lootTableId)
        {
            if (!Enabled) return;
            var rolled = LootTableCatalog.Roll(lootTableId);
            if (rolled == null || rolled.Count == 0) return;

            var inv = VillageInventory.Instance;
            if (inv == null) return; // larder not bootstrapped yet -> drop is lost (rare)

            foreach (var kv in rolled)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                inv.Add(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Roll a table and RETURN the materials WITHOUT depositing (the world-pickup
        /// path uses this so the grant happens only when the hero collects the mote).
        /// Returns an empty map when disabled / nothing dropped.
        /// </summary>
        public static Dictionary<string, int> RollLines(string lootTableId)
        {
            if (!Enabled) return new Dictionary<string, int>();
            return LootTableCatalog.Roll(lootTableId) ?? new Dictionary<string, int>();
        }
    }
}
