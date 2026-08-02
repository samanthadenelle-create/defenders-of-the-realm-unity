// =============================================================================
// RaidDeployVM — the pure ViewModel behind RaidDeployScreen (pre-raid deploy).
// Strict-MVVM migration Silo D.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Moves ALL the deploy math OUT of the View: the party roster (hero class +
// companions), the deployable-troop grouping + owned counts, the army-cap readout,
// the deployable count, and the POWER RATING (sum of each deployable troop's attack
// * veterancy). The View (RaidDeployScreen) binds this and renders portraits + the
// troop list + the summary + the DEPLOY CTA purely from vm.* — it never reads
// GameState / TroopCatalog itself.
//
// SEPARATE from RaidSelectionVM by design (different domain: the browse grid vs the
// deploy math). PURE C#: no UnityEngine UI types; unit-testable over a fake army +
// a fake troop-info resolver (§2c).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village;

namespace DeNelle.Village.Hero
{
    /// <summary>Pure ViewModel for the pre-raid tactical deploy screen.</summary>
    public sealed class RaidDeployVM : IPanelViewModel, IDisposable
    {
        /// <summary>Icon role key on each troop row (the View maps it to art; no game state).</summary>
        public const string IconRoleTroop = "troop";

        /// <summary>Display/combat facts about a troop def, resolved by the caller's seam
        /// (the live path wires this to TroopCatalog). A null def yields the fallbacks.</summary>
        public readonly struct TroopInfo
        {
            public readonly string DisplayName;
            public readonly float Attack;
            public readonly bool Ranged;
            public readonly int Slots;
            public TroopInfo(string displayName, float attack, bool ranged, int slots)
            {
                DisplayName = displayName;
                Attack = attack;
                Ranged = ranged;
                Slots = slots;
            }
        }

        private readonly SceneConfigDef _def;
        private readonly ArmyStorage _army;
        private readonly List<string> _partyClasses = new List<string>();
        private readonly Func<string, TroopInfo> _troopInfo;
        private readonly Action _onClose;

        private readonly List<ItemVM> _troops = new List<ItemVM>();
        private readonly Dictionary<string, bool> _rangedById = new Dictionary<string, bool>();
        private bool _disposed;

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "RAID: " + RaidName;

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Raid display name (raw displayName, else the id — the View may re-space it).</summary>
        public string RaidName =>
            _def == null ? "Raid"
            : (!string.IsNullOrEmpty(_def.displayName) ? _def.displayName : _def.id);

        /// <summary>Raw display name (may be empty) so the View can apply the kit spacer to the id.</summary>
        public string DisplayNameRaw => _def != null && !string.IsNullOrEmpty(_def.displayName) ? _def.displayName : "";
        public string RaidId => _def != null ? _def.id : "";

        public string Difficulty => _def != null ? _def.difficulty : null;
        public float TargetTime => _def != null ? _def.recommendedClearTime : 0f;

        /// <summary>Estimated clear time (2-star band, else recommended).</summary>
        public float EstClearTime =>
            _def == null ? 0f : (_def.twoStarTime > 0f ? _def.twoStarTime : _def.recommendedClearTime);

        public string SceneName => _def != null ? _def.sceneName : null;
        public bool CanDeploy => _def != null && !string.IsNullOrEmpty(_def.sceneName);

        /// <summary>Hero class first, then companion classes (deduped); never empty.</summary>
        public IReadOnlyList<string> PartyClasses => _partyClasses;

        /// <summary>One row per deployable troop type — Id = troopDefId, Name = display name
        /// (raw; may be empty), Price = owned count. Never null.</summary>
        public IReadOnlyList<ItemVM> Troops => _troops;

        public int DeployableCount { get; private set; }
        public int PowerRating { get; private set; }

        private readonly List<string> _scoutReport = new List<string>();

        /// <summary>WO-839 #3: scout-report intel lines for the deploy screen's intel band —
        /// honest facts the scouting party could see, from the raid's SceneConfigDef only
        /// (wall tier + gates, garrison headcount, boss). DELIBERATELY never surfaces
        /// rewardMultiplier / shardDropChance: they are cosmetic-only config fields the loot
        /// math ignores (RAID_BATTLEFIELD_ANATOMY_2026-08-02) and would be lies on screen.
        /// Never null; always at least one line.</summary>
        public IReadOnlyList<string> ScoutReport => _scoutReport;

        /// <summary>"Army: N / M slots" (or "Army: -" with no roster).</summary>
        public string ArmyCapText { get; private set; }

        /// <summary>Whether a troop row's role is ranged (drives the glyph). Pure VM data.</summary>
        public bool IsRanged(string troopDefId) =>
            troopDefId != null && _rangedById.TryGetValue(troopDefId, out var r) && r;

        /// <summary>Canon companion name for a class word (pure mapping moved off the View).</summary>
        public string CompanionName(string cls)
        {
            switch ((cls ?? "").Trim().ToLowerInvariant())
            {
                case "mage":
                case "wizard": return "Thrain";
                case "knight": return "Grom";
                case "ranger": return "Sylas";
                case "cleric":
                case "healer": return "Elara";
                default: return string.IsNullOrEmpty(cls) ? "Hero" : cls;
            }
        }

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>DEPLOY -> load the raid scene (no-op when the raid has no battleground).</summary>
        public void Deploy()
        {
            if (CanDeploy) DeNelle.Core.SceneRouter.GoRaid(_def.sceneName);
        }

        // ── Construction / resolution ───────────────────────────────────────────

