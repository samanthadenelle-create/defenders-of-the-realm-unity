// =============================================================================
// RealmAtmosphereStyle — the SHARED presentation table for realm biomes + content
// pins (WO-829 §1/§2/§3, program WO-825).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// WHY IT LIVES HERE AND NOT IN EITHER SURFACE:
// two surfaces draw the same world — the parchment Realm Map (DeNelle.Village's
// RealmMapPanel) and the corner minimap (DeNelle.HUD's HudMinimapWidget) — and
// those assemblies must never reference each other (§5 cross-assembly law). If
// each owned its own biome palette they WOULD drift, and the swamp would be two
// different teals depending on which screen you opened. One table, both readers.
// It sits beside ElarionUi because that is where the project's palette already
// lives; RealmPins (Core.World) stays pure data with no colour in it.
//
// ⛔ COLOURBLIND LAW (CLAUDE.md §7 / owner is red/green colourblind):
// EVERY style here carries a GLYPH and a LABEL alongside its tint, and every pin
// kind carries a distinct SILHOUETTE. Colour is the third channel, never the
// first. A caller that draws only the tint has used this table wrongly —
// desaturate the screen and the map must still parse.
//
// The forest/swamp/ice/fire/cosmic/home token set is verbatim from
// Data/Canonical/realm-map.json's `biome` fields (WO-829 §2 table). An UNKNOWN
// token falls back to a neutral parchment style and self-reports (no-silent-
// failure law) rather than rendering an invisible node.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.World;

namespace DeNelle.Core.UI
{
    /// <summary>The silhouette a pin is drawn as. SHAPE is the primary channel —
    /// these seven forms stay distinguishable in full greyscale.</summary>
    public enum RealmPinShape
    {
        /// <summary>Filled disc — the player ("you are here").</summary>
        Circle,
        /// <summary>Rounded square rotated 45 deg — the navigation objective.
        /// Deliberately the SAME language HudCompassWidget uses for its objective
        /// marker, so the compass and the minimap agree at a glance.</summary>
        Diamond,
        /// <summary>Apex-up triangle — threat. Same silhouette as the compass enemy pip.</summary>
        TriangleUp,
        /// <summary>Hollow ring — a raid camp (a place you go TO, not a thing chasing you).</summary>
        Ring,
        /// <summary>Hard square — a dungeon portal.</summary>
        Square,
        /// <summary>Wide bar — a rumor (reads like a scrap of paper).</summary>
        BarHorizontal,
        /// <summary>Tall bar — an army muster / barracks (reads like a banner).</summary>
        BarVertical,
    }

    /// <summary>One biome's map treatment: node ring tint + a glyph + a short epithet.</summary>
    public readonly struct RealmBiomeStyle
    {
        /// <summary>The canonical biome token this style renders ("forest", "home", ...).</summary>
        public readonly string Token;
        /// <summary>Node ring tint (the THIRD channel — never the only one).</summary>
        public readonly Color Ring;
        /// <summary>Single ASCII glyph for the node face. ASCII ONLY — the build's
        /// LiberationSans SDF renders tofu for anything else (the compass learned this
        /// the hard way with the degree sign).</summary>
        public readonly string Glyph;
        /// <summary>Short epithet shown under/next to the node ("choked forest").</summary>
        public readonly string Epithet;

        /// <summary>Constructs an immutable biome style from all fields.</summary>
        public RealmBiomeStyle(string token, Color ring, string glyph, string epithet)
        {
            Token = token ?? "";
            Ring = ring;
            Glyph = string.IsNullOrEmpty(glyph) ? "?" : glyph;
            Epithet = epithet ?? "";
        }
    }

    /// <summary>One pin kind's treatment: silhouette + tint + the always-present label.</summary>
    public readonly struct RealmPinStyle
    {
        /// <summary>The silhouette (primary channel).</summary>
        public readonly RealmPinShape Shape;
        /// <summary>Tint (third channel).</summary>
        public readonly Color Tint;
        /// <summary>Legend/detail copy — ALWAYS drawn somewhere, so meaning survives
        /// full desaturation.</summary>
        public readonly string Label;

        /// <summary>Constructs an immutable pin style from all fields.</summary>
        public RealmPinStyle(RealmPinShape shape, Color tint, string label)
        {
            Shape = shape;
            Tint = tint;
            Label = label ?? "";
        }
    }

