// =============================================================================
// PanelManager — the single owner of "which in-game modal panel is open"
// (DEF-212). One panel at a time: opening any registered panel closes the one
// that was open before it.
// -----------------------------------------------------------------------------
// THE PROBLEM (DEF-212): the in-game panels (Cosmetic Shop, Hero Talents,
// Building Upgrade, Village Crafting) each owned their own visibility and knew
// nothing about each other, so pressing C then T left BOTH the shop and the
// talents stacked on screen. There was no modal arbiter.
//
// THE FIX: a tiny, self-bootstrapping, code-built singleton that lives in
// DeNelle.Core so EVERY gameplay assembly (DeNelle.HUD and DeNelle.Village both
// reference Core) can route through it WITHOUT a cross-assembly reference and
// WITHOUT reflection. Panels register a lightweight handle (a close action + an
// is-open probe). When a panel opens it calls NotifyOpened(handle); the manager
// closes whatever handle was open before and records the new one. When a panel
// closes it calls NotifyClosed(handle) to clear the record.
//
// Consumers (e.g. MobileInteractButton) read PanelManager.AnyOpen to suppress
// world prompts while a modal owns the screen.
//
// No MonoBehaviour / no scene object is required: this is pure static state,
// reset on domain reload like any static. That keeps it alive across additive
// scene loads (Village + merged world) without a DDOL host to manage.
// =============================================================================

