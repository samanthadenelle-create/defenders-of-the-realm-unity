// =============================================================================
// DebuggingController — a DYNAMIC, flag-gated on-screen UI debugger.
// -----------------------------------------------------------------------------
// Owner request 2026-06-13: a reusable debugger we drop in at runtime that, on a
// single click, dumps ALL the info needed to track a dead/intercepted UI button —
// across BOTH the uGUI (EventSystem/Canvas/GraphicRaycaster) and UI-Toolkit
// (UIDocument/PanelSettings/IPanel) stacks, where our "button does nothing" bugs live
// (Settings gear, top-right HUD pair, dev-tools-after-Yarn, Start button).
//
// FLAG-GATED, DORMANT WHEN OFF (owner: "never hurts to sit there with a flag turned
//   off, but priceless during triage"): DebuggingController.Enabled defaults FALSE.
//   When off, only a cheap F9 hotkey-watcher + the static Capture() hook run — no
//   overlay, no cost. Flip it on for triage with F9 (or set Enabled = true in code /
//   from a console). Strip the file entirely to remove.
//
// HOW IT LOADS (dynamic, zero wiring): a RuntimeInitializeOnLoadMethod spawns ONE
//   DontDestroyOnLoad host with this component. The overlay (a ScreenSpaceOverlay
//   canvas at sortingOrder int.MaxValue carrying a "🐞 DBG" button + a readout) is
//   built only when enabled.
//
// HOW TO USE:
//   • Press F9                            → toggle the overlay on/off.
//   • Tap the "🐞 DBG" button             → full dump (raycast at 9 anchors + every
//                                            Canvas + every UIDocument's panel state),
//                                            and ARM capture-next-click.
//   • Then tap a DEAD element             → dumps the full hit-stack at THAT point, so
//                                            you see exactly which frame eats the click.
//   • DebuggingController.Capture("yarn-exit")  → from any seam (e.g. on Yarn dialogue
//                                            exit): grabs full state to the log AND arms
//                                            capture-next-click to log the next action.
//                                            Works even with the overlay hidden (logs).
//   • DebuggingController.Instance?.FindFrame("title-connect-wallet", ScreenLocation.Middle)
//                                          → targeted report for a named button at an anchor.
//
// Every dump is tagged [DBG] so it lands in Player.log + the F8 break recorder.
// ASMDEF: DeNelle.HUD (Unity UI + UIElements are auto-referenced). No game deps.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
// Both UnityEngine.UI and UnityEngine.UIElements define Image/Button — this tool builds
// a uGUI overlay, so disambiguate the bare names to the uGUI types (UIElements is used
// only via fully-qualified UIDocument / VisualElement / PickingMode below).
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;

namespace DeNelle.HUD
{
    /// <summary>Screen anchor points FindFrame / dumps probe. <c>Middle</c> = screen centre.</summary>
    public enum ScreenLocation
    {
        Middle, TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
    }

    /// <summary>
    /// Dynamic, flag-gated on-screen UI debugger. Self-bootstraps dormant; dumps the
    /// full uGUI + UI-Toolkit input/render state on a click so a dead button's
    /// interceptor is identified.
    /// </summary>
    public sealed class DebuggingController : MonoBehaviour
    {
        public static DebuggingController Instance { get; private set; }

        /// <summary>Master flag. Default OFF — dormant (only the F9 watcher + Capture()
        /// hook run) until flipped on for triage via F9 or code.</summary>
        public static bool Enabled = false;

        /// <summary>The key that toggles the overlay at runtime.</summary>
        public static KeyCode ToggleKey = KeyCode.F9;

