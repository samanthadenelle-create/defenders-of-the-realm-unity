// =============================================================================
// RealmPins — the SINGLE content-pin seam shared by the parchment Realm Map and
// the corner minimap (WO-829 §3 + §6, program WO-825).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// WO-829 §6 is explicit: "Same projection helpers; NO DUPLICATE GAME LOGIC."
// Two surfaces want to draw the same world content — the full parchment map
// (Village/RealmMapPanel) and the corner minimap (HUD/HudMinimapWidget) — and
// they live in DIFFERENT assemblies that may never reference each other. So the
// pin list cannot live in either of them: it lives HERE, in Core, and both
// surfaces READ it. A producer publishes once; every surface redraws.
//
// PURE DATA — no colour, no sprite, no UnityEngine.UI. The presentation half
// (shape/tint/glyph per kind + per biome) lives in DeNelle.Core.UI's
// RealmAtmosphereStyle, next to ElarionUi where the palette already lives.
// That split is the HP B2B rule: presentation is a separate layer.
//
// FOG LAW (WO-829 acceptance: "Locked regions do not show spoilery pin details").
// A pin carries its own region id; RealmPinBoard does NOT filter by fog, because
// only the caller knows the player's discovery ledger (WO-827). Callers gate with
// RealmPinBoard.RevealsDetail(state) — one shared predicate, so the parchment and
// the minimap can never disagree about what a locked region gives away.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.World
{
    /// <summary>What a content pin represents (WO-829 §3 table). The kind — never a
    /// colour — is what selects the pin's SILHOUETTE, so the map stays readable with
    /// red/green fully desaturated (owner is colourblind; CLAUDE.md §7 canon).</summary>
    public enum RealmPinKind
    {
        /// <summary>The player's own position / home region.</summary>
        You,
        /// <summary>The active navigation target (seam, travel marker, quest anchor).</summary>
        Objective,
        /// <summary>Uncleared camps / hostiles in a region (Count carries "X camps").</summary>
        Threat,
        /// <summary>An available raid camp. NEVER bypasses the full-army gate — this is a
        /// MARKER only; the tap route re-checks the gate at the Raids flow.</summary>
        RaidTarget,
        /// <summary>A known dungeon portal / RoomForge entry.</summary>
        Dungeon,
        /// <summary>An active tracked rumor with a world anchor.</summary>
        Rumor,
        /// <summary>A built barracks / army muster point (WO-822 synergy).</summary>
        Army,
    }

    /// <summary>
    /// One content pin: WHERE (world XZ + optional region id), WHAT (kind), and the
    /// always-present TEXT. <see cref="Label"/> is mandatory-by-convention because the
    /// colourblind guarantee is that meaning is legible without colour — every surface
    /// that draws a pin must be able to name it.
    /// </summary>
    public readonly struct RealmPin
    {
        /// <summary>The pin kind (selects silhouette + copy).</summary>
        public readonly RealmPinKind Kind;
        /// <summary>Realm-map region id this pin belongs to ("" for un-regioned world pins).</summary>
        public readonly string RegionId;
        /// <summary>World X (metres). Used by the minimap projection.</summary>
        public readonly float WorldX;
        /// <summary>World Z (metres). Used by the minimap projection.</summary>
        public readonly float WorldZ;
        /// <summary>Short human label — always present, never carried by colour alone.</summary>
        public readonly string Label;
        /// <summary>Optional multiplicity ("3 camps"). 0 when the pin is singular.</summary>
        public readonly int Count;
        /// <summary>
        /// True when <see cref="WorldX"/>/<see cref="WorldZ"/> are a REAL position in the
        /// live scene, so a world-projecting surface (the corner minimap) may draw it.
        ///
        /// FALSE for a REGION-ANCHORED pin — a dungeon or a raid camp that exists on the
        /// parchment at <see cref="RegionId"/> but has no metres in the town scene. Those
        /// carry (0,0), and without this flag the minimap would faithfully project them onto
        /// the WORLD ORIGIN: every region pin stacked on one spot near the Heart, reading as
        /// "there is a dungeon right there". A pin that lies about WHERE is worse than a pin
        /// that is absent, which is the same reasoning behind the fail-closed
        /// <see cref="RealmPinBoard.RevealsDetail"/>.
        /// </summary>
        public readonly bool WorldAnchored;

        /// <summary>Constructs an immutable pin from all fields.</summary>
        public RealmPin(RealmPinKind kind, float worldX, float worldZ, string label,
                        string regionId = "", int count = 0, bool worldAnchored = true)
        {
            Kind = kind;
            WorldX = worldX;
            WorldZ = worldZ;
            Label = string.IsNullOrEmpty(label) ? kind.ToString() : label;
            RegionId = regionId ?? "";
            Count = count < 0 ? 0 : count;
            WorldAnchored = worldAnchored;
        }

        /// <summary>A pin that lives on the parchment map only: anchored to a region id,
        /// with no world metres. Reads better at every call site than passing (0f, 0f).</summary>
        public static RealmPin InRegion(RealmPinKind kind, string regionId, string label, int count = 0)
            => new RealmPin(kind, 0f, 0f, label, regionId, count, worldAnchored: false);
    }

    /// <summary>
    /// THE stable source ids the shipped producers publish under (WO-829 §3).
    ///
    /// <see cref="RealmPinBoard.Publish"/> is per-source REPLACE, so a stable id is exactly
    /// what makes a producer IDEMPOTENT: re-publishing overwrites that producer's own bucket
    /// instead of stacking a second copy of every pin. That is the whole reason the board is
    /// keyed by source and not a flat list — and it only holds if the id is a CONSTANT and
    /// not a string literal retyped at each call site (one typo = two buckets = duplicates
    /// that no clear can ever reach).
    /// </summary>
    public static class RealmPinSources
    {
        /// <summary>The player's own position ("You").</summary>
        public const string Hero = "realm.hero";
        /// <summary>Known dungeon portals / dungeon regions.</summary>
        public const string Dungeons = "realm.dungeons";
        /// <summary>Available raid camps — MARKERS ONLY; the army gate is re-checked on tap.</summary>
        public const string Raids = "realm.raids";
        /// <summary>Built barracks / muster points in town.</summary>
        public const string Army = "realm.army";
        /// <summary>Legacy IVillageHud.SetMinimapPoi forwarding (VillageHudController).</summary>
        public const string VillageHud = "villageHud";
    }

    /// <summary>
    /// The shared pin registry (see file header). Producers PUBLISH under a stable
    /// source id and re-publish to replace their own set; surfaces READ <see cref="Pins"/>
    /// and redraw on <see cref="Changed"/>.
    ///
    /// Keyed by SOURCE, not by a flat list, so one system re-publishing can never drop
    /// another system's pins — the classic "rebuild the whole list every tick" bug the
    /// retired TownHudBridge.PushMinimapPois had (it cleared and re-scanned the entire
    /// scene every tick; see TownHudBridge's own WO-380/403 note).
    ///
    /// The flat view is rebuilt LAZILY (only when a publish dirtied it), so a 10 Hz
    /// reader that polls an unchanged board allocates nothing.
    /// </summary>
    public static class RealmPinBoard
    {
        /// <summary>Visible-pin cap (WO-829 §3 "Cap visible pins; overflow +N").
        /// Surfaces draw at most this many and show <see cref="Overflow"/> as "+N".</summary>
        public const int MaxVisiblePins = 12;

        private static readonly Dictionary<string, List<RealmPin>> _sources =
            new Dictionary<string, List<RealmPin>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<RealmPin> _flat = new List<RealmPin>();
        private static bool _dirty = true;
        private static int _overflow;

        /// <summary>Raised after any publish/clear changes the board.</summary>
        public static event Action Changed;

        /// <summary>Every published pin, capped at <see cref="MaxVisiblePins"/>.
        /// Never null. Rebuilt only when a publish dirtied the board.</summary>
        public static IReadOnlyList<RealmPin> Pins
        {
            get { Rebuild(); return _flat; }
        }

        /// <summary>Pins beyond the visible cap ("+N"). Read AFTER <see cref="Pins"/>.</summary>
        public static int Overflow
        {
            get { Rebuild(); return _overflow; }
        }

        /// <summary>Replace <paramref name="sourceId"/>'s pins. A null/empty list clears
        /// that source. Copies the caller's list, so the producer may reuse its buffer.</summary>
        public static void Publish(string sourceId, IReadOnlyList<RealmPin> pins)
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                FlowTrace.Warn("RealmPins", "Publish called with an empty sourceId - ignored (a source must be nameable to be replaceable).");
                return;
            }

            if (pins == null || pins.Count == 0) { Clear(sourceId); return; }

            List<RealmPin> bucket;
            if (!_sources.TryGetValue(sourceId, out bucket))
            {
                bucket = new List<RealmPin>(pins.Count);
                _sources[sourceId] = bucket;
            }
            bucket.Clear();
            for (int i = 0; i < pins.Count; i++) bucket.Add(pins[i]);

            _dirty = true;
            FlowTrace.Throttle("RealmPins", "publish:" + sourceId, 1f,
                "source '" + sourceId + "' published " + bucket.Count + " pin(s).");
            Changed?.Invoke();
        }

        /// <summary>Drop one source's pins (it went away / has nothing to show).</summary>
        public static void Clear(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (!_sources.Remove(sourceId)) return;
            _dirty = true;
            FlowTrace.Step("RealmPins", "source '" + sourceId + "' cleared.");
            Changed?.Invoke();
        }

        /// <summary>Drop every source (scene teardown / regression hook).</summary>
        public static void ClearAll()
        {
            if (_sources.Count == 0) { _dirty = true; return; }
            _sources.Clear();
            _dirty = true;
            FlowTrace.Step("RealmPins", "board cleared (all sources).");
            Changed?.Invoke();
        }

        /// <summary>
        /// THE fog predicate (WO-829 acceptance: locked regions keep their secrets).
        /// Accepts the RegionState literal realm-map.json documents
        /// ("locked" | "discovered" | "cleared" | "threatened"). Only "locked" — and an
        /// unknown/absent state, which is treated as locked ON PURPOSE — hides detail.
        /// Fail-closed: a typo in a state string must never leak a spoiler.
        /// </summary>
        public static bool RevealsDetail(string regionState)
        {
            if (string.IsNullOrEmpty(regionState)) return false;
            switch (regionState.Trim().ToLowerInvariant())
            {
                case "discovered":
                case "cleared":
                case "threatened":
                    return true;
                default:
                    return false;   // "locked" and anything unrecognised
            }
        }

        private static void Rebuild()
        {
            if (!_dirty) return;
            _dirty = false;
            _flat.Clear();
            _overflow = 0;

            foreach (var kv in _sources)
            {
                var bucket = kv.Value;
                if (bucket == null) continue;
                for (int i = 0; i < bucket.Count; i++)
                {
                    if (_flat.Count < MaxVisiblePins) _flat.Add(bucket[i]);
                    else _overflow++;
                }
            }
        }
    }
}
