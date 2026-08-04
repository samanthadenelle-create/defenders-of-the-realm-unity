// =============================================================================
// PlacedTowerListVM — pure ViewModel for the placed-tower list (MVVM Silo C).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Strict-MVVM migration (UI_MVVM_MIGRATION_PLAN.md §1, Silo C): the
// FindObjectsByType<Tower>() poll that TowerManagerPanel.Refresh and
// BuildMenu.RenderUpgradeTower did INSIDE the View moves HERE — the VM is the
// sanctioned resolution site (a VM builds no uGUI, so the oracle's Find*Type ban
// on Views never applies to it). The Views become dumb skins: they read
// <see cref="Towers"/> / <see cref="Selected"/> / <see cref="DetailLine"/> and
// route taps to Select/UpgradeSelected/RazeSelected.
//
// Towers ARE world objects, so this VM legitimately holds concrete Tower
// references (the in-world selection marker + Raze need the scene object); it
// stays otherwise pure (no uGUI types). The resolver is injectable so §2c tests
// drive the list/selection mechanics over bare Tower components with no scene.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// Lists the placed <see cref="Tower"/>s and tracks the selection, exposing per-tower
    /// upgrade / raze commands. Shared by TowerManagerPanel and the BuildMenu upgrade screen.
    /// </summary>
    public sealed class PlacedTowerListVM : IPanelViewModel, IDisposable
    {
        private readonly Func<Tower[]> _resolver;
        private readonly Action _onClose;
        private readonly List<Tower> _towers = new List<Tower>();
        private Tower _selected;
        private bool _disposed;

        /// <summary>Resolves the live towers itself via FindObjectsByType (the sole resolution
        /// site — Views never name Find*Type).</summary>
        public static PlacedTowerListVM CreateDefault(Action onClose = null)
            => new PlacedTowerListVM(() => UnityEngine.Object.FindObjectsByType<Tower>(), onClose);

        public PlacedTowerListVM(Func<Tower[]> resolver, Action onClose = null)
        {
            _resolver = resolver;
            _onClose = onClose;
            Refresh();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "Towers";
        public void Close() => _onClose?.Invoke();
        public void Dispose() { _disposed = true; Changed = null; }

        // ── Read-only data ────────────────────────────────────────────────────
        /// <summary>The live placed towers (world objects). Never null.</summary>
        public IReadOnlyList<Tower> Towers => _towers;
        /// <summary>The currently-selected tower, or null.</summary>
        public Tower Selected => _selected;
        public bool HasTowers => _towers.Count > 0;

        /// <summary>The manager footer read-out for the selected tower (level / tier / stats / next cost).</summary>
        public string DetailLine
        {
            get
            {
                if (_selected == null) return "Select a tower to manage.";
                int tier = _selected.EffectiveTier;
                int cost = _selected.NextUpgradeCost;
                bool canUpgrade = _selected.CurrentLevel < Tower.MaxLevel;
                return FormatDetail(_selected.CurrentLevel, tier,
                    _selected.CurrentRange, _selected.CurrentDamage, canUpgrade, cost);
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Re-poll the live towers; drops a selection whose tower is gone. Silent (a pull).</summary>
        public void Refresh()
        {
            _towers.Clear();
            var found = _resolver != null ? _resolver() : null;
            if (found != null)
                foreach (var t in found)
                    if (t != null) _towers.Add(t);

            if (_selected == null || !_towers.Contains(_selected))
                _selected = null;
        }

        /// <summary>Select a tower (or null to clear). Raises <see cref="Changed"/>.</summary>
        public void Select(Tower t) { _selected = t; Raise(); }

        /// <summary>Upgrade the selected tower through the single cost-enforced transaction.</summary>
        public Tower.UpgradeResult UpgradeSelected()
        {
            if (_selected == null) return Tower.UpgradeResult.Uninitialized;
            var result = _selected.TryUpgrade();
            Refresh();
            Raise();
            return result;
        }

        /// <summary>Raze (destroy) the selected tower and clear the selection.</summary>
        public void RazeSelected()
        {
            if (_selected == null) return;
            UnityEngine.Object.Destroy(_selected.gameObject);
            _selected = null;
            Refresh();
            Raise();
        }

        // ── Per-tower projections (the View asks the VM, never the scene object) ──

        /// <summary>
        /// True once the tower has been handed its <see cref="Tower.Data"/>. A placed tower is
        /// created by TowerConstructionQueue as a bare GameObject and only receives its data when
        /// TowerConstruction.CompleteConstruction runs, so a tower still being raised reports
        /// FALSE here — its level, stats and upgrade price do not exist yet and must not be
        /// printed as zeroes.
        /// </summary>
        public bool IsBuilt(Tower t) => t != null && t.Data != null;

        /// <summary>The tower's live upgrade level, or 0 while it is still being raised.</summary>
        public int LevelOf(Tower t) => IsBuilt(t) ? t.CurrentLevel : 0;

        /// <summary>
        /// The player-facing name of a placed tower. Reads the AUTHORED
        /// <see cref="DeNelle.Core.Data.TowerData.towerName"/> first; the GameObject name is only
        /// a fallback, because it is a scene-graph identifier, not a display string — the queue
        /// names towers "Tower_&lt;towerName&gt;", a prefab instance carries "(Clone)", and an
        /// editor-built stub carries whatever it was constructed with. Either way the result runs
        /// through <see cref="PrettifyTowerName"/> so a raw identifier can never reach the screen.
        /// </summary>
        public string DisplayNameFor(Tower t)
        {
            if (t == null) return "Tower";
            string authored = t.Data != null ? t.Data.towerName : null;
            return PrettifyTowerName(!string.IsNullOrWhiteSpace(authored) ? authored : t.name);
        }

        // ── Pure formatting helpers (unit-testable without a scene) ────────────

        /// <summary>
        /// Turns an identifier-shaped tower name into readable English: drops the "Tower_"/"Tower-"
        /// scene prefix and any "(Clone)" suffix, splits a run-together camel hump ("DevTower" ->
        /// "Dev Tower") and separates a trailing index from the word it is stuck to ("Stone4" ->
        /// "Stone 4"). Idempotent — an already-clean name ("Archer Tower") is returned unchanged.
        /// </summary>
        public static string PrettifyTowerName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Tower";

            string s = raw.Replace("Tower-", "").Replace("Tower_", "").Replace("(Clone)", "").Trim();
            if (s.Length == 0) return "Tower";

            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0)
                {
                    char prev = s[i - 1];
                    bool camelHump = char.IsUpper(c) && char.IsLower(prev);
                    bool indexRun  = char.IsDigit(c) && char.IsLetter(prev);
                    if ((camelHump || indexRun) && prev != ' ') sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Manager list row: "&gt; Tower 3  -  Lv 2   (rng 12, dmg 20)".</summary>
        public static string FormatManagerRow(int index1, int level, float range, float damage, bool selected)
            => (selected ? "> " : "")
             + $"Tower {index1}  -  Lv {level}   (rng {range:0}, dmg {damage:0})";

        /// <summary>Manager footer detail line.</summary>
        public static string FormatDetail(int level, int tier, float range, float damage, bool canUpgrade, int cost)
            => $"Selected: Lv {level}/{Tower.MaxLevel}  T{tier}   |   " +
               $"rng {range:0}   dmg {damage:0}   |   " +
               (canUpgrade ? $"Upgrade: {cost} cost" : "Max Level");

        /// <summary>
        /// BuildMenu upgrade-screen row: "&gt; Archer  (Lvl 2/3)". A tower that has not finished
        /// construction has no level yet, so it reads "(building)" instead of a fabricated "Lvl 1".
        /// </summary>
        public static string FormatMenuRow(string towerName, int level, bool selected, bool built = true)
            => (selected ? "> " : "")
             + PrettifyTowerName(towerName)
             + (built ? "  (Lvl " + level + "/" + Tower.MaxLevel + ")" : "  (building)");

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
