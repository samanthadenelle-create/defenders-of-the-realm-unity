// WO-1135 visual evidence: side-by-side color and grayscale wall-tier capture.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeNelle.Village.Walls;

namespace DeNelle.Editor
{
    public static class WallTierProofCapture
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const string OutputDir = "docs/ui-evidence";

        [MenuItem("Defenders/Art/Capture Wall Tier Proof")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutputDir);

            BuildGround();
            BuildLight();
            BuildWall(WallTier.Wood, -5f, "TIER 1  WOOD");
            BuildWall(WallTier.Iron, 0f, "TIER 2  IRON");
            BuildWall(WallTier.ReinforcedSteel, 5f, "TIER 3  STEEL");

            var cameraGo = new GameObject("ProofCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 4.2f, -17f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.5f, 0f) - camera.transform.position);
            camera.fieldOfView = 38f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.09f);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var color = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            color.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            color.Apply();
            File.WriteAllBytes(OutputDir + "/wo1135_wall_tiers_color.png", color.EncodeToPNG());

            var pixels = color.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = pixels[i].r * 0.2126f + pixels[i].g * 0.7152f + pixels[i].b * 0.0722f;
                pixels[i] = new Color(value, value, value, pixels[i].a);
            }
            color.SetPixels(pixels);
            color.Apply();
            File.WriteAllBytes(OutputDir + "/wo1135_wall_tiers_grayscale.png", color.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(color);
            AssetDatabase.Refresh();
            Debug.Log("WALL_TIER_PROOF_OK color=docs/ui-evidence/wo1135_wall_tiers_color.png " +
                      "grayscale=docs/ui-evidence/wo1135_wall_tiers_grayscale.png");
        }

        private static void BuildWall(WallTier tier, float x, string label)
        {
            var source = Resources.Load<GameObject>(WallTierData.Get(tier).SegmentPrefabPath);
            if (source == null) throw new FileNotFoundException("Wall prefab missing", WallTierData.Get(tier).SegmentPrefabPath);
            var seat = new GameObject(label.Replace(' ', '_'));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.transform.SetParent(seat.transform, false);
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            Bounds bounds = BoundsOf(model);
            Vector3 scale = seat.transform.localScale;
            if (bounds.size.x > 0.001f) scale.x *= 4f / bounds.size.x;
            if (bounds.size.y > 0.001f) scale.y *= 3f / bounds.size.y;
            if (bounds.size.z > 0.001f) scale.z *= 1.5f / bounds.size.z;
            seat.transform.localScale = scale;
            bounds = BoundsOf(model);
            seat.transform.position = new Vector3(x, -bounds.min.y, 0f);

            var textGo = new GameObject("Label");
            textGo.transform.position = new Vector3(x, 3.8f, 0f);
            textGo.transform.rotation = Quaternion.identity;
            var text = textGo.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.08f;
            text.color = Color.white;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "NeutralGround";
            ground.transform.localScale = new Vector3(2.2f, 1f, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.16f, 0.16f, 0.17f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void BuildLight()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.36f);
            var lightGo = new GameObject("KeyLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.94f, 0.84f);
            lightGo.transform.rotation = Quaternion.Euler(38f, -30f, 0f);
        }
    }
}