    /// <summary>The shared biome/pin presentation table (see header).</summary>
    public static class RealmAtmosphereStyle
    {
        // ── Biome palette (WO-829 §2 table, owner's tokens) ────────────────────
        // The owner is red/green colourblind and does not pick hues (memory
        // owner-colorblind-delegate-visual-creative), so these are chosen to survive a
        // greyscale check: the six LUMINANCES are spread deliberately, and the glyph
        // carries the identity regardless.
        private static readonly RealmBiomeStyle Forest =
            new RealmBiomeStyle("forest", new Color(0.36f, 0.62f, 0.34f, 1f), "T", "choked green");
        private static readonly RealmBiomeStyle Swamp =
            new RealmBiomeStyle("swamp", new Color(0.24f, 0.48f, 0.46f, 1f), "S", "drowned mire");
        private static readonly RealmBiomeStyle Ice =
            new RealmBiomeStyle("ice", new Color(0.68f, 0.82f, 0.92f, 1f), "I", "locked cold");
        private static readonly RealmBiomeStyle Fire =
            new RealmBiomeStyle("fire", new Color(0.88f, 0.50f, 0.22f, 1f), "E", "magma-veined");
        private static readonly RealmBiomeStyle Cosmic =
            new RealmBiomeStyle("cosmic", ElarionUi.Aether, "R", "thin sky");
        private static readonly RealmBiomeStyle Home =
            new RealmBiomeStyle("home", ElarionUi.Gilt, "H", "the last green sanctuary");
        private static readonly RealmBiomeStyle Unknown =
            new RealmBiomeStyle("", ElarionUi.ParchmentDim, "?", "");

        /// <summary>The style for a biome token from realm-map.json. An unknown token
        /// self-reports and yields the neutral parchment style — never an invisible node.</summary>
        public static RealmBiomeStyle Biome(string token)
        {
            switch ((token ?? "").Trim().ToLowerInvariant())
            {
                case "forest": return Forest;
                case "swamp":  return Swamp;
                case "ice":    return Ice;
                case "fire":   return Fire;
                case "cosmic": return Cosmic;
                // The home base is not a region and carries no `biome` field; callers pass
                // "home" (or the home id) for the gilt heart/tree crest treatment.
                case "home":
                case "avalon":
                    return Home;
                case "":
                    return Unknown;
                default:
                    FlowTrace.Once("RealmMap", "biome:" + token,
                        "unknown biome token '" + token + "' - neutral parchment style used. " +
                        "Add it to RealmAtmosphereStyle.Biome when the region lands.");
                    return Unknown;
            }
        }

        /// <summary>Convenience: the style for a region id, reading its biome off the
        /// catalog. Home resolves to the gilt crest.</summary>
        public static RealmBiomeStyle BiomeForRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return Unknown;
            var home = RealmMapCatalog.Home;
            if (home != null && home.Id == regionId) return Home;
            var def = RealmMapCatalog.Find(regionId);
            return def != null ? Biome(def.Biome) : Unknown;
        }

        /// <summary>
        /// Every biome token that resolves to a REAL style row (i.e. everything
        /// <see cref="Biome"/> answers without falling back to neutral parchment).
        /// Exposed so <c>RealmMapRegression</c> can assert the other direction: that every
        /// token AUTHORED in realm-map.json is in here. Without that check a new region
        /// ships as a grey "?" node and nothing fails — the FlowTrace.Once fires into a log
        /// nobody is reading at authoring time.
        /// </summary>
        public static readonly string[] KnownBiomeTokens =
            { "forest", "swamp", "ice", "fire", "cosmic", "home", "avalon" };

        /// <summary>True when <paramref name="token"/> has an authored style row (see
        /// <see cref="KnownBiomeTokens"/>). Case/whitespace-insensitive, matching
        /// <see cref="Biome"/>'s own normalisation so the two can never disagree.</summary>
        public static bool IsKnownBiome(string token)
        {
            var t = (token ?? "").Trim().ToLowerInvariant();
            if (t.Length == 0) return false;
            for (int i = 0; i < KnownBiomeTokens.Length; i++)
                if (KnownBiomeTokens[i] == t) return true;
            return false;
        }

