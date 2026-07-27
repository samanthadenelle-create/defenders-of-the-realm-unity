// =============================================================================
// IJobEffect / JobEffectRegistry — the per-kind completion handler seam (WO-773).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Jobs
//
// The queue owns TIMING / SLOTS / PERSISTENCE / OFFLINE-FAIRNESS; each system owns
// its EFFECT (what happens when the job lands). A completed job is routed to the
// IJobEffect registered for its JobKind. This is how "everything flows through the
// queue" stays open-ended: a new job type = a new JobKind + one registered handler,
// with the queue untouched.
//
// REUSE, not reinvention: the proven Build/Upgrade completion seams already exist —
//   • Build  → BuildTimerService.JobCompleted event (UnderConstructionVisual reveal).
//   • Upgrade → CompletedUpgradeApplier (called directly in CompleteJob).
// Those keep firing exactly as before. The registry handles the EXTENSIBLE kinds
// (Repair / TrainTroop / UnlockTier / LearnMagic / …). BuildTimerService calls
// Apply() for every completed job; unregistered kinds are a safe no-op, so Build /
// Upgrade never double-apply.
//
// Handlers live in their owning assembly (e.g. a TrainTroopEffect in DeNelle.Village)
// and self-register at startup. The registry is a pure static map — reset on domain
// reload; re-registration is idempotent (last registration per kind wins).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Core.Jobs
{
    /// <summary>A completion handler for one <see cref="JobKind"/> — applied when a job finishes.</summary>
    public interface IJobEffect
    {
        /// <summary>The kind of job this effect applies to.</summary>
        JobKind Kind { get; }

        /// <summary>Apply the effect for a completed <paramref name="job"/> (live expiry, skip, or offline sweep).</summary>
        void Apply(BuildJobData job);
    }

    /// <summary>
    /// Static registry mapping <see cref="JobKind"/> → its <see cref="IJobEffect"/>. The queue
    /// calls <see cref="Apply"/> on every completion; a kind with no registered handler is a
    /// safe no-op (Build/Upgrade are handled by their existing seams, not here).
    /// </summary>
    public static class JobEffectRegistry
    {
        private static readonly Dictionary<JobKind, IJobEffect> Handlers = new Dictionary<JobKind, IJobEffect>();

        /// <summary>Register (or replace) the handler for <paramref name="effect"/>'s kind. Null-safe.</summary>
        public static void Register(IJobEffect effect)
        {
            if (effect == null) return;
            Handlers[effect.Kind] = effect;
            FlowTrace.Step("Obsidian", $"job effect registered for kind '{effect.Kind}'.");
        }

        /// <summary>True if a handler is registered for <paramref name="kind"/>.</summary>
        public static bool Has(JobKind kind) => Handlers.ContainsKey(kind);

        /// <summary>
        /// Apply the registered effect for <paramref name="job"/>'s kind, if any. Guarded (§12) so
        /// one bad effect logs via FlowTrace.Fail and never blocks the queue's completion cascade.
        /// A no-op for a kind with no registered handler (Build/Upgrade use their existing seams).
        /// </summary>
        public static void Apply(BuildJobData job)
        {
            if (!Handlers.TryGetValue(job.JobKind, out var effect) || effect == null) return;
            Guard.Try("Obsidian", $"apply job effect '{job.JobKind}' -> '{job.StructureId}'",
                () => effect.Apply(job));
        }

        /// <summary>Clear all handlers (test/domain-reload hygiene).</summary>
        public static void Clear() => Handlers.Clear();
    }
}
