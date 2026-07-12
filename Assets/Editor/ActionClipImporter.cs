using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Auto-configures Mixamo motion clips dropped into <c>Assets/Action/</c> as
    /// retargetable Humanoid animation.
    ///
    /// Heroes and enemies are Humanoid, so a single clip imported this way
    /// retargets to Knight, Mage, Ranger, and every future CC5 hero with no
    /// per-character re-authoring.
    ///
    /// Horizontal root motion is baked into the pose ("in place") so the mesh
    /// animates without translating — code / NavMesh drives world movement.
    /// This is the fix for the historical "hero slides across the ground" bug:
    /// the slide was unwired animation PLUS root drift fighting the locomotion.
    /// </summary>
    public class ActionClipImporter : AssetPostprocessor
    {
        private const string ActionFolder = "Assets/Action/";

        private bool IsActionAsset =>
            assetPath.Replace('\\', '/').StartsWith(ActionFolder, System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsActionAsset) return;

            var importer = (ModelImporter)assetImporter;

            // Humanoid + self-generated avatar: every Mixamo FBX carries the
            // mixamorig skeleton, so it can build its own avatar with no
            // dependency on X Bot being imported first.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // These are motion clips, not art — keep imports lean.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // WO-283: enforce Optimal keyframe-reduction compression on the whole
            // Action library (current + every future drop) so the convention lives in
            // code, not manual Inspector clicks. Sensible error tolerances keep motion
            // faithful while shrinking the clips.
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = 0.5f;
            importer.animationPositionError = 0.5f;
            importer.animationScaleError    = 0.5f;
        }

        internal static bool IsBindOrTPoseClipName(string clipName)
        {
            string cn = (clipName ?? string.Empty).ToLowerInvariant();
            return cn.Contains("t-pose") || cn.Contains("tpose") || cn.Contains("bind")
                || (cn.StartsWith("0_") && cn.Contains("pose"));
        }

        private void OnPreprocessAnimation()
        {
            if (!IsActionAsset) return;

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            string lower = assetPath.ToLowerInvariant();
            bool looping = lower.Contains("idle")
                        || lower.Contains("walk")
                        || lower.Contains("run");

            var kept = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (IsBindOrTPoseClipName(c.name)) continue;

                c.loopTime = looping;
                // Loop Pose (loopBlend): offset the clip so its END pose matches its START,
                // so a looping clip cycles SEAMLESSLY instead of snapping to a different frame
                // and restarting (the "walk cycle resets" bug). MUST be set here — this
                // postprocessor rewrites clipAnimations every import, so setting loopPose only
                // via the .meta or a one-off menu gets clobbered on the next reimport.
                c.loopPose = looping;

                // Bake horizontal root translation into the pose -> in place.
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = false;

                // Preserve vertical motion (jumps / falling deaths read wrong
                // if flattened) and original facing.
                c.lockRootHeightY = false;
                c.keepOriginalPositionY = true;
                c.lockRootRotation = false;
                c.keepOriginalOrientation = true;

                kept.Add(c);
            }

            if (kept.Count > 0)
                importer.clipAnimations = kept.ToArray();
        }

        // ── Force reimport ────────────────────────────────────────────────────
        /// <summary>
        /// Reimports every FBX under Assets/Action/ so OnPreprocessModel runs and
        /// flips clips imported BEFORE this postprocessor existed (they're stale at
        /// Legacy animationType=3) over to Humanoid. Run once after adding the
        /// importer; the hero animator factory depends on these being Humanoid.
        /// Headless: -executeMethod DeNelle.Editor.ActionClipImporter.ReimportActionClips
        /// </summary>
        [UnityEditor.MenuItem("Defenders/Animation/Reimport Action Clips (force Humanoid)")]
        public static void ReimportActionClips()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { "Assets/Action" });
            int n = 0, flipped = 0;
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                n++;

                // Set authoritatively here (not via OnPreprocessModel — that doesn't
                // reliably re-fire for already-imported Legacy assets). SaveAndReimport
                // persists it to the .meta.
                bool wasLegacy = importer.animationType != ModelImporterAnimationType.Human;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal; // WO-283
                StripTPoseFromImporterClips(importer, path);
                importer.SaveAndReimport();
                if (wasLegacy) flipped++;
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[ActionClipImporter] Reimported {n} Action FBX → Humanoid ({flipped} flipped from non-Human).");
        }

        // ── Fix sliding clips ─────────────────────────────────────────────────
        /// <summary>
        /// Forces every Action clip in-place + loop-matched on its importer and
        /// reimports — fixes the "moves then slides back each cycle" drift. The
        /// OnPreprocessAnimation settings never persisted (postprocessor didn't
        /// re-fire), so the clips kept default root motion / un-looped pose; on loop
        /// the baked pose snapped back = visible slide. Sets lockRootPositionXZ +
        /// loopTime/loopPose authoritatively. Headless:
        /// -executeMethod DeNelle.Editor.ActionClipImporter.FixActionClipRootMotion
        /// </summary>
        [UnityEditor.MenuItem("Defenders/Animation/Fix Action Clip Root Motion (stop slide)")]
        public static void FixActionClipRootMotion()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { "Assets/Action" });
            int n = 0;
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                string lower = path.ToLowerInvariant();
                bool looping = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run");

                // Use defaultClipAnimations (the auto-split takes) as the basis and
                // commit them to clipAnimations with the in-place flags set.
                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;
                var kept = new System.Collections.Generic.List<ModelImporterClipAnimation>();
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    if (IsBindOrTPoseClipName(c.name)) continue;
                    c.loopTime = looping;
                    c.loopPose = looping;              // match start/end pose on loop = no snap
                    c.lockRootPositionXZ = true;       // bake horizontal root into pose = in place
                    c.keepOriginalPositionXZ = false;
                    c.lockRootHeightY = false;         // keep vertical (jumps/deaths)
                    c.keepOriginalPositionY = true;
                    c.lockRootRotation = false;
                    c.keepOriginalOrientation = true;
                    kept.Add(c);
                }
                if (kept.Count == 0) continue;
                importer.clipAnimations = kept.ToArray();
                importer.animationCompression = ModelImporterAnimationCompression.Optimal; // WO-283
                importer.SaveAndReimport();
                n++;
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[ActionClipImporter] FixActionClipRootMotion — {n} clip FBX set in-place + loop-matched.");
        }

        private static void StripTPoseFromImporterClips(ModelImporter importer, string path)
        {
            var source = importer.clipAnimations;
            if (source == null || source.Length == 0)
                source = importer.defaultClipAnimations;
            if (source == null || source.Length == 0) return;

            string lower = path.ToLowerInvariant();
            bool looping = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run");
            var kept = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            for (int i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (IsBindOrTPoseClipName(c.name)) continue;
                c.loopTime = looping;
                c.loopPose = looping;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = false;
                c.lockRootHeightY = false;
                c.keepOriginalPositionY = true;
                c.lockRootRotation = false;
                c.keepOriginalOrientation = true;
                kept.Add(c);
            }
            if (kept.Count > 0)
                importer.clipAnimations = kept.ToArray();
        }

        // ── Creature mesh rigs (orc/goblin/etc) ───────────────────────────────
        /// <summary>
        /// Flips creature character MESHES to Humanoid so the free Mixamo clips
        /// retarget onto them (same fix as the heroes — no CC5, no paid anims).
        /// Add a path to the list and re-run. Materials left intact.
        /// Headless: -executeMethod DeNelle.Editor.ActionClipImporter.RigCreaturesHumanoid
        /// </summary>
        [UnityEditor.MenuItem("Defenders/Animation/Rig Creatures Humanoid (orc/goblin)")]
        public static void RigCreaturesHumanoid()
        {
            string[] creatures =
            {
                "Assets/Models/KayKit/KayKit Mystery Monthly Series 4/1 - July 2023 - Orc Raider/character/OrcRaider.fbx",
            };
            int flipped = 0;
            foreach (var path in creatures)
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    UnityEngine.Debug.LogWarning($"[ActionClipImporter] creature FBX not found: {path}");
                    continue;
                }
                bool wasNonHuman = importer.animationType != ModelImporterAnimationType.Human;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                bool hasAvatar = false;
                foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Avatar av && av.isValid && av.isHuman) hasAvatar = true;
                UnityEngine.Debug.Log($"[ActionClipImporter] {path}: animType→Human, validHumanAvatar={hasAvatar}" +
                    (wasNonHuman ? " (flipped)" : ""));
                if (wasNonHuman) flipped++;
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[ActionClipImporter] RigCreaturesHumanoid done — {flipped} creature(s) flipped.");
        }

        // ── Hero mesh rigs ────────────────────────────────────────────────────
        /// <summary>
        /// Flips the playable hero MESHES (Knight/Mage/Ranger) from Generic to
        /// Humanoid so the free Mixamo clips (Assets/Action, also Humanoid) RETARGET
        /// onto them — fixes the T-pose (Generic mesh + Humanoid clips = no avatar
        /// to map onto = bones never move). Unlike the Action clips, these are real
        /// meshes, so materials are LEFT INTACT (only the rig/avatar changes).
        /// Headless: -executeMethod DeNelle.Editor.ActionClipImporter.RigHeroesHumanoid
        /// </summary>
        [UnityEditor.MenuItem("Defenders/Animation/Rig Heroes Humanoid (fix T-pose)")]
        public static void RigHeroesHumanoid()
        {
            string[] heroes =
            {
                "Assets/Resources/Heroes/Knight.fbx",
                "Assets/Resources/Heroes/Mage.fbx",
                "Assets/Resources/Heroes/Ranger.fbx",
            };
            int flipped = 0;
            foreach (var path in heroes)
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    UnityEngine.Debug.LogWarning($"[ActionClipImporter] hero FBX not found: {path}");
                    continue;
                }
                bool wasNonHuman = importer.animationType != ModelImporterAnimationType.Human;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
                // NOTE: do NOT touch materialImportMode here — heroes are visible meshes
                // and must keep their materials (unlike the motion-only Action clips).
                importer.SaveAndReimport();

                // Report whether a Humanoid avatar actually built (null = rig failed
                // to map; the hero would still T-pose and needs a manual avatar map).
                var avatar = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                bool hasAvatar = false;
                foreach (var a in avatar) if (a is Avatar av && av.isValid && av.isHuman) hasAvatar = true;
                UnityEngine.Debug.Log($"[ActionClipImporter] {path}: animType→Human, " +
                    $"validHumanAvatar={hasAvatar}" + (wasNonHuman ? " (flipped)" : ""));
                if (wasNonHuman) flipped++;
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[ActionClipImporter] RigHeroesHumanoid done — {flipped} hero mesh(es) flipped to Humanoid.");
        }
    }
}
