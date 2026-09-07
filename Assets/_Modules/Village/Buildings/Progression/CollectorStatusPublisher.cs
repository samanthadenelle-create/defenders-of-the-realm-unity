// =============================================================================
// CollectorStatusPublisher — Village -> Core snapshot pump for the ambient
// collector tell (WO-900 §4).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village
//
// THE SHAPE, and why it is this one: BuildTimerService already publishes a
// presentation-ready queue snapshot into ObsidianQueueGate and the HUD polls it. This is
// the same pattern for collectors — Village owns the model, Core holds the snapshot, HUD
// reads it. DeNelle.HUD cannot reference DeNelle.Village (CLAUDE.md §5, the one enforced
// invariant), so this is not a convenience, it is the only legal seam.
//
// ONE OWNER: the numbers are composed HERE, once per tick, out of the registry the
// collectors already register themselves in. Nothing downstream re-derives them, and the
// tap answers with the EXISTING ResourceCollectorService.CollectAll() — no second
// collect command is minted anywhere in this feature.
//
// Lives on the DDOL ResourceCollectorHost (ResourceCollectorBootstrap.EnsureHost), so it
// survives scene loads exactly as the fallback collectors parented to it do.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Village.Buildings.Progression
{
    [DisallowMultipleComponent]
    public sealed class CollectorStatusPublisher : MonoBehaviour
    {
        /// <summary>Publish cadence. The collectors accrue on a slow economy clock; a
        /// half-second pump is far finer than the model moves and costs a handful of
        /// float reads over at most a few collectors.</summary>
        private const float PublishIntervalSec = 0.5f;

        /// <summary>The near-full warning band the diegetic view already uses (85%). Kept
        /// here only so the trace can say WHY a publish is interesting; the chip does its
        /// own text, and neither surface signals with colour.</summary>
        private const int NearFullPct = 85;

        private float _nextPublishAt;
        private int _lastFull = -1, _lastTotal = -1, _lastPct = -1, _lastPending = -1;

        private void OnEnable()
        {
            CollectorStatusGate.CollectAllRequested -= OnCollectAllRequested;
            CollectorStatusGate.CollectAllRequested += OnCollectAllRequested;
            _nextPublishAt = 0f;
            FlowTrace.Step("Harvest", "CollectorStatusPublisher online - the ambient collector tell has a publisher " +
                                      "and the chip's tap has a listener.");
        }

        private void OnDisable()
        {
            CollectorStatusGate.CollectAllRequested -= OnCollectAllRequested;
        }

        private void Update()
        {
            // WO-1483: town frame path — the 0.5s collector publish. The gate is INSIDE the
            // scope on purpose: the skipped frames are what prove the cadence is real.
            using var _perf = DeNelle.Core.Diagnostics.FlowTrace.Measure(
                "Perf", "CollectorStatusPublisher.Update", 4f, 1f);

            if (Time.unscaledTime < _nextPublishAt) return;
            _nextPublishAt = Time.unscaledTime + PublishIntervalSec;
            Guard.Try("Harvest", "publish collector status", Publish);
        }

        private void Publish()
        {
            int full = 0, total = 0;
            float maxFill = 0f;
            foreach (var c in ResourceCollectorRegistry.All)
            {
                if (c == null) continue;
                total++;
                if (c.IsFull) full++;
                if (c.FillFraction > maxFill) maxFill = c.FillFraction;
            }

            int pct = Mathf.Clamp(Mathf.RoundToInt(maxFill * 100f), 0, 100);
            int pending = ResourceCollectorService.TotalPending();

            CollectorStatusGate.PublishStatus(new CollectorStatusGate.CollectorStatus
            {
                Available = true,
                FullCount = full,
                TotalCount = total,
                MaxFillPct = pct,
                TotalPending = pending,
            });

            // Edge-logged only: a per-tick line here would be a 2 Hz firehose for a condition
            // that is permanent while the player stands in town, and would bury real F8 signals.
            // A collector filling up is a NORMAL player state, so this is Step, never Warn.
            if (full != _lastFull || total != _lastTotal || pct != _lastPct || pending != _lastPending)
            {
                _lastFull = full; _lastTotal = total; _lastPct = pct; _lastPending = pending;
                FlowTrace.Step("Harvest", "collector status -> full=" + full + "/" + total +
                               " maxFill=" + pct + "% pending=" + pending +
                               (pct >= NearFullPct ? " (near-full band)" : ""));
            }
        }

        // The tap. The chip carries the WORDS; the command was already built (WO-663).
        private void OnCollectAllRequested()
        {
            int banked = ResourceCollectorService.CollectAll();
            FlowTrace.Step("Harvest", "ambient collector chip -> CollectAll banked=" + banked);
            _nextPublishAt = 0f;   // repaint the chip on the next frame, not half a second later
        }
    }
}
