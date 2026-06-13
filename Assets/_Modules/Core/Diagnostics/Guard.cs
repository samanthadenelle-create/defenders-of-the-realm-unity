using System;
using UnityEngine;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// Error factory — drop-in safe-execution wrapper. Owner directive 2026-06-13:
    /// "there should be an error factory you can implement anywhere easily." Instead of
    /// hand-writing try/catch at every risky call site (catalog parse, row build, service
    /// lookup, UI construction), wrap the call in Guard.Try(...). Any exception is CAUGHT,
    /// logged via FlowTrace (so it lands in the F8 break-log with a [Flow:&lt;system&gt;] tag and
    /// the call's name), and SWALLOWED so one bad object can't blank a whole screen or kill a
    /// frame — the silent-failure / "shows nothing, no error" class of bug becomes a logged line.
    ///
    /// USAGE:
    ///   Guard.Try("Store", "build weapon row", () => CreateBuyRow(...));         // void, returns bool ok
    ///   var def = Guard.Try("Store", "parse weapon", () => Parse(json), fallback: null); // returns T
    ///   if (!Guard.Try("Seam", "warp hero", () => loco.WarpTo(p))) { /* recover */ }
    ///
    /// Use real try/catch only when you must branch on the specific exception or do cleanup;
    /// for "run this, and if it throws just log it and carry on", this is the one-liner.
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Run <paramref name="action"/> inside try/catch. Returns true if it completed,
        /// false if it threw (the exception is logged via FlowTrace.Fail and swallowed).
        /// </summary>
        public static bool Try(string system, string what, Action action)
        {
            if (action == null) return false;
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                Report(system, what, ex);
                return false;
            }
        }

        /// <summary>
        /// Run <paramref name="func"/> inside try/catch. Returns its result, or
        /// <paramref name="fallback"/> if it threw (the exception is logged + swallowed).
        /// </summary>
        public static T Try<T>(string system, string what, Func<T> func, T fallback = default)
        {
            if (func == null) return fallback;
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Report(system, what, ex);
                return fallback;
            }
        }

        /// <summary>
        /// Run a per-item action over a batch, guarding EACH item independently so one bad
        /// item is logged + skipped instead of aborting the rest. Returns (built, failed).
        /// Ideal for list/row population — the exact "store shows nothing because item #3 threw" case.
        /// </summary>
        public static (int built, int failed) TryEach<T>(string system, string what,
            System.Collections.Generic.IEnumerable<T> items, Action<T> perItem)
        {
            int built = 0, failed = 0;
            if (items == null || perItem == null) return (built, failed);
            foreach (var item in items)
            {
                try { perItem(item); built++; }
                catch (Exception ex) { failed++; Report(system, $"{what} [{item}]", ex); }
            }
            return (built, failed);
        }

        private static void Report(string system, string what, Exception ex)
        {
            // Log error-level DIRECTLY (not via FlowTrace.Fail) so the safety net survives even
            // when FlowTrace is later compile-stripped on a ship build (see INSTRUMENTATION_
            // STANDARD §1.3/§1.6): Guard changes control flow and must always record. Same
            // [Flow:<system>] tag for grep + BreakCaptureHarness capture (Debug.LogError ->
            // break-log.jsonl). The full stack still prints to the Unity log.
            Debug.LogError($"[Flow:{system}] {what} FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
