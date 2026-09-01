// =============================================================================
// TerrainLayerSet — THE single authority for the overworld ground layer contract.
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
// -----------------------------------------------------------------------------
// WO-1101 (owner 2026-08-17: "i want the textures for the world added. grass and
// simple aesthetics"), constrained by WO-1044 biome identity (all eleven rulings
// APPROVED 2026-08-17).
//
// ⛔ WHY THIS FILE EXISTS — the duplicated-index defect it closes.
// Until now the splat layer indices lived in TWO places that could not see each other:
//   * Assets/Editor/ExteriorTerrainBuilder.cs   (DeNelle.Editor)  — the BAKE authority,
//     which owned private consts LayerGrass/LayerStone/LayerMud/LayerSnow/LayerDead.
//   * Assets/_Modules/Village/World/WorldSceneLoader.cs (DeNelle.Village) — the DEF-108
//     RUNTIME repaint, which HARDCODED the literals 0 / 1 / 2 / 4.
// The runtime repaint is the ONLY splat the player sees on device (the baked alphamap
// did not survive into the player build — DEF-108). So growing the layer set in the
// builder alone repaints the ground WRONG **on device and nowhere else** — a defect
// class that is invisible in the editor and therefore invisible to every gate we run.
// DeNelle.Core is referenced by DeNelle.Editor, DeNelle.Village AND
// DeNelle.EditorRegression (read their .asmdef — CLAUDE.md §5), so this is the one
// place all three consumers can reach. There must never be a second table.
//
// ⚠ HOW THE VALUES WERE CHOSEN — read before "fixing" one.
// The owner is red/green colourblind (memory `owner-colorblind-delegate-visual-creative`),
// so the biomes are separated by VALUE + TEXTURE + LIGHT, never hue. TargetLuminance is
// Rec.709 luminance of the shipped BaseColor PNG, and TerrainLayerRegression asserts it.
// Curation baked the value INTO the PNG (a gamma grade at copy time) rather than relying
// on TerrainLayer.diffuseRemapMax, so what the oracle measures is literally what ships.
// =============================================================================
using System;

namespace DeNelle.Core.World
{
    /// <summary>
    /// One authored ground layer: which curated textures it uses, how it tiles, and the
    /// value/light targets that make it survive a greyscale check.
    /// </summary>
    public sealed class GroundLayerDef
    {
        /// <summary>Asset name of the generated <c>.terrainlayer</c> (no extension).</summary>
        public readonly string Name;
        /// <summary>File stem under <see cref="TerrainLayerSet.TextureFolder"/> — "&lt;Stem&gt;_BaseColor.png" / "_Normal.png".</summary>
        public readonly string TextureStem;
        /// <summary>World-metres per texture repeat. Larger = no legible repeat at play distance.</summary>
        public readonly float TileSize;
        /// <summary>Rec.709 luminance the shipped BaseColor must measure (±<see cref="TerrainLayerSet.LuminanceTolerance"/>).</summary>
        public readonly float TargetLuminance;
        /// <summary>TerrainLayer smoothness — the LIGHT axis. Mirewood is the one wet/specular ground.</summary>
        public readonly float Smoothness;
        /// <summary>Normal map strength — the TEXTURE axis. Stoneback carries the strongest relief.</summary>
        public readonly float NormalScale;
        /// <summary>Last-resort tint if the curated PNG is missing on this machine (fresh clone / un-fetched LFS).</summary>
        public readonly UnityEngine.Color FallbackTint;
        /// <summary>
        /// WO-1289 — ceiling on mean per-pixel CHROMA (max-min of the 8-bit RGB triple) of the
        /// shipped BaseColor, ±<see cref="TerrainLayerSet.ChromaTolerance"/>.
        /// ⚠ WHY THIS EXISTS, so nobody removes it as redundant with TargetLuminance:
        /// until 2026-09-01 the contract bounded VALUE and nothing else, so
        /// Ground_Meadow_BaseColor.png shipped at RGB 93/189/39 — chroma 150, 35% more
        /// saturated than any other layer and the ground the player actually stands on — and
        /// PASSED the oracle at luminance 0.620 against its authored 0.62. The owner reported
        /// it as "a bright neon green grass" while every gate stayed green. Luminance cannot
        /// see saturation; this is the second axis. Regrading the PNG without this bound just
        /// means the next authored texture does it again.
        /// </summary>
        public readonly float MaxChroma;