        private GameObject _canvasGo;
        private Text _readout;
        private bool _captureNextClick;
        private int _dumpN;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            try
            {
                var go = new GameObject("DebuggingController");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<DebuggingController>();
                if (Enabled) Instance.EnsureOverlay(true);
                Debug.Log($"[DBG] DebuggingController installed (flag {(Enabled ? "ON" : "OFF — press F9 to show")}). " +
                          "Capture() hook is live regardless of the flag.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DBG] Bootstrap failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // PUBLIC API
        // ------------------------------------------------------------------
        /// <summary>
        /// Grab full UI state to the log + ARM capture-next-click (logs the next action).
        /// Safe to call with the overlay hidden — always logs. Hook this into seams like
        /// Yarn-dialogue exit ("on exit of yarn grab everything + log the next action").
        /// </summary>
        public static void Capture(string label)
        {
            if (Instance == null) return;
            Instance._captureNextClick = true;
            Instance.DumpAll($"{label} (capture-next-action ARMED)");
        }

        /// <summary>
        /// Report what UI frame actually owns a screen anchor, and where a named button
        /// really sits — distinguishes "button isn't there / is behind X" from "button is
        /// there but something is on top of it." Logs the full stack. (Owner's API sketch.)
        /// </summary>
        public string FindFrame(string buttonName, ScreenLocation location)
        {
            Vector2 pt = PointFor(location);
            var sb = new StringBuilder();
            sb.Append($"[DBG] FindFrame('{buttonName}' @ {location} = screen {pt}):\n");

            var named = FindNamedRects(buttonName);
            if (named.Count == 0) sb.Append($"  • named uGUI element '{buttonName}': NOT FOUND in any canvas\n");
            foreach (var rt in named)
            {
                bool active = rt.gameObject.activeInHierarchy;
                bool ray = rt.TryGetComponent(out Graphic g) && g.raycastTarget;
                sb.Append($"  • '{rt.name}' active={active} raycastTarget={ray} screenCenter~{RectScreenCenter(rt)} canvas='{CanvasOf(rt)}'\n");
            }

            AppendUguiStack(sb, pt);
            AppendUitkStack(sb, pt);

            string report = sb.ToString();
            Debug.Log(report);
            SetReadout(report);
            return report;
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) ToggleOverlay();

            // capture-next-click runs whenever armed (by the DBG button OR Capture()),
            // independent of the visible-overlay flag so Yarn-exit capture works headless.
            if (!_captureNextClick) return;
            bool pressed = Input.GetMouseButtonDown(0);
            if (!pressed && Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                    if (Input.GetTouch(i).phase == TouchPhase.Began) { pressed = true; break; }
            }
            if (!pressed) return;
            _captureNextClick = false;
            Vector2 p = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            DumpPoint($"captured next-action click @ {p}", p);
        }

        private void ToggleOverlay()
        {
            Enabled = !Enabled;
            EnsureOverlay(Enabled);
            Debug.Log($"[DBG] overlay {(Enabled ? "SHOWN" : "hidden")} ({ToggleKey}).");
        }

        private void EnsureOverlay(bool visible)
        {
            if (visible && _canvasGo == null) BuildOverlay();
            if (_canvasGo != null) _canvasGo.SetActive(visible);
        }

        private void OnDbgButton()
        {
            _captureNextClick = true;   // arm: next click anywhere is dumped
            DumpAll("DBG button (capture-next-click ARMED — now click a dead element)");
        }

        private void BuildOverlay()
        {
            _canvasGo = new GameObject("DBG_Canvas");
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = int.MaxValue;          // above literally everything
            _canvasGo.AddComponent<GraphicRaycaster>();

            var btnGo = new GameObject("DBG_Button");
            btnGo.transform.SetParent(_canvasGo.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.65f, 0.1f, 0.1f, 0.92f);
            var btnRt = btnImg.rectTransform;
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 1f);
            btnRt.anchoredPosition = new Vector2(8f, -8f);
            btnRt.sizeDelta = new Vector2(96f, 36f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(OnDbgButton);

            var capGo = new GameObject("DBG_Cap");
            capGo.transform.SetParent(btnGo.transform, false);
            var cap = capGo.AddComponent<Text>();
            cap.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cap.text = "DBG";
            cap.alignment = TextAnchor.MiddleCenter;
            cap.color = Color.white;
            cap.fontSize = 16;
            cap.raycastTarget = false;
            var capRt = cap.rectTransform;
            capRt.anchorMin = Vector2.zero; capRt.anchorMax = Vector2.one;
            capRt.offsetMin = Vector2.zero; capRt.offsetMax = Vector2.zero;

            var roGo = new GameObject("DBG_Readout");
            roGo.transform.SetParent(_canvasGo.transform, false);
            _readout = roGo.AddComponent<Text>();
            _readout.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _readout.text = "[DBG] ready — tap DBG, then tap a dead button";
            _readout.alignment = TextAnchor.UpperLeft;
            _readout.color = new Color(1f, 0.95f, 0.4f, 1f);
            _readout.fontSize = 14;
            _readout.horizontalOverflow = HorizontalWrapMode.Overflow;
            _readout.verticalOverflow = VerticalWrapMode.Overflow;
            _readout.raycastTarget = false;
            var roRt = _readout.rectTransform;
            roRt.anchorMin = roRt.anchorMax = new Vector2(0f, 1f);
            roRt.pivot = new Vector2(0f, 1f);
            roRt.anchoredPosition = new Vector2(8f, -50f);
            roRt.sizeDelta = new Vector2(900f, 400f);
            var sh = roGo.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(1f, -1f);
        }

        // ------------------------------------------------------------------
        private void DumpAll(string reason)
        {
            _dumpN++;
            var sb = new StringBuilder();
            sb.Append($"[DBG] ===== DUMP #{_dumpN} ({reason}) screen={Screen.width}x{Screen.height} =====\n");
            var es = EventSystem.current;
            sb.Append($"  EventSystem={(es != null ? es.name : "<NONE>")} enabled={(es != null && es.isActiveAndEnabled)}\n");

            AppendUguiStack(sb, PointFor(ScreenLocation.Middle));

            sb.Append("  -- Canvases --\n");
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (!c.isRootCanvas) continue;
                bool gr = c.TryGetComponent<GraphicRaycaster>(out _);
                sb.Append($"    '{c.name}' sort={c.sortingOrder} mode={c.renderMode} raycaster={gr} active={c.gameObject.activeInHierarchy}\n");
            }

            sb.Append("  -- UIDocuments --\n");
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                var ps = d.panelSettings;
                bool liveP = d.rootVisualElement != null && d.rootVisualElement.panel != null;
                string pick = d.rootVisualElement != null ? d.rootVisualElement.pickingMode.ToString() : "n/a";
                sb.Append($"    '{d.name}' enabled={d.enabled} panelSettings='{(ps != null ? ps.name : "<null>")}' " +
                          $"sort={(ps != null ? ps.sortingOrder.ToString() : "?")} livePanel={liveP} picking={pick}\n");
            }

