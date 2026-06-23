// =============================================================================
// BattleAnchorStageVerify — WO-481 slice 1: stage the battle-anchor TABLEAU.
// -----------------------------------------------------------------------------
// Composes the V1 fight as a staged tableau (the combat-pivot battle anchor):
// the armored Knight (left, facing in) vs an Orc FAMILY — Warrior LEADER forward
// + Tank & Mage followers flanking behind — under one composed camera. Renders a
// PNG so the owner can judge the composition + formation BEFORE it's wired into
// AtbCombatantSwapper. Loads the staged Tripo models directly (no Resources/scene
// edits). Non-destructive throwaway harness — the positions it proves become the
// real anchor stance values.
//
//   run-unity-method.ps1 -Method DeNelle.Editor.BattleAnchorStageVerify.Run -LogName battle-anchor.log
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BattleAnchorStageVerify
    {
        private const string Inc = "Assets/Art/Incoming_Tripo/";
        private const string OutPng = "Builds/Verify/battle_anchor_tableau.png";
        private const int W = 1180, H = 680;

        // fbx, textureFolder, texturePrefix, idleClip, pos, yaw, targetHeight
        private struct Actor
        {
            public string Fbx, TexDir, TexPrefix, Clip; public Vector3 Pos; public float Yaw, Height;
        }

        [MenuItem("Defenders/Tripo/Battle Anchor Tableau (WO-481)")]
        public static void Run()
        {
            var actors = new[]
            {
                new Actor { Fbx = Inc+"Heroes/Knight/Knight.fbx", TexDir = Inc+"Heroes/Knight/", TexPrefix = "medieval_knight_3d_model",
                            Clip = "Assets/Action/Knight/sword and shield idle.fbx", Pos = new Vector3(-2.6f,0,0.2f), Yaw = 90f, Height = 1.80f },
                new Actor { Fbx = Inc+"Enemies/Orcs/Orc_Warrior/Orc_Warrior.fbx", TexDir = Inc+"Enemies/Orcs/Orc_Warrior/", TexPrefix = "Orc_Warrior",
                            Clip = "Assets/Action/Orc Idle.fbx", Pos = new Vector3(2.4f,0,0f), Yaw = 270f, Height = 2.00f },   // LEADER
                new Actor { Fbx = Inc+"Enemies/Orcs/Orc_Tank/Orc_Tank.fbx", TexDir = Inc+"Enemies/Orcs/Orc_Tank/", TexPrefix = "Orc_Tank",
                            Clip = "Assets/Action/Orc Idle.fbx", Pos = new Vector3(3.7f,0,1.5f), Yaw = 250f, Height = 2.15f },
                new Actor { Fbx = Inc+"Enemies/Orcs/Orc_Mage/Orc_Mage.fbx", TexDir = Inc+"Enemies/Orcs/Orc_Mage/", TexPrefix = "Orc_Mage",
                            Clip = "Assets/Action/Orc Idle.fbx", Pos = new Vector3(3.7f,0,-1.5f), Yaw = 290f, Height = 1.85f },
            };

            Directory.CreateDirectory("Builds/Verify");
            var stage = new GameObject("_AnchorStage");

            var bodies = new GameObject[actors.Length];
            var clips  = new AnimationClip[actors.Length];
            for (int i = 0; i < actors.Length; i++)
                bodies[i] = StageActor(actors[i], stage.transform, out clips[i]);

            // Pose all four in one AnimationMode block (idle, settled frame).
            try
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                for (int i = 0; i < bodies.Length; i++)
                    if (clips[i] != null) AnimationMode.SampleAnimationClip(bodies[i], clips[i], 0.2f * clips[i].length);
                AnimationMode.EndSampling();
            }
            catch (System.Exception ex) { Debug.LogWarning($"[BattleAnchor] pose failed ({ex.Message}) — bind pose."); }

            // Place each by its FOOT-CENTER on the posed bounds (absorbs Tripo off-pivot + scale).
            for (int i = 0; i < bodies.Length; i++) PlaceFootCenter(bodies[i], actors[i].Pos, stage.transform);

            AddGround(stage);
            AddLights(stage);
            var cam = AddCamera(stage, new Vector3(0.5f, 1.15f, 0.3f));

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(OutPng, tex.EncodeToPNG());
            Debug.Log($"[BattleAnchor] BATTLE_ANCHOR_DONE — wrote {OutPng}");

            AnimationMode.StopAnimationMode();
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(stage);
            AssetDatabase.Refresh();
        }

        private static GameObject StageActor(Actor a, Transform parent, out AnimationClip clip)
        {
            clip = null;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(a.Fbx);
            if (fbx == null) { Debug.LogError($"[BattleAnchor] FBX missing: {a.Fbx}"); return new GameObject("_missing"); }

            var body = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            body.transform.localPosition = Vector3.zero; // placed by foot-center AFTER pose (Tripo off-pivot)
            body.transform.localRotation = Quaternion.Euler(0f, a.Yaw, 0f);

            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            ApplyTextures(body, a.TexDir + a.TexPrefix);

            // Avatar for Humanoid retarget.
            var animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.AddComponent<Animator>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(a.Fbx))
                if (obj is Avatar av && av.isValid) { animator.avatar = av; break; }

            ScaleToHeight(body, a.Height);

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(a.Clip))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview")) { clip = c; break; }
            return body;
        }

        private static void ApplyTextures(GameObject body, string prefix)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;
            string nPath = prefix + "_normal.jpg";
            var ni = AssetImporter.GetAtPath(nPath) as TextureImporter;
            if (ni != null && ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_basecolor.jpg");
            var metMap  = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_metallic.jpg");
            var norMap  = AssetDatabase.LoadAssetAtPath<Texture2D>(nPath);

            var mat = new Material(lit);
            if (baseMap != null) { if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseMap); if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseMap); }
            if (metMap != null && mat.HasProperty("_MetallicGlossMap")) { mat.SetTexture("_MetallicGlossMap", metMap); if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 1f); }
            if (norMap != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", norMap); mat.EnableKeyword("_NORMALMAP"); }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void ScaleToHeight(GameObject body, float target)
        {
            var b = WorldBounds(body); if (b.size.y <= 0.001f) return;
            body.transform.localScale *= (target / b.size.y);
        }

        // Shift the body so its mesh foot-center lands at the intended local target —
        // robust to Tripo off-pivot meshes (the transform origin is NOT the model center).
        private static void PlaceFootCenter(GameObject body, Vector3 localTarget, Transform parent)
        {
            var b = WorldBounds(body);
            Vector3 footCenter = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 targetWorld = (parent != null ? parent.position : Vector3.zero) + localTarget;
            body.transform.position += (targetWorld - footCenter);
        }

        private static Bounds WorldBounds(GameObject body)
        {
            var rs = body.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0) return new Bounds(body.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void AddGround(GameObject stage)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.transform.SetParent(stage.transform, false);
            g.transform.localScale = Vector3.one * 2f;
            var col = g.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) { var m = new Material(lit); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f,0.12f,0.14f)); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",0.08f); g.GetComponent<Renderer>().sharedMaterial = m; }
        }

        private static void AddLights(GameObject stage)
        {
            var k = new GameObject("Key"); k.transform.SetParent(stage.transform, false); k.transform.localRotation = Quaternion.Euler(40f,-20f,0f);
            var kl = k.AddComponent<Light>(); kl.type = LightType.Directional; kl.color = new Color(1f,0.96f,0.88f); kl.intensity = 1.5f;
            var f = new GameObject("Fill"); f.transform.SetParent(stage.transform, false); f.transform.localRotation = Quaternion.Euler(18f,160f,0f);
            var fl = f.AddComponent<Light>(); fl.type = LightType.Directional; fl.color = new Color(0.68f,0.74f,0.95f); fl.intensity = 0.55f;
        }

        private static Camera AddCamera(GameObject stage, Vector3 lookAt)
        {
            var camGo = new GameObject("AnchorCam"); camGo.transform.SetParent(stage.transform, false);
            camGo.transform.position = new Vector3(0.5f, 3.3f, -8.8f);
            camGo.transform.LookAt(lookAt);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f,0.14f,0.17f,1f);
            cam.fieldOfView = 36f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            return cam;
        }
    }
}