        public GroundLayerDef(string name, string textureStem, float tileSize, float targetLuminance,
                              float smoothness, float normalScale, UnityEngine.Color fallbackTint,
                              float maxChroma)
        {
            Name = name;
            TextureStem = textureStem;
            TileSize = tileSize;
            TargetLuminance = targetLuminance;
            Smoothness = smoothness;
            NormalScale = normalScale;
            FallbackTint = fallbackTint;
            MaxChroma = maxChroma;
        }
    }

    /// <summary>
    /// The overworld ground layer contract — indices, art, tiling and value targets.
    /// Consumed by the bake (ExteriorTerrainBuilder), the runtime repaint
    /// (WorldSceneLoader) and the oracle (TerrainLayerRegression).
    /// </summary>
    public static class TerrainLayerSet
    {
        // ── Tracked art location ─────────────────────────────────────────────
        // ⚠ TRACKED, deliberately. The Blink pack these were curated FROM
        // (Assets/Blink/, .gitignore:350) has ZERO tracked files, so a .terrainlayer
        // pointing straight at a Blink guid renders here and is colourless on every
        // other clone — the "pink floor" failure class (CLAUDE.md §12). The curated
        // copies under Assets/Generated/ are what ship.
        public const string TextureFolder = "Assets/Generated/Terrain/Layers";
        public const string BaseColorSuffix = "_BaseColor.png";
        public const string NormalSuffix = "_Normal.png";

        // ── Layer indices — THE contract. Never renumber; append only. ───────
        /// <summary>0 — the hub/default meadow. Whatever no march claims falls back here.</summary>
        public const int Meadow = 0;
        /// <summary>1 — Goldfields (EAST, tier 1): pale dry field, the brightest ground in the game.</summary>
        public const int GoldfieldsField = 1;
        /// <summary>2 — Stoneback (WEST, tier 2): faceted matte rock, strongest normal relief.</summary>
        public const int StonebackRock = 2;
        /// <summary>3 — Stoneback's snow patches: the only true whites in the frame (WO-1044 §1).</summary>
        public const int StonebackSnow = 3;
        /// <summary>4 — Mirewood (SOUTH, tier 3): crushed dark wet ground, the one specular biome.</summary>
        public const int MirewoodMire = 4;
        /// <summary>5 — Mirewood secondary: root-tangled mire. Texture variety WITHOUT value change.</summary>
        public const int MirewoodRoots = 5;
        /// <summary>6 — Ashwood (NORTH, tier 4): PALE powdery ash. See the inversion note below.</summary>
        public const int AshwoodAsh = 6;
        /// <summary>7 — roads + footpaths, stamped by PaintNaturalPaths.</summary>
        public const int PathDirt = 7;

        /// <summary>Number of splat layers. Both splat authorities size their arrays from this.</summary>
        public const int Count = 8;

        /// <summary>Oracle tolerance on <see cref="GroundLayerDef.TargetLuminance"/>.</summary>
        public const float LuminanceTolerance = 0.06f;

        /// <summary>Oracle tolerance on <see cref="GroundLayerDef.MaxChroma"/> (8-bit units). WO-1289.</summary>
        public const float ChromaTolerance = 5f;

        /// <summary>
        /// Minimum Rec.709 ΔL between the primary ground of any two ADJACENT marches.
        /// This is the colourblind gate turned into a measurement. Today's shipped tints
        /// fail it: grass L=0.447 vs stone L=0.521 is ΔL 0.074 — Goldfields and Stoneback
        /// are near-indistinguishable in greyscale right now.
        /// </summary>
        public const float MinAdjacentMarchDeltaL = 0.15f;

