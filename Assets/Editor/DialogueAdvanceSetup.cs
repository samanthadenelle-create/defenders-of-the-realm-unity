// =============================================================================
// DialogueAdvanceSetup — makes the ClassicRPG dialogue advance on TAP/CLICK
// (mobile + desktop) the DOCUMENTED Yarn way, and tames the oversized "blue Next".
// -----------------------------------------------------------------------------
// Per docs/YARNSPINNER_DIALOGUE_NOTES.md, the intended advance path is:
//   LineAdvancer.inputMode = InputActions  (already set on the prefab)
//   LineAdvancer.hurryUpLineAction bound to <Pointer>/press   (← THE fix: one
//     binding covers mouse-click AND touch tap; the bundled Yarn-sample action
//     did not advance on mobile touch)
//   separateHurryUpAndAdvanceControls = false  (one control hurries + advances)
//   runner + presenter assigned (else Start() early-returns → hurry-only, no advance)
//
// We create our OWN tiny .inputactions asset (Dialogue/Advance → <Pointer>/press)
// so the binding is known-good and deterministic, then wire its
// InputActionReference onto the LineAdvancer via SerializedObject (no Yarn / no
// ClassicRPG asmdef reference needed — same reflection-light pattern as
// YarnDialogueSetup). We also shrink + soften the lineCompleteImage continue
// indicator (the big blue "Next") — restyle via serialized fields only, NEVER a
// package fork (the field is [MustNotBeNull], so we shrink/soften, not delete).
//
// Run AFTER YarnDialogueSetup (which re-copies the prefab fresh from the package
// each run and would wipe this wiring). Use the combined entry point:
//   Defenders > Yarn > Setup Dialogue System (Full)
//   (batchmode: DeNelle.Editor.DialogueAdvanceSetup.SetupAll)
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Editor
{
    public static class DialogueAdvanceSetup
    {
        const string DstPrefab      = "Assets/Resources/Dialogue/DialogueSystem.prefab";
        const string InputAssetPath = "Assets/Dialogue/DialogueAdvance.inputactions";
        const string MapName        = "Dialogue";
        const string ActionName     = "Advance";

        // YarnDialogueSetup copies a FRESH prefab from the package, so the advance
        // wiring must run AFTER it. This does both, in order, atomically.
        [MenuItem("Defenders/Yarn/Setup Dialogue System (Full)")]
        public static void SetupAll()
        {
            YarnDialogueSetup.Setup();
            Wire();
        }

        [MenuItem("Defenders/Yarn/Wire Tap-To-Advance")]
        public static void Wire()
        {
            InputActionReference advanceRef = EnsureAdvanceActionReference();
            if (advanceRef == null)
            {
                Debug.LogError("[DialogueAdvanceSetup] Could not create/load the Advance InputActionReference " +
                               $"from '{InputAssetPath}'. Aborting (tap-to-advance NOT wired).");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(DstPrefab) == null)
            {
                Debug.LogError($"[DialogueAdvanceSetup] {DstPrefab} not found — run " +
                               "'Defenders > Yarn > Setup Dialogue System' first. Aborting.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
            try
            {
                Component advancer = FindByExactType(root, "Yarn.Unity.LineAdvancer");
                if (advancer == null)
                {
                    Debug.LogError("[DialogueAdvanceSetup] No LineAdvancer on the dialogue prefab — cannot wire advancing.");
                    return;
                }

                var so = new SerializedObject(advancer);

                // The key fix: a known-good pointer (mouse + touch) advance binding.
                if (!SetObjRef(so, "hurryUpLineAction", advanceRef)) return;

                // One control hurries a typing line AND advances a finished one.
                SetBool(so, "separateHurryUpAndAdvanceControls", false);
                SetBool(so, "enableActions", true);

                // Both must be assigned or hurry-then-advance silently degrades to
                // hurry-only (LineAdvancer.Start early-returns).
                EnsureRefExact(so, root, "runner", "Yarn.Unity.DialogueRunner");
                EnsureRefBase(so, root, "presenter", "Yarn.Unity.DialoguePresenterBase", excludeExact: "Yarn.Unity.LineAdvancer");

                so.ApplyModifiedPropertiesWithoutUndo();

                TameChrome(root);

                PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
                Debug.Log("[DialogueAdvanceSetup] DONE — tap/click advance wired (<Pointer>/press) + continue " +
                          "indicator shrunk/softened. Mobile tap + desktop click now advance the dialogue.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── The pointer-bound advance action ─────────────────────────────────

        // Create (once) a tiny .inputactions asset with one Button action
        // "Dialogue/Advance" bound to <Pointer>/press, and return its generated
        // InputActionReference sub-asset. <Pointer>/press unifies mouse + touch.
        static InputActionReference EnsureAdvanceActionReference()
        {
            if (!File.Exists(InputAssetPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Dialogue"))
                    AssetDatabase.CreateFolder("Assets", "Dialogue");

                var asset = ScriptableObject.CreateInstance<InputActionAsset>();
                InputActionMap map = asset.AddActionMap(MapName);
                map.AddAction(ActionName, InputActionType.Button, "<Pointer>/press");
                string json = asset.ToJson();
                Object.DestroyImmediate(asset);

                File.WriteAllText(InputAssetPath, json);
                AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();
                Debug.Log($"[DialogueAdvanceSetup] Created {InputAssetPath} (Dialogue/Advance → <Pointer>/press).");
            }

            // The InputActionImporter generates an InputActionReference per action.
            Object[] subs = AssetDatabase.LoadAllAssetsAtPath(InputAssetPath);
            foreach (Object o in subs)
            {
                var r = o as InputActionReference;
                if (r != null && r.action != null && r.action.name == ActionName) return r;
            }
            return null;
        }

        // ── Continue indicator restyle (shrink + soften, no fork) ────────────

        static void TameChrome(GameObject root)
        {
            Component presenter = FindByBaseType(root, "Yarn.Unity.DialoguePresenterBase", "Yarn.Unity.LineAdvancer");
            if (presenter == null)
            {
                Debug.LogWarning("[DialogueAdvanceSetup] No RPGDialoguePresenter found — skipping chrome restyle.");
                return;
            }

            var pso = new SerializedObject(presenter);

            // Turn OFF the ClassicRPG chrome the owner doesn't want, via the package's
            // OWN feature-flag booleans (the documented override — no fork). The
            // backing fields are [ShowIf(useX)] so disabling the flag also suppresses
            // their [MustNotBeNull] validation.
            //   useActionButton — the blue "Next / Decide / Return" prompt bar up top
            //                     (redundant now that tap/click advances via LineAdvancer)
            //   useIcons        — the speaker icon slot (the decorative hearts)
            SetPresenterBool(pso, "useActionButton", false);
            SetPresenterBool(pso, "useIcons", false);
            pso.ApplyModifiedPropertiesWithoutUndo();

            // Belt-and-suspenders: deactivate the leftover decorative "Heart" objects
            // the sample box ships with, in case any sit outside the icon container.
            DeactivateByNamePrefix(root, "Heart");

            SerializedProperty img = pso.FindProperty("lineCompleteImage");
            if (img == null || img.objectReferenceValue == null)
            {
                Debug.LogWarning("[DialogueAdvanceSetup] lineCompleteImage not assigned — skipping restyle.");
                return;
            }

            var imgComp = img.objectReferenceValue as Component;
            if (imgComp == null) return;

            // Shrink uniformly (robust regardless of RectTransform anchoring) so the
            // big blue "Next" becomes a small, subtle "there's more" cue.
            imgComp.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            // Soften it a touch (alpha) so it reads as a hint, not a button.
            var iso = new SerializedObject(imgComp);
            SerializedProperty color = iso.FindProperty("m_Color");
            if (color != null)
            {
                color.colorValue = new Color(1f, 1f, 1f, 0.72f);
                iso.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(imgComp);
        }

        // ── Chrome helpers ───────────────────────────────────────────────────

        static void SetPresenterBool(SerializedObject pso, string prop, bool val)
        {
            var p = pso.FindProperty(prop);
            if (p != null) p.boolValue = val;
            else Debug.LogWarning($"[DialogueAdvanceSetup] RPGDialoguePresenter.{prop} not found " +
                                  "(ClassicRPG version changed?) — skipped.");
        }

        // Deactivate every GameObject under root whose name starts with prefix.
        static void DeactivateByNamePrefix(GameObject root, string prefix)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == root.transform) continue;
                if (t.name.StartsWith(prefix) && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    n++;
                }
            }
            if (n > 0) Debug.Log($"[DialogueAdvanceSetup] Deactivated {n} '{prefix}*' decoration object(s).");
        }

        // ── SerializedObject helpers ─────────────────────────────────────────

        static bool SetObjRef(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError($"[DialogueAdvanceSetup] LineAdvancer.{prop} not found — Yarn API changed. Aborting."); return false; }
            p.objectReferenceValue = val;
            return true;
        }

        static void SetBool(SerializedObject so, string prop, bool val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = val;
            else Debug.LogWarning($"[DialogueAdvanceSetup] LineAdvancer.{prop} not found (skipped).");
        }

        static void EnsureRefExact(SerializedObject so, GameObject root, string prop, string exactTypeName)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[DialogueAdvanceSetup] LineAdvancer.{prop} not found."); return; }
            if (p.objectReferenceValue != null) return;
            var c = FindByExactType(root, exactTypeName);
            if (c != null) p.objectReferenceValue = c;
            else Debug.LogWarning($"[DialogueAdvanceSetup] Could not find {exactTypeName} to assign to {prop}.");
        }

        static void EnsureRefBase(SerializedObject so, GameObject root, string prop, string baseTypeName, string excludeExact)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[DialogueAdvanceSetup] LineAdvancer.{prop} not found."); return; }
            if (p.objectReferenceValue != null) return;
            var c = FindByBaseType(root, baseTypeName, excludeExact);
            if (c != null) p.objectReferenceValue = c;
            else Debug.LogWarning($"[DialogueAdvanceSetup] Could not find a {baseTypeName} to assign to {prop}.");
        }

        // ── Reflection-light component finders ───────────────────────────────

        static Component FindByExactType(GameObject root, string fullName)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().FullName == fullName) return c;
            return null;
        }

        static Component FindByBaseType(GameObject root, string baseFullName, string excludeExact)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.FullName == excludeExact) continue;
                for (var bt = t; bt != null; bt = bt.BaseType)
                    if (bt.FullName == baseFullName) return c;
            }
            return null;
        }
    }
}
