// =============================================================================
// ⛔ UNREACHABLE — this VM's only View (BarracksPanel) has ZERO CALLERS.
// Owner ruling 21, 2026-09-06 (WorkOrders/ManageRedesign/OWNER_RULINGS_LOCKED.md §21).
// It is the ONLY composer of a JobKind.BarracksUpgrade job, and that job was the only
// writer of the legacy GameState.BarracksLevel — which is why seven of nine troops were
// unreachable. Troop unlocks now read the barracks BUILDING tier. See BarracksPanel.cs's
// header for the proof. DELIBERATELY NOT DELETED in WO-2011: WO-2009 may reuse this as
// the troop DETAIL surface, and CostFormatSourceRegression.cs:35 pins CostStr() here.
// -----------------------------------------------------------------------------
// BarracksPanelVM — the WO-771.9 Barracks & troop-UPGRADE panel's ViewModel (strict
// MVVM; extracted from BarracksPanel so the View reads NO game state).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Mirrors TroopTrainingVM / ShopVM: ALL state + logic lives here — the BarracksService
// facade (barracks level, per-troop level/unlock/in-flight, CanUpgrade reasons,
// UpgradeBarracks/UpgradeTroop), the BarracksProgression reads (max levels, next-def,
// costs/times, curves, next ability), the TroopCatalog roster, and affordability via
// the injected IEconomy. The View binds this, re-renders on Changed, routes taps back
// as intent commands, and NEVER names EconomyService / FindAnyObjectByType / a catalog.
//
// It is NOT a pure/Unity-free VM (unlike TroopTrainingVM): it also owns the panel's
// self-install host resolution (ResolveOrCreateHost) so the View's static entry point
// keeps ZERO FindAnyObjectByType — services + scene lookups belong in this layer.
// Implements DeNelle.Core.UI.Mvvm.IPanelViewModel. Subscribes to BarracksService.Changed
// + IEconomy.OnChanged and re-projects on each; Dispose detaches both (no handler leak).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Hero
{
    // ── Plain-data projections the View renders (no Unity UI types) ─────────────

    /// <summary>The barracks-level card row (bodyLeft top): level readout + state chip.</summary>
    public readonly struct BarracksRowVM
    {
        public readonly int Level;
        public readonly int Max;
        public readonly string Chip;   // "..." (upgrading) / "UP" (has next) / "MAX"

        public BarracksRowVM(int level, int max, string chip)
        {
            Level = level; Max = max; Chip = chip;
        }
    }

    /// <summary>One troop ladder row (bodyLeft): id/name + unlock state + level + chip.</summary>
    public readonly struct BarracksTroopRowVM
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool Unlocked;
        public readonly int Level;         // meaningful only when Unlocked
        public readonly int MaxLevel;      // meaningful only when Unlocked
        public readonly bool Upgrading;    // in-flight upgrade job -> "..." chip
        public readonly int UnlockLevel;   // meaningful only when !Unlocked

        public BarracksTroopRowVM(string id, string name, bool unlocked, int level,
                                  int maxLevel, bool upgrading, int unlockLevel)
        {
            Id = id; Name = name; Unlocked = unlocked; Level = level;
            MaxLevel = maxLevel; Upgrading = upgrading; UnlockLevel = unlockLevel;
        }
    }

    /// <summary>The barracks-level detail card (bodyRight when the barracks row is selected).</summary>
    public readonly struct BarracksDetailVM
    {
        public readonly int Level;
        public readonly int Max;
        public readonly bool HasNext;
        public readonly string NextName;
        public readonly string UnlocksNames;
        public readonly string CostText;
        public readonly string TimeText;
        public readonly bool Affordable;
        public readonly bool CanUpgrade;
        public readonly string BlockReason;   // meaningful only when !CanUpgrade

        public BarracksDetailVM(int level, int max, bool hasNext, string nextName,
                                string unlocksNames, string costText, string timeText,
                                bool affordable, bool canUpgrade, string blockReason)
        {
            Level = level; Max = max; HasNext = hasNext; NextName = nextName;
            UnlocksNames = unlocksNames; CostText = costText; TimeText = timeText;
            Affordable = affordable; CanUpgrade = canUpgrade; BlockReason = blockReason;
        }
    }

    /// <summary>A troop's detail card (bodyRight when a troop row is selected).</summary>
    public readonly struct BarracksTroopDetailVM
    {
        public readonly bool Exists;
        public readonly string Name;
        public readonly bool Unlocked;
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly float ReachMult;
        public readonly float ReachFill;
        public readonly float StrengthMult;
        public readonly float StrengthFill;
        public readonly bool HasNextAbility;
        public readonly string NextAbilityText;
        public readonly int UnlockLevel;       // meaningful only when !Unlocked
        public readonly bool HasNextLevel;
        public readonly string CostText;       // meaningful only when HasNextLevel
        public readonly string TimeText;       // meaningful only when HasNextLevel
        public readonly bool Affordable;
        public readonly bool CanUpgrade;
        public readonly string BlockReason;    // meaningful only when !CanUpgrade

        public BarracksTroopDetailVM(bool exists, string name, bool unlocked, int level, int maxLevel,
                                     float reachMult, float reachFill, float strengthMult, float strengthFill,
                                     bool hasNextAbility, string nextAbilityText, int unlockLevel,
                                     bool hasNextLevel, string costText, string timeText,
                                     bool affordable, bool canUpgrade, string blockReason)
        {
            Exists = exists; Name = name; Unlocked = unlocked; Level = level; MaxLevel = maxLevel;
            ReachMult = reachMult; ReachFill = reachFill; StrengthMult = strengthMult; StrengthFill = strengthFill;
            HasNextAbility = hasNextAbility; NextAbilityText = nextAbilityText; UnlockLevel = unlockLevel;
            HasNextLevel = hasNextLevel; CostText = costText; TimeText = timeText;
            Affordable = affordable; CanUpgrade = canUpgrade; BlockReason = blockReason;
        }
    }

    /// <summary>Outcome of an upgrade command — the View toasts from it (presentation).</summary>
    public readonly struct UpgradeResult
    {
        public readonly bool Success;
        public readonly string FailReason;   // player-facing block reason on failure

        public UpgradeResult(bool success, string failReason)
        {
            Success = success; FailReason = failReason;
        }
    }

    public sealed class BarracksPanelVM : IPanelViewModel, IDisposable
    {
        /// <summary>Sentinel selection id for the barracks-level card (never a real troop id).</summary>
        public const string BarracksSelId = "__barracks__";

        private readonly IEconomy _economy;
        private readonly Action _onClose;
        private readonly Action _serviceHandler;
        private readonly Action<ResourceSnapshot> _ecoHandler;
        private bool _disposed;

        private BarracksRowVM _barracksRow;
        private BarracksDetailVM _barracksDetail;
        private readonly List<BarracksTroopRowVM> _troopRows = new List<BarracksTroopRowVM>();
        private readonly Dictionary<string, BarracksTroopDetailVM> _troopDetail =
            new Dictionary<string, BarracksTroopDetailVM>();

        /// <summary>
        /// The View-side entry point: resolves the live economy handle HERE so the View never
        /// touches EconomyService itself. Mirrors TroopTrainingVM.CreateDefault.
        /// </summary>
        public static BarracksPanelVM CreateDefault(Action onClose)
        {
            return new BarracksPanelVM(EconomyService.Instance, onClose);
        }

        public BarracksPanelVM(IEconomy economy, Action onClose)
        {
            _economy = economy;
            _onClose = onClose;

            _serviceHandler = () => RebuildAndRaise();
            BarracksService.Changed += _serviceHandler;

            if (_economy != null)
            {
                _ecoHandler = _ => RebuildAndRaise();
                _economy.OnChanged += _ecoHandler;
            }

            Rebuild();
        }

        // ── Self-install host resolution (scene lookup lives here, NOT in the View) ──

        /// <summary>
        /// Finds the existing <see cref="BarracksPanel"/> host or creates one — the self-install
        /// seam moved out of the View so BarracksPanel carries ZERO FindAnyObjectByType.
        /// </summary>
        public static BarracksPanel ResolveOrCreateHost()
        {
            var panel = UnityEngine.Object.FindAnyObjectByType<BarracksPanel>();
            if (panel == null)
                panel = new GameObject("BarracksPanelHost").AddComponent<BarracksPanel>();
            return panel;
        }

        // ── IPanelViewModel ─────────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "Barracks - Upgrade";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            BarracksService.Changed -= _serviceHandler;
            if (_economy != null && _ecoHandler != null) _economy.OnChanged -= _ecoHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ───────────────────────────────────────

        /// <summary>Live wallet readout (the View's footer chips rebuild from these).</summary>
        // Wallet strip — GameState ledger, the SAME wallet the upgrade/training spends
        // charge (the old _economy Wood/Iron read the divergent in-session pool: the
        // panel showed 200 wood against a full HUD wallet). _economy stays for OnChanged.
        public int Wood     => DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(DeNelle.Village.Buildings.Progression.HarvestResource.Wood);
        public int Iron     => DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(DeNelle.Village.Buildings.Progression.HarvestResource.Iron);
        public int Food     => DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(DeNelle.Village.Buildings.Progression.HarvestResource.Food);
        public int Crystals => DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(DeNelle.Village.Buildings.Progression.HarvestResource.Crystals);

        /// <summary>The barracks-level card projection.</summary>
        public BarracksRowVM BarracksRow => _barracksRow;

        /// <summary>The troop ladder in display order (UnlockBarracksTier ASC). Never null.</summary>
        public IReadOnlyList<BarracksTroopRowVM> TroopRows => _troopRows;

        /// <summary>The barracks-level detail card projection.</summary>
        public BarracksDetailVM BarracksDetail => _barracksDetail;

        /// <summary>The selected troop's detail projection; Exists=false for an unknown id.</summary>
        public BarracksTroopDetailVM TroopDetail(string id) =>
            id != null && _troopDetail.TryGetValue(id, out var d) ? d : default;

        // ── Commands ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Spends + enqueues the next barracks-level upgrade through BarracksService (the SERVICE
        /// owns every rule). On refusal returns the player-facing block reason for the toast.
        /// </summary>
        public UpgradeResult UpgradeBarracks()
        {
            UpgradeResult r;
            if (BarracksService.UpgradeBarracks())
            {
                r = new UpgradeResult(true, null);
            }
            else
            {
                BarracksService.CanUpgradeBarracks(out string reason);
                r = new UpgradeResult(false, reason);
            }
            RebuildAndRaise();
            return r;
        }

        /// <summary>
        /// Spends + enqueues the next per-troop upgrade through BarracksService. On refusal returns
        /// the player-facing block reason for the toast.
        /// </summary>
        public UpgradeResult UpgradeTroop(string troopId)
        {
            UpgradeResult r;
            if (BarracksService.UpgradeTroop(troopId))
            {
                r = new UpgradeResult(true, null);
            }
            else
            {
                BarracksService.CanUpgradeTroop(troopId, out string reason);
                r = new UpgradeResult(false, reason);
            }
            RebuildAndRaise();
            return r;
        }

        // ── Projection build (all game-state reads happen HERE) ─────────────────────

        private void Rebuild()
        {
            // Barracks-level card.
            int lvl = BarracksService.BarracksLevel;
            int max = BarracksProgression.MaxBarracksLevel;
            string chip = BarracksService.IsUpgradingBarracks ? "..."
                : (BarracksProgression.HasNextBarracksLevel(lvl) ? "UP" : "MAX");
            _barracksRow = new BarracksRowVM(lvl, max, chip);

            // Troop ladder (UnlockBarracksTier ASC, catalog order otherwise) — locked troops kept.
            _troopRows.Clear();
            _troopDetail.Clear();
            var troops = new List<TroopDef>();
            foreach (var d in TroopCatalog.All) if (d != null) troops.Add(d);
            troops.Sort((a, b) => a.UnlockBarracksTier.CompareTo(b.UnlockBarracksTier));
            foreach (var def in troops)
            {
                if (def == null) continue;
                string id = def.Id;
                _troopRows.Add(BuildTroopRow(def, id));
                _troopDetail[id] = BuildTroopDetail(def, id);
            }

            // Barracks-level detail card.
            _barracksDetail = BuildBarracksDetail(lvl, max);
        }

        private static BarracksTroopRowVM BuildTroopRow(TroopDef def, string id)
        {
            bool unlocked = BarracksService.IsTroopUnlocked(id);
            int lvl = unlocked ? BarracksService.TroopLevel(id) : 0;
            int mx = unlocked ? BarracksProgression.MaxTroopLevel(id) : 0;
            bool upgrading = unlocked && BarracksService.IsUpgradingTroop(id);
            int unlockLevel = unlocked ? 0 : BarracksProgression.UnlockLevelFor(id);
            string name = string.IsNullOrEmpty(def.DisplayName) ? id : def.DisplayName;
            return new BarracksTroopRowVM(id, name, unlocked, lvl, mx, upgrading, unlockLevel);
        }

        private BarracksDetailVM BuildBarracksDetail(int lvl, int max)
        {
            bool hasNext = BarracksProgression.HasNextBarracksLevel(lvl);
            var nextDef = BarracksProgression.NextBarracksDef(lvl);

            if (hasNext && nextDef != null)
            {
                string nextName = nextDef.DisplayName ?? ("Barracks " + (lvl + 1));
                string unlocks = NextUnlockNames(nextDef);
                var cost = BarracksProgression.BarracksUpgradeCost(lvl);
                float seconds = BarracksProgression.BarracksUpgradeSeconds(lvl);
                // Ledger wallet — the SAME source the spend charges (the old _economy read
                // was the divergent in-session pool; see BarracksService's wallet comment).
                bool afford = BarracksService.CanAffordBarracksUpgrade(lvl);
                bool can = BarracksService.CanUpgradeBarracks(out string reason);
                return new BarracksDetailVM(lvl, max, true, nextName, unlocks,
                    CostStr(cost), TimeStr(seconds), afford, can, reason);
            }

            return new BarracksDetailVM(lvl, max, false, null, null, null, null, false, false, null);
        }

        private BarracksTroopDetailVM BuildTroopDetail(TroopDef def, string troopId)
        {
            if (def == null)
                return new BarracksTroopDetailVM(false, null, false, 0, 0, 1f, 0f, 1f, 0f,
                    false, null, 0, false, null, null, false, false, null);

            bool unlocked = BarracksService.IsTroopUnlocked(troopId);
            int lvl = BarracksService.TroopLevel(troopId);
            int maxLvl = BarracksProgression.MaxTroopLevel(troopId);
            string name = string.IsNullOrEmpty(def.DisplayName) ? troopId : def.DisplayName;

            // Reach + Strength bars (from the troop's upgrade curves at its current level).
            var upg = TroopUpgradeCatalog.Find(troopId);
            float reachMult = upg != null && upg.Reach != null ? upg.Reach.Get(lvl) : 1f;
            float strMult   = upg != null && upg.Strength != null ? upg.Strength.Get(lvl) : 1f;
            float reachMax  = upg != null && upg.Reach != null ? upg.Reach.Get(maxLvl) : 1f;
            float strMax    = upg != null && upg.Strength != null ? upg.Strength.Get(maxLvl) : 1f;
            float reachFill = Fill01(reachMult, reachMax, lvl, maxLvl);
            float strFill   = Fill01(strMult, strMax, lvl, maxLvl);

            var next = BarracksProgression.NextAbility(troopId, lvl);
            bool hasNextAbility = next != null;
            string nextAbilityText = next != null
                ? ("Next ability (Lv " + next.LevelThreshold + "): " + (next.Description ?? next.AbilityId))
                : "All special abilities unlocked.";

            int unlockLevel = BarracksProgression.UnlockLevelFor(troopId);
            bool hasNextLevel = BarracksProgression.HasNextTroopLevel(troopId, lvl);
            bool can = BarracksService.CanUpgradeTroop(troopId, out string reason);

            string costText = null, timeText = null;
            bool afford = false;
            if (hasNextLevel)
            {
                var cost = BarracksProgression.TroopUpgradeCost(troopId, lvl + 1);
                float seconds = BarracksProgression.TroopUpgradeSeconds(troopId, lvl + 1);
                costText = CostStr(cost);
                timeText = TimeStr(seconds);
                afford = BarracksService.CanAffordTroopUpgrade(troopId, lvl + 1);   // ledger wallet (see above)
            }

            return new BarracksTroopDetailVM(true, name, unlocked, lvl, maxLvl,
                reachMult, reachFill, strMult, strFill,
                hasNextAbility, nextAbilityText, unlockLevel,
                hasNextLevel, costText, timeText, afford, can, reason);
        }

        // ── Pure helpers (moved verbatim from the View — presentation-free formatting) ──

        private static float Fill01(float mult, float maxMult, int lvl, int maxLvl)
        {
            if (maxMult > 1.0001f) return Mathf.Clamp01((mult - 1f) / (maxMult - 1f));
            return maxLvl > 1 ? Mathf.Clamp01((float)(lvl - 1) / (maxLvl - 1)) : 0f;
        }

        private static string NextUnlockNames(BarracksDef nextDef)
        {
            if (nextDef == null || nextDef.UnlocksTroopIds == null) return "";
            var names = new List<string>();
            foreach (var id in nextDef.UnlocksTroopIds)
            {
                var d = TroopCatalog.Find(id);
                names.Add(d != null && !string.IsNullOrEmpty(d.DisplayName) ? d.DisplayName : id);
            }
            return string.Join(", ", names);
        }

        private static string CostStr(ResourceCost c)
        {
            var parts = DeNelle.Core.UI.CostFormat.Parts(new[] { ("wood", "Wood", c.Wood), ("iron", "Iron", c.Iron), ("stone", "Stone", c.Food), ("crystal", "Crystals", c.Crystals), ("gold", "Gold", c.Coins) });
            return parts.Count == 0 ? "Free" : DeNelle.Core.UI.CostFormat.Words(parts);
        }

        private static string TimeStr(float seconds)
        {
            if (seconds < 60f) return Mathf.RoundToInt(seconds) + "s";
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.RoundToInt(seconds - m * 60f);
            return s > 0 ? m + "m " + s + "s" : m + "m";
        }

        private void RebuildAndRaise()
        {
            Rebuild();
            Raise();
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