        // ─────────────────────────────────────────────────────────────────────
        //  ⚠ TWO CANON CORRECTIONS ARE BAKED INTO THE NUMBERS BELOW. READ BOTH.
        //
        //  (1) ASHWOOD IS INVERTED vs what shipped, ON PURPOSE.
        //      WO-1044 §1 authors Ashwood as "near-black trunks standing on a PALE powdery
        //      ground, like ink on ash … Greyscale test: two values only, plus two glows."
        //      The shipped Exterior_Dead layer was (0.20,0.17,0.16) → L=0.176, i.e. DARK
        //      ground — the exact opposite of ratified canon — and WorldSceneLoader painted
        //      that dark layer across the whole north quadrant. Ashwood's GROUND is lifted;
        //      the darkness belongs to the trunks and props, which is where the contrast
        //      canon asks for actually lives. This also fixes a second defect: Ashwood 0.176
        //      vs Mirewood 0.274 was ΔL 0.098 — two dark quadrants that did not separate.
        //
        //  (2) THE FOUR TARGETS ARE A STAIRCASE, and Ashwood/Stoneback are NOT the plan's
        //      literal 0.68 / 0.50. They cannot be. The marches sit on a COMPASS CYCLE
        //      (E→N→W→S→E), so every march is adjacent to two others and four values must
        //      alternate. With Goldfields at 0.74 (canon: the brightest place in the game)
        //      and Mirewood at 0.27 (canon: crushed, the narrowest range), ΔL ≥ 0.15 on all
        //      four adjacent pairs forces Ashwood ≈ 0.58 and Stoneback ≈ 0.42:
        //          E 0.74 → N 0.58 (Δ0.16) → W 0.42 (Δ0.16) → S 0.27 (Δ0.15) → E (Δ0.47)
        //      0.68/0.50 would have put Stoneback↔Ashwood at ΔL 0.18 but Goldfields↔Ashwood
        //      at ΔL 0.06 — a fail. Ashwood at 0.58 is still unambiguously "pale ground"
        //      against near-black trunks (internal Δ ≈ 0.5, canon's "two values"), and
        //      Stoneback at 0.42 still reads MID while strengthening canon's own
        //      "sky brighter than ground".
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>The eight authored layers, indexed by the constants above.</summary>
        public static readonly GroundLayerDef[] Layers =
        {
            // idx 0 — hub meadow. Lush, mid-high value; flows into the golden east.
            new GroundLayerDef("Ground_Meadow", "Ground_Meadow", 12f, 0.62f,
                0.08f, 0.7f, new UnityEngine.Color(0.28f, 0.52f, 0.22f),
                // WO-1289: shipped PNG regraded 150 -> 85 chroma at UNCHANGED luminance 0.6195.
                maxChroma: 92f),   // regraded PNG measures 85.0 by the suite

            // idx 1 — GOLDFIELDS (E). Brightest ground, LOWEST internal contrast — "a pale
            // page". Largest tile so no repeat is legible across the open field; flattest
            // normal of the four so the ground reads as texture, not modelling.
            new GroundLayerDef("Goldfields_Field", "Goldfields_Field", 15f, 0.74f,
                0.05f, 0.45f, new UnityEngine.Color(0.72f, 0.68f, 0.50f),
                maxChroma: 96f),   // measured 90.4 by the suite

            // idx 2 — STONEBACK (W). Mid value, HIGHEST local contrast, strongest normal:
            // the rock's own faceting does all the modelling under a flat overcast light.
            new GroundLayerDef("Stoneback_Rock", "Stoneback_Rock", 9f, 0.42f,
                0.06f, 1.35f, new UnityEngine.Color(0.42f, 0.41f, 0.38f),
                maxChroma: 106f),  // measured 100.4 by the suite

            // idx 3 — Stoneback snow patches. The only true whites in the game.
            new GroundLayerDef("Stoneback_Snow", "Stoneback_Snow", 16f, 0.90f,
                0.30f, 0.5f, new UnityEngine.Color(0.90f, 0.92f, 0.96f),
                maxChroma: 25f),   // measured 20.3 by the suite - the near-neutral white

            // idx 4 — MIREWOOD (S). Crushed dark, and the ONE ground that is specular:
            // canon's wet sheen is carried by smoothness, not by a hue.
            // 0.255 target (shipped PNG measures 0.262), NOT the 0.27 first authored. The ΔL oracle
            // caught 0.27 at Stoneback↔Mirewood = 0.147 — three thousandths under the 0.15 greyscale
            // bar, i.e. two ADJACENT marches the owner could not tell apart. Nudged Mirewood DOWN
            // rather than Stoneback up, because WO-1044 canon calls Mirewood "crushed": darker is
            // the more canon-true direction, so the fix strengthens the identity instead of
            // compromising it. Now 0.161. ⚠ Change this and you must re-grade the PNG — the value is
            // baked into the shipped texture, not applied via diffuseRemapMax, so the oracle measures
            // exactly what ships.
            new GroundLayerDef("Mirewood_Mire", "Mirewood_Mire", 6f, 0.255f,
                0.55f, 1.0f, new UnityEngine.Color(0.24f, 0.24f, 0.20f),
                maxChroma: 110f),  // measured 104.7 by the suite

            // idx 5 — Mirewood secondary. Same value band on purpose: Mirewood's identity is
            // the NARROWEST value range in the game, so its variety must be texture-only.
            new GroundLayerDef("Mirewood_Roots", "Mirewood_Roots", 6f, 0.25f,
                0.48f, 1.1f, new UnityEngine.Color(0.22f, 0.21f, 0.18f),
                maxChroma: 118f),  // measured 112.4 by the suite

            // idx 6 — ASHWOOD (N). PALE powdery ash (see correction 1). Dry, matte, flat
            // normal — silhouette does the work, so the ground must not compete.
            new GroundLayerDef("Ashwood_Ash", "Ashwood_Ash", 13f, 0.58f,
                0.04f, 0.5f, new UnityEngine.Color(0.58f, 0.56f, 0.53f),
                maxChroma: 92f),   // measured 86.6 by the suite

            // idx 7 — roads/footpaths. Must contrast HARD against Goldfields (Δ 0.44).
            new GroundLayerDef("Path_Dirt", "Path_Dirt", 6f, 0.30f,
                0.10f, 0.9f, new UnityEngine.Color(0.36f, 0.26f, 0.16f),
                maxChroma: 114f),  // measured 108.5 by the suite
        };

