// =============================================================================
// CampPromptVM — the PURE ViewModel behind CampPromptUI (MVVM migration Silo G,
// WO "DungeonHud + Camps + LevelUp").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Projects the claim-prompt + build-menu STATE from the scene-proximity seam
// (ICampProximity) and turns the player's taps into commands. The View reads NO
// game state: it polls Tick() each frame, paints the prompt / menu from the VM's
// flags, positions the prompt via ICampProximity.TryProject, and routes taps back
// as ClaimCurrent() / Build(type).
//
//   * proximity + cleared/claimed reconciliation lives in CampProximityService.
//   * this VM owns the STATE MACHINE: which camp is prompted, whether the build
//     menu is open + on which camp, and the Claim -> open-menu -> Build -> close
//     transitions.
// Unit-testable with a fake ICampProximity / ICampTarget (no scene needed).
// =============================================================================
using System;
using UnityEngine;

namespace DeNelle.Village.World.Camps
{
    /// <summary>Claim-prompt + build-menu ViewModel. Scene-free; driven by an
    /// injected <see cref="ICampProximity"/>.</summary>
    public sealed class CampPromptVM
    {
        private readonly ICampProximity _proximity;

        private ICampTarget _promptTarget;   // nearest claimable camp (prompt shown for it)
        private ICampTarget _menuTarget;     // camp whose build menu is open
        private bool _menuOpen;

        /// <summary>Raised on a state transition (prompt appear/disappear/retarget,
        /// menu open/close). The View also polls per-frame for reposition.</summary>
        public event Action Changed;

        public CampPromptVM(ICampProximity proximity)
        {
            _proximity = proximity;
        }

        // ── Read-only state the View paints ──────────────────────────────────

        /// <summary>True while the build menu owns input.</summary>
        public bool MenuOpen => _menuOpen;

        /// <summary>True when the claim prompt should show (a claimable camp is near
        /// AND the menu is closed).</summary>
        public bool ShowPrompt => !_menuOpen && _promptTarget != null;

        /// <summary>The prompt button copy.</summary>
        public string PromptText => "[ Tap ]  Claim Camp";

        /// <summary>World position the prompt anchors to (View projects it to screen).</summary>
        public Vector3 PromptWorldAnchor => _promptTarget != null ? _promptTarget.WorldAnchor : Vector3.zero;

        // ── Per-frame reconciliation ─────────────────────────────────────────

        /// <summary>Called each frame by the View. While the menu is open it owns
        /// input (no re-target); otherwise refresh refs + find the nearest claimable
        /// camp and project it as the prompt target.</summary>
        public void Tick()
        {
            if (_proximity == null) return;
            if (_menuOpen) return;   // menu owns input until a choice is made
            _proximity.EnsureRefs();
            SetPromptTarget(_proximity.FindClaimable());
        }

        private void SetPromptTarget(ICampTarget t)
        {
            bool changed = !SameTarget(_promptTarget, t);
            _promptTarget = t;
            if (changed) Raise();
        }

        // ── Commands ─────────────────────────────────────────────────────────

        /// <summary>Claim the currently-prompted camp, then open its build menu.</summary>
        public void ClaimCurrent()
        {
            var t = _promptTarget;
            if (t == null) return;
            t.Claim();
            _promptTarget = null;
            _menuTarget = t;
            _menuOpen = true;
            Raise();
        }

        /// <summary>Build the chosen outpost on the menu's camp, then close the menu.</summary>
        public void Build(OutpostType type)
        {
            if (_menuTarget != null) _menuTarget.BuildOutpost(type);
            CloseMenu();
        }

        /// <summary>Dismiss the build menu without building.</summary>
        public void CloseMenu()
        {
            _menuTarget = null;
            _menuOpen = false;
            Raise();
        }

        private static bool SameTarget(ICampTarget a, ICampTarget b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return Equals(a.Key, b.Key);
        }

        private void Raise() => Changed?.Invoke();
    }
}
