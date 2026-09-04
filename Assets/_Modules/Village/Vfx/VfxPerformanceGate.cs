// =============================================================================
// VfxPerformanceGate - the Seeker frame-time gate that protects the 48-loop
// dungeon tier, by MEASURING first and shedding room dress last-resort.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## WHY THIS EXISTS (WO-1242)
//
// The owner ruled the dungeon VFX tier ON: dungeon scenes permit 48 simultaneous
// loops instead of the village 24, and VfxLoopBudget now self-binds that tier from
// the loaded scene set. The ruling is not re-litigated here. This file exists so
// the RAISED CEILING CANNOT COST FRAME TIME:
//
//   "Keep 48-tier ON, but add a Seeker performance gate and automatic VFX
//    degradation if frame time crosses target. Preserve the visual ruling while
//    protecting the device from fill-rate carnage."  - owner, 2026-08-26
//
// ## MEASURE BEFORE YOU DEGRADE (CLAUDE.md section 12) - STRUCTURALLY, NOT AS ADVICE
//
// This gate CANNOT shed anything until it holds two measured numbers from THE SAME
// SESSION on THE SAME DEVICE:
//
//   1. A BASELINE - the mean frame time in this session's lowest-occupancy loop
//      bucket, i.e. what the device costs when VFX is nearly idle.
//   2. A CURRENT smoothed frame time, sustained over budget for several windows.
//
// Degradation is armed ONLY when the baseline is INSIDE budget and the current
// frame time is OUTSIDE it. If the device is over budget even with the pool nearly
// empty, VFX is not the cause: shedding dress would be a silent quality drop that
// buys nothing, and the gate says so in the trace and shed NOTHING. That is the
// section-12 "the data pinpoints the dead step" rule expressed as a state machine
// rather than as a comment somebody has to obey.
//
// No threshold in this file was picked from intuition:
//   * The TARGET is the device's own frame budget - 1000 / Application.targetFrameRate,
//     which SeekerBootstrap sets to 30 or 60 from the selected quality tier with
//     vSync off, so it is authoritative rather than a guess (SeekerBootstrap.cs).
//   * The ESCALATE and RECOVER factors are a hysteresis BAND around that measured
//     budget (1.10 / 0.90), not an absolute millisecond number. A band is required
//     or the ladder oscillates on the boundary; its width is the only free
//     parameter and it costs one dropped frame in ten either side.
//   * The OCCUPANCY-to-FRAME-TIME RELATIONSHIP itself is measured and reported
//     (see SlopeMsPerLoop and the periodic table line), so the ticket's required
//     number - millisecond cost per live loop on the real device - comes out of a
//     capture instead of out of anybody's head.
//
// ## IT SAMPLES EVERY FRAME, NOT ON SCENE ENTRY - THIS IS THE WHOLE POINT
//
// Dungeon candles are additive transparent quads and the Seeker is FILL-RATE bound.
// The cost therefore appears when the player LOOKS TOWARD a lit room, not when they
// walk into it: the loop count is identical in both frames and only the covered
// pixel count changes. A gate that sampled on scene entry, or on a loop-count
// change, would miss the defect it was built for entirely. So the sampler runs in
// Update() unconditionally and the DECISION is made on a rolling window.
//
// ## HITCHES ARE REPORTED BUT NEVER DEGRADED - DIFFERENT SYMPTOM, DIFFERENT FIX
//
// 48 loops can pull pool instantiations mid-play ("demand-warm"), which reads as a
// one-frame HITCH rather than a steady cost. Shedding ambient dress does not fix an
// instantiation spike - it hides the evidence of one. So a spike frame is traced
// with its occupancy DELTA (the demand-warm tell) and is deliberately excluded from
// the steady-cost window that drives the ladder.
//
// ## THE LADDER SHEDS THE RIGHT THING, IN THE RIGHT ORDER
//
//   None        - authored rings, nothing touched.
//   AmbientTrim - the ambient ENVIRONMENT ring is halved. Room dress is the most
//                 numerous and least load-bearing loop class in the game
//                 (VfxLoopBudget's own words), and the additive candle quads are
//                 the fill-rate cost being measured.
//   AmbientOff  - ambient dress holds no loop at all.
//   AuraTrim    - and ONLY once ambient is already at zero, the enemy/pet nearest-N
//                 ring is halved, with a floor of 2 so the nearest bodies keep their
//                 role read.
//
// The ordering is not a convention a caller has to follow: AuraRingAt returns the
// authored ring at every level where AmbientRingAt is non-zero, so "ambient first"
// is arithmetic and VfxPerformanceGateRegression asserts it across the whole ladder.
//
// ## THE ACCESSIBILITY ALLOWLIST IS EXEMPT, ABSOLUTELY, AT EVERY LEVEL
//
// Aura_LowHealth and Aura_NearDeath are the owner's ONLY non-colour danger signal -
// she is red/green colourblind, and WO-1229 made those two loops unrefusable by
// explicit ruling after a captured fight in which the tell lost a pool race to a
// candle. A perf gate able to silence them would re-open that exact hole.
//
// The exemption is STRUCTURAL, in three independent ways, because one would be a
// policy somebody can edit:
//   1. MayShed consults VfxLoopBudget.IsAccessibilityLoop FIRST and returns false
//      for those types at EVERY level, including the maximum.
//   2. This gate owns no lever that can touch them even if MayShed were wrong. It
//      publishes two RING sizes. It does not change the loop CEILING, it does not
//      change VfxLoopBudget.AccessibilityReserve, and it does not participate in
//      VfxLoopBudget.WouldRefuseLoop - which short-circuits on the allowlist before
//      it looks at any number at all. Neither aura type is registered with either
//      ring in the first place (HeroHpStateAura does not implement IProximityAura),
//      so a ring of zero cannot reach them.
//   3. The reserve keeps working underneath: shedding ambient LOWERS pool occupancy,
//      which can only make the tell easier to start.
//
// ## NO SILENT QUALITY DROPS
//
// Every level change is traced with the numbers that caused it, and while degraded
// the gate keeps repeating its state on the periodic line. A quality drop nobody can
// see in a log is indistinguishable from a bug, and this repo has paid for that.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// How much VFX presence the performance gate is currently shedding. Ordered:
    /// every step sheds strictly more than the one before it, and ambient room dress
    /// is exhausted before combat auras are touched at all.
    /// </summary>
    public enum VfxShedLevel
    {
        /// <summary>Authored rings. Nothing is shed.</summary>
        None = 0,
        /// <summary>Ambient environment ring halved.</summary>
        AmbientTrim = 1,
        /// <summary>Ambient environment ring at zero.</summary>
        AmbientOff = 2,
        /// <summary>Ambient at zero AND the enemy/pet nearest-N ring halved (floor 2).</summary>
        AuraTrim = 3,
    }

    /// <summary>
    /// Samples frame time against live loop occupancy, reports the measured
    /// relationship, and - only once that measurement implicates VFX - sheds ambient
    /// room dress before anything else. Never sheds the accessibility loops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxPerformanceGate : MonoBehaviour
    {
        // =====================================================================
        //  Tunables - every one of them relative to the MEASURED frame budget
        // =====================================================================

        /// <summary>The top of the ladder. Kept as a constant so tests can walk to it.</summary>
        public const VfxShedLevel MaxLevel = VfxShedLevel.AuraTrim;

        /// <summary>
        /// Length of one decision window, seconds. Long enough that a single
        /// scheduling stall does not read as a trend, short enough that a player
        /// turning to face a lit room is answered inside a second.
        /// </summary>
        public const float WindowSeconds = 0.5f;

        /// <summary>Frames a window must hold before its mean is trusted at all.</summary>
        public const int MinWindowSamples = 8;

        /// <summary>
        /// Over this multiple of the measured budget, a window counts as OVER.
        /// 1.10 = one dropped frame in ten; the frame pacer absorbs less than that.
        /// </summary>
        public const float OverFactor = 1.10f;

        /// <summary>Under this multiple of budget, a window counts as UNDER (recovery).</summary>
        public const float RecoverFactor = 0.90f;

        /// <summary>Consecutive OVER windows required to shed one more step (1.5 s).</summary>
        public const int SustainWindowsToShed = 3;

        /// <summary>Consecutive UNDER windows required to give one step back (3 s).</summary>
        public const int SustainWindowsToRecover = 6;

        /// <summary>
        /// A frame costing more than this multiple of budget is a HITCH, not a steady
        /// cost. It is traced (with its occupancy delta - the demand-warm tell) and
        /// excluded from the window mean, because shedding dress does not fix a pool
        /// instantiation spike.
        /// </summary>
        public const float HitchFactor = 3f;

        /// <summary>
        /// Loop occupancy at or below which a sample counts toward the BASELINE - what
        /// the device costs with the pool nearly idle. One bucket wide, deliberately:
        /// the baseline must describe "VFX is not doing much", not "VFX is doing less".
        /// </summary>
        public const int BaselineOccupancy = 3;

        /// <summary>Frames the baseline needs before degradation may arm at all.</summary>
        public const int MinBaselineSamples = 120;

        /// <summary>Seconds between periodic occupancy/frame-time table lines.</summary>
        public const float ReportSeconds = 10f;

        // Occupancy histogram: BucketWidth loops per bucket, covering 0..63 so the
        // 48 dungeon ceiling and the allowlist overrun above it both land in range.
        /// <summary>Live loops per histogram bucket.</summary>
        public const int BucketWidth = 4;
        /// <summary>Number of occupancy buckets (0..63 loops).</summary>
        public const int BucketCount = 16;

        // =====================================================================
        //  Pure ladder arithmetic - no Unity state, directly testable headless
        // =====================================================================

        /// <summary>
        /// The AMBIENT ENVIRONMENT ring size in force at <paramref name="level"/>, given
        /// the authored ring (<see cref="VfxLoopBudget.AmbientEnvRing"/>). The authored
        /// value is returned unchanged at <see cref="VfxShedLevel.None"/> - a gate that
        /// trims while healthy would be the silent quality drop this file forbids.
        /// </summary>
        public static int AmbientRingAt(VfxShedLevel level, int authoredRing)
        {
            if (authoredRing <= 0) return 0;
            switch (level)
            {
                case VfxShedLevel.None:        return authoredRing;
                case VfxShedLevel.AmbientTrim: return Mathf.Max(1, authoredRing / 2);
                default:                       return 0;   // AmbientOff and above
            }
        }

        /// <summary>
        /// The ENEMY/PET nearest-N ring in force at <paramref name="level"/>, given the
        /// authored ring (<see cref="VfxLoopBudget.NearestAuraRing"/>).
        /// <para/>
        /// AMBIENT FIRST IS ARITHMETIC HERE: this returns the authored ring at every
        /// level where <see cref="AmbientRingAt"/> is still non-zero, so combat auras
        /// cannot be trimmed while any room dress is still lit. The floor of 2 exists
        /// because an enemy aura is a ROLE READ - the nearest bodies keep theirs even
        /// at the bottom of the ladder.
        /// </summary>
        public static int AuraRingAt(VfxShedLevel level, int authoredRing)
        {
            if (authoredRing <= 0) return 0;
            if (level < VfxShedLevel.AuraTrim) return authoredRing;
            return Mathf.Max(2, authoredRing / 2);
        }

        /// <summary>
        /// May the performance gate reduce the presence of <paramref name="type"/> at
        /// <paramref name="level"/>?
        /// <para/>
        /// FALSE FOR THE ACCESSIBILITY ALLOWLIST AT EVERY LEVEL, INCLUDING THE MAXIMUM -
        /// owner ruling, 2026-08-26. Aura_LowHealth / Aura_NearDeath are the only
        /// non-colour danger signal a red/green colourblind player has. The allowlist is
        /// consulted BEFORE the level, so no future level can be added that reaches them.
        /// </summary>
        public static bool MayShed(VfxShedLevel level, VFXType type)
        {
            if (VfxLoopBudget.IsAccessibilityLoop(type)) return false;
            return level != VfxShedLevel.None;
        }

        /// <summary>
        /// THE decision, pure: given the current level, this window's smoothed frame
        /// time, this session's measured low-occupancy baseline and the device's frame
        /// budget (all milliseconds), return the level that should now be in force.
        /// <para/>
        /// Pass <paramref name="baselineMs"/> &lt;= 0 when the baseline has not been
        /// measured yet - the gate then holds where it is and CANNOT escalate. That is
        /// "measure before you degrade" as a state machine.
        /// </summary>
        /// <param name="why">Always set; the reason the caller traces.</param>
        public static VfxShedLevel Decide(
            VfxShedLevel current,
            float smoothedMs,
            float baselineMs,
            float budgetMs,
            int consecutiveOverWindows,
            int consecutiveUnderWindows,
            out string why)
        {
            if (budgetMs <= 0f)
            {
                why = "no frame budget resolved; gate idle";
                return VfxShedLevel.None;
            }

            if (baselineMs <= 0f)
            {
                why = "no low-occupancy baseline measured yet (need " + MinBaselineSamples +
                      " frames at occupancy <= " + BaselineOccupancy +
                      "); holding at " + current + " - MEASURE BEFORE DEGRADE";
                return current;
            }

            // THE DISCRIMINATOR. If the device is already at or over budget with the
            // pool nearly idle, the cost is not the loops, and shedding dress would buy
            // nothing while quietly lowering quality. Release everything and say so.
            if (baselineMs >= budgetMs * OverFactor)
            {
                why = "baseline " + baselineMs.ToString("F1") + "ms is ITSELF over the " +
                      budgetMs.ToString("F1") + "ms budget at occupancy <= " + BaselineOccupancy +
                      " - VFX loops are NOT the cause, so nothing is shed";
                return VfxShedLevel.None;
            }

            if (smoothedMs > budgetMs * OverFactor && consecutiveOverWindows >= SustainWindowsToShed)
            {
                if (current >= MaxLevel)
                {
                    why = "already at " + MaxLevel + " and still " + smoothedMs.ToString("F1") +
                          "ms vs " + budgetMs.ToString("F1") + "ms budget - the remaining cost is " +
                          "NOT sheddable VFX presence";
                    return MaxLevel;
                }
                var next = (VfxShedLevel)((int)current + 1);
                why = smoothedMs.ToString("F1") + "ms over the " + budgetMs.ToString("F1") +
                      "ms budget for " + consecutiveOverWindows + " window(s), while the " +
                      "low-occupancy baseline is " + baselineMs.ToString("F1") +
                      "ms (inside budget) - loops are implicated, shedding to " + next;
                return next;
            }

            if (smoothedMs < budgetMs * RecoverFactor && consecutiveUnderWindows >= SustainWindowsToRecover
                && current != VfxShedLevel.None)
            {
                var next = (VfxShedLevel)((int)current - 1);
                why = smoothedMs.ToString("F1") + "ms under the " + budgetMs.ToString("F1") +
                      "ms budget for " + consecutiveUnderWindows + " window(s) - restoring to " + next;
                return next;
            }

            why = "holding at " + current + " (" + smoothedMs.ToString("F1") + "ms vs " +
                  budgetMs.ToString("F1") + "ms budget)";
            return current;
        }

        /// <summary>The occupancy histogram bucket for a live loop count.</summary>
        public static int BucketFor(int occupancy)
            => Mathf.Clamp(Mathf.Max(0, occupancy) / BucketWidth, 0, BucketCount - 1);

        /// <summary>The lowest live loop count that lands in <paramref name="bucket"/>.</summary>
        public static int BucketFloor(int bucket) => Mathf.Max(0, bucket) * BucketWidth;

        // =====================================================================
        //  Live state
        // =====================================================================

        /// <summary>The gate instance, once bootstrapped. Null in a run that never ticked.</summary>
        public static VfxPerformanceGate Instance { get; private set; }

        /// <summary>
        /// Master switch. The gate still MEASURES when this is false - it only stops
        /// DECIDING - so a session can collect the occupancy table without any risk of a
        /// quality change (which is how the device capture for this ticket is taken).
        /// </summary>
        public static bool DegradationArmed = true;

        /// <summary>The shed level in force right now.</summary>
        public static VfxShedLevel Level { get; private set; } = VfxShedLevel.None;

        /// <summary>The AMBIENT ENVIRONMENT ring the culler should use right now.</summary>
        public static int AmbientRingNow => AmbientRingAt(Level, VfxLoopBudget.AmbientEnvRing);

        /// <summary>The ENEMY/PET nearest-N ring the culler should use right now.</summary>
        public static int AuraRingNow => AuraRingAt(Level, VfxLoopBudget.NearestAuraRing);

        /// <summary>Mean frame time (ms) of the last completed decision window; 0 before one closes.</summary>
        public static float LastWindowMs { get; private set; }

        /// <summary>
        /// The measured low-occupancy baseline (ms), or 0 while it is still being
        /// gathered. Degradation cannot arm until this is positive.
        /// </summary>
        public static float BaselineMs { get; private set; }

        /// <summary>Hitch frames seen this session (frame &gt; HitchFactor * budget).</summary>
        public static int HitchCount { get; private set; }

        /// <summary>Hitch frames that coincided with the loop count RISING - the demand-warm tell.</summary>
        public static int DemandWarmHitchCount { get; private set; }

        private static readonly float[] _bucketMsSum   = new float[BucketCount];
        private static readonly int[]   _bucketSamples = new int[BucketCount];
        private static readonly float[] _bucketMsMax   = new float[BucketCount];

        private float _windowMsSum;
        private int   _windowSamples;
        private float _windowElapsed;
        private int   _windowOccupancySum;

        private int _overWindows;
        private int _underWindows;

        private float _reportTimer;
        private int   _lastOccupancy = -1;
        private bool  _disarmNoted;

        /// <summary>
        /// The device's frame budget in milliseconds: 1000 / Application.targetFrameRate,
        /// which SeekerBootstrap sets from the selected quality tier with vSync off, so
        /// it is the device's real pacing target rather than a number chosen here. Falls
        /// back to 60 Hz when the frame rate is uncapped.
        /// </summary>
        public static float FrameBudgetMs()
        {
            int fps = Application.targetFrameRate;
            if (fps <= 0) fps = 60;
            fps = Mathf.Clamp(fps, 20, 240);
            return 1000f / fps;
        }

        // =====================================================================
        //  Lifecycle - self-bootstrapping, no scene authoring
        // =====================================================================
        //
        // Deliberately the same shape as VfxAuraProximityCuller and VfxLoopBudget: a
        // RuntimeInitializeOnLoadMethod, not a component somebody must place. The dungeon
        // tier this gate protects spent its entire life dead because it depended on a
        // component present in zero scenes (WO-1229); repeating that here would produce a
        // perf gate that never once ran and a RESULT that could not tell.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            try
            {
                if (Instance != null) return;
                var go = new GameObject("[VfxPerformanceGate]");
                go.AddComponent<VfxPerformanceGate>();
                Object.DontDestroyOnLoad(go);
            }
            catch (System.Exception ex)
            {
                // Degrade to "no gate" - the authored rings stay in force, which is the
                // permissive state - rather than throwing into the scene loader.
                Debug.LogWarning("[VfxPerfGate] bootstrap skipped: " + ex.Message);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ResetMeasurements();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            // Leave the world PERMISSIVE. If the ticker dies while degraded, the rings
            // must return to their authored sizes or the shed would outlive the gate
            // with nothing left to restore it - the same "revoked with no re-granter"
            // trap VfxAuraProximityCuller guards on teardown.
            if (Level != VfxShedLevel.None)
            {
                FlowTrace.Step("VfxPerfGate",
                    "gate torn down while shedding at " + Level + " - restoring the authored rings " +
                    "(ambient " + VfxLoopBudget.AmbientEnvRing + ", enemy/pet " +
                    VfxLoopBudget.NearestAuraRing + "). A shed must never outlive its gate.");
                Level = VfxShedLevel.None;
            }
            Instance = null;
        }

        /// <summary>Clear every measurement. Public so a headless harness can re-baseline.</summary>
        public static void ResetMeasurements()
        {
            for (int i = 0; i < BucketCount; i++)
            {
                _bucketMsSum[i] = 0f;
                _bucketSamples[i] = 0;
                _bucketMsMax[i] = 0f;
            }
            LastWindowMs = 0f;
            BaselineMs = 0f;
            HitchCount = 0;
            DemandWarmHitchCount = 0;
        }

        private void Update()
        {
            float budgetMs = FrameBudgetMs();
            float frameMs  = Time.unscaledDeltaTime * 1000f;

            int occupancy = 0;
            var mgr = VFXManager.Instance;
            if (mgr != null) occupancy = mgr.ActiveLoopCount;

            SampleFrame(frameMs, occupancy, budgetMs);

            _windowElapsed += Time.unscaledDeltaTime;
            if (_windowElapsed >= WindowSeconds) CloseWindow(budgetMs);

            _reportTimer -= Time.unscaledDeltaTime;
            if (_reportTimer <= 0f) { _reportTimer = ReportSeconds; Report(budgetMs); }
        }

        // =====================================================================
        //  Measurement
        // =====================================================================

        /// <summary>
        /// Record one frame against its loop occupancy. Public and side-effect-contained
        /// so a headless harness can drive a synthetic session without a renderer.
        /// </summary>
        public void SampleFrame(float frameMs, int occupancy, float budgetMs)
        {
            if (frameMs <= 0f) return;

            int bucket = BucketFor(occupancy);
            _bucketMsSum[bucket]   += frameMs;
            _bucketSamples[bucket] += 1;
            if (frameMs > _bucketMsMax[bucket]) _bucketMsMax[bucket] = frameMs;

            // HITCH: traced, counted, and EXCLUDED from the steady-cost window. A pool
            // instantiation spike is a different defect with a different fix, and folding
            // it into the mean would make the ladder shed dress to chase a one-frame cost
            // that shedding cannot remove.
            if (budgetMs > 0f && frameMs > budgetMs * HitchFactor)
            {
                HitchCount++;
                int delta = _lastOccupancy < 0 ? 0 : occupancy - _lastOccupancy;
                bool demandWarm = delta > 0;
                if (demandWarm) DemandWarmHitchCount++;

                FlowTrace.Throttle("VfxPerfGate", "hitch", 1f,
                    "HITCH " + frameMs.ToString("F1") + "ms (budget " + budgetMs.ToString("F1") +
                    "ms) at loop occupancy " + occupancy + "/" + VfxLoopBudget.CurrentCap +
                    ", occupancy delta " + (delta >= 0 ? "+" : "") + delta + ". " +
                    (demandWarm
                        ? "The loop count ROSE on this frame - this reads as a demand-warm pool " +
                          "instantiation, NOT a steady fill-rate cost. Shedding room dress would not " +
                          "remove it; pre-warming the pool would. Excluded from the shed window."
                        : "The loop count did not rise - a stall from outside the VFX pool. " +
                          "Excluded from the shed window.") +
                    " Session hitches " + HitchCount + " (" + DemandWarmHitchCount + " demand-warm).");

                // WO-1324: Force an immediate flush on hitch. The window before a crash is exactly
                // what we need to diagnose, and a hitch frame often precedes a kill. Post immediately
                // so the buffer doesn't die with the tab.
                DeNelle.Core.Diagnostics.WebTrace.ForceFlush();

                _lastOccupancy = occupancy;
                return;
            }

            _lastOccupancy = occupancy;
            _windowMsSum += frameMs;
            _windowSamples += 1;
            _windowOccupancySum += occupancy;
        }

        /// <summary>
        /// This session's measured low-occupancy baseline in milliseconds, or 0 when it
        /// does not yet hold <see cref="MinBaselineSamples"/> frames. This is the number
        /// that decides whether frame-time excess is attributable to loops at all.
        /// </summary>
        public static float MeasuredBaselineMs()
        {
            int bucket = BucketFor(BaselineOccupancy);
            if (_bucketSamples[bucket] < MinBaselineSamples) return 0f;
            return _bucketMsSum[bucket] / _bucketSamples[bucket];
        }

        /// <summary>
        /// THE MEASURED RELATIONSHIP the ticket asks for, in numbers: milliseconds of
        /// frame time per live loop, taken between the low-occupancy baseline bucket and
        /// the highest-occupancy bucket that holds real samples. Returns false when there
        /// are not yet two populated buckets to draw a line between - which is a HONEST
        /// "not measured", never a zero passed off as a measurement.
        /// </summary>
        public static bool SlopeMsPerLoop(out float msPerLoop, out int loOcc, out float loMs,
                                          out int hiOcc, out float hiMs)
        {
            msPerLoop = 0f; loOcc = 0; loMs = 0f; hiOcc = 0; hiMs = 0f;

            int lo = -1, hi = -1;
            for (int i = 0; i < BucketCount; i++)
            {
                if (_bucketSamples[i] < MinWindowSamples) continue;
                if (lo < 0) lo = i;
                hi = i;
            }
            if (lo < 0 || hi < 0 || hi == lo) return false;

            loOcc = BucketFloor(lo) + BucketWidth / 2;
            hiOcc = BucketFloor(hi) + BucketWidth / 2;
            loMs  = _bucketMsSum[lo] / _bucketSamples[lo];
            hiMs  = _bucketMsSum[hi] / _bucketSamples[hi];
            msPerLoop = (hiMs - loMs) / Mathf.Max(1, hiOcc - loOcc);
            return true;
        }

        // =====================================================================
        //  Decision
        // =====================================================================

        private void CloseWindow(float budgetMs)
        {
            float elapsed = _windowElapsed;
            _windowElapsed = 0f;

            int samples = _windowSamples;
            float sum = _windowMsSum;
            int occSum = _windowOccupancySum;
            _windowSamples = 0; _windowMsSum = 0f; _windowOccupancySum = 0;

            if (samples < MinWindowSamples)
            {
                // Too few non-hitch frames to judge. NOT a pass and NOT a fail - the
                // counters are left alone so a stall cannot be read as either a trend
                // or a recovery.
                FlowTrace.Throttle("VfxPerfGate", "thin-window", 5f,
                    "decision window closed with only " + samples + " non-hitch frame(s) in " +
                    elapsed.ToString("F2") + "s (need " + MinWindowSamples +
                    "). Neither escalating nor recovering; the over/under counters are held.");
                return;
            }

            float meanMs = sum / samples;
            LastWindowMs = meanMs;
            BaselineMs = MeasuredBaselineMs();

            if (meanMs > budgetMs * OverFactor) { _overWindows++; _underWindows = 0; }
            else if (meanMs < budgetMs * RecoverFactor) { _underWindows++; _overWindows = 0; }
            else { _overWindows = 0; _underWindows = 0; }

            if (!DegradationArmed || !ArmedOnThisRun())
            {
                if (!_disarmNoted)
                {
                    _disarmNoted = true;
                    FlowTrace.Step("VfxPerfGate",
                        "MEASURING ONLY - degradation is not armed on this run (armed=" +
                        DegradationArmed + ", batchmode=" + Application.isBatchMode +
                        "). Frame times in a headless run have no fill-rate component, so a shed " +
                        "there would be noise rather than protection. The occupancy table is still " +
                        "collected and reported.");
                }
                return;
            }

            var next = Decide(Level, meanMs, BaselineMs, budgetMs, _overWindows, _underWindows, out string why);
            if (next == Level) return;

            var prev = Level;
            Level = next;
            _overWindows = 0;
            _underWindows = 0;

            // NO SILENT QUALITY DROP. Both directions, with every number behind them.
            FlowTrace.Step("VfxPerfGate",
                (next > prev ? "DEGRADING" : "RESTORING") + " " + prev + " -> " + next + ". " + why +
                ". Ambient env ring " + AmbientRingAt(prev, VfxLoopBudget.AmbientEnvRing) + " -> " +
                AmbientRingAt(next, VfxLoopBudget.AmbientEnvRing) + ", enemy/pet ring " +
                AuraRingAt(prev, VfxLoopBudget.NearestAuraRing) + " -> " +
                AuraRingAt(next, VfxLoopBudget.NearestAuraRing) + ". Tier=" + VfxLoopBudget.TierName +
                " (ceiling " + VfxLoopBudget.CurrentCap + ", UNCHANGED - this gate never moves the " +
                "ceiling). Aura_LowHealth / Aura_NearDeath are EXEMPT at every level and are " +
                "unaffected by this change.");
        }

        /// <summary>
        /// Whether degradation may act on this run at all. Batchmode has no fill-rate
        /// component, so its frame times cannot implicate the additive quads this gate
        /// exists to shed; measuring there is useful, shedding there is noise.
        /// </summary>
        public static bool ArmedOnThisRun() => !Application.isBatchMode;

        // =====================================================================
        //  Reporting - the occupancy/frame-time table the ticket asks for
        // =====================================================================

        private void Report(float budgetMs)
        {
            var sb = new System.Text.StringBuilder(320);
            sb.Append("frame time vs loop occupancy (budget ").Append(budgetMs.ToString("F1"))
              .Append("ms @ ").Append(Application.targetFrameRate).Append("fps target, tier=")
              .Append(VfxLoopBudget.TierName).Append(" ceiling ").Append(VfxLoopBudget.CurrentCap)
              .Append("): ");

            bool any = false;
            for (int i = 0; i < BucketCount; i++)
            {
                if (_bucketSamples[i] < MinWindowSamples) continue;
                if (any) sb.Append(" | ");
                any = true;
                sb.Append("occ ").Append(BucketFloor(i)).Append('-').Append(BucketFloor(i) + BucketWidth - 1)
                  .Append(": mean ").Append((_bucketMsSum[i] / _bucketSamples[i]).ToString("F1"))
                  .Append("ms peak ").Append(_bucketMsMax[i].ToString("F1"))
                  .Append("ms n=").Append(_bucketSamples[i]);
            }
            if (!any) sb.Append("no bucket has ").Append(MinWindowSamples).Append(" samples yet");

            if (SlopeMsPerLoop(out float slope, out int loOcc, out float loMs, out int hiOcc, out float hiMs))
                sb.Append(". MEASURED SLOPE ").Append(slope.ToString("F3")).Append("ms per live loop (")
                  .Append(loOcc).Append(" loops -> ").Append(loMs.ToString("F1")).Append("ms, ")
                  .Append(hiOcc).Append(" loops -> ").Append(hiMs.ToString("F1")).Append("ms)");
            else
                sb.Append(". Slope NOT MEASURABLE yet (needs two populated buckets)");

            float baseline = MeasuredBaselineMs();
            sb.Append(". Baseline at occupancy <= ").Append(BaselineOccupancy).Append(": ")
              .Append(baseline > 0f ? baseline.ToString("F1") + "ms" : "not yet measured");

            var culler = VfxAuraProximityCuller.Instance;
            sb.Append(". Ambient hold ")
              .Append(culler != null ? culler.AmbientGrantedCount.ToString() : "n/a")
              .Append(" of ").Append(VfxAuraProximityCuller.AmbientRegisteredCount)
              .Append(" registered - IF FRAME TIME MOVED AND THIS IS AT ITS RING, ambient dress is ")
              .Append("capped and is NOT the cause; look at enemy auras and portals instead.");

            sb.Append(" Shed level ").Append(Level).Append(" (ambient ring ").Append(AmbientRingNow)
              .Append('/').Append(VfxLoopBudget.AmbientEnvRing).Append(", enemy/pet ring ")
              .Append(AuraRingNow).Append('/').Append(VfxLoopBudget.NearestAuraRing)
              .Append("). Hitches ").Append(HitchCount).Append(" (").Append(DemandWarmHitchCount)
              .Append(" demand-warm). Aura_LowHealth / Aura_NearDeath are never shed.");

            FlowTrace.Step("VfxPerfGate", sb.ToString());
        }
    }
}
