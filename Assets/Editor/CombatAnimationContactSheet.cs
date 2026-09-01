using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Renders sampled Humanoid motion poses for contact-frame review.</summary>
    public static class CombatAnimationContactSheet
    {
        private const string ModelPath = DeNelle.Core.AssetRoots.EnemyContent + "/Orc_Warrior.fbx";
        private static readonly string[] ClipPaths =
        {
            "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/atk_slashleft.fbx",
            "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/atk_slashright.fbx",
            "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/atk_slashup.fbx",
            "Assets/Action/Knight/standing melee attack 360 high.fbx",
            "Assets/Action/Knight/standing melee attack downward.fbx",
            "Assets/Action/Knight/standing melee attack horizontal.fbx",
            "Assets/Action/Knight/standing melee combo attack ver. 1.fbx",
            "Assets/Action/Knight/standing melee combo attack ver. 2.fbx",
            "Assets/Action/Sword And Shield Attack.fbx"
        };

        public static void Render()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null) throw new InvalidOperationException("Review model missing: " + ModelPath);
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string output = Path.Combine(root, "Builds", "combat-animation-contact-sheets");
            Directory.CreateDirectory(output);

            int rendered = 0;
            foreach (string clipPath in ClipPaths)
            {
                AnimationClip clip = ResolveClip(clipPath);
                if (clip == null) throw new InvalidOperationException("Review clip missing: " + clipPath);
                float candidate = RenderClip(modelAsset, clip,
                    Path.Combine(output, Path.GetFileNameWithoutExtension(clipPath) + ".png"));
                Debug.Log($"[CombatContactReview] {clipPath} rightHandVelocityPeak={candidate:0.000}");
                rendered++;
            }
            Debug.Log($"COMBAT_CONTACT_SHEETS_OK rendered={rendered} output={output}");
        }

        private static float RenderClip(GameObject modelAsset, AnimationClip clip, string outputPath)
        {
            const int cell = 384;
            const int samples = 9;
            var sheet = new Texture2D(cell * samples, cell, TextureFormat.RGB24, false);
            var preview = new PreviewRenderUtility();
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(modelAsset);
                instance.hideFlags = HideFlags.HideAndDontSave;
                AddHandMarker(instance, HumanBodyBones.RightHand, new Color(1f, 0.15f, 0.05f), "RIGHT-HIT");
                AddHandMarker(instance, HumanBodyBones.LeftHand, new Color(0.05f, 0.8f, 1f), "LEFT-GUARD");
                preview.AddSingleGO(instance);
                float candidate = FindRightHandVelocityPeak(instance, clip);
                preview.lights[0].intensity = 2.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                preview.lights[1].intensity = 1.2f;
                preview.ambientColor = new Color(0.55f, 0.55f, 0.58f);
                preview.camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

                Bounds bounds = BoundsFor(instance);
                float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                preview.camera.transform.position = bounds.center + new Vector3(0f, radius * 0.1f, radius * 2.6f);
                preview.camera.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.1f);
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = radius * 10f;
                preview.cameraFieldOfView = 32f;

                for (int i = 0; i < samples; i++)
                {
                    float normalized = Mathf.Clamp01(candidate - 0.1f + i * 0.025f);
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(instance, clip, clip.length * normalized);
                    AnimationMode.EndSampling();

                    preview.BeginPreview(new Rect(0, 0, cell, cell), GUIStyle.none);
                    preview.camera.Render();
                    Texture rendered = preview.EndPreview();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rendered as RenderTexture;
                    var frame = new Texture2D(cell, cell, TextureFormat.RGB24, false);
                    frame.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
                    frame.Apply();
                    sheet.SetPixels(i * cell, 0, cell, cell, frame.GetPixels());
                    UnityEngine.Object.DestroyImmediate(frame);
                    RenderTexture.active = previous;
                    AnimationMode.StopAnimationMode();
                }
                sheet.Apply();
                File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
                return candidate;
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                preview.Cleanup();
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        private static float FindRightHandVelocityPeak(GameObject instance, AnimationClip clip)
        {
            Animator animator = instance.GetComponentInChildren<Animator>();
            Transform hand = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null) throw new InvalidOperationException("Review model has no Humanoid right hand.");

            Vector3 previous = Vector3.zero;
            float bestSpeed = -1f;
            float bestNormalized = 0.5f;
            for (int i = 10; i <= 90; i++)
            {
                float normalized = i / 100f;
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(instance, clip, clip.length * normalized);
                AnimationMode.EndSampling();
                Vector3 current = hand.position;
                AnimationMode.StopAnimationMode();
                if (i > 10)
                {
                    float speed = (current - previous).magnitude;
                    if (speed > bestSpeed) { bestSpeed = speed; bestNormalized = normalized - 0.005f; }
                }
                previous = current;
            }
            return bestNormalized;
        }

        private static AnimationClip ResolveClip(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    return clip;
            return null;
        }

        private static Bounds BoundsFor(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.up, new Vector3(2f, 2f, 2f));
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AddHandMarker(GameObject instance, HumanBodyBones bone, Color color, string markerName)
        {
            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            Transform hand = animator.GetBoneTransform(bone);
            if (hand == null) return;
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = markerName;
            marker.transform.SetParent(hand, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 0.16f;
            var collider = marker.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.color = color;
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
