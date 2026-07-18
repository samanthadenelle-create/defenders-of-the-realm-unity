// =============================================================================
// EchoWorkforceVM -- the SHARED Echo-workforce snapshot ViewModel (MVVM, Silo F).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The ONE place the Echo-workforce readout is projected out of game state, shared
// by BOTH Views that show it:
//   * EchoWorkforceHud  -- the tucked-away "Echo Harvest" panel (count + silo + Collect All)
//   * EchoRosterView    -- the "pet box" (via EchoRosterVM, which extends this base)
//
// It owns ALL EchoService + ResourceCollectorService reads (previously inline in the
// two Views): the count/silo/rate snapshot, the collector "pending" readout, the
// Collect-All command, and the first-run / empty / roster-complete framing math
// (the old `owned<=1` / `owned<=0` / `owned>=max` branches + the "wavesToNext" fallback
// cadence). The Views become dumb skins that bind this and render its strings/flags.
//
// TESTABLE over a fake: the concrete services are behind <see cref="IEchoWorkforce"/>;
// the live adapter (<see cref="EchoServiceWorkforce"/>) wraps EchoService +
// ResourceCollectorService, and <see cref="CreateDefault"/> is the ONLY resolution site.
// Mirrors the BuildingUpgradeVM gold standard (IEconomy + CreateDefault).
// =============================================================================
using System;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village
{
    /// <summary>
    /// The minimal Echo-workforce model surface the VMs read. Implemented live by
    /// <see cref="EchoServiceWorkforce"/> (EchoService + ResourceCollectorService) and by a
    /// fake in EditMode tests -- so the VM projection is unit-testable without a scene.
    /// </summary>
    public interface IEchoWorkforce
    {
        /// <summary>True when a live EchoService exists (the HUD only renders a real snapshot then).</summary>
        bool Available { get; }
        int EchoCount { get; }
        int MaxEchoes { get; }
        int WavesPerEcho { get; }
        int WavesUntilNextEcho { get; }
        float NextEchoProgress { get; }
        double GlobalHarvestMultiplier { get; }
        float FillFraction { get; }
        /// <summary>Sum of pending across all hub collectors (ResourceCollectorService.TotalPending).</summary>
        int PendingCollect { get; }
        /// <summary>Max collector fill fraction 0..1 (ResourceCollectorService.MaxFillFraction).</summary>
        float CollectorMaxFill { get; }
        /// <summary>Collect All (hub collectors + echo silo dump). Returns integer resources banked.</summary>
        int CollectAll();
        event Action Changed;
        event Action<int> EchoUnlocked;
    }

    /// <summary>
    /// Pure snapshot ViewModel of the Echo workforce. Subscribes to the model's Changed /
    /// EchoUnlocked and re-snapshots, raising <see cref="Changed"/>. All framing math lives here.
    /// </summary>
    public class EchoWorkforceVM : IPanelViewModel, IDisposable
    {
        protected readonly IEchoWorkforce Model;
        private readonly Action _onClose;
        private bool _disposed;

        public EchoWorkforceVM(IEchoWorkforce model, Action onClose)
        {
            Model = model;
            _onClose = onClose;
            if (Model != null)
            {
                Model.Changed += OnModelChanged;
                Model.EchoUnlocked += OnEchoUnlocked;
            }
            Recompute();
        }

        /// <summary>The ONLY resolution site: wire the live EchoService + ResourceCollectorService.</summary>
        public static EchoWorkforceVM CreateDefault(Action onClose)
        {
            return new EchoWorkforceVM(new EchoServiceWorkforce(), onClose);
        }

        // -- IPanelViewModel ----------------------------------------------------
        public event Action Changed;
        public string Title { get; protected set; } = "ECHO HARVEST";
        public void Close() => _onClose?.Invoke();

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (Model != null)
            {
                Model.Changed -= OnModelChanged;
                Model.EchoUnlocked -= OnEchoUnlocked;
                if (Model is IDisposable d) d.Dispose();
            }
            Changed = null;
            EchoUnlocked = null;
        }

        /// <summary>Re-raised Echo-unlock signal (the HUD's "New Echo joined!" toast).</summary>
        public event Action<int> EchoUnlocked;

        private void OnModelChanged() { Recompute(); Raise(); }
        private void OnEchoUnlocked(int newCount) => EchoUnlocked?.Invoke(newCount);

        // -- Snapshot fields (the Views render from these ONLY) -----------------

        /// <summary>True when a live workforce exists (the HUD guards its Refresh on this, mirroring
        /// the old `EchoService.Instance == null` early return).</summary>
        public bool HasWorkforce { get; private set; }

        /// <summary>Owned Echo count, clamped to [0, MaxEchoes] (the roster's old OwnedCount()).</summary>
        public int Owned { get; private set; }
        public int MaxEchoes { get; private set; }
        public double GlobalHarvestMultiplier { get; private set; }
        public float FillFraction { get; private set; }
        public int PendingCollect { get; private set; }
        /// <summary>Echo silo fill percent (round(FillFraction * 100)).</summary>
        public int EchoSiloPct { get; private set; }
        /// <summary>Max collector fill percent (round(CollectorMaxFill * 100)).</summary>
        public int CollectorPct { get; private set; }
        public int WavesUntilNext { get; private set; }
        /// <summary>Waves-per-echo cadence, floored at 1.</summary>
        public int PerEcho { get; private set; }
        /// <summary>The cadence the invite copy names: WavesUntilNext, but never 0 (falls back to PerEcho).</summary>
        public int WavesToNext { get; private set; }
        public float NextEchoProgress { get; private set; }

        public bool Empty { get; private set; }          // owned <= 0
        public bool FirstRun { get; private set; }        // owned <= 1
        public bool RosterComplete { get; private set; }  // owned >= max

        // -- Composed strings (verbatim from the old Views) ---------------------

        /// <summary>The HUD count line: "Echoes  N/M" (two spaces).</summary>
        public string HudCountLine { get; private set; }
        /// <summary>The HUD silo line: "Pending  P   Echo S%   Collectors C%".</summary>
        public string HudSiloLine { get; private set; }
        /// <summary>The roster header ETA line ("Echoes N/M   -   ...").</summary>
        public string RosterEtaText { get; private set; }
        /// <summary>The honest shared-perk line ("Each Echo speeds ALL harvest -- now xN ..."); null when owned == 0.</summary>
        public string HarvestPerkLine { get; private set; }

        protected virtual void Recompute()
        {
            bool avail = Model != null && Model.Available;
            HasWorkforce = avail;

            int max = Model != null ? Model.MaxEchoes : EchoRosterCatalog.Count;
            if (max < 1) max = 1;
            // Roster's OwnedCount contract: clamp [0,max] when a service exists, else default 1.
            int owned = avail ? Clamp(Model.EchoCount, 0, max) : (Model != null ? Clamp(Model.EchoCount, 0, max) : 1);
            MaxEchoes = max;
            Owned = owned;

            int per = Model != null ? Math.Max(1, Model.WavesPerEcho) : 5;
            PerEcho = per;
            int nextWaves = Model != null ? Model.WavesUntilNextEcho : 0;
            WavesUntilNext = nextWaves;
            WavesToNext = nextWaves > 0 ? nextWaves : per;
            NextEchoProgress = Model != null ? Model.NextEchoProgress : 0f;
            GlobalHarvestMultiplier = Model != null ? Model.GlobalHarvestMultiplier : 1.0;
            FillFraction = Model != null ? Model.FillFraction : 0f;
            PendingCollect = Model != null ? Model.PendingCollect : 0;
            EchoSiloPct = RoundPct(FillFraction);
            CollectorPct = RoundPct(Model != null ? Model.CollectorMaxFill : 0f);

            Empty = owned <= 0;
            FirstRun = owned <= 1;
            RosterComplete = owned >= max;

            HudCountLine = "Echoes  " + owned + "/" + max;
            HudSiloLine = "Pending  " + PendingCollect + "   Echo " + EchoSiloPct
                        + "%   Collectors " + CollectorPct + "%";

            if (RosterComplete)
                RosterEtaText = "Echoes " + owned + "/" + max + "   -   Roster complete!";
            else if (FirstRun)
                RosterEtaText = "Echoes " + owned + "/" + max + "   -   " + WavesToNext
                              + " more wave" + (WavesToNext == 1 ? "" : "s") + " to your next spirit";
            else
                RosterEtaText = "Echoes " + owned + "/" + max + "   -   Next Echo in " + nextWaves
                              + " wave" + (nextWaves == 1 ? "" : "s");

            HarvestPerkLine = owned > 0
                ? "Each Echo speeds ALL harvest -- now x" + GlobalHarvestMultiplier.ToString("0.#")
                  + " to every node's yield."
                : null;
        }

        // -- Command ------------------------------------------------------------

        /// <summary>Collect All (collectors + echo silo). Returns the integer banked; raises Changed.</summary>
        public int CollectAll()
        {
            int banked = Model != null ? Model.CollectAll() : 0;
            Recompute();
            Raise();
            return banked;
        }

        // -- helpers ------------------------------------------------------------
        protected void Raise() { if (!_disposed) Changed?.Invoke(); }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        private static int RoundPct(float f) => (int)Math.Round(f * 100f);
    }

    /// <summary>
    /// The live adapter: <see cref="IEchoWorkforce"/> over EchoService + ResourceCollectorService.
    /// Null-safe (no service -> neutral defaults). Re-raises EchoService's own events.
    /// </summary>
    public sealed class EchoServiceWorkforce : IEchoWorkforce, IDisposable
    {
        private readonly EchoService _svc;

        public EchoServiceWorkforce()
        {
            _svc = EchoService.Instance;
            if (_svc != null)
            {
                _svc.Changed += RaiseChanged;
                _svc.EchoUnlocked += RaiseUnlocked;
            }
        }

        public void Dispose()
        {
            if (_svc != null)
            {
                _svc.Changed -= RaiseChanged;
                _svc.EchoUnlocked -= RaiseUnlocked;
            }
            Changed = null;
            EchoUnlocked = null;
        }

        private void RaiseChanged() => Changed?.Invoke();
        private void RaiseUnlocked(int n) => EchoUnlocked?.Invoke(n);

        public bool Available => EchoService.Instance != null;
        public int EchoCount => EchoService.Instance != null ? EchoService.Instance.EchoCount : 1;
        public int MaxEchoes => EchoService.Instance != null ? EchoService.Instance.MaxEchoes : EchoRosterCatalog.Count;
        public int WavesPerEcho => EchoService.Instance != null ? EchoService.Instance.WavesPerEcho : 5;
        public int WavesUntilNextEcho => EchoService.Instance != null ? EchoService.Instance.WavesUntilNextEcho : 0;
        public float NextEchoProgress => EchoService.Instance != null ? EchoService.Instance.NextEchoProgress : 0f;
        public double GlobalHarvestMultiplier => EchoService.Instance != null ? EchoService.Instance.GlobalHarvestMultiplier : 1.0;
        public float FillFraction => EchoService.Instance != null ? EchoService.Instance.FillFraction : 0f;
        public int PendingCollect => ResourceCollectorService.TotalPending();
        public float CollectorMaxFill => ResourceCollectorService.MaxFillFraction();
        public int CollectAll() => ResourceCollectorService.CollectAll();

        public event Action Changed;
        public event Action<int> EchoUnlocked;
    }
}
