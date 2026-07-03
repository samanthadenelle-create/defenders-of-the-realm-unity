// =============================================================================
// PrivacySensitiveUi — WO-596: identity-privacy registry for report captures.
// -----------------------------------------------------------------------------
// The player bug-report form screenshots the CLEAN frame (BreakCaptureHarness.
// CaptureForReport). Owner directive 2026-07-02: identity is covered on submit —
// any UI that displays a player identity (the Pi sign-in button / username
// readout, chat panels, anything showing a player name) must NOT appear in that
// screenshot. Mechanism: identity-displaying widgets OPT IN here; the capture
// toggles the registered objects OFF for the capture frame and restores them
// after. Hidden at the source — no image post-processing, can't miss.
//
// USAGE (widget side, one line at build time):
//   PrivacySensitiveUi.Register(myIdentityBearingRoot);
//
// USAGE (capture side):
//   using (PrivacySensitiveUi.HideForCapture()) { ...screenshot the frame... }
//
// Registrations are pruned automatically when objects die (scene unload) — no
// Unregister call is required, though one exists for widgets that want it.
//
// TODO (WO-596, owned elsewhere — do NOT edit PiSignInController from this WO):
//   PiSignInController.BuildButton() must register its canvas root:
//     PrivacySensitiveUi.Register(canvasGo);   // after the PiSignInCanvas GameObject is created
//   Same one-liner applies to ClanChatPanel and any future username readout.
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// WO-596 — registry of identity-bearing UI roots that must be hidden for the
    /// one capture frame of a player bug report. Widgets opt in via
    /// <see cref="Register"/>; captures wrap the frame in <see cref="HideForCapture"/>.
    /// Every path is guarded — privacy plumbing must never break the game.
    /// </summary>
    public static class PrivacySensitiveUi
    {
        private static readonly List<GameObject> s_registered = new List<GameObject>();

        /// <summary>Opt an identity-bearing UI root into capture hiding. Safe to call
        /// repeatedly; nulls are ignored; dead entries are pruned automatically.</summary>
        public static void Register(GameObject go)
        {
            if (go == null) return;
            Prune();
            if (!s_registered.Contains(go))
            {
                s_registered.Add(go);
                FlowTrace.Step("BugReport", $"PrivacySensitiveUi registered '{go.name}' ({s_registered.Count} total)");
            }
        }

        /// <summary>Remove a previously registered root (optional — death auto-prunes).</summary>
        public static void Unregister(GameObject go)
        {
            if (go == null) return;
            s_registered.Remove(go);
        }

        /// <summary>Registered live roots right now (diagnostic).</summary>
        public static int Count { get { Prune(); return s_registered.Count; } }

        /// <summary>
        /// Hide every registered (and currently active) root for a capture frame.
        /// Returns a scope that restores exactly the objects it deactivated on Dispose.
        /// Never throws — a privacy hide failure logs and the capture proceeds.
        /// </summary>
        public static IDisposable HideForCapture()
        {
            var scope = new HideScope();
            Guard.Try("BugReport", "privacy hide", () =>
            {
                Prune();
                foreach (var go in s_registered)
                {
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                        scope.Hidden.Add(go);
                    }
                }
                FlowTrace.Step("BugReport", $"privacy: hid {scope.Hidden.Count} identity-bearing root(s) for capture frame");
            });
            return scope;
        }

        private static void Prune()
        {
            for (int i = s_registered.Count - 1; i >= 0; i--)
                if (s_registered[i] == null) s_registered.RemoveAt(i);
        }

        private sealed class HideScope : IDisposable
        {
            public readonly List<GameObject> Hidden = new List<GameObject>();
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Guard.Try("BugReport", "privacy restore", () =>
                {
                    foreach (var go in Hidden)
                        if (go != null) go.SetActive(true);
                });
            }
        }
    }
}
