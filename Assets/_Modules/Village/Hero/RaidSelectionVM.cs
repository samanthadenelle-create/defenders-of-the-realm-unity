// =============================================================================
// RaidSelectionVM — the pure ViewModel behind RaidSelectionScreen (the raid grid).
// Strict-MVVM migration Silo D.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Owns the SceneConfigCatalog projection: the 3 flagship enemy raids (fallback to
// all enemy raids) as ItemVM cards + per-id helpers (difficulty / target time /
// reward hint fields). The View (RaidSelectionScreen) binds this, renders the card
// grid from vm.Raids + the helpers, and routes a card tap through vm.DefFor(id) to
// open the deploy screen — it never touches the gameplay catalog.
//
// SEPARATE from RaidDeployVM by design (different domain: this is the browse grid,
// that is the pre-raid deploy math). They only share the SceneConfigDef formatting.
// PURE C#: no UnityEngine UI types; unit-testable over a fake def list (§2c).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village;

namespace DeNelle.Village.Hero
{
    /// <summary>Pure ViewModel for the Raids-tab card grid.</summary>
    public sealed class RaidSelectionVM : IPanelViewModel, IDisposable
    {
        /// <summary>Icon role key on each raid card (the View maps it to art; no game state).</summary>
        public const string IconRoleRaid = "raid";

        // The three flagship enemy raids, in card order (mirrors the View's grid).
        private static readonly string[] FlagshipRaidIds =
        {
            "raider_camp_small",
            "fortified_garrison",
            "mage_enclave",
        };

        private readonly List<SceneConfigDef> _defs = new List<SceneConfigDef>();
        private readonly List<ItemVM> _raids = new List<ItemVM>();
        private readonly Dictionary<string, SceneConfigDef> _byId =
            new Dictionary<string, SceneConfigDef>(StringComparer.OrdinalIgnoreCase);
        private readonly Action _onClose;
        private bool _disposed;

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "RAIDS";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>One card per raid (Name = raw displayName, may be empty — the View
        /// falls back to a spaced id). Never null.</summary>
        public IReadOnlyList<ItemVM> Raids => _raids;

        /// <summary>The raw SceneConfigDef for a card id (the View forwards it to the deploy
        /// screen so it never re-pulls the catalog itself), or null.</summary>
        public SceneConfigDef DefFor(string id) =>
            id != null && _byId.TryGetValue(id, out var d) ? d : null;

        // Per-card presentation inputs (raw values; the View formats colour/time/hint).
        public string DifficultyFor(string id) { var d = DefFor(id); return d != null ? d.difficulty : null; }
        public float TargetTimeFor(string id) { var d = DefFor(id); return d != null ? d.recommendedClearTime : 0f; }
        public float RewardMultiplierFor(string id) { var d = DefFor(id); return d != null ? d.rewardMultiplier : 1f; }
        public float ShardChanceFor(string id) { var d = DefFor(id); return d != null ? d.shardDropChance : 0f; }

        // ── Construction / resolution ───────────────────────────────────────────

        /// <summary>The ONLY resolution site: pulls the flagship raids (fallback to all
        /// enemy raids) from <see cref="SceneConfigCatalog"/> so the View never touches it.</summary>
        public static RaidSelectionVM CreateDefault(Action onClose = null)
        {
            var list = new List<SceneConfigDef>();
            foreach (var id in FlagshipRaidIds)
            {
                var def = SceneConfigCatalog.Find(id);
                if (def != null) list.Add(def);
            }
            if (list.Count == 0)
                foreach (var def in SceneConfigCatalog.All)
                    if (def != null && def.IsEnemy) list.Add(def);
            return new RaidSelectionVM(list, onClose);
        }

        public RaidSelectionVM(IReadOnlyList<SceneConfigDef> defs, Action onClose)
        {
            _onClose = onClose;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null) continue;
                    _defs.Add(d);
                    if (!string.IsNullOrEmpty(d.id)) _byId[d.id] = d;
                }
            Rebuild();
        }

        private void Rebuild()
        {
            _raids.Clear();
            foreach (var d in _defs)
            {
                if (d == null) continue;
                // Name carries the RAW displayName (may be empty); the View falls back to a
                // kit-spaced id so the VM never references the presentation kit.
                string name = string.IsNullOrEmpty(d.displayName) ? "" : d.displayName;
                _raids.Add(new ItemVM(d.id, name, IconRoleRaid, d.id, 0, "", true));
            }
        }
    }
}
