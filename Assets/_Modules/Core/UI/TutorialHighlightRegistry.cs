// =============================================================================
// TutorialHighlightRegistry — stable-id lookup of spotlightable targets (WO-T2).
// -----------------------------------------------------------------------------
// HUD/panels REGISTER their affordances by id as they build ("hud.build_button",
// "hud.wave_button"); world systems register Transforms or lazy RESOLVERS
// ("world.sylas", "world.gate_direction"). The tutorial (UiSpotlight /
// TutorialFlow) resolves ids here — replacing TutorialHudOverlay.Highlight's
// UIDocument name-reach (spec §2.2). Reusable beyond the tutorial: any coach
// mark / attention cue can resolve the same ids.
//
// NOT tutorial-gated: registration is a dictionary write; nothing renders until
// something resolves an id. Destroyed targets resolve to null (Unity fake-null
// checked) so a scene reload can never hand out a stale RectTransform.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>A resolved highlight target: either a uGUI rect or a world anchor.</summary>
    public readonly struct HighlightTarget
    {
        public readonly RectTransform Rect;     // uGUI target (screen-space)
        public readonly Transform World;        // world anchor (project via camera)
        public bool IsValid => Rect != null || World != null;
        public HighlightTarget(RectTransform rect) { Rect = rect; World = null; }
        public HighlightTarget(Transform world) { Rect = null; World = world; }
    }

    /// <summary>
    /// Id → target registry the tutorial spotlight resolves through. Owners
    /// register eagerly (UI, at build time) or lazily (world resolvers).
    /// </summary>
    public static class TutorialHighlightRegistry
    {
        private static readonly Dictionary<string, RectTransform> _rects =
            new Dictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Transform> _worlds =
            new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Func<HighlightTarget>> _resolvers =
            new Dictionary<string, Func<HighlightTarget>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised when a target registers — a spotlight waiting on a late-built
        /// UI element can re-resolve without polling.</summary>
        public static event Action<string> TargetRegistered;

        /// <summary>Register a uGUI target by id (HUD/panels call this as they build). Idempotent.</summary>
        public static void Register(string id, RectTransform target)
        {
            if (string.IsNullOrEmpty(id) || target == null) return;
            _rects[id] = target;
            TargetRegistered?.Invoke(id);
        }

        /// <summary>Register a world-space anchor by id. Idempotent.</summary>
        public static void RegisterWorld(string id, Transform anchor)
        {
            if (string.IsNullOrEmpty(id) || anchor == null) return;
            _worlds[id] = anchor;
            TargetRegistered?.Invoke(id);
        }

        /// <summary>Register a LAZY resolver (for targets that move/spawn late, e.g.
        /// "world.gate_direction" = the nearest gate at resolve time). Explicit
        /// registrations win over resolvers for the same id.</summary>
        public static void RegisterResolver(string id, Func<HighlightTarget> resolver)
        {
            if (string.IsNullOrEmpty(id) || resolver == null) return;
            _resolvers[id] = resolver;
            TargetRegistered?.Invoke(id);
        }

        /// <summary>Remove a registration (any kind) for <paramref name="id"/>.</summary>
        public static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _rects.Remove(id);
            _worlds.Remove(id);
            _resolvers.Remove(id);
        }

        /// <summary>Resolve an id to a live target. Invalid (never registered, destroyed,
        /// or resolver returned nothing) ⇒ IsValid == false — callers degrade gracefully.</summary>
        public static HighlightTarget Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return default;

            if (_rects.TryGetValue(id, out var rt))
            {
                if (rt != null) return new HighlightTarget(rt);
                _rects.Remove(id);   // destroyed — drop the stale entry
            }
            if (_worlds.TryGetValue(id, out var w))
            {
                if (w != null) return new HighlightTarget(w);
                _worlds.Remove(id);
            }
            if (_resolvers.TryGetValue(id, out var fn) && fn != null)
            {
                try { return fn(); }
                catch (Exception ex)
                {
                    Diagnostics.FlowTrace.Warn("Tutorial", $"highlight resolver '{id}' threw: {ex.Message}");
                }
            }
            return default;
        }

        /// <summary>True when the id is registered (eager or resolver). Used by the
        /// DataRegression invariant "every highlight id is a known registry key" via
        /// the KnownIds snapshot below.</summary>
        public static bool IsRegistered(string id) =>
            !string.IsNullOrEmpty(id) &&
            (_rects.ContainsKey(id) || _worlds.ContainsKey(id) || _resolvers.ContainsKey(id));

        /// <summary>The ids the shipped game is expected to register (build-time contract
        /// for data validation — runtime registration is scene-dependent).</summary>
        public static readonly string[] KnownIds =
        {
            "hud.build_button",     // VillageHudController TownActions BUILD icon
            "hud.wave_button",      // VillageHudController Start Wave CTA
            "world.sylas",          // TutorialWorldAnchors resolver (Sylas NPC / gate fallback)
            "world.gate_direction", // TutorialWorldAnchors resolver (nearest gate to hero)
        };
    }
}
