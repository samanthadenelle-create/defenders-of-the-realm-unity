// =============================================================================
// HudAreasHost — ONE screen-space canvas; the named ACTIONABLE AREAS as stable
// mount RectTransforms (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 A4/A4.1 — P23).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// THE OWNER'S TAXONOMY (A4): the HUD kit is GENERIC — one skin divided into
// named actionable areas, each a stable mount point with a wiring contract.
// A4.1 first division (them|us): the HOSTILE tree sits territorially HIGH
// (status crown / targetInfo), the FRIENDLY tree sits LOW at the thumbs
// (actionBar/actionRail/moveCluster/dock). system + feedback are overlays
// outside both trees.
//
// This host builds ONLY scaffolding (canvas + empty RectTransform mounts) —
// zero widgets, zero art. Widgets land in the mounts via HudKitController,
// all from the ElarionUiKit factory (§5 review law).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD.Kit
{
    /// <summary>The named actionable areas (A4). Values are hud-areas.json keys.</summary>
    public enum HudArea
    {
        /// <summary>FRIENDLY — hero vitals/telemetrics (top-left).</summary>
        Vitals,
        /// <summary>HOSTILE crown — wave block / heart / target cycle (top-center).</summary>
        Status,
        /// <summary>Overlay chrome — settings/pause/flee (top-right).</summary>
        System,
        /// <summary>HOSTILE — target frame + cast telegraph (high, under the crown).</summary>
        TargetInfo,
        /// <summary>FRIENDLY — big attack + rail slots (bottom-right thumb arc).</summary>
        ActionRail,
        /// <summary>FRIENDLY — ability/potion/town-action row (bottom-center).</summary>
        ActionBar,
        /// <summary>FRIENDLY — the four round movement buttons (bottom-left thumb).</summary>
        MoveCluster,
        /// <summary>Overlay — combat stamps / toasts (full screen, never raycast).</summary>
        Feedback,
        /// <summary>FRIENDLY — chat/social dock (left edge).</summary>
        Dock,
        /// <summary>FRIENDLY — Heart of Elarion / tree-of-life status (left, just below vitals).</summary>
        HeartStatus,
        /// <summary>FRIENDLY — persistent Builders/Training status chip (right, under System; WO-778).</summary>
        QueueStatus,
    }

    /// <summary>One canvas, nine mounts. Pure scaffolding (see header).</summary>
    public sealed class HudAreasHost : MonoBehaviour
    {
        private readonly Dictionary<HudArea, RectTransform> _mounts = new Dictionary<HudArea, RectTransform>();

        /// <summary>The host canvas.</summary>
        public Canvas Canvas { get; private set; }

        /// <summary>The whole-HUD fade/interactivity group (SetHudVisible seam).</summary>
        public CanvasGroup Group { get; private set; }

        /// <summary>The mount for an area (never null after Build).</summary>
        public RectTransform Mount(HudArea area)
        {
            RectTransform rt;
            return _mounts.TryGetValue(area, out rt) ? rt : null;
        }

        /// <summary>Create the host canvas + all area mounts on a fresh GameObject.</summary>
        public static HudAreasHost Create(Transform parentSceneObject)
        {
            var go = new GameObject("HudAreasHost");
            if (parentSceneObject != null) go.transform.SetParent(parentSceneObject, false);
            var host = go.AddComponent<HudAreasHost>();
            host.Build();
            return host;
        }

        private void Build()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 4000;   // above world/legacy chrome, below battle overlays (5000)/modals (30000+)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            Group = gameObject.AddComponent<CanvasGroup>();

            // A4 area geometry (reference-fraction anchors; hostile HIGH, friendly LOW/thumbs).
            Add(HudArea.Vitals,      new Vector2(0.010f, 0.800f), new Vector2(0.330f, 0.985f));
            Add(HudArea.Status,      new Vector2(0.340f, 0.845f), new Vector2(0.660f, 0.990f));
            Add(HudArea.System,      new Vector2(0.845f, 0.880f), new Vector2(0.995f, 0.985f));
            Add(HudArea.TargetInfo,  new Vector2(0.280f, 0.660f), new Vector2(0.720f, 0.840f));
            Add(HudArea.ActionRail,  new Vector2(0.780f, 0.040f), new Vector2(0.995f, 0.420f));
            // WO-835: widened 0.280-0.720 -> 0.270-0.730 (still symmetric, clear of the
            // MoveCluster right edge at 0.270 and the ActionRail left edge at 0.780) so the
            // 7-face applicability MAX (Build/Talk/Bag/Raids/Map/Quests/Upgrade) keeps each
            // face near the previous 6-face touch size at the constant per-button width.
            Add(HudArea.ActionBar,   new Vector2(0.270f, 0.015f), new Vector2(0.730f, 0.150f));
            Add(HudArea.MoveCluster, new Vector2(0.010f, 0.030f), new Vector2(0.270f, 0.330f));
            Add(HudArea.Dock,        new Vector2(0.000f, 0.330f), new Vector2(0.230f, 0.430f));
            // Heart of Elarion status: left column, directly BELOW the Vitals cluster (WO-432).
            Add(HudArea.HeartStatus, new Vector2(0.010f, 0.700f), new Vector2(0.330f, 0.792f));
            // WO-778: Builders/Training chip — right column, below System (.88), above the
            // ActionRail top (.42); the only occupant of this free band (no collision).
            // Taller since 2026-07-30: top band = the Builders summary button, the rest =
            // the WC3-style 5-deep queue rows (plate hides when empty, so an idle HUD
            // shows only the original chip-sized button). Clear of ActionRail (tops 0.420).
            Add(HudArea.QueueStatus, new Vector2(0.780f, 0.530f), new Vector2(0.995f, 0.865f));
            Add(HudArea.Feedback,    Vector2.zero,                Vector2.one);

            // Feedback overlay never eats taps (stamps/toasts are decorative).
            var fb = Mount(HudArea.Feedback);
            if (fb != null) fb.SetAsLastSibling();

            FlowTrace.Step("HudKit", "HudAreasHost built: 9 area mounts on one canvas (scaffolding only)");
        }

        private void Add(HudArea area, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Area_" + area, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _mounts[area] = rt;
        }
    }
}
