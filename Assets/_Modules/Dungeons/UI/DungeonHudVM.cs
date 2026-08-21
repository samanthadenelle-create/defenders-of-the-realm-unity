// =============================================================================
// DungeonHudVM — the PURE ViewModel behind DungeonHudController (MVVM migration
// Silo G, WO "DungeonHud + Camps + LevelUp").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// The dungeon HUD's ONLY element is the lantern oil meter. Before this VM the
// View (DungeonHudController) read the Lantern's public API each frame and ran
// the low-oil / critical BAND logic inline — a game-state read in a View. That
// logic now lives HERE:
//   * the Lantern is exposed to the VM through the narrow <see cref="ILanternReadout"/>
//     seam (OilFraction / IsLowOil / EstimatedSecondsRemaining) so the VM is
//     unit-testable with a fake and never references the MonoBehaviour Lantern.
//   * the critical band (fraction <= threshold), the warning band (low && !critical),
//     the bar fraction, the low-oil pill, and the burn-time label copy are all
//     projected here; the View binds and paints, reading NO game state.
//
// PUSH SEAM PRESERVED: DungeonController still pushes the Lantern in on load via
// DungeonHudController.SetLantern — that call now just routes the ref into this VM
// (wrapped in LanternReadoutAdapter). No Find is added; the push direction stays.
// =============================================================================

using System;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// The narrow read-only lantern seam the HUD VM projects from. Lets the VM stay
    /// a pure, scene-free, unit-testable class (no reference to the MonoBehaviour
    /// <see cref="Lantern"/>). Implemented for production by <see cref="LanternReadoutAdapter"/>.
    /// </summary>
    public interface ILanternReadout
    {
        /// <summary>Oil remaining as a 0..1 fraction of a full flask.</summary>
        float OilFraction { get; }
        /// <summary>True when oil has dropped into the lantern's warning band.</summary>
        bool IsLowOil { get; }
        /// <summary>Estimated seconds of light left at the current drain rate
        /// (PositiveInfinity when not draining, e.g. pre-run).</summary>
        float EstimatedSecondsRemaining { get; }
    }

    /// <summary>Bridges the concrete <see cref="Lantern"/> MonoBehaviour to the pure
    /// <see cref="ILanternReadout"/> seam so the VM never touches a UnityEngine type.
    /// Reads live each getter, so a bound VM stays current as oil drains.</summary>
    public sealed class LanternReadoutAdapter : ILanternReadout
    {
        private readonly Lantern _lantern;
        public LanternReadoutAdapter(Lantern lantern) { _lantern = lantern; }
        public float OilFraction => _lantern != null ? _lantern.OilFraction : 1f;
        public bool IsLowOil => _lantern != null && _lantern.IsLowOil;
        public float EstimatedSecondsRemaining =>
            _lantern != null ? _lantern.EstimatedSecondsRemaining : float.PositiveInfinity;
    }

    /// <summary>
    /// Pure ViewModel for the dungeon HUD's lantern oil meter. Projects the bar
    /// fraction, the amber (low) / red (critical) band, the low-oil pill flag, and
    /// the glanceable burn-time label from an injected <see cref="ILanternReadout"/>.
    /// The View binds these and paints; it reads no game state.
    /// </summary>
    public sealed class DungeonHudVM
    {
        /// <summary>Default oil fraction at/below which the bar flips from amber to red.</summary>
        public const float DefaultCriticalOilFraction = 0.1f;

        // LOCALIZE: player-facing oil-readout copy (was on the View; moved here so
        // the projection owns the copy). ASCII-only.
        private const string MsgLightFmt = "Light: {0}";
        private const string MsgLightUnknown = "Light: --";
        private const string MsgLightFull = "Light: steady";

        private readonly float _criticalOilFraction;
        private ILanternReadout _lantern;

        /// <summary>Raised when the bound lantern reference changes (SetLantern). The
        /// meter itself is polled per-frame by the View, so continuous oil drain does
        /// not raise this — the View re-reads the live projections each Update.</summary>
        public event Action Changed;

        public DungeonHudVM(float criticalOilFraction = DefaultCriticalOilFraction)
        {
            _criticalOilFraction = Clamp01(criticalOilFraction);
        }

        /// <summary>True once a lantern has been bound; until then the meter reads idle.</summary>
        public bool HasLantern => _lantern != null;

        /// <summary>Routes the pushed lantern ref into the VM (PUSH seam preserved).</summary>
        public void SetLantern(ILanternReadout lantern)
        {
            _lantern = lantern;
            Raise();
        }

        // ── Read-only projections the View paints ────────────────────────────

        /// <summary>Oil bar fill as a 0..1 fraction. Full (1) when no lantern is bound.</summary>
        public float BarFraction => _lantern == null ? 1f : Clamp01(_lantern.OilFraction);

        /// <summary>True while the lantern reads low oil (drives the warning pill).</summary>
        public bool IsLow => _lantern != null && _lantern.IsLowOil;

        /// <summary>True when oil is at/below the critical band — the bar tints red.</summary>
        public bool IsCritical => _lantern != null && BarFraction <= _criticalOilFraction;

        /// <summary>True in the amber (low but not yet critical) band.</summary>
        public bool IsWarning => IsLow && !IsCritical;

        /// <summary>Whether the low-oil warning pill should show.</summary>
        public bool ShowLowWarning => IsLow;

        /// <summary>0..1 visual urgency during the final thirty seconds.</summary>
        public float FinalWarningProgress
        {
            get
            {
                if (_lantern == null) return 0f;
                float seconds = _lantern.EstimatedSecondsRemaining;
                if (float.IsInfinity(seconds) || float.IsNaN(seconds) || seconds > 30f) return 0f;
                return Clamp01(1f - seconds / 30f);
            }
        }

        /// <summary>The glanceable burn-time label ("Light: 1m 12s" / "Light: --" /
        /// "Light: steady").</summary>
        public string TimeLabel =>
            _lantern == null ? MsgLightUnknown : FormatBurnTime(_lantern.EstimatedSecondsRemaining);

        // ── Helpers (pure) ───────────────────────────────────────────────────

        /// <summary>Formats the estimated remaining burn into a glanceable label.
        /// A non-draining lantern (infinite / pre-run) reads "Light: steady".</summary>
        public static string FormatBurnTime(float seconds)
        {
            if (float.IsInfinity(seconds) || float.IsNaN(seconds))
                return MsgLightFull;

            int total = (int)Math.Ceiling(Math.Max(0f, seconds));
            string time;
            if (total >= 60)
            {
                int m = total / 60;
                int s = total % 60;
                time = string.Format("{0}m {1:00}s", m, s);
            }
            else
            {
                time = total + "s";
            }
            return string.Format(MsgLightFmt, time);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private void Raise() => Changed?.Invoke();
    }
}
