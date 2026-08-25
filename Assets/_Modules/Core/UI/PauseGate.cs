// =============================================================================
// PauseGate — the Core-level "back / pause" seam (keyboard-removal sweep).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE PROBLEM: the Escape key was the SOLE trigger for BOTH "close the open modal"
// (PanelManager.CloseOpen) AND "toggle pause" (PauseController.TogglePause). On a
// phone there is no Escape key, so removing it without a replacement would orphan
// both actions (§12 — no silent breakage). The on-screen HUD PAUSE/BACK button must
// drive the exact same decision Escape did.
//
// THE SEAM: the HUD (DeNelle.HUD) and the pause overlay (DeNelle.Settings) cannot
// reference each other — both reference DeNelle.Core only. So the back/pause request
// routes through this tiny static gate in Core (the same cross-assembly pattern
// PanelManager uses):
//   • The HUD's PAUSE/BACK button calls RequestBack().
//   • RequestBack() applies the back-or-pause rule directly for the half it owns
//     (modal-close, via PanelManager which lives here in Core) and raises
//     PauseToggleRequested for the half it does not (the actual Time.timeScale pause,
//     owned by PauseController in DeNelle.Settings, which subscribes to this event).
//
// This is behaviour-identical to the removed Escape block in PauseController.Update():
//     if (!paused && PanelManager.AnyOpen) PanelManager.CloseOpen();
//     else                                 TogglePause();
// — except the "is paused?" check is deferred to the subscriber so the gate stays
// state-free. When a modal is open we close it and do NOT also toggle pause (no
// double-fire), exactly as before.
//
// Pure static state (reset on domain reload). No MonoBehaviour / scene object — alive
// across additive scene loads, same as PanelManager.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static "back / pause" arbiter. The HUD's on-screen PAUSE/BACK button calls
    /// <see cref="RequestBack"/>; an open modal is closed (via <see cref="PanelManager"/>)
    /// or, when none is open, <see cref="PauseToggleRequested"/> is raised for the pause
    /// overlay (PauseController) to toggle. Replaces the removed Escape-key trigger.
    /// </summary>
    public static class PauseGate
    {
        private static int s_externalPresentationDepth;

        /// <summary>True while native full-screen UI (for example a rewarded ad) owns the
        /// foreground. The caller's registered panel remains the navigation authority beneath it.</summary>
        public static bool ExternalPresentationActive => s_externalPresentationDepth > 0;

        /// <summary>
        /// Raised when a back/pause request arrives and NO modal is open — i.e. the
        /// request should toggle the pause overlay. PauseController subscribes to this
        /// and calls its TogglePause(). Kept event-based (not a direct call) so Core
        /// never references DeNelle.Settings.
        /// </summary>
        public static event Action PauseToggleRequested;

        /// <summary>
        /// Prevent OS background callbacks caused by native full-screen presentation from opening
        /// Pause over (and therefore closing) the panel that invoked it. This scope owns no route
        /// and reopens nothing: the existing <see cref="PanelManager"/> caller remains untouched.
        /// </summary>
        public static IDisposable BeginExternalPresentation(string presentation)
        {
            s_externalPresentationDepth++;
            string caller = PanelManager.OpenPanelName ?? "<gameplay>";
            FlowTrace.Step("UI", "external presentation BEGIN kind=" +
                (string.IsNullOrEmpty(presentation) ? "<unknown>" : presentation) +
                " caller=" + caller + " depth=" + s_externalPresentationDepth);
            return new ExternalPresentationLease(presentation, caller);
        }

        /// <summary>Pure decision used by PauseController and the return-path regression.</summary>
        public static bool ShouldAutoPause(bool isBackgrounded, bool alreadyPaused) =>
            isBackgrounded && !alreadyPaused && !ExternalPresentationActive;

        /// <summary>
        /// The single on-screen "back / pause" action (the HUD PAUSE/BACK button).
        /// If a registered modal panel is open, close it (and do NOT also toggle pause).
        /// Otherwise raise <see cref="PauseToggleRequested"/> so the pause overlay toggles.
        /// Behaviour-identical to the retired Escape handler. Null-safe; safe to call
        /// from any assembly that references DeNelle.Core.
        /// </summary>
        public static void RequestBack()
        {
            if (PanelManager.AnyOpen)
            {
                PanelManager.CloseOpen();
                return;
            }
            PauseToggleRequested?.Invoke();
        }

        private sealed class ExternalPresentationLease : IDisposable
        {
            private readonly string _presentation;
            private readonly string _caller;
            private bool _disposed;

            public ExternalPresentationLease(string presentation, string caller)
            {
                _presentation = presentation;
                _caller = caller;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (s_externalPresentationDepth > 0) s_externalPresentationDepth--;
                string current = PanelManager.OpenPanelName ?? "<gameplay>";
                FlowTrace.Step("UI", "external presentation END kind=" +
                    (string.IsNullOrEmpty(_presentation) ? "<unknown>" : _presentation) +
                    " caller=" + _caller + " current=" + current +
                    " depth=" + s_externalPresentationDepth);
            }
        }
    }
}
