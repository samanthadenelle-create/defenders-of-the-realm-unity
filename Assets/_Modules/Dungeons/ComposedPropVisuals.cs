// =============================================================================
// ComposedPropVisuals -- the ONE runtime "lit primitive" body builder for composed
// (Pipeline A) dungeon pillars (WO-1112).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// THE DEFECT THIS FIXES: DungeonBaker places keys, locks, traps and oil stones as
// BARE GameObjects -- a transform, a trigger collider and a MonoBehaviour, with no
// Renderer anywhere (DungeonBaker.PlaceComposeKeys / PlaceComposeLocks /
// PlaceComposeTraps / PlaceComposeOilStones). They are fully functional and
// COMPLETELY INVISIBLE in a player build. ComposedTrapHazard's own header promises
// "an optional particle telegraph"; the only thing it ever drew was an OnDrawGizmos
// inside #if UNITY_EDITOR, which does not render in a build at all.
//
// The KEY and the LOCK are the ship-blocker of the four: a floor is gated behind a
// key the player cannot see, so a run can HARD-STALL with nothing on screen to
// explain it.
//
// WHY RUNTIME AND NOT BAKE-TIME: every dg_* scene on disk is ALREADY baked. A fix in
// DungeonBaker would need a re-bake before the owner saw a single key, and re-baking
// dungeon scenes is its own hazard (the DungeonCompose NUL-corruption history). This
// is the same runtime-bootstrap idiom DungeonExitSpawner and ComposedDungeonBootstrap
// already use for exactly this reason -- it covers the scenes on disk with NO re-bake,
// and every future bake automatically. It is also idempotent: a body is built only
// when the object has no Renderer of its own, so a future bake-time art pass silently
// wins over it with no code change here.
//
// ART IDIOM, NOT NEW ART: URP/Lit (Standard fallback) primitives with an emissive
// tint -- the identical shape DungeonBaker.TintFallbackHeroBody already uses for the
// hero stand-in. No prefab, no material asset, no new dependency, and every piece is
// emissive so it reads inside an unlit dungeon even at low lantern oil.
//
// COLOURBLIND LAW: each prop is identified by its SHAPE and its MOTION -- a spinning
// bobbing key silhouette, a flat door plate with a keyhole, a floor pad, a squat
// pedestal. The tints reinforce; they never carry the meaning alone.
//
// Every primitive's own collider is STRIPPED. These are decoration hung under a live
// trigger volume; leaving the primitive colliders on would block the hero, and on the
// key it would shadow the trigger sphere the pickup depends on.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>Builds the runtime visual body for composed-dungeon pillars.</summary>
    internal static class ComposedPropVisuals
    {
        private const string Sys = "ComposedProp";

        /// <summary>Name of the child root every built body hangs under (idempotency marker).</summary>
        public const string BodyName = "Visual";

        // ── Tints (hex parsed, same idiom as DungeonBaker.TintFallbackHeroBody) ──
        private const string GoldHex = "#d8a93b";   // key: warm brass
        private const string IronHex = "#4a4a52";   // lock plate: cold iron
        private const string HazardHex = "#8c3320"; // trap pad: dull rust
        private const string OilHex = "#c98a3a";    // oil stone: lantern amber

        /// <summary>
        /// True when this object already has a visible body — either a baked Renderer or a
        /// previously built one. The guard that makes every builder below idempotent AND lets
        /// a future bake-time art pass take precedence with no code change.
        /// </summary>
        public static bool HasBody(GameObject go)
        {
            if (go == null) return true;   // nothing to build on; treat as done
            return go.GetComponentInChildren<Renderer>(true) != null;
        }

        /// <summary>
        /// A floating brass KEY: a ring (flattened cylinder, laid on its side) with a shaft and
        /// two bit teeth. Spins and bobs (ComposedPropSpin) so it reads as a pickup from across
        /// a dark room. The silhouette is the identifier, not the colour.
        /// </summary>
        public static void BuildKey(GameObject host, float scale = 1f)
        {
            if (HasBody(host)) return;
            Guard.Try(Sys, $"build key body on '{host.name}'", () =>
            {
                var body = NewBody(host);
                Color gold = Hex(GoldHex);

                // Ring head — a cylinder squashed to a disc and tipped on its side.
                var ring = Prim(body, "Ring", PrimitiveType.Cylinder, gold);
                ring.transform.localPosition = new Vector3(0f, 0.20f, 0f) * scale;
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(0.26f, 0.04f, 0.26f) * scale;

                // Shaft.
                var shaft = Prim(body, "Shaft", PrimitiveType.Cube, gold);
                shaft.transform.localPosition = new Vector3(0f, -0.05f, 0f) * scale;
                shaft.transform.localScale = new Vector3(0.06f, 0.34f, 0.06f) * scale;

                // Two bit teeth — the detail that makes the silhouette read as a KEY and not a
                // lollipop, which matters far more than the tint in a dark room.
                var toothA = Prim(body, "BitA", PrimitiveType.Cube, gold);
                toothA.transform.localPosition = new Vector3(0.08f, -0.14f, 0f) * scale;
                toothA.transform.localScale = new Vector3(0.13f, 0.05f, 0.05f) * scale;

                var toothB = Prim(body, "BitB", PrimitiveType.Cube, gold);
                toothB.transform.localPosition = new Vector3(0.08f, -0.22f, 0f) * scale;
                toothB.transform.localScale = new Vector3(0.13f, 0.05f, 0.05f) * scale;

                var spin = body.AddComponent<ComposedPropSpin>();
                spin.Configure(degreesPerSecond: 70f, bobAmplitude: 0.12f, bobHz: 0.5f);

                FlowTrace.Step(Sys, $"KEY body built on '{host.name}' @ {host.transform.position} (was invisible before WO-1112)");
            });
        }

        /// <summary>
        /// A LOCKED PORT plate: a dark iron slab standing in the doorway with a gold keyhole,
        /// yawed to face the way the port leads. Static (a lock is furniture, not a pickup).
        /// </summary>
        public static void BuildLock(GameObject host, float faceYaw)
        {
            if (HasBody(host)) return;
            Guard.Try(Sys, $"build lock body on '{host.name}'", () =>
            {
                var body = NewBody(host);
                body.transform.localRotation = Quaternion.Euler(0f, faceYaw, 0f);

                var plate = Prim(body, "Plate", PrimitiveType.Cube, Hex(IronHex), emissiveMul: 0.25f);
                plate.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                plate.transform.localScale = new Vector3(1.6f, 2.1f, 0.16f);

                // Gold keyhole — reads at a glance as "this needs the key you are looking for",
                // and deliberately matches the key's brass so the two are visibly a pair.
                var hole = Prim(body, "Keyhole", PrimitiveType.Cylinder, Hex(GoldHex));
                hole.transform.localPosition = new Vector3(0f, 1.05f, -0.12f);
                hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                hole.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);

                var bar = Prim(body, "Bar", PrimitiveType.Cube, Hex(GoldHex));
                bar.transform.localPosition = new Vector3(0f, 0.80f, -0.12f);
                bar.transform.localScale = new Vector3(0.10f, 0.34f, 0.06f);

                FlowTrace.Step(Sys, $"LOCK body built on '{host.name}' @ {host.transform.position} yaw={faceYaw:F0} (was invisible before WO-1112)");
            });
        }

        /// <summary>
        /// A TRAP pad: a flat, dull disc set into the floor, sized to the trap's own radius.
        /// <para>
        /// DELIBERATELY UNDERSTATED, and that is a design call, not an oversight. Owner standing
        /// ruling: "the dungeons should be confusing" / "im not trying to make them easy". A
        /// bright pulsing warning would hand the player every trap for free. A dull floor plate
        /// rewards LOOKING -- and it replaces a trap that had literally no visual at all, so the
        /// damage read as unexplained.
        /// </para>
        /// </summary>
        public static void BuildTrapPad(GameObject host, float radius, bool grate)
        {
            if (HasBody(host)) return;
            Guard.Try(Sys, $"build trap pad on '{host.name}'", () =>
            {
                var body = NewBody(host);
                // Shape, not colour, separates the kinds: a round spike plate vs a square grate.
                var pad = Prim(body, grate ? "GratePad" : "SpikePad",
                    grate ? PrimitiveType.Cube : PrimitiveType.Cylinder,
                    Hex(HazardHex), emissiveMul: 0.18f);
                pad.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                pad.transform.localScale = grate
                    ? new Vector3(radius * 1.7f, 0.05f, radius * 1.7f)
                    : new Vector3(radius * 1.7f, 0.025f, radius * 1.7f);

                FlowTrace.Step(Sys, $"TRAP pad built on '{host.name}' r={radius:F1} grate={grate} (was invisible before WO-1112)");
            });
        }

        /// <summary>
        /// An OIL STONE: a squat stone pedestal with a bright amber bowl on top. The bowl is the
        /// brightest thing this file builds ON PURPOSE — with the lantern tripled the player now
        /// has to plan refills, and a refill point you cannot see is not a decision.
        /// </summary>
        public static void BuildOilStone(GameObject host)
        {
            if (HasBody(host)) return;
            Guard.Try(Sys, $"build oil stone body on '{host.name}'", () =>
            {
                var body = NewBody(host);

                var plinth = Prim(body, "Plinth", PrimitiveType.Cylinder, Hex(IronHex), emissiveMul: 0.15f);
                plinth.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                plinth.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);

                var bowl = Prim(body, "Bowl", PrimitiveType.Sphere, Hex(OilHex), emissiveMul: 1.1f);
                bowl.transform.localPosition = new Vector3(0f, 0.78f, 0f);
                bowl.transform.localScale = new Vector3(0.55f, 0.32f, 0.55f);

                FlowTrace.Step(Sys, $"OIL STONE body built on '{host.name}' @ {host.transform.position} (was invisible before WO-1112)");
            });
        }

        // ── Primitive plumbing ──────────────────────────────────────────────────

        private static GameObject NewBody(GameObject host)
        {
            var body = new GameObject(BodyName);
            body.transform.SetParent(host.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            return body;
        }

        private static GameObject Prim(GameObject parent, string name, PrimitiveType type,
                                       Color tint, float emissiveMul = 0.55f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // STRIP the primitive's collider: these are decoration hung under a live trigger
            // volume. Left on, the cube plate would block the hero and the key's own primitives
            // would shadow the SphereCollider the pickup fires from.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent.transform, false);
            Paint(go, tint, emissiveMul);
            return go;
        }

        private static void Paint(GameObject go, Color tint, float emissiveMul)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                // NOT silent (sec.12): no shader means the prop is built but unpainted, which
                // looks like "the fix did not land" rather than "the shader is missing".
                FlowTrace.Warn(Sys, $"no URP/Lit or Standard shader for '{go.name}' - prop keeps the default material.");
                return;
            }
            var mat = new Material(shader) { color = tint };
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", tint * emissiveMul);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
