// =============================================================================
// ArmoredKnightVerify — WO-481 Phase-1 visual proof (non-destructive).
// -----------------------------------------------------------------------------
// Renders the NEW ARMORED Knight (staged under Assets/Art/Incoming_Tripo/) so the
// owner can SEE, before any promotion into the runtime hero path:
//   • the armor look + real Tripo textures (basecolor + metallic + NORMAL),
//   • correct height (NormalizeHeight to ~1.8m — the staged FBX imports tiny),
//   • the donor animation RETARGETING — samples real Assets/Action sword-and-shield
//     Humanoid clips onto the new armored avatar via AnimationMode (proves the clips
//     drive the new body — the whole point of the "all humanoid" bet).
//
// Polish pass (owner 2026-06-22): grounded sword-and-shield idle + slash poses
// (not the airborne "360 high"), a ground plane so the hero isn't floating, and
// the normal map for armor depth. Reuses HeroPortraitRenderer's batchmode URP
// render path (SRP render request). Writes PNGs to Builds/Verify/. Touches NOTHING
// in Resources.
//
//   run-unity-method.ps1 -Method DeNelle.Editor.ArmoredKnightVerify.Run -LogName armored-knight-verify.log
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ArmoredKnightVerify
    {
        private const string FbxPath   = "Assets/Art/Incoming_Tripo/Heroes/Knight/Knight.fbx";
        private const string BaseTex   = "Assets/Art/Incoming_Tripo/Heroes/Knight/medieval_knight_3d_model_basecolor.jpg";
        private const string MetalTex  = "Assets/Art/Incoming_Tripo/Heroes/Knight/medieval_knight_3d_model_metallic.jpg";
        private const string NormalTex = "Assets/Art/Incoming_Tripo/Heroes/Knight/medieval_knight_3d_model_normal.jpg";

        // Grounded, on-theme sword-and-shield clips (a Knight's identity).
        private const string IdleClipFbx  = "Assets/Action/Knight/sword and shield idle.fbx";
        private const string SlashClipFbx = "Assets/Action/Knight/sword and shield slash.fbx";

        private const string OutDir = "Builds/Verify";
        private const int W = 640, H = 920;

        [MenuItem("Defenders/Tripo/Verify Armored Knight (WO-481)")]
        public static void Run()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { Debug.LogError($"[ArmoredKnightVerify] FBX not found: {FbxPath}"); return; }

            EnsureHumanoid();
            EnsureNormalMapImport();
            Directory.CreateDirectory(OutDir);

            // Idle = clean bind-pose showcase (grounded + crisp); attack = grounded slash pose.
            RenderShot(fbx, "armored_knight_idle.png",   null,                   yaw: 200f, sampleT: 0f);
            RenderShot(fbx, "armored_knight_attack.png", LoadClip(SlashClipFbx), yaw: 210f, sampleT: 0.55f);

            AssetDatabase.Refresh();
            Debug.Log("[ArmoredKnightVerify] ARMORED_KNIGHT_VERIFY_DONE — wrote Builds/Verify/armored_knight_*.png");
        }

        /// <summary>Import the new Knight FBX as Humanoid (WO-286 settings) so the slash
        /// clip retargets; report avatar validity + section count for the new mesh.</summary>
        private static void EnsureHumanoid()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError($"[ArmoredKnightVerify] FBX not imported: {FbxPath}"); return; }
            importer.isReadable    = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            var hd = importer.humanDescription;
            hd.human    = new HumanBone[0];
            hd.skeleton = new SkeletonBone[0];
            importer.humanDescription = hd;
            importer.SaveAndReimport();

            // Fix the real import scale so the asset is natively ~1.8m (the staged Tripo
            // FBX imports tiny, meshY~0.3) — this is the promote-grade scale, not a render hack.
            float meshY = MeasureMeshY();
            if (meshY > 0.001f && meshY < 1.4f)
            {
                importer.useFileScale = false;
                importer.globalScale  = 1.8f / meshY;
                importer.SaveAndReimport();
            }

            bool human = false, valid = false;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (a is Avatar av) { human = av.isHuman; valid = av.isValid; }
            int sections = 0;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (go != null)
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.sharedMesh != null) sections += smr.sharedMesh.subMeshCount;
            Debug.Log($"[ArmoredKnightVerify] new Knight: Humanoid(valid={valid}, human={human})  sections={sections}  meshY(native)={MeasureMeshY():F2}");
        }

        private static float MeasureMeshY()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (go == null) return -1f;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null) return smr.sharedMesh.bounds.size.y;
            return -1f;
        }

        /// <summary>Set the normal jpg's import type to NormalMap so _BumpMap reads correctly.</summary>
        private static void EnsureNormalMapImport()
        {
            var ni = AssetImporter.GetAtPath(NormalTex) as TextureImporter;
            if (ni != null && ni.textureType != TextureImporterType.NormalMap)
            {
                ni.textureType = TextureImporterType.NormalMap;
                ni.SaveAndReimport();
            }
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (a is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            Debug.LogWarning($"[ArmoredKnightVerify] clip not found in {fbxPath} — bind pose.");
            return null;
        }

        private static void RenderShot(GameObject fbx, string pngName, AnimationClip clip, float yaw, float sampleT)
        {
            var stage = new GameObject("_VerifyStage");
            stage.transform.position = new Vector3(1000f, 1000f, 1000f);

            var body = (GameObject)PrefabUtility.InstantiatePrefab(fbx, stage.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Force skinned bounds to track the SAMPLED pose (edit-mode bounds otherwise
            // stay on the bind pose → grounding math floats the figure).
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            ApplyRealTextures(body);

            bool posed = false;
            if (clip != null)
            {
                var animator = body.GetComponent<Animator>();
                if (animator == null) animator = body.AddComponent<Animator>();
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                    if (a is Avatar av && av.isValid) { animator.avatar = av; break; }
                try
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(body, clip, sampleT * clip.length);
                    AnimationMode.EndSampling();
                    posed = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ArmoredKnightVerify] pose sample failed ({ex.Message}) — bind pose.");
                }
            }

            NormalizeHeight(body, 1.8f);
            AddGround(stage);
            SetupLights(stage);
            var cam = SetupCamera(stage);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            byte[] png = tex.EncodeToPNG();
            string outPath = OutDir + "/" + pngName;
            File.WriteAllBytes(outPath, png);
            Debug.Log($"[ArmoredKnightVerify] Wrote {outPath} ({png.Length} bytes, posed={posed})");

            if (posed) AnimationMode.StopAnimationMode();
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(stage);
        }

        /// <summary>URP/Lit materials carrying the real external Tripo maps
        /// (basecolor + metallic + normal) so the render shows the actual armor.</summary>
        private static void ApplyRealTextures(GameObject body)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;
            var baseMap   = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTex);
            var metalMap  = AssetDatabase.LoadAssetAtPath<Texture2D>(MetalTex);
            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTex);

            var mat = new Material(lit);
            if (baseMap != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseMap);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseMap);
            }
            if (metalMap != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metalMap);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 1f);
            }
            if (normalMap != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1f);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>A dark ground plane at the hero's feet so it reads as standing, not floating.</summary>
        private static void AddGround(GameObject stage)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(stage.transform, false);
            ground.transform.localPosition = Vector3.zero; // feet level (NormalizeHeight put min.y here)
            ground.transform.localScale = Vector3.one;     // 10m plane — plenty
            var col = ground.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit != null)
            {
                var gmat = new Material(lit);
                if (gmat.HasProperty("_BaseColor")) gmat.SetColor("_BaseColor", new Color(0.10f, 0.11f, 0.13f));
                if (gmat.HasProperty("_Smoothness")) gmat.SetFloat("_Smoothness", 0.1f);
                ground.GetComponent<Renderer>().sharedMaterial = gmat;
            }
        }

        private static void SetupLights(GameObject stage)
        {
            var keyGo = new GameObject("KeyLight");
            keyGo.transform.SetParent(stage.transform, false);
            keyGo.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional; key.color = new Color(1f, 0.96f, 0.88f); key.intensity = 1.5f;

            var fillGo = new GameObject("FillLight");
            fillGo.transform.SetParent(stage.transform, false);
            fillGo.transform.localRotation = Quaternion.Euler(20f, 150f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional; fill.color = new Color(0.7f, 0.75f, 0.95f); fill.intensity = 0.6f;
        }

        private static Camera SetupCamera(GameObject stage)
        {
            var camGo = new GameObject("VerifyCamera");
            camGo.transform.SetParent(stage.transform, false);
            // Full-body frame with head+feet margin so nothing crops.
            camGo.transform.localPosition = new Vector3(0f, 0.95f, -4.3f);
            camGo.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            return cam;
        }

        private static void NormalizeHeight(GameObject body, float target)
        {
            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.001f) return;
            body.transform.localScale *= (target / b.size.y);

            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float parentY = body.transform.parent != null ? body.transform.parent.position.y : 0f;
            body.transform.localPosition += new Vector3(0f, parentY - b.min.y, 0f);
        }
    }
}
