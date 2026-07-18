// =============================================================================
// LevelUpVM — the PURE ViewModel behind LevelUpSkillPopup (MVVM migration Silo G,
// WO "DungeonHud + Camps + LevelUp").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// The level-up skill-point spend popup is low-traffic (and currently RETIRED —
// see LevelUpSkillPopup.PopupRetired). This VM keeps it MINIMAL but strict: the
// View reads NO game state. Everything the popup projects — available points,
// the points/pill copy, the per-skill button labels, the "auto-show only at
// level >= 2" gate, and the spend command — lives here, behind the narrow
// <see cref="ILevelUpModel"/> seam so the VM is unit-testable with a fake and
// never touches SkillSystem / HeroProgression directly.
//
// EVENT PUSH preserved: the model relays SkillSystem.OnSkillsChanged and the
// instance-swap-proof HeroProgression.OnAnyLevelUp static relay; the VM re-raises
// them (Changed / LeveledUp) and the View re-renders / re-shows.
// =============================================================================

using System;
using DeNelle.Core.Data;
using DeNelle.Core.Progression;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// Narrow seam over the skill-point economy + hero level the popup projects.
    /// Lets the VM stay scene-free and unit-testable (no SkillSystem/HeroProgression
    /// reference). Implemented for production by <see cref="LevelUpModelAdapter"/>.
    /// </summary>
    public interface ILevelUpModel
    {
        /// <summary>Spendable skill points banked.</summary>
        int AvailablePoints { get; }
        /// <summary>The hero's current level (1 when no hero is live).</summary>
        int HeroLevel { get; }
        /// <summary>Current level of a craft skill.</summary>
        int SkillLevel(SkillType type);
        /// <summary>Spend one point into a craft skill. Returns whether a point was spent.</summary>
        bool Spend(SkillType type);
        /// <summary>Fired when a skill level or the available-point count changes.</summary>
        event Action SkillsChanged;
        /// <summary>Fired on each hero level gained — arg = the new level.</summary>
        event Action<int> LeveledUp;
    }

    /// <summary>
    /// Bridges the real SkillSystem + HeroProgression statics to the pure
    /// <see cref="ILevelUpModel"/> seam. Subscribes to SkillSystem.OnSkillsChanged
    /// and the HeroProgression.OnAnyLevelUp static relay (immune to the hero's
    /// instance swap — DEF-261), forwarding both. Dispose detaches.
    /// </summary>
    public sealed class LevelUpModelAdapter : ILevelUpModel, IDisposable
    {
        private readonly Action _skillsHandler;
        private readonly Action<int> _levelHandler;
        private bool _disposed;

        public LevelUpModelAdapter()
        {
            _skillsHandler = () => SkillsChanged?.Invoke();
            _levelHandler = n => LeveledUp?.Invoke(n);
            if (SkillSystem.Instance != null)
                SkillSystem.Instance.OnSkillsChanged += _skillsHandler;
            HeroProgression.OnAnyLevelUp += _levelHandler;
        }

        public int AvailablePoints => SkillSystem.Instance != null ? SkillSystem.Instance.AvailablePoints : 0;
        public int HeroLevel => HeroProgression.Instance != null ? HeroProgression.Instance.Level : 1;
        public int SkillLevel(SkillType type) => SkillSystem.Instance != null ? SkillSystem.Instance.GetSkillLevel(type) : 0;
        public bool Spend(SkillType type) => SkillSystem.Instance != null && SkillSystem.Instance.SpendPoint(type);

        public event Action SkillsChanged;
        public event Action<int> LeveledUp;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (SkillSystem.Instance != null)
                SkillSystem.Instance.OnSkillsChanged -= _skillsHandler;
            HeroProgression.OnAnyLevelUp -= _levelHandler;
            SkillsChanged = null;
            LeveledUp = null;
        }
    }

    /// <summary>
    /// Pure ViewModel for the level-up skill-point spend popup. Projects the point
    /// count + copy, per-skill button labels, and the auto-show gate; exposes Spend
    /// as a command; re-raises the model's skills-changed / leveled-up signals.
    /// </summary>
    public sealed class LevelUpVM : IDisposable
    {
        private readonly ILevelUpModel _model;
        private readonly Action _onClose;
        private readonly Action _skillsHandler;
        private readonly Action<int> _levelHandler;
        private bool _disposed;

        /// <summary>Raised whenever the projected data changes; the View re-renders.</summary>
        public event Action Changed;

        /// <summary>Re-raised hero level-up (arg = new level) so the View can re-show.</summary>
        public event Action<int> LeveledUp;

        /// <summary>Resolution site — wires to the real SkillSystem + HeroProgression.</summary>
        public static LevelUpVM CreateDefault(Action onClose = null) =>
            new LevelUpVM(new LevelUpModelAdapter(), onClose);

        public LevelUpVM(ILevelUpModel model, Action onClose = null)
        {
            _model = model;
            _onClose = onClose;
            if (_model != null)
            {
                _skillsHandler = Raise;
                _model.SkillsChanged += _skillsHandler;
                _levelHandler = n => { LeveledUp?.Invoke(n); Raise(); };
                _model.LeveledUp += _levelHandler;
            }
        }

        public string Title => "Level Up!";

        /// <summary>Spendable points banked.</summary>
        public int AvailablePoints => _model != null ? _model.AvailablePoints : 0;

        /// <summary>The hero's current level.</summary>
        public int HeroLevel => _model != null ? _model.HeroLevel : 1;

        /// <summary>Whether any point can currently be spent.</summary>
        public bool CanSpend => AvailablePoints > 0;

        /// <summary>The "Available points: N" readout copy.</summary>
        public string PointsLine => "Available points: " + AvailablePoints;

        /// <summary>The persistent pill copy ("N skill points — Spend").</summary>
        public string PillText =>
            AvailablePoints == 1 ? "1 skill point — Spend" : AvailablePoints + " skill points — Spend";

        /// <summary>The per-skill button label ("Blacksmith  (Lv 3)   +").</summary>
        public string SkillButtonLabel(string label, SkillType type) =>
            label + "  (Lv " + (_model != null ? _model.SkillLevel(type) : 0) + ")   +";

        /// <summary>The auto-popup gate: level 1 (account creation / starter gift) does
        /// NOT auto-open; genuine level-UPs (2+) do. Guards on the level REACHED.</summary>
        public bool ShouldAutoShow(int newLevel) => newLevel >= 2;

        /// <summary>The hero has reached a genuine level-up (>= 2) — used by the popup's
        /// collapse-to-pill fallback so it never surfaces at the level-1 baseline.</summary>
        public bool HeroAtLevel2Plus => HeroLevel >= 2;

        /// <summary>Spend one banked point into a craft skill. Returns TRUE when the
        /// spend left no points remaining (the View should hide). Raises Changed.</summary>
        public bool Spend(SkillType type)
        {
            if (_model == null) return true;
            _model.Spend(type);   // fires SkillsChanged -> Raise via the model relay
            return AvailablePoints <= 0;
        }

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_model != null)
            {
                if (_skillsHandler != null) _model.SkillsChanged -= _skillsHandler;
                if (_levelHandler != null) _model.LeveledUp -= _levelHandler;
            }
            (_model as IDisposable)?.Dispose();
            Changed = null;
            LeveledUp = null;
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
