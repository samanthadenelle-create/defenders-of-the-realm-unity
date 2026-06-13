// =============================================================================
// PointerInterceptDiagnostic — trace-only pointer-hit dump.
// -----------------------------------------------------------------------------
// "Dev tools dead after a Yarn dialogue" investigation (2026-06-13). The Settings
// (HelpMenu) / Dev-tools (AdminOverlay) overlay OPENS after a Yarn dialogue, but a
// click on its buttons never reaches the handler. THREE prior fixes (panel dedup,
// CanvasGroup release, disabling the orphaned DialogueAdvance InputAction) all RAN
// per the trace, yet the click is still eaten — there is a 4th interceptor consuming
// the pointer.
//
// This component does NOT change behaviour. On every pointer PRESS, IF a dev/settings
// overlay is open, it dumps EVERYTHING under the pointer from BOTH UI systems so the
// next playtest names the exact interceptor:
//   • uGUI:   EventSystem.RaycastAll — the ordered hit stack. The FIRST entry is what
//             uGUI delivers the click to; if that is NOT the dev menu it's the suspect.
//   • UITK:   panel.Pick per active UIDocument — the topmost picking panel that ISN'T
//             the dev menu is the suspect.
// Each line marks whether the hit IS the dev menu (HelpMenu/AdminOverlay) or something
// else, plus EventSystem name/enabled and Time.timeScale.
//
// Gated to "an overlay is open" + the press event only (no per-frame spam). Uses the
// legacy Input Manager for press detection because the HUD asmdef does not reference
// Unity.InputSystem (same constraint AdminOverlay's chord lives under).
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class PointerInterceptDiagnostic : MonoBehaviour
    {
        // Reused per-press to avoid per-click allocations in the dump.
        private readonly List<RaycastResult> _uguiResults = new List<RaycastResult>();

        private void Update()
        {
            // PRESS only — never per-frame. Mouse button 0 or a touch that began.
            bool pressed = Input.GetMouseButtonDown(0);
            if (!pressed && Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    if (Input.GetTouch(i).phase == TouchPhase.Began) { pressed = true; break; }
                }
            }
            if (!pressed) return;

            // GATE: only dump while a dev/settings overlay is actually open, to avoid
            // per-click spam during normal play.
            if (!IsAnyOverlayOpen()) return;

            Vector2 screenPos = ResolvePointerScreenPos();
            DumpHitStack(screenPos);
        }

        /// <summary>True while the Settings (HelpMenu) or Dev-tools (AdminOverlay) overlay is open.</summary>
        private static bool IsAnyOverlayOpen()
        {
            if (HelpMenu.Instance != null && HelpMenu.Instance.IsOpen) return true;
            var admin = Object.FindFirstObjectByType<AdminOverlay>(FindObjectsInactive.Include);
            return admin != null && admin.IsOpen;
        }

        private static Vector2 ResolvePointerScreenPos()
        {
            if (Input.touchCount > 0) return Input.GetTouch(0).position;
            return (Vector2)Input.mousePosition;
        }

        private void DumpHitStack(Vector2 screenPos)
        {
            var es = EventSystem.current;
            FlowTrace.Step("UI",
                $"[POINTER-DUMP] press @ screen={screenPos} | EventSystem='" +
                $"{(es != null ? es.name : "<null>")}' enabled={(es != null && es.enabled)} | " +
                $"timeScale={Time.timeScale} (dev-tools-after-Yarn interceptor probe)");

            DumpUgui(es, screenPos);
            DumpUiToolkit(screenPos);
        }

        // ── uGUI: EventSystem.RaycastAll — ordered hit stack ─────────────────────
        private void DumpUgui(EventSystem es, Vector2 screenPos)
        {
            if (es == null)
            {
                FlowTrace.Warn("UI", "[POINTER-DUMP] uGUI: no EventSystem.current — uGUI raycast skipped");
                return;
            }

            var ped = new PointerEventData(es) { position = screenPos };
            _uguiResults.Clear();
            es.RaycastAll(ped, _uguiResults);

            if (_uguiResults.Count == 0)
            {
                FlowTrace.Step("UI", "[POINTER-DUMP] uGUI RaycastAll: 0 hits (no uGUI graphic under pointer)");
                return;
            }

            FlowTrace.Step("UI", $"[POINTER-DUMP] uGUI RaycastAll: {_uguiResults.Count} hit(s), top first " +
                "(FIRST = who uGUI delivers the click to):");
            for (int i = 0; i < _uguiResults.Count; i++)
            {
                var r = _uguiResults[i];
                var go = r.gameObject;
                var canvas = go != null ? go.GetComponentInParent<Canvas>() : null;
                var graphic = go != null ? go.GetComponent<UnityEngine.UI.Graphic>() : null;

                var sb = new StringBuilder(128);
                sb.Append("[POINTER-DUMP]   uGUI#").Append(i)
                  .Append(" go='").Append(go != null ? go.name : "?").Append('\'');
                if (canvas != null)
                {
                    sb.Append(" canvas='").Append(canvas.name).Append('\'')
                      .Append(" sortOrder=").Append(canvas.sortingOrder)
                      .Append(" render=").Append(canvas.renderMode);
                }
                else sb.Append(" canvas=<none>");
                sb.Append(" raycastTargetGraphic=")
                  .Append(graphic != null && graphic.raycastTarget ? "yes" : (graphic != null ? "no" : "n/a"));
                FlowTrace.Step("UI", sb.ToString());
            }
        }

        // ── UI Toolkit: panel.Pick per active UIDocument ─────────────────────────
        private void DumpUiToolkit(Vector2 screenPos)
        {
            var docs = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (docs == null || docs.Length == 0)
            {
                FlowTrace.Step("UI", "[POINTER-DUMP] UITK: no UIDocuments in scene");
                return;
            }

            FlowTrace.Step("UI", $"[POINTER-DUMP] UITK: {docs.Length} UIDocument(s) — picking each panel " +
                "(topmost non-dev-menu picker = suspect):");
            foreach (var doc in docs)
            {
                if (doc == null) continue;
                var root = doc.rootVisualElement;
                var panel = root != null ? root.panel : null;
                float sort = doc.panelSettings != null ? doc.panelSettings.sortingOrder : float.MinValue;
                string docName = doc.gameObject != null ? doc.gameObject.name : "?";
                bool isDev = IsDevMenuDoc(doc);

                if (panel == null)
                {
                    FlowTrace.Step("UI", $"[POINTER-DUMP]   UITK doc='{docName}' sortOrder={sort} " +
                        $"isDevMenu={(isDev ? "YES" : "no")} panel=<null> (not attached/visible)");
                    continue;
                }

                // RuntimePanelUtils.ScreenToPanel converts the screen point into this
                // panel's coordinate space, then panel.Pick returns the topmost picking
                // VisualElement in THIS panel (or null if nothing under the pointer here).
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
                VisualElement picked = panel.Pick(panelPos);

                FlowTrace.Step("UI", $"[POINTER-DUMP]   UITK doc='{docName}' sortOrder={sort} " +
                    $"isDevMenu={(isDev ? "YES" : "no")} " +
                    $"picked='{(picked != null ? DescribeElement(picked) : "<none>")}' " +
                    $"panelPos={panelPos}");
            }
        }

        /// <summary>
        /// Is this UIDocument the Settings (HelpMenu) or Dev-tools (AdminOverlay) panel?
        /// Both live on a GameObject named "HelpMenu"/"AdminOverlay" and sit at the
        /// known top-most sortingOrders (HelpMenu 2700, AdminOverlay 2710). Matched by
        /// owning component first, GameObject name as a fallback.
        /// </summary>
        private static bool IsDevMenuDoc(UIDocument doc)
        {
            if (doc == null) return false;
            if (doc.GetComponent<HelpMenu>() != null) return true;
            if (doc.GetComponent<AdminOverlay>() != null) return true;
            string n = doc.gameObject != null ? doc.gameObject.name : null;
            return n == "HelpMenu" || n == "AdminOverlay";
        }

        /// <summary>name + type + pickingMode of a picked element (and its first named ancestor).</summary>
        private static string DescribeElement(VisualElement ve)
        {
            var sb = new StringBuilder(96);
            sb.Append('\'').Append(string.IsNullOrEmpty(ve.name) ? "<unnamed>" : ve.name).Append('\'')
              .Append(" type=").Append(ve.GetType().Name)
              .Append(" picking=").Append(ve.pickingMode);

            // Walk up to the nearest NAMED ancestor so an unnamed leaf still identifies its panel.
            VisualElement p = ve.parent;
            while (p != null && string.IsNullOrEmpty(p.name)) p = p.parent;
            if (p != null) sb.Append(" namedAncestor='").Append(p.name).Append('\'');
            return sb.ToString();
        }
    }
}