        /// <summary>The ONLY resolution site: pulls the army + party from GameState and the
        /// troop facts from TroopCatalog so the View never touches either.</summary>
        public static RaidDeployVM CreateDefault(SceneConfigDef def, Action onClose = null)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var army = state != null ? state.Army : null;
            var party = BuildPartyClasses(state);

            Func<string, TroopInfo> resolver = id =>
            {
                var d = TroopCatalog.Find(id);
                if (d == null) return new TroopInfo(null, 10f, false, 1);
                bool ranged = d.Role != null && d.Role.ToLowerInvariant().Contains("ranged");
                return new TroopInfo(d.DisplayName, d.AttackDamage, ranged, d.Slots > 0 ? d.Slots : 1);
            };

            return new RaidDeployVM(def, army, party, resolver, onClose);
        }

        public RaidDeployVM(SceneConfigDef def, ArmyStorage army, IReadOnlyList<string> partyClasses,
                            Func<string, TroopInfo> troopInfo, Action onClose)
        {
            _def = def;
            _army = army;
            _troopInfo = troopInfo ?? (id => new TroopInfo(null, 10f, false, 1));
            _onClose = onClose;

            if (partyClasses != null) _partyClasses.AddRange(partyClasses);
            if (_partyClasses.Count == 0) _partyClasses.Add("Knight");

            Rebuild();
            BuildScoutReport();
        }

        // WO-839 #3: honest intel from the def only (walls / gates / garrison / boss).
        // Pure string work — stays unit-testable with no Unity types.
        private void BuildScoutReport()
        {
            _scoutReport.Clear();
            if (_def != null)
            {
                if (!string.IsNullOrEmpty(_def.wallTier))
                {
                    string gates = _def.entranceCount > 0
                        ? ", " + _def.entranceCount + (_def.entranceCount == 1 ? " gate" : " gates")
                        : "";
                    _scoutReport.Add("Walls: " + SpaceCamelCase(_def.wallTier) + gates);
                }
                var g = _def.garrison;
                if (g != null)
                {
                    int defenders = 0;
                    if (g.composition != null)
                        foreach (var u in g.composition)
                            if (u != null && u.count > 0) defenders += u.count;
                    if (defenders > 0)
                        _scoutReport.Add("Garrison: " + defenders + (defenders == 1 ? " defender" : " defenders"));
                    if (!string.IsNullOrEmpty(g.boss))
                        _scoutReport.Add("Boss: " + TitleCaseId(g.boss));
                }
            }
            if (_scoutReport.Count == 0) _scoutReport.Add("No scout intel available.");
        }

        /// <summary>"ReinforcedSteel" -> "Reinforced Steel" (pure, no Unity/regex).</summary>
        private static string SpaceCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        /// <summary>"necromancer" / "orc-warlord" -> "Necromancer" / "Orc Warlord" (id, never raw on screen).</summary>
        private static string TitleCaseId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var sb = new System.Text.StringBuilder(id.Length);
            bool startOfWord = true;
            foreach (var ch in id)
            {
                if (ch == '-' || ch == '_' || ch == ' ')
                {
                    sb.Append(' ');
                    startOfWord = true;
                    continue;
                }
                sb.Append(startOfWord ? char.ToUpperInvariant(ch) : ch);
                startOfWord = false;
            }
            return sb.ToString().Trim();
        }

        private void Rebuild()
        {
            _troops.Clear();
            _rangedById.Clear();

            // Group deployable troops by TroopDefId (preserving first-seen order), count each.
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            float power = 0f;
            int deployable = 0;

            if (_army != null)
            {
                foreach (var t in _army.GetDeployable())
                {
                    if (t == null || string.IsNullOrEmpty(t.TroopDefId)) continue;
                    deployable++;
                    if (!counts.ContainsKey(t.TroopDefId)) { counts[t.TroopDefId] = 0; order.Add(t.TroopDefId); }
                    counts[t.TroopDefId]++;
                    var info = _troopInfo(t.TroopDefId);
                    power += info.Attack * t.DamageMultiplier;
                }
            }

            foreach (var defId in order)
            {
                var info = _troopInfo(defId);
                _rangedById[defId] = info.Ranged;
                string name = string.IsNullOrEmpty(info.DisplayName) ? "" : info.DisplayName;
                // Price carries the owned count for this troop type.
                _troops.Add(new ItemVM(defId, name, IconRoleTroop, defId, counts[defId], "", true));
            }

            DeployableCount = deployable;
            PowerRating = (int)System.Math.Round(power);
            ArmyCapText = ComputeArmyCapText();
        }

        private string ComputeArmyCapText()
        {
            if (_army == null) return "Army: -";
            try
            {
                Func<string, int> slotOf = id =>
                {
                    var info = _troopInfo(id);
                    return info.Slots > 0 ? info.Slots : 1;
                };
                int used = _army.SlotsUsed(slotOf);
                return "Army: " + used + " / " + _army.MaxArmySize + " slots";
            }
            catch
            {
                return "Army: -";
            }
        }

        // Hero class first, then companion classes from PartyMemberIds (deduped). Always
        // returns at least the hero placeholder so the row never reads empty.
        private static List<string> BuildPartyClasses(GameState state)
        {
            var list = new List<string>();
            if (state != null && state.HeroClass != HeroClassOpt.None)
                list.Add(state.HeroClass.ToString());
            if (state != null && state.PartyMemberIds != null)
                foreach (var id in state.PartyMemberIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!list.Contains(id)) list.Add(id);
                }
            if (list.Count == 0) list.Add("Knight");
            return list;
        }
    }
}
