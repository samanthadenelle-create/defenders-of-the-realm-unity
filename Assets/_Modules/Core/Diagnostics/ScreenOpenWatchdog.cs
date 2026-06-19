// =============================================================================
// ScreenOpenWatchdog — names every in-game screen/panel that pops, in the trace.
// -----------------------------------------------------------------------------
// Owner directive (§12): we do NOT guess at bugs — we instrument the flow and let
// the data tell us. The owner's playtest must never have a key load a screen or
// spawn something; the keyboard hotkeys that did that are now editor-gated, but
// this watchdog is the belt-and-braces CAPTURE: whenever ANY registered modal
// panel becomes active it emits a [Flow:ScreenOpen] line with the panel's name,
// so a stray open shows up by name in the F8 break-capture (break-log.jsonl /
// Player.log).
//
// HOW IT HOOKS: PanelManager (DeNelle.Core.UI) is the single modal arbiter —
// every in-game panel (Cosmetic Shop, Hero Talents, Building Upgrade, Crafting,
// DevConsole, AdminOverlay, Battle HUD, Pause, …) routes its open through
// PanelManager.NotifyOpened and the manager raises OpenStateChanged on every
// open/close/swap. We subscribe to that one event — no per-panel wiring, no
// reflection, no scene object — and read PanelManager.OpenPanelName.
//
// STRAY-HOTKEY HEURISTIC: a panel that opens on a frame where a NON-pointer key
// went down AND no pointer (mouse/touch) was pressed is *likely* a keyboard
// hotkey rather than an on-screen button tap, so we additionally emit a
// FlowTrace.Fail — which rolls up to the capture as a bug. This is a cheap hint,
// not proof (UI Toolkit button clicks are pointer-driven, so a real button tap
// has a pointer down and is NOT flagged). When the signal is ambiguous we still
// always emit the Step line, which is the load-bearing record.
//
// LIGHTWEIGHT + DARK: a single [RuntimeInitializeOnLoadMethod] subscriber. When
// FlowTrace.Enabled is false every emit short-circuits inside FlowTrace, and the
// per-frame input sampling is skipped entirely, so the off path is near-zero cost.
// =============================================================================

using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Core.Diagnostics
{
    /// <summary>
    /// Subscribes to <see cref="PanelManager.OpenStateChanged"/> and emits a
    /// <c>[Flow:ScreenOpen]</c> line naming every panel that becomes active, so any
    /// screen that pops during a playtest is captured by name. Flags an open that
    /// looks key-driven (a non-pointer key down with no pointer press that frame) as
    /// a possible stray hotkey via <see cref="FlowTrace.Fail"/>.
    /// </summary>
    public static class ScreenOpenWatchdog
    {
        // The panel name we last saw as open. Lets us emit only on the OPEN edge
        // (open / swap), never on close, and never repeatedly for the same panel.
        private static string s_lastOpenName;

        // Per-frame input snapshot, refreshed by a tiny driver MonoBehaviour. Used
        // only for the "looks like a hotkey" heuristic; both default false so the
        // watchdog never false-flags before the driver is alive.
        private static bool s_keyDownThisFrame;
        private static bool s_pointerDownThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Idempotent: unsubscribe-then-subscribe so a domain reload (or a second
            // call) never double-hooks.
            PanelManager.OpenStateChanged -= OnPanelStateChanged;
            PanelManager.OpenStateChanged += OnPanelStateChanged;
            s_lastOpenName = PanelManager.OpenPanelName;

            // Spin up the per-frame input sampler (DDOL) so the stray-hotkey
            // heuristic has fresh data. Harmless and cheap; one component, no UI.
            if (InputSampler.Instance == null)
            {
                var go = new GameObject("ScreenOpenWatchdog");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<InputSampler>();
            }
        }

        /// <summary>
        /// PanelManager raises this on every open / close / swap. We act only on the
        /// OPEN edge (a panel name is now present and differs from the last one).
        /// </summary>
        private static void OnPanelStateChanged()
        {
            // Dark when tracing is off — no work, no alloc.
            if (!FlowTrace.Enabled) { s_lastOpenName = PanelManager.OpenPanelName; return; }

            string now = PanelManager.OpenPanelName;
            if (string.IsNullOrEmpty(now))
            {
                // A close (or swap-to-none). Nothing opened; just remember the state.
                s_lastOpenName = null;
                return;
            }

            // Only emit when the open panel actually changed to a new one (open or swap).
            if (now == s_lastOpenName) return;
            s_lastOpenName = now;

            FlowTrace.Step("ScreenOpen", $"panel '{now}' opened");

            // Heuristic: a key went down this frame and no pointer was pressed — the
            // open looks keyboard-driven rather than an on-screen UI-button tap.
            // (UI Toolkit button clicks are pointer events, so a real tap has a
            // pointer down and is NOT flagged.) Cheap hint, not proof.
            if (s_keyDownThisFrame && !s_pointerDownThisFrame)
            {
                FlowTrace.Fail("ScreenOpen",
                    $"panel '{now}' opened with no UI trigger — possible stray hotkey");
            }
        }

        /// <summary>
        /// Tiny per-frame driver: samples whether a non-pointer key and/or a pointer
        /// (mouse / touch) went down this frame, so the watchdog can tell a likely
        /// keyboard-driven open from an on-screen button tap. Dark when tracing off.
        /// </summary>
        private sealed class InputSampler : MonoBehaviour
        {
            public static InputSampler Instance { get; private set; }

            private void Awake()
            {
                if (Instance != null && Instance != this) { Destroy(gameObject); return; }
                Instance = this;
            }

            private void OnDestroy()
            {
                if (Instance == this) Instance = null;
            }

            private void Update()
            {
                if (!FlowTrace.Enabled)
                {
                    // Keep the snapshot clear when tracing is off so a later enable
                    // never reads stale "a key was down" state.
                    s_keyDownThisFrame = false;
                    s_pointerDownThisFrame = false;
                    return;
                }

                // Input.anyKeyDown is true for keys AND mouse buttons; separate the
                // pointer presses out so a key-only frame can be distinguished from a
                // tap. Touch counts as a pointer too.
                bool mouseDown =
                    Input.GetMouseButtonDown(0) ||
                    Input.GetMouseButtonDown(1) ||
                    Input.GetMouseButtonDown(2);

                bool touchDown = Input.touchCount > 0;

                s_pointerDownThisFrame = mouseDown || touchDown;

                // A non-pointer key down = anyKeyDown this frame that wasn't a mouse
                // press. (Touch does not set anyKeyDown.)
                s_keyDownThisFrame = Input.anyKeyDown && !mouseDown;
            }
        }
    }
}
