// =============================================================================
// ObsidianQueueVM — the work-queue panel's ViewModel (WO-1512), restoring the
// WO-864 "DUMB SKIN" contract that ObsidianQueueHud's own header claims.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT WAS ACTUALLY WRONG (and what was NOT). The audit line for this file reads
// "SPENDS CURRENCY directly". Read at source, the View never debited a wallet —
// BuildTimerService.TryBuySlot / TryInstantFinish own the basket (WO-911 Q1) and
// always did. The real breach is one level up and is still a breach: the VIEW
// resolved the service singleton, decided WHETHER a spend was on offer (price
// quote, gold-vs-crystal currency, the ff.rewardedadskip gate), invoked the spend
// verb, and interpreted the outcome. A skin that decides when the player may be
// asked for money is not a skin.
//
// So this VM does NOT re-home the debit — that would move it OUT of the service
// that correctly owns it. It owns the DECISION and the CALL:
//   • QueueOffer  — the per-job "what may be sold here": price, currency, ad gate.
//   • BuySlot / InstantFinish / WatchAdSkip — the three spend verbs, as commands.
//   • Channel projections (active/pending/slots) so the header stops reading the
//     queue itself.
// The View renders an offer and routes a tap. It resolves no service and quotes
// no price.
//
// PURE C#: implements IPanelViewModel, no UnityEngine UI types (§2). Outcomes come
// back as QueueCommandResult (message + neutral tone); the View maps the tone.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Ads;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village
{
    /// <summary>Neutral outcome tone. The View maps it to the kit's toast palette (§2).</summary>
    public enum QueueTone { Info, Good, Bad }

    /// <summary>What a queue command wants said on screen.</summary>
    public readonly struct QueueCommandResult
    {
        public readonly string Message;
        public readonly QueueTone Tone;
        public readonly bool Ok;

        public QueueCommandResult(string message, QueueTone tone, bool ok)
        {
            Message = message;
            Tone = tone;
            Ok = ok;
        }

        public static readonly QueueCommandResult None = new QueueCommandResult(null, QueueTone.Info, false);
    }

    /// <summary>
    /// The sell-time offer for ONE active job — everything the action row needs to decide what to
    /// draw, computed once in the VM so the View invents no pricing or gating rule.
    /// </summary>
    public readonly struct QueueOffer
    {
        /// <summary>Price of the instant finish, in whichever currency <see cref="PaysGold"/> names.
        /// Zero means "no instant finish is on offer for this job".</summary>
        public readonly int Price;
        /// <summary>TRUE = gold (the HIRE REINFORCEMENTS face); FALSE = crystals.</summary>
        public readonly bool PaysGold;
        /// <summary>TRUE only when a rewarded ad skip is genuinely available — the ff.rewardedadskip
        /// prerequisite AND the service's own per-job gate. The View must not re-derive either.</summary>
        public readonly bool AdAvailable;
        /// <summary>Ready-made face for the priced button ("HIRE REINFORCEMENTS 500g" / "120c").</summary>
        public readonly string PriceFace;

        public QueueOffer(int price, bool paysGold, bool adAvailable, string priceFace)
        {
            Price = price;
            PaysGold = paysGold;
            AdAvailable = adAvailable;
            PriceFace = priceFace;
        }

        /// <summary>Nothing is on offer — the row renders nothing at all.</summary>
        public bool IsEmpty => Price <= 0 && !AdAvailable;

        public static readonly QueueOffer Empty = new QueueOffer(0, false, false, null);
    }

    /// <summary>
    /// ViewModel for <see cref="ObsidianQueueHud"/>: channel projections + the three spend commands.
    /// Stateless over the queue itself (BuildTimerService remains the single owner); this type is the
    /// SEAM, not a second copy of the queue.
    /// </summary>
    public sealed class ObsidianQueueVM : IPanelViewModel
    {
        private const string Sys = "Obsidian";

        public event Action Changed;

        public string Title => "Queues";

        public static ObsidianQueueVM CreateDefault() => new ObsidianQueueVM();

        /// <summary>TRUE when the queue service is alive; the View shows its "unavailable" line otherwise.</summary>
        public bool ServiceReady => BuildTimerService.Instance != null;

        // ── lifecycle ─────────────────────────────────────────────────────────

        /// <summary>Subscribe to the service's QueueChanged so the View re-renders through the VM's
        /// own Changed, never off a service event it subscribed to itself.</summary>
        public void Attach()
        {
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged += Raise;
        }

        public void Detach()
        {
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged -= Raise;
        }

        public void Close() { /* the View owns its own hide/teardown. */ }

        public void Dispose()
        {
            Detach();
            Changed = null;
        }

        // ── projections ───────────────────────────────────────────────────────

        public IReadOnlyList<BuildJobData> ActiveJobs(ChannelId id)
        {
            var svc = BuildTimerService.Instance;
            return svc != null ? svc.ActiveJobsOf(id) : (IReadOnlyList<BuildJobData>)Array.Empty<BuildJobData>();
        }

        public IReadOnlyList<BuildJobData> PendingJobs(ChannelId id)
        {
            var svc = BuildTimerService.Instance;
            return svc != null ? svc.PendingJobsOf(id) : (IReadOnlyList<BuildJobData>)Array.Empty<BuildJobData>();
        }

        public int SlotCount(ChannelId id)
        {
            var svc = BuildTimerService.Instance;
            return svc != null ? svc.SlotCount(id) : 0;
        }

        /// <summary>
        /// THE SELL-TIME DECISION, in one place. The View used to compute all four of these itself:
        /// the price quote, the currency branch, the FeatureFlags.RewardedAdSkip read and the
        /// service's per-job ad gate. A View that decides whether to ask for money is not a skin.
        /// </summary>
        public QueueOffer OfferFor(BuildJobData job)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null || job.StartMs <= 0 || string.IsNullOrEmpty(job.StructureId))
                return QueueOffer.Empty;

            ChannelId channel = job.ChannelId;
            int price = svc.InstantFinishPrice(channel, job.StructureId);
            bool paysGold = svc.FinishPaysGold(channel, job.StructureId);

            // RELEASE BLOCKER GATE (2026-08-07, preserved verbatim in intent): no ad SDK exists, so
            // the "Ad" affordance is ABSENT — not greyed, not silently dead — until
            // FeatureFlags.RewardedAdSkip's prerequisites land (a real SDK plus WO-912 server-side
            // ad-window validation). Both halves of the gate are evaluated HERE so no View can ship
            // an ad button by forgetting one of them.
            bool adOk = DeNelle.Core.FeatureFlags.RewardedAdSkip &&
                        svc.CanWatchAdToSkip(channel, job.StructureId);

            string face = price <= 0 ? null
                        : paysGold ? BuildTimerService.HireReinforcementsVerb + " " + price + "g"
                                   : price + "c";

            return new QueueOffer(price, paysGold, adOk, face);
        }

        // ── spend commands ────────────────────────────────────────────────────

        /// <summary>
        /// Buy an extra slot on a channel. WO-911 Q6/B3: the free increment is GONE — TryBuySlot
        /// applies the owner's TWO-STEP gate (the Echo count unlocks the RIGHT to buy, crystals
        /// complete it) and reports a player-readable refusal instead of granting a free worker.
        /// </summary>
        public QueueCommandResult BuySlot(ChannelId channel)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null) return QueueCommandResult.None;

            if (svc.TryBuySlot(channel, out string failure))
            {
                FlowTrace.Step(Sys, "ObsidianQueueVM.BuySlot " + channel + " OK");
                Raise();
                return new QueueCommandResult("Extra " + channel + " slot unlocked.", QueueTone.Good, true);
            }

            FlowTrace.Step(Sys, "ObsidianQueueVM.BuySlot " + channel + " REFUSED: " + failure);
            return new QueueCommandResult(failure ?? "Could not buy a slot.", QueueTone.Bad, false);
        }

        /// <summary>
        /// The priced finish. The CURRENCY and the DEBIT are the service's decision (FinishPaysGold
        /// inside TryInstantFinish) — this command carries the offer's currency only so the toast can
        /// use the right words. It reads no wallet and debits nothing (HireReinforcementsRegression 5a).
        /// </summary>
        public QueueCommandResult InstantFinish(ChannelId channel, string structureId, bool paysGold)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null || string.IsNullOrEmpty(structureId)) return QueueCommandResult.None;

            bool ok = svc.TryInstantFinish(channel, structureId, out string failure);
            FlowTrace.Step(Sys, "ObsidianQueueVM.InstantFinish '" + structureId + "' on " + channel +
                                " paysGold=" + paysGold + " ok=" + ok);
            if (ok)
            {
                Raise();
                return new QueueCommandResult(
                    paysGold ? "Reinforcements hired." : "Finished instantly.", QueueTone.Good, true);
            }
            return new QueueCommandResult(failure ?? "Can't finish now.", QueueTone.Bad, false);
        }

        /// <summary>
        /// The rewarded-ad skip. ASYNC by construction (WO-1125): a real SDK cannot answer "reward
        /// earned" synchronously — the callback lands seconds after the return — so the outcome is
        /// delivered to <paramref name="onOutcome"/> when the ad actually finishes.
        /// A dismissal is NOT an error and must not read as one: the player chose to stop watching,
        /// and telling them something broke is a lie.
        /// </summary>
        public void WatchAdSkip(ChannelId channel, string structureId, Action<QueueCommandResult> onOutcome)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null || string.IsNullOrEmpty(structureId))
            {
                onOutcome?.Invoke(QueueCommandResult.None);
                return;
            }

            svc.WatchAdToSkip(channel, structureId, result =>
            {
                QueueCommandResult outcome;
                if (result.Rewarded)
                    outcome = new QueueCommandResult("Time skipped.", QueueTone.Info, true);
                else if (result.Reason == AdUnavailableReason.Abandoned)
                    outcome = new QueueCommandResult("Ad closed early - no time skipped.", QueueTone.Info, false);
                else
                    outcome = new QueueCommandResult("Ad skip unavailable right now.", QueueTone.Bad, false);

                FlowTrace.Step(Sys, "ObsidianQueueVM.WatchAdSkip '" + structureId + "' outcome=" + result);
                if (result.Rewarded) Raise();
                onOutcome?.Invoke(outcome);
            });
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
