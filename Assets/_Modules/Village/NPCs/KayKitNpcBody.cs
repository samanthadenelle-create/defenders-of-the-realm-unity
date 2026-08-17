// =============================================================================
// KayKitNpcBody — WO-818: the ONE data-driven resolver for a structure's KayKit
// NPC body. The structures catalog authors repo.npcModel (a KayKit slug, owner-
// approved mapping table — creative pick is OWNER-ONLY, a swap is a one-word JSON
// retag). Both NPC injectors (BarracksNpcInjector drillmaster +
// CastleVendorNpcInjector vendors) call this FIRST; a null return means "use the
// legacy People prefab chain" (then the capsule placeholder) — never a blank NPC.
// -----------------------------------------------------------------------------
// Failure semantics (WO-818 acceptance criteria):
//   • row absent / npcModel not authored  -> quiet null (the People chain is the
//     designed fallback for un-mapped structures; no warn spam).
//   • npcModel AUTHORED but the load misses (typo'd slug / un-staged FBX)
//     -> exactly ONE FlowTrace.Warn naming slug + structure, then null so the
//     caller degrades to the People chain.
// Guard.Try wraps the catalog lookup + Resources.Load per §12 /
// docs/INSTRUMENTATION_STANDARD.md (no silent failures, one bad row never blanks
// a screen). Village -> Core only (CatalogRegistry lives in DeNelle.Core.Catalog).
// =============================================================================

