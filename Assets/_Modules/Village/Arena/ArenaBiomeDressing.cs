// =============================================================================
// ArenaBiomeDressing — biome BACKDROP selection + subtle per-biome PARTICLES for
// the BattleArena (WO-499 P1: "the visible wow").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// Two responsibilities, both pure-helper (no MonoBehaviour state):
//   1) ResolveBiome(context, threat) -> a biome KEY, with a THEME-BY-THREAT cycle
//      (WO-499 #3 danger gradient): forest = early/easy ... volcanic = the hard
//      family ... castle = tanky. The BackdropContext wins when it names a biome;
//      otherwise the threat tier picks one (the backdrop SIGNALS the difficulty).
//   2) BuildParticles(root, biome) -> ONE cheap, short-lived, looping ParticleSystem
//      parented to the arena root (auto torn down): forest=leaves/pollen, cave=motes,
//      volcanic/dungeon=embers, castle=dust, + a faint mist drift (WO-499 #2). Code-
//      built (no prefab), unlit additive/alpha, tiny rates -> near-zero perf.
//
// Backdrop FILENAMES (assigned by viewing the Grok art, WO-499):
//   forest   -> Resources/Arena/Backdrops/forest_backdrop   (runed forest, c1S70)
//   cavern   -> cavern_backdrop      (crystal cavern, LugGn)
//   ruins    -> ruins_backdrop       (statue courtyard, PNBkH)
//   volcanic -> volcanic_backdrop    (lava field, MxSKY)   <- the HARD-family backdrop
//   dungeon  -> dungeon_backdrop     (runed stone hall, Bh1tD)
//   castle   -> castle_backdrop      (castle courtyard, KTj1N)
// (9SBll firefly-pond DROPPED: a big foreground pond eats the kite space, WO-499.)
//
// Skip-safe (LogWarning, never throws into the fight); ASCII-only logs; instrumented
// per CLAUDE.md S12 (FlowTrace "BattleArena").
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Arena
{
    /// <summary>Static helper: biome resolution + cheap biome particles for the arena stage.</summary>
    public static class ArenaBiomeDressing
    {
        // Canonical biome keys (also the <key>_backdrop filename stem).
        public const string Forest   = "forest";
        public const string Cavern   = "cavern";
        public const string Ruins    = "ruins";
        public const string Volcanic = "volcanic";
        public const string Dungeon  = "dungeon";
        public const string Castle   = "castle";

        /// <summary>
        /// Resolve the biome key. An explicit biome in <paramref name="context"/> wins
        /// (alias-folded); legacy "outerworld" maps to forest. Otherwise the THREAT tier
        /// drives the danger-gradient cycle (WO-499 #3): low=forest/ruins, mid=cavern/
        /// dungeon, high=volcanic/castle. Deterministic-ish within a tier (threat-seeded).
        /// </summary>
        public static string ResolveBiome(string context, int threat)
        {
            string c = (context ?? "").ToLowerInvariant().Trim();

            // 1) Explicit biome / alias in the context wins.
            switch (c)
            {
                case Forest: case "outerworld": case "woods": case "grass": return Forest;
                case Cavern: case "cave": case "crystal": return Cavern;
                case Ruins: case "statue": case "temple": return Ruins;
                case Volcanic: case "lava": case "volcano": case "ember": return Volcanic;
                case Dungeon: case "crypt": case "hall": case "runed": return Dungeon;
                case Castle: case "keep": case "courtyard": case "fortress": return Castle;
            }

            // 2) Theme-by-threat danger gradient. Two biomes per tier, picked by parity
            //    so the same threat reads consistently but the set has variety.
            int t = Mathf.Max(0, threat);
            bool alt = (t & 1) == 1;
            if (t <= 2) return alt ? Ruins : Forest;     // early / easy
            if (t <= 5) return alt ? Dungeon : Cavern;   // mid: wizard-heavy caves / runed halls
            return alt ? Castle : Volcanic;              // hard: tanky castle / the volcanic family
        }

        /// <summary>
        /// Build ONE cheap looping ParticleSystem (per-biome flavour + faint mist) parented
        /// to <paramref name="arenaRoot"/> so it tears down with the stage. Skip-safe.
        /// </summary>
        public static void BuildParticles(Transform arenaRoot, string biome)
        {
            if (arenaRoot == null) return;
            Guard.Try("BattleArena", "build biome particles", () =>
            {
                string key = (biome ?? Forest).ToLowerInvariant();

                var host = new GameObject("[BiomeParticles_" + key + "]");
                host.transform.SetParent(arenaRoot, false);
                host.transform.localPosition = new Vector3(0f, 6f, 0f); // drift down over the kite floor

                // Per-biome flavour layer.
                BiomeFx fx = FxFor(key);
                BuildSystem(host.transform, "flavour", fx);

                // A faint, shared mist drift under everything (WO-499 #2 "+ mist").
                BuildSystem(host.transform, "mist", new BiomeFx
                {
                    color = new Color(0.75f, 0.78f, 0.82f, 0.06f),
                    rate = 4f, size = 9f, sizeJitter = 4f, lifetime = 9f,
                    gravity = -0.01f, speed = 0.25f, additive = false,
                });

                FlowTrace.Step("BattleArena", "BuildParticles: biome '" + key + "' flavour + mist (cheap, pooled with stage).");
            });
        }

        // ── per-biome particle recipe ────────────────────────────────────────────
        private struct BiomeFx
        {
            public Color color;
            public float rate;        // particles/sec (kept tiny for mobile)
            public float size;        // base size
            public float sizeJitter;  // +/- size variety
            public float lifetime;    // seconds (short -> "clears out fast", WO-496 #11)
            public float gravity;     // <0 falls, >0 rises (embers rise)
            public float speed;       // start speed
            public bool additive;     // additive (glowing motes/embers) vs alpha (leaves/dust)
        }

        private static BiomeFx FxFor(string key)
        {
            switch (key)
            {
                case Volcanic:
                case Dungeon:
                    // Embers: warm, glowing, drifting UP, short-lived.
                    return new BiomeFx { color = new Color(1f, 0.45f, 0.12f, 0.55f), rate = 10f, size = 0.12f, sizeJitter = 0.08f, lifetime = 3.5f, gravity = 0.06f, speed = 0.6f, additive = true };
                case Cavern:
                    // Glowing motes: cool cyan, near-still, faint.
                    return new BiomeFx { color = new Color(0.35f, 0.75f, 1f, 0.4f), rate = 8f, size = 0.1f, sizeJitter = 0.06f, lifetime = 6f, gravity = -0.005f, speed = 0.15f, additive = true };
                case Castle:
                    // Dust motes: pale, slow, alpha.
                    return new BiomeFx { color = new Color(0.85f, 0.82f, 0.72f, 0.18f), rate = 7f, size = 0.14f, sizeJitter = 0.08f, lifetime = 7f, gravity = -0.01f, speed = 0.2f, additive = false };
                case Ruins:
                    // Pollen/dust over the old stones: warm, drifting.
                    return new BiomeFx { color = new Color(0.95f, 0.9f, 0.65f, 0.22f), rate = 9f, size = 0.13f, sizeJitter = 0.07f, lifetime = 6.5f, gravity = -0.02f, speed = 0.25f, additive = true };
                default: // forest
                    // Leaves / pollen: greenish-gold, gentle fall.
                    return new BiomeFx { color = new Color(0.6f, 0.8f, 0.35f, 0.45f), rate = 9f, size = 0.16f, sizeJitter = 0.1f, lifetime = 7f, gravity = -0.04f, speed = 0.3f, additive = false };
            }
        }

        // Build a single looping ParticleSystem from a recipe over the kite footprint.
        private static void BuildSystem(Transform parent, string suffix, BiomeFx fx)
        {
            var go = new GameObject("Fx_" + suffix);
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = fx.lifetime;
            main.startSpeed = fx.speed;
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, fx.size - fx.sizeJitter), fx.size + fx.sizeJitter);
            main.startColor = fx.color;
            main.gravityModifier = fx.gravity;
            main.maxParticles = 120; // hard cap -> mobile-safe
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = fx.rate;

            // Box shape spread across the kite footprint (slightly wider than the floor).
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 1f, 56f);

            // Gentle fade-out so nothing pops away (alpha-over-lifetime tail).
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Unlit particle material (additive for glows, alpha-blend for dust/leaves). Skip-safe.
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (r != null)
            {
                var sh = Shader.Find(fx.additive ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    var mat = new Material(sh) { name = "BiomeFx_" + suffix };
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fx.color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", fx.color);
                    // Additive surface for the glowing biomes (embers/motes).
                    if (fx.additive)
                    {
                        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // transparent
                        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);     // additive
                    }
                    r.sharedMaterial = mat;
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = 0;
            }

            ps.Play(true);
        }
    }
}
