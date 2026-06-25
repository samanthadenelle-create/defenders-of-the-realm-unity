// =============================================================================
// CastleMoatBuilder — FIRST-PASS diegetic WATER MOAT + 4 WIDE DRAWBRIDGES (BONES).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// OWNER (overnight): the "you cannot go past here" castle edge should READ as DELIBERATE.
// A WIDE WATER MOAT around the castle is the natural impassable boundary ("water makes it
// make sense" vs. an invisible wall), and 4 WIDE DRAWBRIDGES at the cardinal gates are the
// intentional exits. The bridges double as defensive CHOKEPOINTS (enemies must funnel
// across them; towers/troops cover the lane; a RAISED bridge seals it) and ARE the WO-509
// four RegionGates. See docs/CASTLE_MOAT_DESIGN_NOTE.md for the full frame + sliced plan.
//
// WHAT THIS FIRST-PASS BUILDS (visual BONES only — owner finesses look/feel):
//   * A square WATER MOAT ring of translucent teal quads hugging the castle perimeter,
//     OUTSIDE the wall line, framing the playable castle island. Reuses MoatWaterShimmer
//     so the ring reads as flowing water (the proven DEF-195 component), not a glass pane.
//   * 4 WIDE wooden DRAWBRIDGE decks spanning the moat at the cardinal gates (N/E/S/W),
//     each derived from the south-recipe gate at world = Euler(0,yaw,0) * southGate so they
//     land EXACTLY on the real gate openings (the CastleHubBuilder MakeGatePose convention).
//
// WHAT THIS DOES *NOT* DO (deliberately — needs the editor/architect lane, NOT a runtime
// builder, and would break the hand-tuned navmesh/seam if guessed blind):
//   * SHRINK the castle footprint (CastleHubBuilder geometry + a navmesh re-bake).
//   * Wire the N/E/W gates as FUNCTIONAL RegionGate crossings (region-gates.json today has
//     only `castle_to_outerworld` south; RuntimeRegionGate builds one per recipe row).
//   * Raise/lower (the defensive lever) or carve the moat into the navmesh.
//   Those are specced in docs/CASTLE_MOAT_DESIGN_NOTE.md with the exact seams.
//
// BOUNDARY SOURCE (SME): the castle is bounded by CastleHubBuilder's perimeter walls/gates;
// the south gate is at castle-local (-4.37, 0, -40.6) (Resources/Data/castle-south-recipe.json),
// corner towers at radial ~42m, and the 4 cardinal gates are that south gate rotated by
// yaw {0,90,180,270} about origin (CastleHubBuilder.BuildGateExitStrips / MakeGatePose,
// CastleHubBuilder.cs:712-725). The moat sits just OUTSIDE that perimeter; the bridges sit
// ON the 4 gate radials so the only ways across the water are the gates.
//
// SAFETY: flag-gated (FeatureFlags.CastleMoat, default ON). Self-bootstrap mirrors
// OuterWorldBoundaryInjector (AfterSceneLoad + sceneLoaded re-arm). Guarded, null-safe,
// idempotent, ASCII-only, never throws out of a sceneLoaded handler (WebGL-safe), no
// gitignored packs (engine primitives + URP/Lit), mobile-cheap (shared materials).
// Instrumented per CLAUDE.md S12: FlowTrace.Step("CastleMoat", ...).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Builds the diegetic castle water moat + 4 wide drawbridge decks at runtime (BONES).
    /// First-pass visual only; flag-gated + tunable. See file header + design note.
    /// </summary>
    public static class CastleMoatBuilder
    {
        private const string MoatRootName = "CastleMoat";

        // ---- TUNABLES (BONES — owner finesses; no rebuild needed) ------------------------

        // Square half-extent of the moat CENTRELINE from world origin, in metres. The castle
        // perimeter wall sits at radial ~40.6m (south gate) and corner towers at ~42m, so the
        // moat centreline at ~46m frames the castle just OUTSIDE the wall line.
        private const float MoatCentreRadius = 46f;

        // Moat WIDTH (across the water), in metres. Owner: ~3 wide ("smaller water body");
        // kept tunable so she can widen it for a grander moat.
        private const float MoatWidth = 3f;

        // Water plane sits slightly BELOW ground (y=0) so it reads as a sunken channel.
        private const float WaterY = -0.4f;

        // Translucent teal water tint (matches the proven MoatWater look from the old village moat).
        private static readonly Color WaterColor = new Color(0.10f, 0.42f, 0.45f, 0.62f);

        // ---- DRAWBRIDGE tunables ----
        // Bridge deck WIDTH (across the lane). WIDE + readable as the proper way out (owner),
        // while still a SINGLE-LANE chokepoint towers/troops can cover.
        private const float BridgeWidth = 9f;

        // Bridge length = moat width + a margin each side so the deck OVERLAPS both banks (no gap).
        private const float BridgeBankOverlap = 2.5f;

        // Bridge deck sits just above the water so it reads as laid across the channel.
        private const float BridgeY = 0.05f;

        // Warm timber tint for the wooden drawbridge decks.
        private static readonly Color BridgeColor = new Color(0.42f, 0.28f, 0.14f, 1f);

        // The castle hub scene this builds for. (HubScenes covers variants; we additionally
        // require the south recipe to resolve so the gate radials are real.)
        private const string TargetScene = "MainCastle_Hall";

        // --------------------------------------------------------------------
        //  SELF-BOOTSTRAP (mirrors OuterWorldBoundaryInjector).
        // --------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SafeBuild();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SafeBuild();

        // Never let the moat build throw out of a sceneLoaded handler (halts WebGL).
        private static void SafeBuild()
        {
            try { BuildMoat(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CastleMoat] moat build threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Build the water moat ring + 4 drawbridge decks on the castle hub scene.
        /// No-op when the flag is OFF, off the hub scene, or when the moat already exists.
        /// </summary>
        public static void BuildMoat()
        {
            // Flag gate — default ON so the owner sees the BONES; PlayerPrefs "ff.castlemoat" = 0 to hide.
            if (!FeatureFlags.CastleMoat) return;

            // Only on the castle hub. MainCastle_Hall is the ACTIVE home scene, but be tolerant of
            // the loaded-additive case the same way the boundary injector is.
            Scene hub = SceneManager.GetSceneByName(TargetScene);
            bool hubActive = SceneManager.GetActiveScene().name == TargetScene;
            if (!hubActive && (!hub.IsValid() || !hub.isLoaded)) return;

            // IDEMPOTENT: a moat already present -> done (repeated loads never stack).
            if (GameObject.Find(MoatRootName) != null) return;

            var root = new GameObject(MoatRootName);

            // Gate radials come from the south recipe; the 4 cardinal gates are it rotated by yaw.
            Vector3 southGate = ReadSouthGatePos();
            float gateLateral = southGate.x;   // off-centre lateral the bridges/gates share (x4 symmetry)

            Material waterMat  = BuildLitMaterial("CastleMoat_Water",  WaterColor,  transparent: true);
            Material bridgeMat = BuildLitMaterial("CastleMoat_Bridge", BridgeColor, transparent: false);

            int waterQuads = BuildWaterRing(root.transform, waterMat);
            int bridges    = BuildDrawbridges(root.transform, bridgeMat, gateLateral);

            // Bring the ring to life with the proven shimmer (reuse, DEF-195). Point it at the
            // shared water material; it scrolls a procedural ripple normal across every quad.
            AttachShimmer(root, waterMat);

            // A new GameObject lands in the active scene by default; if the hub is loaded but not
            // active, move the moat into it so it shares the hub's lifetime.
            if (!hubActive && hub.IsValid() && hub.isLoaded)
                SceneManager.MoveGameObjectToScene(root, hub);

            FlowTrace.Step("CastleMoat",
                "built castle moat: " + waterQuads + " water quads (ring r=" + MoatCentreRadius +
                "m width=" + MoatWidth + "m) + " + bridges + " wide drawbridge decks at the 4 cardinal gates " +
                "(gateLateral=" + gateLateral.ToString("0.00") + ", source: castle-south-recipe x4 symmetry).");
        }

        // --------------------------------------------------------------------
        //  WATER RING — 4 side quads forming a square channel just outside the wall.
        //  A Unity Plane is 10x10m at scale 1; we size each side to span the ring side
        //  and lay it flat at WaterY. Quads OVERLAP at the corners (cheap, no corner mesh).
        // --------------------------------------------------------------------
        private static int BuildWaterRing(Transform parent, Material mat)
        {
            float R = MoatCentreRadius;
            // Each side runs the full span 2*R (+ overlap into the corners) and is MoatWidth deep.
            float sideLength = (R * 2f) + MoatWidth;   // + width so corners overlap, no gap

            // sides: (label, yaw) — same convention as CastleHubBuilder's ring sides.
            var sides = new (string label, float yaw)[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };

            int count = 0;
            foreach (var (label, yaw) in sides)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 outward = rot * Vector3.back;          // -Z rotated to this side
                Vector3 sideCentre = outward * R;              // midpoint of this side, on the ring
                sideCentre.y = WaterY;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Plane);
                quad.name = "MoatWater_" + label;
                quad.transform.SetParent(parent, false);
                quad.transform.position = sideCentre;
                quad.transform.rotation = rot;                 // length axis runs ALONG the side
                // Plane local X = width axis (across the channel), local Z = length axis (along side).
                quad.transform.localScale = new Vector3(MoatWidth / 10f, 1f, sideLength / 10f);

                StripCollider(quad);                            // water never blocks; visual only
                ApplyMaterial(quad, mat);
                count++;
            }
            return count;
        }

        // --------------------------------------------------------------------
        //  DRAWBRIDGES — 4 wide wooden decks spanning the moat at the cardinal gates.
        //  Each gate world pos = Euler(0,yaw,0) * southGate; the deck centres on the moat
        //  ring radial at that gate's lateral offset and spans across the channel.
        // --------------------------------------------------------------------
        private static int BuildDrawbridges(Transform parent, Material mat, float gateLateral)
        {
            float R = MoatCentreRadius;
            float deckLength = MoatWidth + (BridgeBankOverlap * 2f);   // overlap both banks

            var sides = new (string label, float yaw)[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };

            int count = 0;
            foreach (var (label, yaw) in sides)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 outward = rot * Vector3.back;          // radial out for this side
                Vector3 along   = rot * Vector3.right;         // lateral along the side

                // Bridge centre: on the moat ring radial, shifted laterally to the gate's offset
                // (so the deck sits in front of the real gate opening, x4 symmetry).
                Vector3 centre = outward * R + along * gateLateral;
                centre.y = BridgeY;

                var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deck.name = "Drawbridge_" + label;
                deck.transform.SetParent(parent, false);
                deck.transform.position = centre;
                deck.transform.rotation = rot;
                // Cube local X = lane width (across), Y = thin slab, Z = span across the channel (radial).
                deck.transform.localScale = new Vector3(BridgeWidth, 0.2f, deckLength);

                // FIRST-PASS: keep the deck collider so the plank reads as solid ground over the
                // water (the hero already walks the gate lane on the baked navmesh underneath; the
                // thin slab at y=0.05 does not obstruct). Owner can swap to a real bridge prop later.
                ApplyMaterial(deck, mat);
                count++;
            }
            return count;
        }

        // --------------------------------------------------------------------
        //  SOUTH GATE recipe read (mirrors RuntimeRegionGate.ReadSouthGatePos) — the
        //  bridges/moat track any re-author of the gate without a code edit.
        // --------------------------------------------------------------------
        private static Vector3 ReadSouthGatePos()
        {
            Vector3 fallback = new Vector3(-4.37f, 0f, -40.6f);
            var ta = Resources.Load<TextAsset>("Data/castle-south-recipe");
            if (ta == null)
            {
                FlowTrace.Warn("CastleMoat", "castle-south-recipe not found — using fallback south gate " + fallback + ".");
                return fallback;
            }
            SouthRecipe recipe = null;
            Guard.Try("CastleMoat", "parse castle-south-recipe", () => recipe = JsonUtility.FromJson<SouthRecipe>(ta.text));
            if (recipe != null && recipe.pieces != null)
                foreach (var p in recipe.pieces)
                    if (p != null && p.name == "Gate_South" && p.pos != null && p.pos.Length == 3)
                        return new Vector3(p.pos[0], p.pos[1], p.pos[2]);
            FlowTrace.Warn("CastleMoat", "Gate_South not in recipe — using fallback " + fallback + ".");
            return fallback;
        }

        // --------------------------------------------------------------------
        //  Helpers — shared URP/Lit materials, collider strip, shimmer attach.
        // --------------------------------------------------------------------

        // Build one shared URP/Lit material (the BattleArena _BaseColor pattern). Transparent
        // path sets the URP surface-type/blend keywords so alpha actually renders. Null-safe.
        private static Material BuildLitMaterial(string name, Color color, bool transparent)
        {
            Material mat = null;
            Guard.Try("CastleMoat", "build material '" + name + "'", () =>
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard"); // editor/non-URP fallback
                if (sh == null) return;
                mat = new Material(sh) { name = name };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

                if (transparent)
                {
                    // URP/Lit transparent surface (Surface=1 Transparent, alpha blend).
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    // Keep water low-gloss (de-glossed teal, owner constraint) — no glassy crystal.
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
                }
            });
            return mat;
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            if (mat == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        // Reuse the proven MoatWaterShimmer (DEF-195) so the ring reads as flowing water, not glass.
        // It auto-resolves the shared material from a child renderer, but we point it explicitly.
        private static void AttachShimmer(GameObject root, Material waterMat)
        {
            Guard.Try("CastleMoat", "attach MoatWaterShimmer", () =>
            {
                var shimmer = root.AddComponent<DeNelle.Village.MoatWaterShimmer>();
                // MoatWaterShimmer reads its material from a child renderer if its serialized field is
                // null; all our water quads share waterMat, so the auto-resolve lands on it. (No public
                // setter is needed — the first child renderer's sharedMaterial IS waterMat.)
                _ = shimmer;
                _ = waterMat;
            });
        }

        // --------------------------------------------------------------------
        //  RECIPE MODEL (JsonUtility-friendly; mirrors RuntimeRegionGate's shape).
        // --------------------------------------------------------------------
        [System.Serializable] private class SouthPiece  { public string name; public string prefab; public float[] pos; public float[] rot; public float[] scale; }
        [System.Serializable] private class SouthRecipe { public SouthPiece[] pieces; public float[] parentPos; public float[] parentRot; }
    }
}