using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>WO-818 — resolves a structure's data-driven KayKit NPC body (repo.npcModel).</summary>
    internal static class KayKitNpcBody
    {
        /// <summary>
        /// Resources root ALL NPC body packs live under. Named rather than inlined so a
        /// folder-qualified npcModel ("CraftPixPeople/NPC_Peasant_1") and the legacy bare slug
        /// build their path from the SAME constant — two string literals for one concept is how
        /// they drift apart.
        /// </summary>
        internal const string NpcResourcesRoot = "NPCs/";

        /// <summary>
        /// The pack a BARE slug resolves against — the KayKit stage, which is where every
        /// pre-PROD-002 row points. It is the BACK-COMPAT DEFAULT, not the preferred pack: the
        /// owner-purchased CraftPix bodies are replacing these (PROD-002), and rows migrate by
        /// retagging npcModel to "CraftPixPeople/&lt;name&gt;". Kept as a named constant so the
        /// default can be moved in ONE place once every row has migrated, instead of hunting a
        /// literal through the resolver.
        /// </summary>
        internal const string DefaultPackFolder = "KayKit/";

        /// <summary>Resources folder the staged KayKit bodies live under (tracked, WO-818 phase 1).</summary>
        internal const string ResourcesRoot = NpcResourcesRoot + DefaultPackFolder;

        /// <summary>Resources path (no extension) of the WO-833 shared idle controller
        /// (built by DeNelle.Editor.KayKitNpcAnimatorSetup.Build).</summary>
        internal const string IdleControllerRes = "NPCs/KayKit/KayKitNpcIdle";

        /// <summary>
        /// Load the KayKit body the catalog authors for <paramref name="catalogId"/>
        /// (repo.npcModel). Null when the row/slug is absent (quiet — People chain is the
        /// authored fallback) or when an authored slug fails to load (exactly ONE
        /// FlowTrace.Warn, caller falls back — never a blank NPC).
        /// <paramref name="resolvedRes"/> = the Resources path actually loaded
        /// (for the caller's trace/verify messages); null whenever this returns null.
        /// </summary>
        internal static GameObject Load(string catalogId, string system, out string resolvedRes)
        {
            resolvedRes = null;
            if (string.IsNullOrEmpty(catalogId)) return null;

            string slug = null;
            Guard.Try(system, $"resolve npcModel for '{catalogId}'", () =>
            {
                var entry = CatalogRegistry.Get(catalogId);
                if (entry != null && entry.repo != null) slug = entry.repo.npcModel;
            });
            if (string.IsNullOrWhiteSpace(slug)) return null;   // not authored -> People chain, no warn

            // PROD-002 (owner 2026-08-17: "swap out the kaykat" / "replace the placeholder kay kat
            // with the people i purchased"): a slug may now name its FOLDER.
            //   bare slug          -> "NPCs/KayKit/<slug>"          — every pre-existing row, unchanged
            //   contains a '/'     -> "NPCs/<slug>"                 — e.g. "CraftPixPeople/NPC_Peasant_1"
            // Chosen over adding a second field or a second resolver because the catalog's own
            // contract for npcModel is "a swap is a ONE-WORD JSON RETAG, never a code pick"
            // (RepoProps.npcModel). Making the slug folder-qualified keeps that promise intact for
            // the purchased bodies; a parallel npcModelPack field would have made every future swap
            // a two-field edit and given the same fact two homes to disagree from.
            string res = slug.Contains("/") ? NpcResourcesRoot + slug : ResourcesRoot + slug;
            GameObject body = null;
            Guard.Try(system, $"load KayKit npc body '{res}'", () =>
            {
                body = Resources.Load<GameObject>(res);
            });
            if (body == null)
            {
                // Authored-but-broken slug: ONE Warn (captured by the F8 harness), then the
                // caller's People-chain fallback keeps the structure speaker visible.
                FlowTrace.Warn(system,
                    $"KayKit npc body '{slug}' for structure '{catalogId}' loads NULL from Resources/{res} " +
                    "- falling back to the People prefab chain (check repo.npcModel vs the staged Assets/Resources/NPCs/KayKit files).");
                return null;
            }
            resolvedRes = res;
            return body;
        }

        /// <summary>
        /// WO-833 - make the INSTANTIATED KayKit body's Animator live so it plays the
        /// shared retargeted humanoid idle instead of rendering the FBX bind pose
        /// (owner F8 2026-08-02 "NPC Stuck in T Pose"). The staged FBXs import as
        /// Humanoid, so Unity's model prefab root ALREADY carries an Animator with the
        /// imported avatar but NO controller (verified: KayKitNpcImporter's avatar
        /// verdict reads exactly that Animator, OK 12/12) - the normal path here is
        /// ONLY assigning runtimeAnimatorController. Defensive fallback: if the
        /// Animator is somehow absent, add one and recover the Humanoid avatar from
        /// the staged FBX's sub-assets (Resources.LoadAll&lt;Avatar&gt; on
        /// <paramref name="resolvedRes"/> - the Avatar is a sub-asset of the FBX, so
        /// LoadAll on the FBX path returns it at runtime). Missing controller asset
        /// => exactly ONE Warn and the NPC stays VISIBLE in bind pose - never blank.
        /// Callers pass the <c>resolvedRes</c> that <see cref="Load"/> returned; the
        /// People-chain bodies (resolvedRes null at the call sites) are never armed -
        /// they ship their own Animator + controller.
        /// </summary>
        internal static void ArmIdle(GameObject bodyInstance, string resolvedRes, string system)
            => ArmController(bodyInstance, resolvedRes, IdleControllerRes, system);

        /// <summary>
        /// WO-871 generalization of <see cref="ArmIdle"/>: arm a staged KayKit body with ANY
        /// Resources-loadable controller. ArmIdle is this with the shared WO-833 idle; the build
        /// worker (ConstructionWorkerPool) passes its work/rest controller. ONE authority for
        /// "make a staged KayKit humanoid actually animate" - the avatar-recovery fallback and
        /// the applyRootMotion rule must never fork per caller.
        /// </summary>
        internal static void ArmController(GameObject bodyInstance, string resolvedRes,
                                           string controllerRes, string system)
        {
            if (bodyInstance == null || string.IsNullOrEmpty(controllerRes)) return;
            Guard.Try(system, $"arm KayKit npc controller '{controllerRes}' on '{bodyInstance.name}'", () =>
            {
                var controller = Resources.Load<RuntimeAnimatorController>(controllerRes);
                if (controller == null)
                {
                    FlowTrace.Warn(system,
                        $"KayKit controller MISSING at Resources/{controllerRes} - body " +
                        $"'{bodyInstance.name}' stays visible in bind pose. Rebuild it: " +
                        "Defenders/Art/Build KayKit NPC Idle Controller (KayKitNpcAnimatorSetup.Build) " +
                        "or Defenders/Art/Build Builder Worker Controller (BuilderWorkerAnimatorSetup.Build).");
                    return;
                }

                var animator = bodyInstance.GetComponentInChildren<Animator>(true);

                // ⛔ A BODY THAT SHIPS ITS OWN CONTROLLER KEEPS IT. The CraftPix prefabs
                // (NPCs/CraftPixPeople/*, PROD-002) are built with AC_CraftPixTownsfolk already
                // BOUND — verified in the prefab YAML, not assumed. Assigning the KayKit idle over
                // that would replace a working, purpose-built controller with a generic one and
                // look exactly like a regression: the vendor animates, just wrongly. The KayKit
                // staged FBXs are the opposite case — Humanoid import gives them an Animator with a
                // NULL controller, which is why this method exists (WO-833 "NPC Stuck in T Pose").
                // Distinguishing them by what is actually bound handles both without either caller
                // needing to know which pack it got.
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    FlowTrace.Once(system, "npc-body-keeps-own-controller-" + bodyInstance.name,
                        $"NPC body '{bodyInstance.name}' already carries controller " +
                        $"'{animator.runtimeAnimatorController.name}' — leaving it alone rather than " +
                        $"overwriting with '{controllerRes}'.");
                    return;
                }

                if (animator == null)
                {
                    // NOT the verified import case (Humanoid model prefabs carry an
                    // Animator) - self-heal: add one + recover the avatar from the FBX.
                    animator = bodyInstance.AddComponent<Animator>();
                    Avatar avatar = null;
                    if (!string.IsNullOrEmpty(resolvedRes))
                    {
                        var avatars = Resources.LoadAll<Avatar>(resolvedRes);
                        if (avatars != null && avatars.Length > 0) avatar = avatars[0];
                    }
                    if (avatar != null)
                        animator.avatar = avatar;
                    else
                        FlowTrace.Warn(system,
                            $"KayKit body '{bodyInstance.name}' had NO Animator and NO Avatar sub-asset at " +
                            $"Resources/{resolvedRes ?? "<null>"} - controller assigned but the humanoid idle " +
                            "cannot retarget (no avatar).");
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;   // NPCs are ground-seated in place - the clip must not drift them
                FlowTrace.Once(system, "kaykit-controller-armed-" + controllerRes,
                    $"KayKit body armed (controller={controllerRes}, retargeted humanoid clip).");
            });
        }
    }
}
