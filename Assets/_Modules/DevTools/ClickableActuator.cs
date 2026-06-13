// =============================================================================
// ClickableActuator — DEV-ONLY helper the AutoPilot bot uses to "press every
// button" on whatever interactive surface is currently open, so a playtest run
// exercises real click handlers (and surfaces any that throw) instead of just
// opening panels and walking away.
// -----------------------------------------------------------------------------
// Two surface kinds are actuated:
//   (a) uGUI  — UnityEngine.UI.Button (the legacy HUD / shop buttons).
//   (b) UI Toolkit — UnityEngine.UIElements.Button inside any live UIDocument
//       (the code-built panels: DevPanel, BuildMenu, etc.).
//
// SAFETY: a DENYLIST (by name substring) prevents the bot from clicking
// teardown / destructive controls (Quit / Logout / Reset / Delete /
// Disconnect / Wallet) — those would end the run or corrupt save state. Each
// click is wrapped in try/catch: a throwing handler is reported via
// FlowTrace.Fail("Auto", …) (which the always-on BreakCaptureHarness records to
// break-log.jsonl) and the sweep CONTINUES. Clicks are capped per surface so a
// pathological tree (hundreds of buttons) can't stall the run.
//
// RELEASE-SAFE: the whole file is #if DEVELOPMENT_BUILD || UNITY_EDITOR — it
// compiles to nothing in a shipped player build, and the DeNelle.DevTools asmdef
// carries the matching define constraint.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;
using UGuiButton = UnityEngine.UI.Button;
using UiToolkitButton = UnityEngine.UIElements.Button;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY static helper that "clicks" every interactable button on the
    /// currently visible surfaces (uGUI + UI Toolkit), skipping a small denylist
    /// of destructive controls, capping clicks per surface, and reporting any
    /// throwing handler via FlowTrace without aborting the sweep.
    /// </summary>
    public static class ClickableActuator
    {
        /// <summary>Max buttons actuated per surface so a huge tree can't stall the run.</summary>
        public const int MaxClicksPerSurface = 30;

        // Name fragments (case-insensitive) the bot must NEVER click — they would
        // tear down its own run or mutate persistent state.
        private static readonly string[] Denylist =
        {
            "quit", "logout", "log out", "reset", "delete", "disconnect", "wallet",
        };

        private static bool IsDenied(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            foreach (var bad in Denylist)
                if (lower.Contains(bad)) return true;
            return false;
        }

        /// <summary>
        /// Actuate every safe, interactable button on the live surfaces. When
        /// <paramref name="uiToolkitRoot"/> is supplied, only that UI Toolkit
        /// subtree is swept for UITK buttons (uGUI is always swept globally);
        /// pass null to sweep every UIDocument in the scene. When <paramref name="rng"/>
        /// is supplied (fleet mode), the button lists are shuffled with it so
        /// different bots click in different orders. Returns the number of buttons
        /// clicked.
        /// </summary>
        public static int ActuateAll(VisualElement uiToolkitRoot = null, System.Random rng = null)
        {
            int clicked = 0;
            clicked += ActuateUGui(rng);
            clicked += ActuateUiToolkit(uiToolkitRoot, rng);
            FlowTrace.Step("Auto", $"ClickableActuator: actuated {clicked} clickable(s).");
            return clicked;
        }

        // Fisher-Yates in-place shuffle (seeded). No-op when rng is null so the
        // default single-run order is preserved.
        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            if (rng == null || list == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        // ── uGUI (UnityEngine.UI.Button) ─────────────────────────────────────
        private static int ActuateUGui(System.Random rng = null)
        {
            int clicked = 0;
            UGuiButton[] buttons;
            try
            {
                // FindObjectsOfTypeAll catches buttons whose canvas just turned on
                // this frame; we still filter to active + interactable below.
                buttons = Resources.FindObjectsOfTypeAll<UGuiButton>();
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Auto", "ClickableActuator: uGUI scan failed — " + ex.Message);
                return 0;
            }

            if (buttons == null) return 0;
            Shuffle(buttons, rng);   // seeded click order (no-op when rng null)
            foreach (var b in buttons)
            {
                if (clicked >= MaxClicksPerSurface) break;
                if (b == null) continue;
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                // Skip prefab assets / hidden editor objects (not in a live scene).
                if (!b.gameObject.scene.IsValid()) continue;
                if (IsDenied(b.name)) continue;

                FlowTrace.Step("Auto", $"ClickableActuator: uGUI click '{b.name}'.");
                try { b.onClick?.Invoke(); clicked++; }
                catch (Exception ex)
                {
                    FlowTrace.Fail("Auto", $"uGUI button '{b.name}' handler threw: {ex.Message}");
                }
            }
            return clicked;
        }

        // ── UI Toolkit (UnityEngine.UIElements.Button) ───────────────────────
        private static int ActuateUiToolkit(VisualElement explicitRoot, System.Random rng = null)
        {
            int clicked = 0;

            var roots = new List<VisualElement>();
            if (explicitRoot != null)
            {
                roots.Add(explicitRoot);
            }
            else
            {
                UIDocument[] docs;
                try
                {
                    docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                        FindObjectsSortMode.None);
                }
                catch (Exception ex)
                {
                    FlowTrace.Warn("Auto", "ClickableActuator: UIDocument scan failed — " + ex.Message);
                    docs = null;
                }
                if (docs != null)
                    foreach (var d in docs)
                        if (d != null && d.rootVisualElement != null)
                            roots.Add(d.rootVisualElement);
            }

            foreach (var root in roots)
            {
                if (clicked >= MaxClicksPerSurface) break;
                List<UiToolkitButton> uiButtons;
                try
                {
                    uiButtons = root.Query<UiToolkitButton>().ToList();
                }
                catch (Exception ex)
                {
                    FlowTrace.Warn("Auto", "ClickableActuator: UITK query failed — " + ex.Message);
                    continue;
                }

                Shuffle(uiButtons, rng);   // seeded click order (no-op when rng null)
                foreach (var b in uiButtons)
                {
                    if (clicked >= MaxClicksPerSurface) break;
                    if (b == null) continue;
                    // Only visible, enabled buttons (don't fire hidden ones).
                    if (b.resolvedStyle.display == DisplayStyle.None) continue;
                    if (!b.enabledInHierarchy) continue;
                    string label = !string.IsNullOrEmpty(b.name) ? b.name : b.text;
                    if (IsDenied(label) || IsDenied(b.text)) continue;

                    FlowTrace.Step("Auto", $"ClickableActuator: UITK click '{label}'.");
                    try
                    {
                        // Synthesize the pointer click on the button. SendEvent assigns
                        // the event target to the receiving element, so we don't touch
                        // the (version-sensitive) target setter. A ClickEvent dispatched
                        // at the button runs its Clickable manipulator -> the registered
                        // clicked handlers, which is the panel's real action.
                        Vector2 c = b.worldBound.center;
                        using (var down = MouseDownEvent.GetPooled(c, 0, 1, Vector2.zero))
                            b.SendEvent(down);
                        using (var up = MouseUpEvent.GetPooled(c, 0, 1, Vector2.zero))
                            b.SendEvent(up);
                        using (var click = ClickEvent.GetPooled())
                            b.SendEvent(click);
                        clicked++;
                    }
                    catch (Exception ex)
                    {
                        FlowTrace.Fail("Auto", $"UITK button '{label}' handler threw: {ex.Message}");
                    }
                }
            }
            return clicked;
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