        /// <summary>
        /// The primary ground layer of each march. The four values here are what
        /// <see cref="MinAdjacentMarchDeltaL"/> is asserted across.
        /// </summary>
        public static int PrimaryLayerFor(RegionId region)
        {
            switch (region)
            {
                case RegionId.Goldfields: return GoldfieldsField;
                case RegionId.Stoneback:  return StonebackRock;
                case RegionId.Mirewood:   return MirewoodMire;
                case RegionId.Ashwood:    return AshwoodAsh;
                default:                  return Meadow;   // Village / centre
            }
        }

        /// <summary>
        /// The compass cycle, in order. Consecutive entries (wrapping) are the ADJACENT
        /// march pairs — E→N→W→S→E. Derived from ZoneManager's cardinals, not retyped as
        /// a second table of directions.
        /// </summary>
        public static readonly RegionId[] CompassCycle =
        {
            RegionId.Goldfields,  // East
            RegionId.Ashwood,     // North
            RegionId.Stoneback,   // West
            RegionId.Mirewood,    // South
        };

        /// <summary>Absolute asset path of a layer's BaseColor PNG.</summary>
        public static string BaseColorPath(int index) =>
            TextureFolder + "/" + Layers[index].TextureStem + BaseColorSuffix;

        /// <summary>Absolute asset path of a layer's Normal PNG.</summary>
        public static string NormalPath(int index) =>
            TextureFolder + "/" + Layers[index].TextureStem + NormalSuffix;

        /// <summary>Asset path of the generated .terrainlayer for a layer index.</summary>
        public static string TerrainLayerPath(int index) =>
            "Assets/Generated/Terrain/" + Layers[index].Name + ".terrainlayer";

        /// <summary>
        /// One-line manifest for the trace/capture diff, so a log read proves WHICH layer
        /// contract a run used (CLAUDE.md §12 — instrument, don't guess).
        /// </summary>
        public static string Manifest()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("layers=").Append(Count).Append(" [");
            for (int i = 0; i < Layers.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(i).Append(':').Append(Layers[i].Name)
                  .Append(" L=").Append(Layers[i].TargetLuminance.ToString("0.00"))
                  .Append(" tile=").Append(Layers[i].TileSize.ToString("0"));
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