using System;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// A panel's registration with <see cref="PanelManager"/>. Holds the minimal
    /// hooks the manager needs to enforce "one panel at a time": a way to close the
    /// panel and a way to ask whether it is currently open. Panels keep one cached
    /// handle for their lifetime and pass it to NotifyOpened / NotifyClosed.
    /// </summary>
    public sealed class PanelHandle
    {
        internal readonly Action Close;
        internal readonly Func<bool> IsOpen;
        public readonly string Name;

        /// <summary>WO-437: panels that may open DURING an active battle (Battle HUD,
        /// Pause). All other (gameplay) panels are rejected by the battle-lock gate.</summary>
        internal readonly bool BattleAllowed;

        internal PanelHandle(string name, Action close, Func<bool> isOpen, bool battleAllowed)
        {
            Name = name ?? "Panel";
            Close = close;
            IsOpen = isOpen;
            BattleAllowed = battleAllowed;
        }
    }

    /// <summary>
    /// Static modal arbiter. Guarantees at most one registered panel is open at a
    /// time and exposes <see cref="AnyOpen"/> for prompt suppression. Self-contained
    /// (no scene object); safe to call from any assembly that references DeNelle.Core.
    /// </summary>
    public static class PanelManager
    {
        private static PanelHandle _open;

        /// <summary>Raised whenever the open panel changes (opened, closed, or swapped).
        /// Listeners (e.g. an interaction prompt) can refresh their suppressed state.</summary>
        public static event Action OpenStateChanged;

        /// <summary>True while some registered panel is currently open.</summary>
        public static bool AnyOpen => _open != null;

        /// <summary>The display name of the currently open panel, or null when none is open.</summary>
        public static string OpenPanelName => _open != null ? _open.Name : null;

        /// <summary>
        /// Create a handle for a panel. <paramref name="close"/> hides the panel;
        /// <paramref name="isOpen"/> reports its current visibility. Both are invoked
        /// by the manager, so they must be null-safe on the caller's side.
        /// </summary>
        public static PanelHandle Register(string name, Action close, Func<bool> isOpen)
        {
            return new PanelHandle(name, close, isOpen, battleAllowed: false);
        }

        /// <summary>
        /// WO-437: register a panel that is ALLOWED to open during an active battle
        /// (Battle HUD, Pause). Every other panel uses the plain
        /// <see cref="Register(string, Action, Func{bool})"/> and is rejected by the
        /// battle-lock gate in <see cref="NotifyOpened"/> while a battle is in progress.
        /// </summary>
        public static PanelHandle RegisterBattleAllowed(string name, Action close, Func<bool> isOpen)
        {
            return new PanelHandle(name, close, isOpen, battleAllowed: true);
        }

        /// <summary>
        /// Announce that <paramref name="handle"/> just opened. Closes the previously
        /// open panel (if any and different) so only one panel is ever visible. No-op
        /// if the same handle is already the open one.
        /// </summary>
        public static bool NotifyOpened(PanelHandle handle)
        {
            if (handle == null) return false;
            if (ReferenceEquals(_open, handle)) return true;

            // WO-437 battle-lock: during an active battle (ATB combat / Arena raid)
            // only battle-allowed panels (Battle HUD, Pause) may open. Every gameplay
            // panel is REJECTED here — the one choke point covers all arbiter panels.
            // The caller should close itself when this returns false.
            if (!handle.BattleAllowed && BattleLock.IsInBattle())
            {
                FlowTrace.Warn("Input", "battle-lock: rejected open of '" + handle.Name + "' (in battle)");
                var blocked = handle;
                try { blocked.Close?.Invoke(); }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        "[PanelManager] battle-lock close of '" + blocked.Name + "' threw: " + ex.Message);
                }
                return false;
            }

            var previous = _open;
            _open = handle; // set first so a re-entrant probe sees the new owner

            if (previous != null)
            {
                try { previous.Close?.Invoke(); }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        "[PanelManager] closing '" + previous.Name + "' threw: " + ex.Message);
                }
            }

            OpenStateChanged?.Invoke();

            // VISIBILITY VERIFY (WO-465 invisible-scrim class): we have RECORDED this panel as
            // the open one, but recording != rendered. The handle already carries an IsOpen probe
            // the manager never used here — ask it now. If the panel that just "opened" reports it
            // is NOT actually open/visible, that is the bug (a blank panel masquerading as open:
            // the owner's blocked Done-button + empty-store symptoms). Self-report via FlowTrace.Fail
            // so a run pinpoints the dead panel instead of the owner discovering an invisible scrim.
            // The probe is the CALLER's code — guard it so a throwing probe never breaks the arbiter.
            if (handle.IsOpen != null)
            {
                bool reportedOpen = false;
                bool probeRan = Guard.Try("UI", "PanelManager.NotifyOpened isOpen-verify '" + handle.Name + "'",
                    () => { reportedOpen = handle.IsOpen(); });
                if (probeRan && !reportedOpen)
                {
                    FlowTrace.Fail("UI",
                        "PanelManager: '" + handle.Name + "' recorded as OPEN but its IsOpen probe reports NOT open " +
                        "— blank/failed panel masquerading as open (WO-465 invisible-scrim class).");
                }
                else if (probeRan)
                {
                    FlowTrace.Step("UI",
                        "PanelManager: '" + handle.Name + "' opened and verified visible (IsOpen=true).");
                }
            }

            return true;
        }

        /// <summary>
        /// Announce that <paramref name="handle"/> just closed. Clears the record only
        /// if this handle is the one currently held (a stale close from a panel that was
        /// already swapped out is ignored).
        /// </summary>
        public static void NotifyClosed(PanelHandle handle)
        {
            if (handle == null) return;
            if (!ReferenceEquals(_open, handle)) return;
            _open = null;
            OpenStateChanged?.Invoke();
        }

        /// <summary>
        /// WO-611: close EVERY open registered panel so a screen can take sole ownership (the
        /// combat HUD on the hostile posture flip). Today the arbiter enforces at-most-one-open, so
        /// this closes that one; the bounded loop guards against any future multi-open without ever
        /// spinning. Safe to call when nothing is open (no-op).
        /// </summary>
        public static void CloseAll()
        {
            int guard = 0;
            while (_open != null && guard++ < 32)
                CloseOpen();
        }

        /// <summary>
        /// Close whatever panel is currently open (if any). Useful for a global "back"
        /// / ESC handler. Safe to call when nothing is open.
        /// </summary>
        public static void CloseOpen()
        {
            var open = _open;
            if (open == null) return;
            _open = null;
            try { open.Close?.Invoke(); }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[PanelManager] CloseOpen '" + open.Name + "' threw: " + ex.Message);
            }
            OpenStateChanged?.Invoke();
        }
    }
}
