// =============================================================================
// WallRepairHudBridge — runtime glue between WallRepairController and the HUD.
// -----------------------------------------------------------------------------
// Workstream B. The repair mechanic spans two assemblies that, by the port
// spec's module-isolation rule (Part 2), cannot reference each other:
//
//   WallRepairController  -> DeNelle.Village  (gameplay; references Core only)
//   VillageHudController  -> DeNelle.HUD      (passive display; references Core only)
//
// Neither may name the other's type. This bridge is the seam: it lives in
// DeNelle.Village, holds a typed reference to WallRepairController and a
// REFLECTION handle on the HUD controller (a plain UnityEngine.Object — a
// Core-safe type). At Start() it:
//
//   * subscribes to WallRepairController's UnityEvents (PromptShown /
//     PromptHidden / FeedbackShown) and forwards each to the HUD's
//     ShowRepairPrompt / HideRepairPrompt / ShowRepairFeedback methods by
//     reflection;
//   * subscribes to the HUD's RepairConfirmRequested / RepairCancelRequested
//     UnityEvents (read by reflection) and forwards them to
//     WallRepairController.ConfirmRepair / CancelRepair.
//
// Reflection here mirrors how the project's editor scene builders already wire
// cross-module references — it is the established no-dependency seam. The
// scene-setup editor file (WallRepairSceneSetup.cs) drops this component in and
// assigns the two references.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime glue forwarding <see cref="WallRepairController"/> events to the
    /// village HUD and HUD repair-button events back to the controller. Bridges
    /// the DeNelle.Village / DeNelle.HUD asmdef gap by reflection so neither
    /// module has to reference the other.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallRepairHudBridge : MonoBehaviour
    {
        [Header("Wiring (assigned by WallRepairSceneSetup)")]
        [Tooltip("The repair controller this bridge forwards to / from.")]
        [SerializeField] private WallRepairController _controller;

        [Tooltip("The VillageHudController instance — held as a plain Object because the " +
                 "Village asmdef cannot reference DeNelle.HUD. Bound by reflection at Start().")]
        [SerializeField] private UnityEngine.Object _hud;

        // ── Cached HUD reflection handles ────────────────────────────────────
        private MethodInfo _hudShowPrompt;     // ShowRepairPrompt(string,int,bool)
        private MethodInfo _hudHidePrompt;     // HideRepairPrompt()
        private MethodInfo _hudShowFeedback;   // ShowRepairFeedback(string,bool)
        private UnityEvent _hudConfirmEvent;   // RepairConfirmRequested
        private UnityEvent _hudCancelEvent;    // RepairCancelRequested
        private bool _bound;

        private void Start()
        {
            Bind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>
        /// Binds the controller and HUD together. Safe to call again — it
        /// unbinds first. Called from <see cref="Start"/>; exposed so the
        /// scene-setup / a test can force a (re)bind after assigning references.
        /// </summary>
        public void Bind()
        {
            Unbind();

            if (_controller == null)
            {
                _controller = GetComponent<WallRepairController>();
                if (_controller == null)
                    _controller = FindAnyController();
            }
            if (_controller == null)
            {
                Debug.LogWarning("[WallRepairHudBridge] No WallRepairController assigned / found — " +
                                 "the repair prompt will not reach the HUD.");
                return;
            }

            if (_hud == null)
            {
                Debug.LogWarning("[WallRepairHudBridge] No HUD object assigned — the repair prompt " +
                                 "will not display. Assign the VillageHudController via the inspector " +
                                 "or WallRepairSceneSetup.");
                return;
            }

            ResolveHudHandles();

            // Controller -> HUD.
            _controller.PromptShown.AddListener(OnPromptShown);
            _controller.PromptHidden.AddListener(OnPromptHidden);
            _controller.FeedbackShown.AddListener(OnFeedbackShown);

            // HUD -> controller.
            _hudConfirmEvent?.AddListener(OnHudConfirm);
            _hudCancelEvent?.AddListener(OnHudCancel);

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound) return;
            if (_controller != null)
            {
                _controller.PromptShown.RemoveListener(OnPromptShown);
                _controller.PromptHidden.RemoveListener(OnPromptHidden);
                _controller.FeedbackShown.RemoveListener(OnFeedbackShown);
            }
            _hudConfirmEvent?.RemoveListener(OnHudConfirm);
            _hudCancelEvent?.RemoveListener(OnHudCancel);
            _bound = false;
        }

        /// <summary>Assigns the bridge's references (used by the scene-setup editor file / tests).</summary>
        public void Configure(WallRepairController controller, UnityEngine.Object hud)
        {
            _controller = controller;
            _hud = hud;
        }

        // ── Controller -> HUD forwarders ─────────────────────────────────────

        private void OnPromptShown(RepairPromptInfo info)
        {
            // The HUD's ShowRepairPrompt(string subtitle, int cost, bool affordable)
            // takes the already-composed sub-line — the HUD displays it verbatim.
            _hudShowPrompt?.Invoke(_hud, new object[]
            {
                info.Subtitle, info.CrystalCost, info.Affordable,
            });
        }

        private void OnPromptHidden()
        {
            _hudHidePrompt?.Invoke(_hud, null);
        }

        private void OnFeedbackShown(string message, bool isError)
        {
            _hudShowFeedback?.Invoke(_hud, new object[] { message, isError });
        }

        // ── HUD -> controller forwarders ─────────────────────────────────────

        private void OnHudConfirm()
        {
            if (_controller == null) return;
            // The HUD button consumed this OS click — stop the controller's
            // raycast reading the same press as a world tap.
            _controller.SuppressNextWorldTap();
            _controller.ConfirmRepair();
        }

        private void OnHudCancel()
        {
            if (_controller == null) return;
            _controller.SuppressNextWorldTap();
            _controller.CancelRepair();
        }

        // ── Reflection plumbing ──────────────────────────────────────────────

        /// <summary>
        /// Resolves the HUD methods + UnityEvents by reflection. The HUD type is
        /// <c>DeNelle.HUD.VillageHudController</c>; this file cannot name it
        /// (asmdef isolation), so every member is looked up by name.
        /// </summary>
        private void ResolveHudHandles()
        {
            var t = _hud.GetType();

            _hudShowPrompt = t.GetMethod("ShowRepairPrompt",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string), typeof(int), typeof(bool) }, null);
            _hudHidePrompt = t.GetMethod("HideRepairPrompt",
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            _hudShowFeedback = t.GetMethod("ShowRepairFeedback",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string), typeof(bool) }, null);

            _hudConfirmEvent = ReadUnityEvent(t, "RepairConfirmRequested");
            _hudCancelEvent = ReadUnityEvent(t, "RepairCancelRequested");

            if (_hudShowPrompt == null || _hudHidePrompt == null || _hudShowFeedback == null)
                Debug.LogWarning("[WallRepairHudBridge] One or more HUD repair-prompt methods were " +
                                 $"not found on '{t.FullName}'. Is the HUD module up to date?");
        }

        /// <summary>
        /// Reads a public instance <see cref="UnityEvent"/>-typed member named
        /// <paramref name="memberName"/> off the HUD instance — checked as a
        /// field first, then a property. Returns null (with a warning) when the
        /// member is missing or is not a plain <see cref="UnityEvent"/>.
        /// </summary>
        private UnityEvent ReadUnityEvent(Type type, string memberName)
        {
            object raw = null;

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                raw = field.GetValue(_hud);
            }
            else
            {
                var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                    raw = prop.GetValue(_hud);
            }

            if (raw is UnityEvent ue) return ue;

            Debug.LogWarning($"[WallRepairHudBridge] HUD UnityEvent '{memberName}' not found " +
                             $"(or not a plain UnityEvent) on {type.Name}.");
            return null;
        }

        private static WallRepairController FindAnyController()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<WallRepairController>();
#else
            return UnityEngine.Object.FindAnyObjectByType<WallRepairController>();
#endif
        }
    }
}
