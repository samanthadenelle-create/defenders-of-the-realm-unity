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
using UGuiGraphic = UnityEngine.UI.Graphic;

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

        // Greppable fleet-ticket prefix for buttons a real player COULD NOT click
        // because something pickable covers them. The autopilot fires handlers
        // directly, which would BYPASS such a cover (the "cannot build defense"
        // scrim that passed the fleet 3x) — so we headless-detect occlusion and
        // refuse to click blocked buttons, logging them as a Fail instead.
        private const string ClickBlockedTag = "CLICK-BLOCKED";

        // Names of buttons already reported blocked THIS run, so the same covered
        // button logs exactly once per ActuateAll pass (deduped fleet ticket).
        private static readonly HashSet<string> _reportedBlocked = new HashSet<string>();

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
            _reportedBlocked.Clear();   // fresh blocked-dedupe slate per run
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

                // REACHABILITY: a real player's click lands on whatever pickable UI
                // is topmost at the button's center. If something covers it, firing
                // onClick directly would be a false pass — refuse + report instead.
                string blocker = FindUGuiBlocker(b, buttons);
                if (blocker != null)
                {
                    ReportBlocked(b.name, blocker);
                    continue;
                }

                FlowTrace.Step("Auto", $"ClickableActuator: uGUI click '{b.name}' (reachable).");
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

                    // REACHABILITY: panel.Pick at the button center returns the
                    // topmost picking element across ALL live panels (headless-safe
                    // layout pick, no GPU). If it isn't this button (or its kin),
                    // a player's click would hit the cover, not the button.
                    string blocker = FindUiToolkitBlocker(b);
                    if (blocker != null)
                    {
                        ReportBlocked(label, blocker);
                        continue;
                    }

                    FlowTrace.Step("Auto", $"ClickableActuator: UITK click '{label}' (reachable).");
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

        // ── Reachability / occlusion (headless-robust) ───────────────────────
        //
        // The fleet runs -nographics: GraphicRaycaster + EventSystem screen
        // raycasts often resolve NO hits without a camera/display, so we cannot
        // lean on EventSystem.RaycastAll the way PointerInterceptDiagnostic does
        // at runtime. Instead we reconstruct the cover analytically from layout:
        //   • uGUI  — compare screen-space rects + render order of raycast-target
        //             Graphics (RectTransform world corners; layout-only, no GPU).
        //   • UITK  — panel.Pick (a layout pick, GPU-free) over every live panel.

        // Emit the deduped CLICK-BLOCKED fleet ticket for a covered button.
        private static void ReportBlocked(string buttonName, string blockerName)
        {
            string key = buttonName ?? "<unnamed>";
            if (!_reportedBlocked.Add(key)) return;   // already logged this run
            // A registered modal owns the screen (OpenEachHUDPanel opens a panel, then sweeps
            // EVERY button) — HUD/other buttons BEHIND that modal are EXPECTED-unreachable, not an
            // occlusion bug. The 85%-screen + RectMask2D guards above miss a ~67%-screen modal
            // (PartyShopPanelMvvm), so the modal's own panel/viewport/rows leak through as false
            // CLICK-BLOCKED Fails and drown the fleet ticket board. While a modal is open, downgrade
            // to a non-error Step; a genuine NO-MODAL HUD overlap still Fails (the real-bug path).
            if (DeNelle.Core.UI.PanelManager.AnyOpen)
            {
                FlowTrace.Step("Auto",
                    $"CLICK-COVERED(expected): '{key}' behind modal '{DeNelle.Core.UI.PanelManager.OpenPanelName}' (blocker '{blockerName}')");
                return;
            }
            FlowTrace.Fail("Auto",
                $"{ClickBlockedTag}: button '{key}' is covered by '{blockerName}' — a player cannot click it");
        }

        // Compact screen-rect for the self-verifying CLICK-BLOCKED log.
        private static string RectStr(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

        // ── uGUI occlusion ───────────────────────────────────────────────────
        // Returns the name of a pickable Graphic that covers btn's screen center
        // and renders ON TOP of it, or null when the button is reachable.
        private static string FindUGuiBlocker(UGuiButton btn, UGuiButton[] allButtons)
        {
            try
            {
                var btnGraphic = btn.targetGraphic != null
                    ? btn.targetGraphic
                    : btn.GetComponent<UGuiGraphic>();
                if (btnGraphic == null) return null;   // no drawable area to occlude

                var btnCanvas = btn.GetComponentInParent<Canvas>();
                if (btnCanvas == null) return null;

                if (!TryGetScreenRect(btnGraphic, btnCanvas, out Rect btnRect))
                    return null;
                Vector2 center = btnRect.center;

                // Scan ALL active raycast-target graphics; a higher-priority one
                // whose screen-rect contains the button center is the cover.
                UGuiGraphic[] graphics;
                try { graphics = Resources.FindObjectsOfTypeAll<UGuiGraphic>(); }
                catch { return null; }
                if (graphics == null) return null;

                foreach (var g in graphics)
                {
                    if (g == null || g == btnGraphic) continue;
                    if (!g.isActiveAndEnabled || !g.raycastTarget) continue;
                    if (!g.gameObject.scene.IsValid()) continue;
                    // Don't treat the button's own sub-graphics (label/icon) as a cover.
                    if (g.transform.IsChildOf(btn.transform)) continue;

                    var gCanvas = g.GetComponentInParent<Canvas>();
                    if (gCanvas == null) continue;

                    if (!TryGetScreenRect(g, gCanvas, out Rect gRect)) continue;
                    // Canonical containment (handles overlay/camera correctly) — replaces the
                    // hand-rolled gRect.Contains so a mis-built rect can't fake an overlap.
                    Camera gCam = gCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null : (gCanvas.worldCamera != null ? gCanvas.worldCamera : Camera.main);
                    var grt = g.transform as RectTransform;
                    if (grt == null || !RectTransformUtility.RectangleContainsScreenPoint(grt, center, gCam)) continue;

                    if (RendersOnTop(gCanvas, g, btnCanvas, btnGraphic))
                    {
                        // A FULL-SCREEN scrim/backdrop/panel covering the button is an INTENTIONAL modal
                        // cover (the bot opens panels in OpenEachHUDPanel) — the button being unreachable
                        // BEHIND an open modal is EXPECTED, not an occlusion bug. Only PARTIAL-element
                        // overlaps are real (e.g. the fixed Icon_hud_build-behind-Slot0). Skip covers that
                        // span >=85% of the screen — this was ~389 expected CLICK-BLOCKED lines/fleet.
                        float screenArea = (float)Screen.width * Screen.height;
                        float gArea = gRect.width * gRect.height;
                        if (screenArea > 0f && gArea >= 0.85f * screenArea) continue;

                        // RectMask2D clip: a scroll-content row (BuyRow_*/EquipRow_*/LabelRow) has a
                        // RectTransform that can extend OVER a button in CONTENT space, but its parent
                        // Viewport's RectMask2D clips it there — so the player CAN actually click the button.
                        // Honor the mask: if the button center lies OUTSIDE an ancestor RectMask2D of g, g is
                        // clipped where the button is and is NOT a real cover (the false 'Btn_Close <- BuyRow'
                        // soft-trap, and the bulk of the scroll-panel CLICK-BLOCKED noise).
                        var clipMask = g.GetComponentInParent<UnityEngine.UI.RectMask2D>();
                        if (clipMask != null)
                        {
                            var maskRt = clipMask.transform as RectTransform;
                            if (maskRt != null &&
                                !RectTransformUtility.RectangleContainsScreenPoint(maskRt, center, gCam))
                                continue;
                        }

                        // Embed the coords so a fleet run is SELF-VERIFYING (real overlap vs math artifact).
                        return $"{g.name} [blockerRect={RectStr(gRect)} btnRect={RectStr(btnRect)} center={center.x:0},{center.y:0}]";
                    }
                }

                // A UI Toolkit panel can also sit over a uGUI button. Pick the
                // topmost UITK element at the same screen point; if a pickable
                // one exists there, it eats the player's click first.
                string utkBlocker = PickTopmostUiToolkit(center, null);
                if (utkBlocker != null) return utkBlocker;

                return null;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Auto",
                    $"ClickableActuator: uGUI reachability check failed for '{btn?.name}' — {ex.Message}");
                return null;   // fail-open: don't block a click on a probe error
            }
        }

        // Screen-space rect of a Graphic. For ScreenSpaceOverlay the RectTransform
        // world corners ARE screen coords; for Camera/World canvases project via
        // the canvas camera. Layout-only — safe headless.
        private static bool TryGetScreenRect(UGuiGraphic g, Canvas canvas, out Rect rect)
        {
            rect = default;
            var rt = g.transform as RectTransform;
            if (rt == null) return false;

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Camera cam = null;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? (Vector2)corners[i]
                    : (cam != null
                        ? RectTransformUtility.WorldToScreenPoint(cam, corners[i])
                        : (Vector2)corners[i]);
                if (sp.x < minX) minX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y > maxY) maxY = sp.y;
            }
            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return rect.width > 0f && rect.height > 0f;
        }

        // Does graphic 'g' (on canvas gC) draw ON TOP of 'btnG' (on btnC)?
        // Order of precedence: root-canvas sortingOrder, then this canvas's
        // sortingOrder, then sibling/hierarchy order (later == on top).
        private static bool RendersOnTop(Canvas gC, UGuiGraphic g, Canvas btnC, UGuiGraphic btnG)
        {
            var gRoot = gC.rootCanvas != null ? gC.rootCanvas : gC;
            var bRoot = btnC.rootCanvas != null ? btnC.rootCanvas : btnC;
            if (gRoot != bRoot)
            {
                if (gRoot.sortingOrder != bRoot.sortingOrder)
                    return gRoot.sortingOrder > bRoot.sortingOrder;
            }
            if (gC.sortingOrder != btnC.sortingOrder)
                return gC.sortingOrder > btnC.sortingOrder;

            // Same canvas sortingOrder: later in the rendered hierarchy draws on top.
            return HierarchyDrawIndex(g.transform) > HierarchyDrawIndex(btnG.transform);
        }

        // A monotonically-increasing "paint order" key from root→leaf sibling
        // indices, so a deeper/later element compares greater (draws on top).
        private static double HierarchyDrawIndex(Transform t)
        {
            // Build the chain root→t, then fold sibling indices into a positional
            // number (most-significant = closest to root).
            var chain = new List<int>(8);
            for (Transform c = t; c != null; c = c.parent)
                chain.Add(c.GetSiblingIndex());
            double key = 0d;
            for (int i = chain.Count - 1; i >= 0; i--)
                key = key * 4096d + (chain[i] + 1);
            return key;
        }

        // ── UI Toolkit occlusion ─────────────────────────────────────────────
        // Returns a cover name, or null if 'btn' is the topmost picker at its
        // own center (i.e. reachable). Works headless (pure layout pick).
        private static string FindUiToolkitBlocker(UiToolkitButton btn)
        {
            try
            {
                var bPanel = btn.panel;
                if (bPanel == null) return null;

                Rect wb = btn.worldBound;
                if (wb.width <= 0f || wb.height <= 0f) return null;

                // Convert the button center from THIS panel's coords to screen
                // coords, then pick the topmost element across every live panel.
                Vector2 screenPt = PanelToScreen(bPanel, wb.center);
                return PickTopmostUiToolkit(screenPt, btn);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Auto",
                    $"ClickableActuator: UITK reachability check failed for '{btn?.name}' — {ex.Message}");
                return null;   // fail-open on probe error
            }
        }

        // Panel-space point -> screen point. RuntimePanelUtils only gives us
        // ScreenToPanel; invert by adding the screen->panel delta back. For the
        // common overlay panel (scale 1, no offset) panel coords == screen coords,
        // so this is identity; for scaled panels we recover the screen point by
        // solving panelPt = ScreenToPanel(screenPt).
        private static Vector2 PanelToScreen(IPanel panel, Vector2 panelPt)
        {
            // ScreenToPanel is affine: panel = A*screen + b. Sample it at two
            // points to recover the inverse without a dedicated API.
            Vector2 p0 = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 px = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(1f, 0f));
            Vector2 py = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(0f, 1f));
            float ax = px.x - p0.x, bx = py.x - p0.x;
            float ay = px.y - p0.y, by = py.y - p0.y;
            float det = ax * by - bx * ay;
            if (Mathf.Abs(det) < 1e-6f) return panelPt;   // singular → assume identity
            Vector2 d = panelPt - p0;
            float sx = (d.x * by - bx * d.y) / det;
            float sy = (ax * d.y - d.x * ay) / det;
            return new Vector2(sx, sy);
        }

        // Across every live UIDocument panel, find the topmost (highest
        // sortingOrder) panel that PICKS a non-button element at 'screenPt'.
        // 'ignore' (and its descendants/ancestors) count as "the button itself"
        // and are NOT treated as a cover. Returns the cover's name or null.
        private static string PickTopmostUiToolkit(Vector2 screenPt, UiToolkitButton ignore)
        {
            UIDocument[] docs;
            try { docs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None); }
            catch { return null; }
            if (docs == null) return null;

            string blocker = null;
            float topSort = float.MinValue;
            foreach (var d in docs)
            {
                if (d == null || d.rootVisualElement == null) continue;
                var panel = d.rootVisualElement.panel;
                if (panel == null) continue;
                float sort = d.panelSettings != null ? d.panelSettings.sortingOrder : 0f;

                Vector2 panelPt;
                VisualElement picked;
                try
                {
                    panelPt = RuntimePanelUtils.ScreenToPanel(panel, screenPt);
                    picked = panel.Pick(panelPt);
                }
                catch { continue; }
                if (picked == null) continue;

                // The button itself (or its own subtree / a containing ancestor)
                // picking is REACHABLE, not a cover.
                if (ignore != null && IsButtonOrKin(picked, ignore)) continue;

                // A FULL-SCREEN UITK overlay (cosmetic-shop-overlay, HeroTalentOverlay, PetSkillTreeOverlay,
                // help-overlay, a scrim) is an INTENTIONAL modal cover — the button behind it is
                // expected-unreachable, not an occlusion bug (mirrors the uGUI full-screen-scrim skip). Only
                // a PARTIAL element picked over the button (e.g. a shop BuyRow over a Close button — a real
                // intra-panel layout bug) should flag. The picked overlay roots ARE the full-screen element;
                // a partial widget is small — so test the picked element's own area vs the panel.
                var rootWB = d.rootVisualElement.worldBound;
                float rootArea = rootWB.width * rootWB.height;
                var pWB = picked.worldBound;
                if (rootArea > 0f && pWB.width * pWB.height >= 0.85f * rootArea) continue;

                if (sort >= topSort)
                {
                    topSort = sort;
                    blocker = !string.IsNullOrEmpty(picked.name)
                        ? picked.name
                        : picked.GetType().Name;
                }
            }
            return blocker;
        }

        // True when 'picked' is the button, a descendant of it, or an ancestor of
        // it (the click still reaches the button in all three cases).
        private static bool IsButtonOrKin(VisualElement picked, UiToolkitButton btn)
        {
            if (picked == null) return false;
            for (VisualElement v = picked; v != null; v = v.parent)
                if (v == btn) return true;          // picked is btn or its child
            for (VisualElement v = btn.parent; v != null; v = v.parent)
                if (v == picked) return true;       // picked is an ancestor of btn
            return false;
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
