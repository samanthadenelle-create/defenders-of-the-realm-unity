// =============================================================================
// FoundersMonumentInjector - WO-1073, seats the Founders Monument near the Heart
// of Elarion at runtime, and attaches the wall's one door to it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// NO SCENE EDIT. CLAUDE.md section 3 forbids hand-editing Village.unity /
// Main_Castle_Overworld, so this is a runtime placer in the established house
// pattern (HubStructureVisualInjector.TryPlace / CampSystem / StoryCompanion-
// Injector): hook sceneLoaded, defer off the engine callback, place once,
// idempotent by object name.
//
// =============================================================================
// ⭐⭐ THE FBX DROP-IN POINT. THIS IS THE PARAGRAPH THE NEXT PERSON NEEDS. ⭐⭐
// -----------------------------------------------------------------------------
// The monument's mesh is DATA, not code. It is resolved by ADDRESS through
// StructureAssetLoader, exactly like every other structure in the game, from the
// single constant:
//
//        DeNelle.Core.Patronage.BenefactorsCatalog.StandInMonumentAssetKey
//        == "monument_founder_standin"
//
// TO DROP THE REAL MONUMENT IN, WITH NO CODE CHANGE:
//   1. Import the FBX the owner and the artist authored.
//   2. Make a prefab and register it with the structure Addressables grouper under
//      the address "monument_founder_standin" - that exact string, no folder
//      prefix, no extension.
//   3. Run a content build and PUSH IT: tools\r2-ship.ps1. CLAUDE.md section 16 -
//      structure art is served from the R2 CDN with NO local fallback and bundle
//      names are CONTENT-HASHED, so an authored-but-unpushed monument renders as
//      NOTHING with no error on screen. That failure has already hit this project
//      three times.
//   4. That is all. The next hub load resolves the address and the primitive
//      placeholder below is never built.
//
// Nothing else needs touching: not this file, not FoundersMonument.cs, not the
// panel, not the server. The address is the seam.
// =============================================================================
//
// -----------------------------------------------------------------------------
// THE PRIMITIVE PLACEHOLDER, AND WHY IT EXISTS AT ALL
// -----------------------------------------------------------------------------
// On the day this landed the address above resolved to NOTHING - the asset has
// not been authored yet. Section 16's whole lesson is that a structure whose art
// is absent renders as an invisible object with no error on screen; here that
// would mean the Benefactors wall has NO DOOR, and WO-1073 section 3.2 keeps the
// $500 tier switched off until the stand-in RENDERS. An invisible monument would
// therefore hold the top rung of the ladder closed silently.
//
// So when the address misses, this builds a deliberately plain grey plinth out of
// Unity primitives, names it so nobody can mistake it for art, and SHOUTS about
// it in the trace every hub load. It is a marker, not a design:
//   * It is not the final art and is not a proposal for the final art. The owner
//     is authoring that WITH an artist, one-on-one (ruling 2026-08-27(c)).
//   * It is replaced the moment the address resolves - including MID-SESSION, via
//     the content warmer's settled callback, so a late bundle download swaps it
//     without a scene reload.
//   * It carries no colour-borne meaning (CLAUDE.md: the owner is red/green
//     colourblind); it reads as a plinth by SHAPE.
//
// -----------------------------------------------------------------------------
// SITING: NEAR THE HEART, NEVER ON IT. Owner ruling, BINDING on this tier.
// -----------------------------------------------------------------------------
// Verbatim: "that protects your most important world object from becoming a
// NASCAR hood covered in sponsor names." No inscription on the Heart mesh, no
// per-patron decal, no name list on the world tree - a SEPARATE ADJACENT OBJECT.
//
// The position is therefore DERIVED FROM THE HEART at runtime rather than
// authored as a world constant: find the "HeartOfElarion" anchor, add
// <see cref="OffsetFromHeart"/>. Two reasons, both learned the hard way in this
// repo:
//   * The Heart is NOT at world origin and NOT at y=0. CastleHubBuilder seats it
//     at (0, CastleFootprintLiftY, 12) and WO-593 raised the whole island, so a
//     hardcoded world position would be wrong the next time the island moves, and
//     a hardcoded y=0 would bury the monument under the courtyard floor. Reading
//     the Heart's own transform means the monument rides the island for free.
//   * "Near the Heart" then holds BY CONSTRUCTION rather than by a comment, and
//     the regression can assert the distance bounds against the same constant.
// The offset is bounded at both ends: far enough to never touch the Heart or its
// canopy, close enough to read as the Heart's companion rather than as a random
// prop. Both bounds are pinned in FoundersMonumentWallRegression.
//
// ASCII only. Instrumentation: FlowTrace tag "Benefactors". Never strip it.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Patronage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Places the Founders Monument (stand-in or bespoke) beside the Heart.</summary>
    public static class FoundersMonumentInjector
    {
        /// <summary>Object name of the placed host. Idempotency guard, and the name the
        /// regression and any diagnostic look for.</summary>
        public const string HostName = "FoundersMonument";

        /// <summary>Name of the child built when the addressable is absent. Deliberately
        /// unmistakable in a hierarchy dump - see the header.</summary>
        public const string PlaceholderChildName = "FoundersMonument_PrimitivePlaceholder";

        /// <summary>The exact scene-object name CastleHubBuilder gives the Heart anchor
        /// (CastleHubBuilder.HeartAnchorName). If the bake ever renames it, this misses and
        /// the injector STANDS DOWN loudly rather than guessing a world position.</summary>
        public const string HeartAnchorName = "HeartOfElarion";

        /// <summary>
        /// Offset from the Heart anchor, in metres, in world axes. +X is the east side of
        /// the north-centre plaza: the storefronts sit at +/-22 and the Colosseum at z=23,
        /// so this spot is clear.
        /// <para>y is 0 - the monument shares the Heart's ground plane, whatever the island
        /// has been raised to. See the siting note in the header.</para>
        /// </summary>
        public static readonly Vector3 OffsetFromHeart = new Vector3(8f, 0f, 0f);

        /// <summary>
        /// Fit-to-HEIGHT multiplier against StructureFactory.YHeightVariable (4 m), the
        /// same dial every structure uses (WO-764).
        /// <para>⚠ DELIBERATELY 1.0, THE UNIFORM BUILDING BASE, and not a "landmark tier".
        /// The Cathedral of Magic sat at 1.25 as exactly that kind of deliberate exception
        /// and the owner overruled it on sight ("why is the cathedral of magic so large?
        /// Normalize"). If the real FBX wants a different presence, that is a conversation
        /// with the owner and a change to this one number - not a second cadence.</para>
        /// </summary>
        public const float HeightMultiplier = 1f;

        /// <summary>Re-place attempts per hub load once the content warmer settles. Bounded
        /// so a genuinely absent address cannot spin forever.</summary>
        private const int MaxWarmRetries = 3;

        private static int s_warmRetries;
        private static bool s_warmRetryArmed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Apply();
        }

        // ⛔ DO NOT CALL Apply() INLINE HERE. Same P0 as HubStructureVisualInjector's:
        // resolving structure art from inside a sceneLoaded ENGINE CALLBACK is what
        // deadlocked the game for three minutes on 2026-08-20. StructureContentWarmer.Defer
        // gets the work off the callback and onto the player loop.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!HubScenes.IsHub(scene.name)) return;
            s_warmRetries = 0;
            StructureContentWarmer.Defer(Apply);
        }

        /// <summary>
        /// Place the monument if it is not already standing. Idempotent, gate-checked and
        /// never throws. Public so a dev tool or a headless probe can force it.
        /// </summary>
        public static void Apply()
        {
            Guard.Try(BenefactorsCatalog.Sys, "place the Founders Monument", PlaceInternal);
        }

        private static void PlaceInternal()
        {
            if (!FeatureFlags.FoundersMonument)
            {
                FlowTrace.Step(BenefactorsCatalog.Sys,
                    "standdown: ff.foundersmonument is OFF - no monument, no collider, and NO DOOR " +
                    "onto the Benefactors wall (PanelId.Benefactors has no other entry point by " +
                    "owner ruling 2026-08-27(c)). Set PlayerPrefs ff.foundersmonument=1 to restore.");
                return;
            }

            var existing = FindByName(HostName);
            if (existing != null)
            {
                // Already standing. If it is still on the primitive placeholder, the art may
                // have arrived since - try the upgrade rather than leaving a plinth up forever.
                TryUpgradePlaceholder(existing.gameObject);
                return;
            }

            Transform heart = FindByName(HeartAnchorName);
            if (heart == null)
            {
                // NOT a silent return. If the Heart anchor is missing from a hub scene, the
                // siting rule has nothing to be near and guessing a world position is exactly
                // the brittleness the header rejects.
                FlowTrace.Warn(BenefactorsCatalog.Sys,
                    "no '" + HeartAnchorName + "' anchor in this scene - the Founders Monument is " +
                    "NOT placed. Its position is derived from the Heart by design (owner siting " +
                    "ruling: near the Heart, never on it), so there is nothing to derive from and " +
                    "no world constant to fall back to. If the bake renamed the anchor, update " +
                    "FoundersMonumentInjector.HeartAnchorName.");
                return;
            }

            Vector3 pos = heart.position + OffsetFromHeart;
            var host = new GameObject(HostName);
            host.transform.position = pos;
            SceneManager.MoveGameObjectToScene(host, heart.gameObject.scene);

            FlowTrace.Step(BenefactorsCatalog.Sys,
                "placing '" + HostName + "' at " + pos + " = Heart" + heart.position + " + offset" +
                OffsetFromHeart + " (|offset|=" + OffsetFromHeart.magnitude.ToString("F1") + "m). " +
                "NEAR the Heart, never ON it - owner siting ruling 2026-08-24.");

            if (!TrySkin(host))
            {
                BuildPrimitivePlaceholder(host);
                ArmWarmRetry();
            }

            // The door goes on LAST and unconditionally: whether the player is looking at the
            // real FBX or at the placeholder plinth, walking up to it opens the wall. That is
            // the owner's "the monument IS the door" ruling, and it is why the tier can switch
            // on before any collaboration finishes.
            if (host.GetComponent<FoundersMonument>() == null)
                host.AddComponent<FoundersMonument>();

            FlowTrace.Step(BenefactorsCatalog.Sys,
                "'" + HostName + "' is standing with its FoundersMonument door attached (activate " +
                "radius " + FoundersMonument.ActivateRadius + "m, TalkPromptRegistry -> the HUD TALK " +
                "button). This is the ONE world entry to PanelId.Benefactors.");
        }

        /// <summary>
        /// Resolve + skin the monument art by ADDRESS. Returns false when the address is not
        /// resolvable right now, which is never fatal and never silent.
        /// </summary>
        private static bool TrySkin(GameObject host)
        {
            var opts = SkinOptions.Structure(0f);   // clears FitLargest; keeps SeatOnGround + Tripo URP fix
            opts.FitHeight = StructureFactory.YHeightVariable * HeightMultiplier;
            opts.TraceId = BenefactorsCatalog.StandInMonumentAssetKey;

            GameObject vis = VisualFactory.Skin(host.transform,
                                                BenefactorsCatalog.StandInMonumentAssetKey, opts);
            if (vis == null)
            {
                FlowTrace.Warn(BenefactorsCatalog.Sys,
                    "monument address '" + BenefactorsCatalog.StandInMonumentAssetKey + "' did not " +
                    "resolve - falling back to the PRIMITIVE PLACEHOLDER so the wall still has a " +
                    "door. THIS IS THE EXPECTED STATE until the shared stand-in asset is authored " +
                    "and PUSHED (CLAUDE.md section 16: content-hashed bundles, no local fallback, " +
                    "no error on screen). See FoundersMonumentInjector's drop-in header.");
                return false;
            }

            EnsureCollider(host, vis);
            FlowTrace.Step(BenefactorsCatalog.Sys,
                "monument art resolved from address '" + BenefactorsCatalog.StandInMonumentAssetKey +
                "' -> '" + vis.name + "', fit to " + opts.FitHeight.ToString("F1") + "m.");
            return true;
        }

        /// <summary>
        /// The placeholder is standing; see whether the real art has become resolvable and,
        /// if so, replace it in place. Runs on every hub load and on every warm-settled
        /// callback, so an asset that lands mid-session is picked up without a reload.
        /// </summary>
        private static void TryUpgradePlaceholder(GameObject host)
        {
            var placeholder = host.transform.Find(PlaceholderChildName);
            if (placeholder == null) return;   // already on the real art - nothing to do

            var opts = SkinOptions.Structure(0f);
            opts.FitHeight = StructureFactory.YHeightVariable * HeightMultiplier;
            opts.TraceId = BenefactorsCatalog.StandInMonumentAssetKey;

            GameObject vis = VisualFactory.Skin(host.transform,
                                                BenefactorsCatalog.StandInMonumentAssetKey, opts);
            if (vis == null) { ArmWarmRetry(); return; }

            Object.Destroy(placeholder.gameObject);
            var oldBox = host.transform.Find("StructureCollider");
            if (oldBox != null) Object.Destroy(oldBox.gameObject);
            EnsureCollider(host, vis);

            FlowTrace.Step(BenefactorsCatalog.Sys,
                "PLACEHOLDER REPLACED: the address '" + BenefactorsCatalog.StandInMonumentAssetKey +
                "' resolved mid-session, so the primitive plinth was destroyed and the real monument " +
                "art is now standing. The door component was never touched.");
        }

        /// <summary>
        /// Ask the warmer to tell us when structure content settles, then try once more.
        /// Bounded by <see cref="MaxWarmRetries"/> so an address that genuinely does not
        /// exist costs three attempts, not an endless loop.
        /// </summary>
        private static void ArmWarmRetry()
        {
            if (s_warmRetryArmed || s_warmRetries >= MaxWarmRetries) return;
            s_warmRetryArmed = true;
            StructureContentWarmer.WhenSettled(() =>
            {
                s_warmRetryArmed = false;
                s_warmRetries++;
                if (!HubScenes.IsHub(SceneManager.GetActiveScene().name)) return;
                FlowTrace.Step(BenefactorsCatalog.Sys,
                    "structure content settled (" + StructureContentWarmer.State + ") - retrying the " +
                    "monument art (attempt " + s_warmRetries + "/" + MaxWarmRetries + ").");
                Apply();
            });
        }

        // ---------------------------------------------------------------------
        //  The primitive placeholder. A MARKER, NOT ART. See the header.
        // ---------------------------------------------------------------------
        private static void BuildPrimitivePlaceholder(GameObject host)
        {
            var root = new GameObject(PlaceholderChildName);
            root.transform.SetParent(host.transform, false);

            // A plinth reads as a plinth by SHAPE: a wide base, a narrower pillar. No colour
            // carries any part of the meaning (the owner is red/green colourblind), and it is
            // deliberately plain so nobody mistakes it for a design proposal.
            float h = StructureFactory.YHeightVariable * HeightMultiplier;

            AddBlock(root.transform, "Base",   new Vector3(2.4f, h * 0.15f, 2.4f), h * 0.075f);
            AddBlock(root.transform, "Pillar", new Vector3(1.2f, h * 0.70f, 1.2f), h * 0.15f + h * 0.35f);
            AddBlock(root.transform, "Cap",    new Vector3(1.6f, h * 0.15f, 1.6f), h * 0.925f);

            // ⛔ REGISTERED WITH THE MAGENTA GUARD ON PURPOSE, and this is load-bearing.
            // MagentaGuard's sweep HIDES renderers whose mesh is a built-in primitive when it
            // finds them magenta (IsPrimitivePlaceholder), because a stray pill is not art. The
            // blocks above are already given a fresh URP/Lit material so they are not magenta -
            // but if a shader lookup ever fails, the sweep would hide this plinth, and a hidden
            // plinth is an INVISIBLE DOOR onto the Benefactors wall. Registering makes the sweep
            // RECOVER it instead. The registration disappears with the object the moment the real
            // FBX resolves (TryUpgradePlaceholder destroys it).
            MagentaGuard.ProtectPrimitiveArt(root, "FoundersMonumentInjector.BuildPrimitivePlaceholder");

            EnsureCollider(host, root);

            FlowTrace.Warn(BenefactorsCatalog.Sys,
                "PRIMITIVE PLACEHOLDER built for the Founders Monument (" + PlaceholderChildName +
                ", " + h.ToString("F1") + "m). This is NOT the art and is NOT a proposal for the " +
                "art - the real monument is a custom FBX the owner authors with an artist. It exists " +
                "only so the Benefactors wall has a visible, reachable door from day one, per WO-1073 " +
                "section 3.2 ('a threshold whose cosmetic cannot render is not authored yet').");
        }

        private static void AddBlock(Transform parent, string name, Vector3 size, float centreY)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, centreY, 0f);
            go.transform.localScale = size;
            // The fitted StructureCollider on the host is the one that walls the monument off;
            // the per-block primitive colliders would otherwise stack three more on top of it.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // CreatePrimitive ships the BUILT-IN STANDARD SHADER, which URP renders MAGENTA -
            // the "pink floor" lesson (CLAUDE.md section 12). Never leave it on.
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                // A plain, unsaturated stone grey. It carries NO meaning - the shape says
                // "plinth" and the TALK prompt says the rest. The owner is red/green
                // colourblind; nothing here may depend on hue.
                var mat = MagentaGuard.BuildUrpLitMaterial(new Color(0.62f, 0.60f, 0.57f));
                if (mat != null) rend.sharedMaterial = mat;
                else
                    FlowTrace.Warn(BenefactorsCatalog.Sys,
                        "no URP/Lit shader resolvable for the placeholder block '" + name +
                        "' - leaving its default material. It may render magenta; MagentaGuard's " +
                        "recovery pass is the net (this subtree is registered as deliberate art).");
            }
        }

        // ---------------------------------------------------------------------

        /// <summary>
        /// World-axis-aligned box fitted to the visible mesh, on a child whose world rotation
        /// is identity and world scale is 1 so the box maps 1:1 to world units. Same primitive
        /// HubStructureVisualInjector.EnsureStructureCollider uses (ticket #10) - copied rather
        /// than shared because that one is private to a different placer and this file must not
        /// widen its surface.
        /// </summary>
        private static void EnsureCollider(GameObject host, GameObject vis)
        {
            if (host == null || vis == null) return;
            if (host.transform.Find("StructureCollider") != null) return;

            Bounds b = default; bool have = false;
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!have) { b = r.bounds; have = true; } else b.Encapsulate(r.bounds);
            }
            if (!have)
            {
                FlowTrace.Warn(BenefactorsCatalog.Sys,
                    "no renderable mesh under '" + vis.name + "' - no collider fitted. The monument " +
                    "is walkable-through; the door still works because it is proximity-based.");
                return;
            }

            var holder = new GameObject("StructureCollider");
            holder.transform.SetParent(host.transform, false);
            holder.transform.position = b.center;
            holder.transform.rotation = Quaternion.identity;
            Vector3 pls = host.transform.lossyScale;
            holder.transform.localScale = new Vector3(
                Mathf.Abs(pls.x) > 1e-4f ? 1f / pls.x : 1f,
                Mathf.Abs(pls.y) > 1e-4f ? 1f / pls.y : 1f,
                Mathf.Abs(pls.z) > 1e-4f ? 1f / pls.z : 1f);
            var box = holder.AddComponent<BoxCollider>();
            box.size = b.size;

            FlowTrace.Step(BenefactorsCatalog.Sys,
                "fitted BoxCollider on '" + host.name + "' size=" + b.size + " center=" + b.center + ".");
        }

        private static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }
    }
}