        // ── Pin table (WO-829 §3) ─────────────────────────────────────────────
        /// <summary>The style for a pin kind. Total — every enum member has a row, so a
        /// new kind cannot silently render as nothing.</summary>
        public static RealmPinStyle Pin(RealmPinKind kind)
        {
            switch (kind)
            {
                case RealmPinKind.You:
                    return new RealmPinStyle(RealmPinShape.Circle, ElarionUi.Gilt, "You");
                case RealmPinKind.Objective:
                    return new RealmPinStyle(RealmPinShape.Diamond, ElarionUi.Gilt, "Objective");
                case RealmPinKind.Threat:
                    return new RealmPinStyle(RealmPinShape.TriangleUp, ElarionUi.Danger, "Threat");
                case RealmPinKind.RaidTarget:
                    return new RealmPinStyle(RealmPinShape.Ring, ElarionUi.Parchment, "Raid camp");
                case RealmPinKind.Dungeon:
                    return new RealmPinStyle(RealmPinShape.Square, ElarionUi.Aether, "Dungeon");
                case RealmPinKind.Rumor:
                    return new RealmPinStyle(RealmPinShape.BarHorizontal, ElarionUi.ParchmentDim, "Rumor");
                case RealmPinKind.Army:
                    return new RealmPinStyle(RealmPinShape.BarVertical, ElarionUi.StoneTrim, "Army");
                default:
                    return new RealmPinStyle(RealmPinShape.Circle, ElarionUi.ParchmentDim, kind.ToString());
            }
        }

        /// <summary>
        /// The pin silhouette as a single ASCII character, for a surface that draws TEXT
        /// rather than geometry (the parchment map's in-disc pin strip).
        ///
        /// WHY ASCII AND WHY HERE: the minimap draws real shapes because it has the room;
        /// the map's node discs do NOT — a marker there is ~16 ref px and must live INSIDE
        /// the node's published WO-941 footprint, where a squashed triangle is a smudge.
        /// One glyph per SHAPE (not per kind) keeps the two surfaces speaking the same
        /// vocabulary from one table, instead of the map growing a private shape library
        /// that drifts from <see cref="Pin"/>. ASCII only — the build's LiberationSans SDF
        /// tofus everything else.
        /// </summary>
        public static string PinAscii(RealmPinShape shape)
        {
            switch (shape)
            {
                case RealmPinShape.Circle:        return "o";
                case RealmPinShape.Diamond:       return "+";
                case RealmPinShape.TriangleUp:    return "^";
                case RealmPinShape.Ring:          return "O";
                case RealmPinShape.Square:        return "#";
                case RealmPinShape.BarHorizontal: return "=";
                case RealmPinShape.BarVertical:   return "|";
                default:                          return ".";
            }
        }

        /// <summary>Convenience: <see cref="PinAscii(RealmPinShape)"/> for a pin KIND.</summary>
        public static string PinAscii(RealmPinKind kind) => PinAscii(Pin(kind).Shape);

        // ── Withering / danger edge (WO-829 §1) ───────────────────────────────
        /// <summary>The corrupted edge-band tint for the parchment border, and the same
        /// tint the minimap uses for its rim so the two surfaces read as one world.
        /// Atmospheric ONLY — realm-map.json's own comment forbids a punishing timer
        /// ("the cozy covenant forbids FOMO countdowns").</summary>
        public static readonly Color WitheringEdge = new Color(0.18f, 0.09f, 0.16f, 0.92f);

        /// <summary>One line of canon for the Withering band. Elarion, never Avalon
        /// (DESIGN-DECISIONS #1 / CLAUDE.md §7).</summary>
        public const string WitheringLore =
            "The Withering creeps in from the Wound. Elarion is the last green sanctuary.";

        /// <summary>Rim tint for a ZoneManager danger tier (0 = home .. 4 = deepest).
        /// VISUAL ONLY — the region chip's TEXT is what actually carries the tier, so
        /// this can be ignored entirely and nothing is lost (colourblind law).</summary>
        public static Color DangerRim(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 4))
            {
                case 0:  return ElarionUi.Gilt;                              // home
                case 1:  return new Color(0.62f, 0.60f, 0.32f, 1f);
                case 2:  return new Color(0.68f, 0.48f, 0.26f, 1f);
                case 3:  return new Color(0.62f, 0.30f, 0.30f, 1f);
                default: return WitheringEdge;                               // the Wound's edge
            }
        }
    }
}
