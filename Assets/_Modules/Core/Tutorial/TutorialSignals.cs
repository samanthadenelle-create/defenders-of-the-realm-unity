// =============================================================================
// TutorialSignals — the Tutorial V2 completion-signal bus (WO-T1, spec §2.1b).
// -----------------------------------------------------------------------------
// A thin adapter that maps events the game ALREADY emits to stable string ids
// ("build.tower_placed", "wave.cleared", "dialogue.ended:<id>", ...). The
// TutorialFlow interpreter awaits these ids; gameplay-side adapters
// (DeNelle.Village.TutorialSignalAdapters) subscribe the real C#/Unity events
// and Raise() here. Core-side sources (DialogueService, PanelRouter) are wired
// by TutorialCoreSignalAdapter below.
//
// Modeled on the proven DialogueEventBus (Core/Events): pure static, latching,
// case-insensitive, main-thread only. Latching matters — a completion signal
// that fires one frame before the interpreter arms its await must still count,
// so the interpreter Clear()s the id when it STARTS waiting and then accepts
// either the latch or a fresh raise.
//
// Every raise writes FlowTrace.Step("Tutorial", ...) — ONE instrumentation seam
// for humans, headless bots, and telemetry (spec §2.1b).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Tutorial
{
    /// <summary>
    /// Process-wide signal bus for Tutorial V2 step triggers/completions.
    /// Gameplay adapters <see cref="Raise"/> stable ids; the interpreter awaits
    /// them via <see cref="Raised"/> + the <see cref="HasFired"/> latch.
    /// </summary>
    public static class TutorialSignals
    {
        // ── Canonical signal ids (spec §2.1b) — keep in sync with tutorial-steps.json ──
        public const string BuildModeEntered = "build.mode_entered";
        public const string TowerPlaced      = "build.tower_placed";
        public const string WaveCleared      = "wave.cleared";
        public const string ArenaWin         = "arena.resolved:win";
        public const string ArenaLoss        = "arena.resolved:loss";
        public const string DialogueEndedPrefix = "dialogue.ended:";   // + dialogue id
        public const string HeroReachedPrefix   = "hero.reached:";     // + anchor id
        public const string PanelOpenedPrefix   = "panel.opened:";     // + PanelId
        // Contextual triggers (spec CREATIVE SCOPE) — sources noted per adapter.
        public const string CanAffordUpgrade = "economy.can_afford_upgrade";
        public const string EchoBornSecond   = "echo.born:2";
        public const string FirstGearAdded   = "inventory.gear_added:first";
        public const string FirstSkillPoint  = "skillpoint.earned:first";

        private static readonly HashSet<string> _fired =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised whenever a signal fires, with the signal id.</summary>
        public static event Action<string> Raised;

        /// <summary>Raise a named tutorial signal. No-op on null/empty. Never throws.</summary>
        public static void Raise(string signalId)
        {
            if (string.IsNullOrEmpty(signalId)) return;
            _fired.Add(signalId);
            FlowTrace.Step("Tutorial", $"signal '{signalId}' raised.");
            try { Raised?.Invoke(signalId); }
            catch (Exception ex)
            {
                // No silent failures (§12) — a throwing subscriber self-reports but
                // never breaks the raiser (gameplay must not fault on tutorial wiring).
                FlowTrace.Fail("Tutorial", $"signal '{signalId}' subscriber threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>True if <paramref name="signalId"/> has fired since the last Clear.</summary>
        public static bool HasFired(string signalId) =>
            !string.IsNullOrEmpty(signalId) && _fired.Contains(signalId);

        /// <summary>Clear one signal's latch — the interpreter calls this when it begins waiting.</summary>
        public static void Clear(string signalId)
        {
            if (!string.IsNullOrEmpty(signalId)) _fired.Remove(signalId);
        }

        /// <summary>Clear every latched signal (fresh tutorial run / New Game).</summary>
        public static void ClearAll() => _fired.Clear();
    }

    /// <summary>
    /// Wires the CORE-side signal sources (WO-T1): DialogueService end-of-dialogue
    /// (by id) and PanelRouter opens. Village-side sources (waves, towers, arena,
    /// economy) live in DeNelle.Village.TutorialSignalAdapters — Core never
    /// references gameplay. Registered once per process; the subscriptions are
    /// inert no-ops while ff.tutorialv2 content isn't running (raising into an
    /// un-awaited bus costs a hash-set add).
    /// </summary>
    internal static class TutorialCoreSignalAdapter
    {
        private static bool _wired;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Wire()
        {
            if (_wired) return;
            _wired = true;
            // dialogue.ended:<id> ← DialogueService.EndedWithId (DialogueService.cs).
            Dialogue.DialogueService.EndedWithId += id =>
                TutorialSignals.Raise(TutorialSignals.DialogueEndedPrefix + id);
            // panel.opened:<PanelId> ← PanelRouter.PanelOpened (PanelRouter.cs).
            UI.PanelRouter.PanelOpened += id =>
                TutorialSignals.Raise(TutorialSignals.PanelOpenedPrefix + id);
        }
    }
}
