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
// bobbing key silhouette, a framed CLOSED DOOR wearing a keyhole (WO-1588; it was a
// flat plate until the owner photographed it reading as a wall), a floor pad, a squat
// pedestal. The tints reinforce; they never carry the meaning alone.
//
// Every primitive's own collider is STRIPPED. These are decoration hung under a live
// trigger volume; leaving the primitive colliders on would block the hero, and on the
// key it would shadow the trigger sphere the pickup depends on.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>Builds the runtime visual body for composed-dungeon pillars.</summary>
    // WO-1588: PUBLIC (was internal) so DungeonSceneCapture can photograph the locked port
    // through the same builder the game runs. Visibility only - same precedent as
    // CommonDungeonDoor.OpenAngle in WO-1568. No member behaviour changed by this line.
    public static class ComposedPropVisuals
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
        /// A LOCKED PORT: the SAME door the rest of the dungeon uses, closed, with a gold keyhole
        /// hung on its face. Yawed to face the way the port leads. Static (a lock is furniture,
        /// not a pickup).
        /// </summary>
        // =====================================================================================
        // WO-1588 - THIS USED TO BUILD ITS OWN FLAT CUBE, AND THAT WAS THE DEFECT.
        // -------------------------------------------------------------------------------------
        // The body was one `PrimitiveType.Cube` 1.6 x 2.1 x 0.16 - the identical "moving wall"
        // silhouette WO-1568 removed from CommonDungeonDoor, still standing here because the
        // locked port was a SECOND door builder. In the owner's frame (F8 seq 4699,
        // logs/f8-inbox/device/SM02G4061955851/flag_20260907-143255_00.png) it reads as a plain
        // WHITE slab with a floating yellow blob: no frame, no lintel, no relief, nothing that
        // says "door", let alone "locked door".
        //
        // There is now ONE door builder in this module. This method composes:
        //   CommonDungeonDoor.BuildDoorVisual(body, DoorGap/2, open:false)  <- the seam
        //   + a keyhole and a bar, which are the only parts a LOCK adds to a door.
        // The glow stays: the keyhole is emissive, and it is still the affordance that says
        // "the key you are carrying goes here" - shape first, tint reinforcing (colourblind law).
        //
        // THE BLOCKER IS STRIPPED ON PURPOSE. BuildDoorVisual's leaf carries one BoxCollider,
        // which is right for a door filling a wall gap. This port is a TELEPORT seated at a room
        // seat, not a gap: the old plate deliberately had no collider, and adding one here would
        // put a solid box in open floor that no NavMesh knows about. Same reasoning as the frame
        // pieces, which BuildDoorVisual already strips.
        // =====================================================================================
        public static void BuildLock(GameObject host, float faceYaw)
        {
            if (HasBody(host)) return;
            Guard.Try(Sys, $"build lock body on '{host.name}'", () =>
            {
                var body = NewBody(host);
                body.transform.localRotation = Quaternion.Euler(0f, faceYaw, 0f);

                // THE ONE DOOR SEAM. Never build a second door here.
                var door = CommonDungeonDoor.BuildDoorVisual(
                    body.transform, RoomForgeCanon.DoorGap * 0.5f, open: false);

                // A teleport port is not a wall gap - see the header. Strip the leaf's blocker.
                // Read the flag BEFORE the strip: DestroyNow is DestroyImmediate in edit mode, so
                // testing door.Blocker afterwards would report "there was none" in the very run
                // (the capture) where it did the work.
                bool hadBlocker = door.Blocker != null;
                if (hadBlocker) DestroyNow(door.Blocker);

                // Gold keyhole — reads at a glance as "this needs the key you are looking for",
                // and deliberately matches the key's brass so the two are visibly a pair. Seated
                // on the closed leaf's face, in BODY space, so it does not depend on the leaf art's
                // bounds (which differ between the KayKit leaf and the primitive fallback).
                // Depth is DERIVED, never typed: BuildArtLeaf clamps the leaf's collider depth to
                // RoomForgeCanon.WallThickness and seats the leaf centred, so the art leaf's front
                // face sits at most half a wall thickness toward the viewer. Sit just proud of it.
                // NOT PROVEN: the primitive fallback leaf is thinner (FallbackLeafThickness), so
                // on that path the keyhole stands a little further off the face. door_locked.png
                // is what settles it - do not re-tune this number without looking at that frame.
                float faceZ = -((RoomForgeCanon.WallThickness * 0.5f) + 0.03f);
                var hole = Prim(body, "Keyhole", PrimitiveType.Cylinder, Hex(GoldHex));
                hole.transform.localPosition = new Vector3(0f, 1.05f, faceZ);
                hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                hole.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);

                var bar = Prim(body, "Bar", PrimitiveType.Cube, Hex(GoldHex));
                bar.transform.localPosition = new Vector3(0f, 0.80f, faceZ);
                bar.transform.localScale = new Vector3(0.10f, 0.34f, 0.06f);

                FlowTrace.Step(Sys, $"LOCK body built on '{host.name}' @ {host.transform.position} " +
                                    $"yaw={faceYaw:F0} builder=CommonDungeonDoor.BuildDoorVisual " +
                                    $"leaf='{door.LeafSource}' leafTop={door.LeafTop:0.##}m " +
                                    $"blockerStripped={hadBlocker} " +
                                    "(was a flat cube plate before WO-1588)");
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
            if (col != null) DestroyNow(col);
            go.transform.SetParent(parent.transform, false);
            Paint(go, tint, emissiveMul);
            return go;
        }

        /// <summary>
        /// Destroy that works in BOTH play mode and edit mode (CommonDungeonDoor.DestroyNow shape,
        /// reused rather than invented twice). WO-1588: DungeonSceneCapture drives BuildLock in
        /// EDIT mode to photograph the locked port, and a plain Object.Destroy there logs an error
        /// and leaves the collider alive until the next frame that never comes.
        /// </summary>
        private static void DestroyNow(Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Object.DestroyImmediate(o); return; }
#endif
            Object.Destroy(o);
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
            // WO-1588 - WHY THIS SETS _BaseColor EXPLICITLY, AND WHY IT TRACES.
            // The owner's frame shows a WHITE slab carrying a GOLD glow: the emission landed and
            // the base colour did not, which is the signature of `Material.color` failing to
            // reach URP/Lit's _BaseColor (Material.color only routes there when the shader
            // variant that resolved actually declares a [MainColor]). That cause is NOT PROVEN -
            // it cannot be, from this machine - so this does both: sets the property by name when
            // it exists, and prints what the device actually resolved. Look for
            // [Flow:ComposedProp] "paint" in a device log to settle it.
            var mat = new Material(shader) { color = tint };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", tint * emissiveMul);
            // isSupported is the field that splits the two candidate causes: a property that never
            // applied, versus URP/Lit found by name but with every variant STRIPPED from the
            // Android build, so the renderer falls back to magenta/white. Without it a device log
            // can read all-green and the slab is still white.
            FlowTrace.Once(Sys, "paint-material",
                                $"paint shader='{shader.name}' supported={shader.isSupported} " +
                                $"hasBaseColor={mat.HasProperty("_BaseColor")} " +
                                $"hasColor={mat.HasProperty("_Color")} want={tint} readback={mat.color}");
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