            string report = sb.ToString();
            Debug.Log(report);
            SetReadout(report);
        }

        private void DumpPoint(string reason, Vector2 pt)
        {
            var sb = new StringBuilder();
            sb.Append($"[DBG] ===== POINT DUMP ({reason}) =====\n");
            AppendUguiStack(sb, pt);
            AppendUitkStack(sb, pt);
            string report = sb.ToString();
            Debug.Log(report);
            SetReadout(report);
        }

        // ---- helpers ----
        private static void AppendUguiStack(StringBuilder sb, Vector2 pt)
        {
            var es = EventSystem.current;
            sb.Append($"  uGUI RaycastAll @ {pt} (top first = who gets the click):\n");
            if (es == null) { sb.Append("    <no EventSystem>\n"); return; }
            var ped = new PointerEventData(es) { position = pt };
            var results = new List<RaycastResult>();
            es.RaycastAll(ped, results);
            if (results.Count == 0) { sb.Append("    <no hits — nothing uGUI here>\n"); return; }
            for (int i = 0; i < results.Count && i < 8; i++)
            {
                var r = results[i];
                string canvas = r.gameObject.GetComponentInParent<Canvas>() is Canvas c ? c.name : "?";
                sb.Append($"    #{i} '{r.gameObject.name}' canvas='{canvas}' sort={r.sortingOrder} {(i == 0 ? "<= TOP (eats the click)" : "")}\n");
            }
        }

        private static void AppendUitkStack(StringBuilder sb, Vector2 pt)
        {
            sb.Append($"  UI-Toolkit pick @ {pt}:\n");
            int live = 0;
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                var root = d.rootVisualElement;
                if (root == null || root.panel == null) continue;
                live++;
                Vector2 panelPt = new Vector2(pt.x, Screen.height - pt.y); // UITK is top-left origin
                var picked = root.panel.Pick(panelPt);
                sb.Append($"    doc='{d.name}' picked={(picked != null ? (string.IsNullOrEmpty(picked.name) ? picked.GetType().Name : picked.name) : "<none>")}\n");
            }
            if (live == 0) sb.Append("    <no UIDocument has a live panel here>\n");
        }

        private static List<RectTransform> FindNamedRects(string buttonName)
        {
            var hits = new List<RectTransform>();
            if (string.IsNullOrEmpty(buttonName)) return hits;
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
            {
                if (rt.name.IndexOf(buttonName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(rt);
            }
            return hits;
        }

        private static string CanvasOf(Component c)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.name : "?";
        }

        private static Vector2 RectScreenCenter(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Vector3 world = rt.TransformPoint(rt.rect.center);
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return new Vector2(world.x, world.y);
            var cam = canvas != null ? canvas.worldCamera : null;
            return cam != null ? (Vector2)cam.WorldToScreenPoint(world) : (Vector2)world;
        }

        private static Vector2 PointFor(ScreenLocation loc)
        {
            float w = Screen.width, h = Screen.height;
            switch (loc)
            {
                case ScreenLocation.TopLeft:      return new Vector2(w * 0.08f, h * 0.92f);
                case ScreenLocation.TopCenter:    return new Vector2(w * 0.50f, h * 0.92f);
                case ScreenLocation.TopRight:     return new Vector2(w * 0.92f, h * 0.92f);
                case ScreenLocation.MiddleLeft:   return new Vector2(w * 0.08f, h * 0.50f);
                case ScreenLocation.MiddleRight:  return new Vector2(w * 0.92f, h * 0.50f);
                case ScreenLocation.BottomLeft:   return new Vector2(w * 0.08f, h * 0.08f);
                case ScreenLocation.BottomCenter: return new Vector2(w * 0.50f, h * 0.08f);
                case ScreenLocation.BottomRight:  return new Vector2(w * 0.92f, h * 0.08f);
                default:                          return new Vector2(w * 0.50f, h * 0.50f);
            }
        }

        private void SetReadout(string report)
        {
            if (_readout == null) return;
            var lines = report.Split('\n');
            int n = Mathf.Min(lines.Length, 16);
            _readout.text = string.Join("\n", lines, 0, n);
        }
    }
}
