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
// <see cref="Rows"/> / <see cref="SelectedRow"/> / <see cref="DetailLine"/> and
// route taps to SelectRow/UpgradeSelected/RazeSelected.
//
// Towers ARE world objects, so this VM legitimately holds concrete Tower
// references (the in-world selection marker + Raze need the scene object); it
// stays otherwise pure (no uGUI types). The resolver is injectable so §2c tests
// drive the list/selection mechanics over bare Tower components with no scene.
//
// -----------------------------------------------------------------------------
// WO-880 — WHY THE MANAGER READ "rng 0, dmg 0" (the DATA half of the ticket).
//
// The game raises towers down TWO DISJOINT LANES, and BuildModeController.
// LiveTowerCount (BuildModeController.cs:2891-2917) is the canonical statement of
// that split — it SUMS both, precisely because neither lane sees the other:
//
//   LANE A (LIVE, Build-Mode / BaseLayout): a PlacedStructure carrying the catalog
//     id, whose combat stats StructureFactory.AttachBehaviorImpl copies off the
//     CATALOG repo block onto a DefenseTower/ArcaneTower component
//     (StructureFactory.cs:686-690  `t.Range = r.range; t.Damage = r.damage;`).
//     This is what the player builds today.
//   LANE B (LEGACY Build-Menu): a Tower component fed a TowerData ScriptableObject
//     out of Resources/Towers by TowerConstruction.CompleteConstruction
//     (TowerConstruction.cs:88-95). Tower.CurrentRange/CurrentDamage
//     (Tower.cs:185-216) return **0f** whenever CurrentUpgrade() is null — i.e.
//     whenever _data is null, which is the whole of a tower's life until the crew
//     finishes raising it.
//
// The VM used to resolve LANE B ONLY, and the View then read CurrentRange /
// CurrentDamage straight off the scene object. So the panel (a) could not see a
// single tower the player actually builds, and (b) printed a FABRICATED
// "Lv 1 (rng 0, dmg 0)" for a still-being-raised legacy tower — a tower that this
// same VM already knew was unbuilt (IsBuilt/LevelOf), and that the BuildMenu row
// correctly renders as "(building)".
//
// THE FIX (data lane, not the View): the VM now resolves BOTH lanes into one
// <see cref="PlacedTowerRow"/> list and reads each row's stats FROM THE SOURCE THE
// GAME USES — the live DefenseTower/ArcaneTower component first (the numbers the
// tower actually shoots with), the catalog repo row behind it, and only then the
// legacy TowerData. A row with no stat source at all reports it as TEXT
// ("(building)" / "(no stats)"), never a manufactured zero. The View renders the
// string the VM composes; it computes nothing.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// ONE placed tower, lane-agnostic: the manager list renders these and never asks a
    /// scene object anything. <see cref="Legacy"/> is non-null only for a LANE B tower
    /// (the legacy Build-Menu / TowerConstructionQueue path) — that is the only lane whose
    /// upgrade + raze verbs live on the object itself, so it is also the only lane the
    /// manager's action row acts on (a LANE A structure is upgraded/sold in Build Mode,
    /// whose BaseLayout record a bare Destroy() here would desynchronise).
    /// </summary>
    public sealed class PlacedTowerRow
    {
        /// <summary>The tower's scene object (never null while the row is live).</summary>
        public GameObject Go { get; private set; }
        /// <summary>The LANE B <see cref="Tower"/> component, or null for a catalog-built tower.</summary>
        public Tower Legacy { get; private set; }
        /// <summary>The LANE A catalog entry id, or null for a legacy tower.</summary>
        public string CatalogId { get; private set; }
        /// <summary>Player-facing name (already run through <see cref="PlacedTowerListVM.PrettifyTowerName"/>).</summary>
        public string DisplayName { get; private set; }
        /// <summary>Live upgrade level, or 0 while the tower is still being raised.</summary>
        public int Level { get; private set; }
        /// <summary>The ceiling this tower can be upgraded to (Tower.MaxLevel / repo.maxLevel).</summary>
        public int MaxLevel { get; private set; }
        /// <summary>True once the tower has a data source at all (a raised tower).</summary>
        public bool Built { get; private set; }
        /// <summary>True when a stat source yielded a REAL range/damage. False =&gt; the row
        /// text says so; the View never prints a manufactured 0.</summary>
        public bool StatsKnown { get; private set; }
        /// <summary>Attack range as the game reads it (see the file banner for the order).</summary>
        public float Range { get; private set; }
        /// <summary>Attack damage as the game reads it (see the file banner for the order).</summary>
        public float Damage { get; private set; }
        /// <summary>Which source answered — "DefenseTower" | "ArcaneTower" | "catalog:&lt;id&gt;" |
        /// "TowerData:&lt;name&gt;" | "none". Diagnostic/regression only; never rendered.</summary>
        public string StatSource { get; private set; }

        internal PlacedTowerRow(GameObject go, Tower legacy, string catalogId, string displayName,
            int level, int maxLevel, bool built, bool statsKnown, float range, float damage, string statSource)
        {
            Go = go; Legacy = legacy; CatalogId = catalogId; DisplayName = displayName;
            Level = level; MaxLevel = maxLevel; Built = built; StatsKnown = statsKnown;
            Range = range; Damage = damage; StatSource = statSource;
        }
    }

    /// <summary>
    /// Lists the placed towers of BOTH lanes (see the file banner) and tracks the selection,
    /// exposing per-tower upgrade / raze commands. Shared by TowerManagerPanel and the
    /// BuildMenu upgrade screen.
    /// </summary>
    public sealed class PlacedTowerListVM : IPanelViewModel, IDisposable
    {
        private readonly Func<Tower[]> _resolver;
        private readonly Func<PlacedStructure[]> _catalogResolver;
        private readonly Action _onClose;
        private readonly List<Tower> _towers = new List<Tower>();
        private readonly List<PlacedTowerRow> _rows = new List<PlacedTowerRow>();
        private Tower _selected;
        private PlacedTowerRow _selectedRow;
        private bool _disposed;

        /// <summary>Resolves the live towers itself via FindObjectsByType (the sole resolution
        /// site — Views never name Find*Type). BOTH lanes: the legacy Tower components AND the
        /// PlacedStructures whose catalog row is a tower — the SAME pair BuildModeController.
        /// LiveTowerCount sums, so the panel can never disagree with the game about what a
        /// placed tower is.</summary>
        public static PlacedTowerListVM CreateDefault(Action onClose = null)
            => new PlacedTowerListVM(
                () => UnityEngine.Object.FindObjectsByType<Tower>(),
                onClose,
                () => UnityEngine.Object.FindObjectsByType<PlacedStructure>());

        public PlacedTowerListVM(Func<Tower[]> resolver, Action onClose = null,
            Func<PlacedStructure[]> catalogResolver = null)
        {
            _resolver = resolver;
            _catalogResolver = catalogResolver;
            _onClose = onClose;
            Refresh();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "Towers";
        public void Close() => _onClose?.Invoke();
        public void Dispose() { _disposed = true; Changed = null; }

        // ── Read-only data ────────────────────────────────────────────────────
        /// <summary>The live LANE B towers (world objects). Never null. The BuildMenu upgrade
        /// screen still drives off this list — its verbs are Tower-only.</summary>
        public IReadOnlyList<Tower> Towers => _towers;
        /// <summary>Every placed tower, BOTH lanes, as render-ready rows. Never null.</summary>
        public IReadOnlyList<PlacedTowerRow> Rows => _rows;
        /// <summary>The currently-selected LANE B tower, or null (also null when a LANE A row is selected).</summary>
        public Tower Selected => _selected;
        /// <summary>The currently-selected row, either lane, or null.</summary>
        public PlacedTowerRow SelectedRow => _selectedRow;
        public bool HasTowers => _towers.Count > 0;
        /// <summary>True when at least one tower of EITHER lane stands.</summary>
        public bool HasRows => _rows.Count > 0;

        /// <summary>True when the manager's Upgrade verb can act on the selection (LANE B only —
        /// a catalog structure is upgraded through Build Mode's cost-enforced verb).</summary>
        public bool CanUpgradeSelected =>
            _selectedRow != null && _selectedRow.Legacy != null && _selectedRow.Legacy.CanUpgrade;

        /// <summary>True when the manager's Raze verb can act on the selection (LANE B only —
        /// destroying a LANE A object here would leave its BaseLayout record behind).</summary>
        public bool CanRazeSelected => _selectedRow != null && _selectedRow.Legacy != null;

        /// <summary>The manager footer read-out for the selected tower (level / tier / stats / next cost).</summary>
        public string DetailLine
        {
            get
            {
                var row = _selectedRow;
                if (row == null) return "Select a tower to manage.";

                if (row.Legacy != null)
                {
                    var t = row.Legacy;
                    if (t.Data == null) return FormatUnbuiltDetail(row.DisplayName);
                    int tier = t.EffectiveTier;
                    int cost = t.NextUpgradeCost;
                    bool canUpgrade = t.CurrentLevel < Tower.MaxLevel;
                    return FormatDetail(t.CurrentLevel, tier, t.CurrentRange, t.CurrentDamage, canUpgrade, cost);
                }

                return FormatCatalogDetail(row.DisplayName, row.Level, row.MaxLevel,
                    row.Range, row.Damage, row.StatsKnown);
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Re-poll the live towers of BOTH lanes; drops a selection whose tower is
        /// gone. Silent (a pull).</summary>
        public void Refresh()
        {
            _towers.Clear();
            _rows.Clear();

            var found = _resolver != null ? _resolver() : null;
            if (found != null)
                foreach (var t in found)
                    if (t != null) { _towers.Add(t); _rows.Add(BuildLegacyRow(t)); }

            var placed = _catalogResolver != null ? _catalogResolver() : null;
            if (placed != null)
            {
                foreach (var ps in placed)
                {
                    if (ps == null || string.IsNullOrEmpty(ps.itemId)) continue;
                    var entry = CatalogRegistry.Get(ps.itemId);
                    // The SAME classifier the build economy + the live tower count use — so
                    // "what counts as a tower" can never drift between the game and this panel.
                    if (!BuildModeController.IsTowerEntry(entry)) continue;
                    _rows.Add(BuildCatalogRow(ps, entry));
                }
            }

            if (_selected == null || !_towers.Contains(_selected)) _selected = null;
            _selectedRow = RowForGameObject(_selectedRow != null ? _selectedRow.Go : null);
            if (_selectedRow == null) _selected = null;
        }

        /// <summary>Select a LANE B tower (or null to clear). Raises <see cref="Changed"/>.</summary>
        public void Select(Tower t)
        {
            _selected = t;
            // A caller may select a tower that is not in the current row set (a poll raced the
            // tap). Synthesise its row rather than silently losing the selection.
            _selectedRow = t != null ? (RowForGameObject(t.gameObject) ?? BuildLegacyRow(t)) : null;
            Raise();
        }

        /// <summary>Select any row, either lane (or null to clear). Raises <see cref="Changed"/>.</summary>
        public void SelectRow(PlacedTowerRow row)
        {
            _selectedRow = row;
            _selected = row != null ? row.Legacy : null;
            Raise();
        }

        /// <summary>Upgrade the selected tower through the single cost-enforced transaction.
        /// LANE A rows return <see cref="Tower.UpgradeResult.Uninitialized"/> — they carry no
        /// Tower component; <see cref="CanUpgradeSelected"/> is the gate the View reads.</summary>
        public Tower.UpgradeResult UpgradeSelected()
        {
            if (_selected == null) return Tower.UpgradeResult.Uninitialized;
            var result = _selected.TryUpgrade();
            Refresh();
            Raise();
            return result;
        }

        /// <summary>Raze (destroy) the selected LANE B tower and clear the selection. Refuses on
        /// a LANE A row (its BaseLayout record would survive the Destroy and rebuild the tower
        /// on the next load) — <see cref="CanRazeSelected"/> is the gate the View reads.</summary>
        public void RazeSelected()
        {
            if (_selected == null) return;
            UnityEngine.Object.Destroy(_selected.gameObject);
            _selected = null;
            _selectedRow = null;
            Refresh();
            Raise();
        }

        // ── Row construction (the ONE place stats are resolved) ───────────────

        private PlacedTowerRow BuildLegacyRow(Tower t)
        {
            bool built = t.Data != null;
            float range = 0f, damage = 0f;
            string source = "none";
            if (built)
            {
                range = t.CurrentRange;
                damage = t.CurrentDamage;
                source = "TowerData:" + (t.Data.towerName ?? "<unnamed>");
            }
            return new PlacedTowerRow(t.gameObject, t, null, DisplayNameFor(t),
                built ? t.CurrentLevel : 0, Tower.MaxLevel, built,
                built && HasStat(range, damage), range, damage, source);
        }

        private static PlacedTowerRow BuildCatalogRow(PlacedStructure ps, CatalogEntry entry)
        {
            float range, damage;
            string source;
            bool known = TryReadLiveStats(ps.gameObject, entry, out range, out damage, out source);
            var repo = entry != null ? entry.repo : null;
            string name = entry != null && !string.IsNullOrWhiteSpace(entry.displayName)
                ? entry.displayName : ps.itemId;
            return new PlacedTowerRow(ps.gameObject, null, ps.itemId, PrettifyTowerName(name),
                Mathf.Max(1, ps.level), Mathf.Max(1, repo != null ? repo.maxLevel : 1),
                true, known, range, damage, source);
        }

        /// <summary>
        /// Read a catalog-built tower's REAL range/damage from the source the GAME uses, in
        /// the order the game itself would: the live behaviour component first (what the tower
        /// actually shoots with — StructureFactory copied the catalog onto it at build time,
        /// StructureFactory.cs:686-690), then the catalog repo row it was built from. Returns
        /// false when neither source yields a stat, so the caller can say so in TEXT instead of
        /// printing a manufactured zero. Public so the oracle can drive it with no scene.
        /// </summary>
        public static bool TryReadLiveStats(GameObject go, CatalogEntry entry,
            out float range, out float damage, out string source)
        {
            range = 0f; damage = 0f; source = "none";

            if (go != null)
            {
                var defense = go.GetComponentInChildren<DefenseTower>(true);
                if (defense != null)
                {
                    range = defense.Range; damage = defense.Damage; source = "DefenseTower";
                    if (HasStat(range, damage)) return true;
                }
                var arcane = go.GetComponentInChildren<ArcaneTower>(true);
                if (arcane != null)
                {
                    range = arcane.Range; damage = arcane.Damage; source = "ArcaneTower";
                    if (HasStat(range, damage)) return true;
                }
            }

            var repo = entry != null ? entry.repo : null;
            if (repo != null)
            {
                range = repo.range; damage = repo.damage;
                source = "catalog:" + (entry.id ?? "<null>");
                return HasStat(range, damage);
            }
            return false;
        }

        /// <summary>A stat source that answered 0/0 has told us NOTHING — treat it as unknown
        /// so the row says so rather than shipping the fabricated "(rng 0, dmg 0)".</summary>
        private static bool HasStat(float range, float damage) => range > 0f || damage > 0f;

        private PlacedTowerRow RowForGameObject(GameObject go)
        {
            if (go == null) return null;
            // Unity's == (native instance id), NOT ReferenceEquals: it also reports a DESTROYED
            // object as unequal, so a razed tower can never re-match a live row.
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Go == go) return _rows[i];
            return null;
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

        /// <summary>The manager list row for <paramref name="row"/> — the ONE string the View
        /// renders. The View performs no stat maths and touches no scene object.</summary>
        public string ManagerRowFor(PlacedTowerRow row, int index1)
        {
            if (row == null) return FormatManagerRow(index1, 0, 0f, 0f, false, false, false);
            return FormatManagerRow(index1, row.Level, row.Range, row.Damage,
                ReferenceEquals(row, _selectedRow), row.Built, row.StatsKnown);
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

        /// <summary>
        /// Manager list row, STATE-AWARE (WO-880). A tower still being raised has no level and no
        /// stats, and a tower whose stat source answered nothing has no numbers to print — both
        /// say so in TEXT (the colour-blind-safe grammar the BuildMenu row already uses) instead
        /// of the fabricated "Lv 1 (rng 0, dmg 0)" the capture caught. ASCII only.
        /// </summary>
        public static string FormatManagerRow(int index1, int level, float range, float damage,
            bool selected, bool built, bool statsKnown)
        {
            string head = (selected ? "> " : "") + "Tower " + index1;
            if (!built)      return head + "  -  (building)";
            if (!statsKnown) return head + "  -  Lv " + level + "   (no stats)";
            return FormatManagerRow(index1, level, range, damage, selected);
        }

        /// <summary>Manager footer detail line.</summary>
        public static string FormatDetail(int level, int tier, float range, float damage, bool canUpgrade, int cost)
            => $"Selected: Lv {level}/{Tower.MaxLevel}  T{tier}   |   " +
               $"rng {range:0}   dmg {damage:0}   |   " +
               (canUpgrade ? $"Upgrade: {cost} cost" : "Max Level");

        /// <summary>Footer for a still-being-raised tower — it has no level, stats or price yet,
        /// so none are invented.</summary>
        public static string FormatUnbuiltDetail(string name)
            => $"Selected: {name}   |   still being raised - no stats yet";

        /// <summary>Footer for a catalog-built (Build-Mode) tower: its REAL stats, and the honest
        /// statement that its upgrade/sell verbs live in Build Mode, not on this panel.</summary>
        public static string FormatCatalogDetail(string name, int level, int maxLevel,
            float range, float damage, bool statsKnown)
            => $"Selected: {name}  Lv {level}/{maxLevel}   |   "
             + (statsKnown ? $"rng {range:0}   dmg {damage:0}" : "no stats authored")
             + "   |   Upgrade in Build Mode";

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
