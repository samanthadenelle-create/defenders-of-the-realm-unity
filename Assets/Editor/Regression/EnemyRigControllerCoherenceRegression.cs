// =============================================================================
// EnemyRigControllerCoherenceRegression — every enemy mesh must be paired with a
// controller whose CLIP TYPE its rig can actually play.
// -----------------------------------------------------------------------------
// THE INVARIANT. For every .fbx reachable by Resources.Load under
// Assets/EnemyContent/**, the rig type of the MODEL and the clip type of the
// controller EnemyAnimatorFactory will hand it must agree:
//
//     Humanoid model  ->  controller carrying Humanoid clips
//     Generic  model  ->  controller carrying NO Humanoid clips
//
// Cross the streams either way and the Animator holds the bind/T-pose while the
// NavMeshAgent slides the model across the ground. That is the "sliding statue"
// (WO-445; QA 2026-07-11 capture-20260711-181253, orcs frozen in T-pose). It reads
// on screen as broken ANIMATION, which is why it has been mis-diagnosed repeatedly —
// the animation is fine, the pairing is wrong.
//
// WHY A STATIC ORACLE WHEN THE RUNTIME ALREADY CHECKS THIS. EnemyAnimatorFactory.Apply
// does detect the mismatch and FlowTrace.Fail's on it, and EnemyPoseVerifier samples
// the rig for ~8 frames after spawn. Both are real, and both fire ONLY WHEN THAT ENEMY
// ACTUALLY SPAWNS IN A PLAY SESSION. A model that is wired but rarely spawned — a boss,
// an outpost-only variant — ships broken and reports nothing. This oracle asks the same
// question at GATE TIME, over EVERY model in the folder, spawned or not.
//
// SHARED AUTHORITY, NOT A SECOND COPY. The model->controller mapping is read from
// EnemyAnimatorFactory.ResolveControllerName and the clip test from
// EnemyAnimatorFactory.ControllerHasHumanoidClips — the exact members the runtime uses.
// Re-deriving the mapping here is how a gate and the game come to disagree while both
// report success (the same discipline VfxResourceSelfContainmentRegression keeps).
//
// A MISSING CONTROLLER IS ALSO A FAILURE. Resources.Load returning null leaves the
// Animator in its empty default state and the enemy slides with no clip at all — the
// WO-436 Failure-B path. Silence is not a pass.
//
// Deterministic, editor-only asset reads. No scene, no PlayMode.
//
// Registered in DataRegression.RunAll (covenant style):
//   Guard.Try(... EnemyRigControllerCoherenceRegression.Run(out var r) ...)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Village;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EnemyRigControllerCoherenceRegression
    {
        // ⚠ NOT a const, and deliberately so. The enemy art is migrating OUT of Resources into
        // Addressables (Assets/EnemyContent) so its ~539 MB stops being force-included in every
        // build. A hardcoded DeNelle.Core.AssetRoots.EnemyContent here does NOT call Resources.Load, so the
        // EnemyAssetLoader seam does not rescue it — the instant the assets physically move, every
        // FindAssets below returns empty and this whole suite hard-reds with "no models found",
        // which reads as "the enemy roster broke" when nothing is wrong at all.
        //
        // Resolve the live root instead, so the suite is correct in BOTH states and during a
        // PARTIAL migration. Mirrors EnemyAddressablesGrouper.ResolveActiveRoot / the WO-545
        // HeroAddressablesGrouper precedent — same rule, so the two cannot drift apart.
        //
        // The emptiness guard below stays load-bearing: if NEITHER root holds models that is a
        // genuine failure and must still red. This resolves WHERE to look; it never weakens WHAT
        // is asserted.
        private const string EnemyResourcesRoot = DeNelle.Core.AssetRoots.EnemyContent;
        private const string EnemyContentRoot   = DeNelle.Core.AssetRoots.EnemyContent;

        /// <summary>
        /// The folder that actually holds the enemy models right now: the migrated
        /// <c>Assets/EnemyContent</c> once it contains at least one model, else the pre-migration
        /// <c>Assets/EnemyContent</c>. Probed per access (AssetDatabase-cheap) so a migration
        /// that happens between suite runs needs no code change.
        /// </summary>
        private static string EnemyRoot
        {
            get
            {
                if (AssetDatabase.IsValidFolder(EnemyContentRoot))
                {
                    string[] migrated = AssetDatabase.FindAssets("t:Model", new[] { EnemyContentRoot });
                    if (migrated != null && migrated.Length > 0) return EnemyContentRoot;
                }
                return EnemyResourcesRoot;
            }
        }

        /// <summary>
        /// Models deliberately present but NOT wired for animation, with the reason. A name here
        /// is exempt from the coherence check ONLY — it is still required to exist. Keep this list
        /// SHORT and reasoned; it is a declaration, never a place to silence a real mismatch.
        /// </summary>
        private static readonly Dictionary<string, string> UnwiredByDesign =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Orc_Mage_Legacy", "preserved pre-2026-08-09 mesh, kept only so the original guid still resolves; never spawned" },
            };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log      = new StringBuilder();
            int checkedCount = 0, humanoid = 0, generic = 0, exempt = 0, clipSources = 0, wrapped = 0;

            // PREFAB-WRAPPED FAMILIES. Some enemies spawn as a PREFAB, not a bare mesh — the Blink
            // orcs are prefab VARIANTS of a *_Mesh.fbx, and the licensed dragon ships as
            // Boss_Dragon.prefab. For those the prefab is the spawn unit and the .fbx underneath is
            // a source asset that is never handed to EnemyAnimatorFactory, so judging the raw mesh
            // reports a failure that cannot happen. Collect every model consumed by a prefab here
            // and skip it below.
            var meshConsumedByPrefab = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefabUnits          = new List<string>();
            foreach (string pg in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyRoot }))
            {
                string pPath = AssetDatabase.GUIDToAssetPath(pg);
                if (string.IsNullOrEmpty(pPath)) continue;
                prefabUnits.Add(pPath.Substring(EnemyRoot.Length + 1).Replace(".prefab", ""));
                foreach (string dep in AssetDatabase.GetDependencies(pPath, true))
                    if (dep.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        meshConsumedByPrefab.Add(dep);
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { EnemyRoot });
            if (guids == null || guids.Length == 0)
            {
                reason = "FAIL: no enemy models found under EITHER '" + EnemyContentRoot +
                         "' (post-migration) OR '" + EnemyResourcesRoot + "' (pre-migration) — " +
                         "so this is not a migration artefact, the models are genuinely gone. " +
                         "Every enemy spawn would fall back to a null model.";
                return false;
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                // The Resources KEY is the path under Enemies/, without extension — including any
                // subfolder (the Blink orcs are addressed as "Blink/Blink_Orc_Warrior").
                string modelKey = path.Substring(EnemyRoot.Length + 1);
                modelKey = modelKey.Substring(0, modelKey.Length - 4);

                if (UnwiredByDesign.ContainsKey(modelKey))
                {
                    log.AppendLine("      exempt  " + modelKey + " — " + UnwiredByDesign[modelKey]);
                    exempt++;
                    continue;
                }

                // CLIP-SOURCE FBXs ARE NOT ENEMY MODELS. The Blink pack ships its 22-clip sets as
                // one FBX per animation under Blink/Anim/ and Blink/AnimBoss/ — 48 files that are
                // t:Model and Humanoid, but carry NO GEOMETRY. They exist to be sliced into clips,
                // are never handed to EnemyAnimatorFactory, and can never be a spawned visual.
                // Discriminated by MESH CONTENT rather than by folder name: "has no mesh, so it
                // cannot be an enemy's body" is a property of the asset, whereas a path rule would
                // silently start skipping real models the day someone adds an Anim/ subfolder to a
                // character directory.
                if (!HasMesh(path))
                {
                    log.AppendLine("      clip-src " + modelKey + " (no mesh — animation source, not a spawnable body)");
                    clipSources++;
                    continue;
                }

                if (meshConsumedByPrefab.Contains(path))
                {
                    log.AppendLine("      wrapped  " + modelKey + " (source mesh for a prefab — the prefab is the spawn unit)");
                    wrapped++;
                    continue;
                }

                checkedCount++;

                // --- what the MODEL is ---
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                bool modelIsHumanoid = importer != null &&
                                       importer.animationType == ModelImporterAnimationType.Human;

                if (modelIsHumanoid)
                {
                    humanoid++;
                    // Humanoid without a valid avatar cannot be posed by ANY clip.
                    Avatar avatar = null;
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (sub is Avatar a) { avatar = a; break; }

                    if (avatar == null || !avatar.isValid)
                    {
                        failures.Add("model '" + modelKey + "' is set Humanoid but has " +
                                     (avatar == null ? "NO avatar" : "an INVALID avatar") +
                                     " — it will hold the bind/T-pose while the agent slides it. " +
                                     "Re-import in place (HumanoidRigFixup / PeopleCharacterImporter).");
                        continue;
                    }
                }
                else generic++;

                // --- what the FACTORY will hand it (shared authority) ---
                string ctrlName = EnemyAnimatorFactory.ResolveControllerName(modelKey);
                string ctrlPath = EnemyRoot + "/" + ctrlName + ".controller";
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);

                if (ctrl == null)
                {
                    failures.Add("model '" + modelKey + "' resolves to controller '" + ctrlName +
                                 "' but no asset exists at " + ctrlPath +
                                 " — Resources.Load returns null, the Animator idles in its empty " +
                                 "default state and the enemy SLIDES with no clip (WO-436 Failure B). " +
                                 "Run EnemyAnimatorSetup / Build Animator Controllers.");
                    continue;
                }

                bool ctrlIsHumanoid = EnemyAnimatorFactory.ControllerHasHumanoidClips(ctrl);

                if (modelIsHumanoid && !ctrlIsHumanoid)
                    failures.Add("model '" + modelKey + "' is HUMANOID but controller '" + ctrlName +
                                 "' carries only GENERIC clips — a Generic clip cannot pose a Humanoid " +
                                 "avatar, so it T-poses and slides. Route it to a Humanoid controller " +
                                 "(LargeHumanoid / SkeletonHumanoid / OrcHumanoid) in EnemyAnimatorFactory.RigFor.");
                else if (!modelIsHumanoid && ctrlIsHumanoid)
                    failures.Add("model '" + modelKey + "' is GENERIC but controller '" + ctrlName +
                                 "' carries Humanoid clips — a Humanoid clip cannot pose a Generic rig " +
                                 "at all. Either re-import the model Humanoid or route it to a Generic " +
                                 "controller (HumanoidEnemy / LargeEnemy / Boss).");
                else
                    log.AppendLine("      OK      " + modelKey + " (" +
                                   (modelIsHumanoid ? "Humanoid" : "Generic") + ") -> " + ctrlName);
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("FAIL: " + failures.Count + " enemy model(s) are paired with a controller " +
                              "their rig cannot play — each one ships as a sliding statue:");
                foreach (var f in failures) sb.AppendLine("    - " + f);
                reason = sb.ToString().TrimEnd();
                return false;
            }

            // The pass line carries what is NOT covered. A bare "OK" here would read as "every
            // enemy is verified", and the prefab-wrapped families are exactly the ones nobody
            // would think to re-check. Declared coverage, not implied coverage.
            reason = "OK (WITH DECLARED COVERAGE LIMIT) — " + checkedCount +
                     " directly-spawned model(s) coherent with their controllers (" +
                     humanoid + " Humanoid, " + generic + " Generic; skipped " +
                     clipSources + " clip-source FBX, " + wrapped + " prefab-wrapped source mesh, " +
                     exempt + " exempt by design)." + Environment.NewLine +
                     "    NOT COVERED: " + prefabUnits.Count + " prefab spawn unit(s) — " +
                     string.Join(", ", prefabUnits.ToArray()) + ". These carry their rig through a " +
                     "prefab variant / a setup-built controller rather than EnemyAnimatorFactory's " +
                     "model->controller map, so this oracle cannot judge them yet. Extending it is " +
                     "open work, NOT a clean bill of health for those models." +
                     Environment.NewLine + log.ToString().TrimEnd();
            return true;
        }

        /// <summary>
        /// True if the FBX carries at least one Mesh — i.e. it is a BODY that can be spawned,
        /// not a bare animation take. Reads the imported representation rather than the file
        /// bytes so it agrees with whatever Unity actually produced.
        /// </summary>
        private static bool HasMesh(string path)
        {
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub is Mesh) return true;
            return false;
        }
    }
}
