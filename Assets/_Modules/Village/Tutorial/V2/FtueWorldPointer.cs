// =============================================================================
// FtueWorldPointer — WO-1344: the FTUE points WHERE TO GO with the owner's
// tagged marker instead of the yellow glow.
// -----------------------------------------------------------------------------
// THE OWNER TAGGED THE KEY. This file maps that key -> a named hook VERBATIM:
//
//     Assets/Editor/VfxManualPicks.json
//       key        : "FTUEPointerofwheretogo_Aura"
//       prefabPath : "Assets/Hovl Studio/Map track markers VFX/Prefabs/
//                     Marker 1 arrows Loop.prefab"
//       isLoop     : false        scale : 1.0        manual : true
//       intent     : "added FTUE vfx for pointing instead of the yellow thing"
//
// ⛔ NO PREFAB IS CHOSEN, SUBSTITUTED OR RESCALED HERE. The key is a const; the
// prefab behind it is resolved by HovlVfxCatalog (generated from HER row) and
// the scale passed to PlayKey is 0, which means "use the row's authored scale"
// — her 1.0. Nothing in this file names a prefab path.
//
// ── WHICH BEATS IT SERVES, AND WHY ONLY THOSE ────────────────────────────────
// The FTUE has TWO kinds of highlight, and they resolve through ONE registry:
//   * "TAP THIS THING"  — TutorialHighlightRegistry hands back a RectTransform
//     ("hud.build_button", "build.card.*", "hud.hero_button", "deck.card.skills").
//     These live on a screen-space overlay Canvas. A world-space particle marker
//     parented into a Canvas renders at the wrong scale/depth or not at all, so
//     this piece DECLINES them and the FocusMask/chevron keeps serving them.
//   * "GO THERE"        — the registry hands back a world Transform
//     ("world.guide", "world.gate_direction"). THIS is what her key names
//     literally ("FTUEPointerofwheretogo") and the only case a world marker can
//     honestly express. This piece owns those.
// TutorialFlow asks TryShow() first; a TRUE answer means the glow stands down
// for that id, a FALSE answer leaves the existing presentation exactly as it was.
//
// ── INPUT TRANSPARENCY (load-bearing — WO-1340 gates nothing by construction) ─
// The marker is spawned into WORLD SPACE by VFXManager.PlayKey. It is never
// parented to a Canvas, this file adds NO GraphicRaycaster, no EventSystem
// component and no Collider, and her prefab itself carries only GameObject /
// Transform / ParticleSystem / ParticleSystemRenderer components — zero
// Collider, zero uGUI Graphic. It therefore cannot receive or swallow a tap or a
// world raycast. Pinned by FtuePointerVfxRegression.
//
// ── isLoop ───────────────────────────────────────────────────────────────────
// Her row says isLoop:false, so every play is a BOUNDED oneshot on VFXManager's
// leak-proof oneshot path (no loop slot is ever held) and the pointer's presence
// is driven by the STEP'S OWN ACTIVE WINDOW: while this piece is armed it
// re-triggers on a <see cref="PulseSeconds"/> cadence, and it stops the moment
// TutorialFlow releases the highlight. Her tag is read, never written.
//
// Presentation-only: no service calls, no game-state reads, no completion logic.
// One owner, one spawner — every body comes from VFXManager.PlayKey; this file
// adds no pool and no second spawner. [Flow:Tutorial] on every decision.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The owner-tagged FTUE "where to go" marker, drawn on WORLD-anchored
    /// tutorial highlights only. <see cref="TryShow"/> claims a highlight id (and
    /// returns false when the id is a UI rect or the key cannot draw, so the
    /// caller keeps the old glow); <see cref="Hide"/> releases it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FtueWorldPointer : MonoBehaviour
    {
        /// <summary>The owner's catalog key, VERBATIM from VfxManualPicks.json.
        /// Pinned against that file by FtuePointerVfxRegression so a refactor can
        /// never silently re-point her tag.</summary>
        public const string PointerKey = "FTUEPointerofwheretogo_Aura";

        // Her row is isLoop:false, so a play is finite. This is the bounded lifetime
        // handed to PlayKey AND the re-trigger cadence — the two are deliberately the
        // same number, so exactly one body is ever live and the objective never sits
        // there un-pointed while the step is still open. It is a CADENCE, not a scale
        // and not an edit to her tag.
        private const float PulseSeconds = 1.25f;

        private static FtueWorldPointer _instance;

        private string _targetId;      // the highlight id this piece currently owns
        private Transform _anchor;     // last resolved world anchor
        private float _nextPlayAt;     // unscaled clock for the re-trigger

        /// <summary>The highlight id this piece currently owns, or null.</summary>
        public static string ArmedTargetId => _instance != null ? _instance._targetId : null;

        /// <summary>True while the pointer owns a highlight (test/oracle hook).</summary>
        public static bool IsArmed => !string.IsNullOrEmpty(ArmedTargetId);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Claim <paramref name="highlightId"/> for the owner-tagged marker.
        /// TRUE  = this piece now draws that beat (the caller must stand the glow down).
        /// FALSE = not a world-anchored beat, or the key cannot draw — the caller keeps
        ///         its existing presentation unchanged. Never throws; never gates.
        /// </summary>
        public static bool TryShow(string highlightId)
        {
            if (string.IsNullOrEmpty(highlightId)) { Hide(); return false; }

            var target = TutorialHighlightRegistry.Resolve(highlightId);
            bool isWorld = target.Rect == null && target.World != null;
            if (!isWorld)
            {
                Hide();
                FlowTrace.Throttle("Tutorial", "ftueptr-decline:" + highlightId, 5f,
                    $"FtuePointer DECLINED highlightId={highlightId} key={PointerKey} " +
                    $"space={(target.Rect != null ? "canvas(ui-rect)" : "unresolved")} — a world-space " +
                    "marker cannot be parented into a Canvas, so the FocusMask/chevron keeps this beat.");
                return false;
            }

            // Never stand the glow down for an effect that could not have drawn: a
            // suppressed glow plus a missing marker is an invisible cue, and a throttled
            // miss-log inside PlayKey cannot undo a decision already taken (§12).
            if (!VFXManager.CanPlayKey(PointerKey))
            {
                Hide();
                FlowTrace.Throttle("Tutorial", "ftueptr-nokey", 5f,
                    $"FtuePointer key='{PointerKey}' resolved to NO playable prefab (no HovlVfxCatalog row, " +
                    "or the row's Prefab is null) — the FocusMask glow KEEPS serving " +
                    $"highlightId={highlightId}. Regenerate the catalog (Defenders/VFX/Generate Hovl VFX " +
                    "Catalog) to wire the owner's VfxManualPicks row.");
                return false;
            }

            var p = Ensure();
            if (!string.Equals(p._targetId, highlightId, StringComparison.OrdinalIgnoreCase))
            {
                p._targetId = highlightId;
                p._nextPlayAt = 0f;   // draw on the very next frame
                FlowTrace.Step("Tutorial",
                    $"FtuePointer SHOW highlightId={highlightId} key={PointerKey} space=world " +
                    $"anchor='{target.World.name}' pos={target.World.position} " +
                    $"(isLoop:false as authored -> re-triggered every {PulseSeconds:0.00}s while the step is open)");
            }
            p._anchor = target.World;
            return true;
        }

        /// <summary>Release the highlight. Safe when nothing is armed. A body already in
        /// flight finishes its bounded life (VFXManager owns oneshot return; there is no
        /// second despawn path here).</summary>
        public static void Hide()
        {
            var p = _instance;
            if (p == null || string.IsNullOrEmpty(p._targetId)) return;
            FlowTrace.Step("Tutorial", $"FtuePointer HIDE (was highlightId={p._targetId})");
            p._targetId = null;
            p._anchor = null;
            p._nextPlayAt = 0f;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private static FtueWorldPointer Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("FtueWorldPointer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FtueWorldPointer>();
            return _instance;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(_targetId)) return;

            // Re-resolve every frame, exactly like UiSpotlight/GuidePointer: a moving
            // anchor is followed, and a late-registered one is picked up without polling.
            var t = TutorialHighlightRegistry.Resolve(_targetId);
            _anchor = t.World;
            if (_anchor == null)
            {
                FlowTrace.Throttle("Tutorial", "ftueptr-lost:" + _targetId, 5f,
                    $"FtuePointer anchor for highlightId={_targetId} is NOT resolving (destroyed, or not " +
                    "registered yet) — nothing drawn this frame; it re-acquires when the anchor returns.");
                return;
            }

            if (Time.unscaledTime < _nextPlayAt) return;
            _nextPlayAt = Time.unscaledTime + PulseSeconds;

            Vector3 pos = _anchor.position;
            // scale: 0 => the catalog row's AUTHORED scale (her 1.0). Never a number of ours.
            // lifetime: PulseSeconds => bounded, leak-proof oneshot path, honouring isLoop:false.
            // parent: the anchor, so the marker rides a guide that walks.
            VFXManager.PlayKey(PointerKey, pos, Quaternion.identity, _anchor,
                               color: null, scale: 0f, lifetime: PulseSeconds, follow: null);

            FlowTrace.Throttle("Tutorial", "ftueptr-play", 1f,
                $"FtuePointer PLAY key={PointerKey} highlightId={_targetId} space=world " +
                $"anchor='{_anchor.name}' pos={pos} scale=<row-authored> lifetime={PulseSeconds:0.00}s " +
                "(VFXManager names the resolved prefab on its own [Flow:VFXManager] line)");
        }
    }
}
