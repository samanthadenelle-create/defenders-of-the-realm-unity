// WO-1143 reproduce-first proof through the real troop deployment path.
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class CatapultProofCapture
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const string Output = "docs/ui-evidence/wo1143_catapult_after.png";

        [MenuItem("Defenders/Troops/Capture Catapult Deployment Proof")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("docs/ui-evidence");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "RaidGround";
            ground.transform.position = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(18f, 0.5f, 12f);
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();

            TroopController footman = TroopDeployer.SpawnTroop("troop-footman", new Vector3(-3f, 0f, 0f));
            TroopController catapult = TroopDeployer.SpawnTroop("troop-catapult", new Vector3(3f, 0f, 0f));
            if (footman == null || catapult == null)
                throw new System.InvalidOperationException("real TroopDeployer failed to spawn proof pair");

            TraceBounds("footman", footman.gameObject);
            TraceBounds("catapult", catapult.gameObject);
            AddLabel(new Vector3(-3f, 3.6f, 0f), "FOOTMAN");
            AddLabel(new Vector3(3f, 3.6f, 0f), "CATAPULT");

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.38f, 0.40f);
            var lightGo = new GameObject("KeyLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightGo.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            var cameraGo = new GameObject("ProofCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 4.6f, -14f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.4f, 0f) - camera.transform.position);
            camera.fieldOfView = 42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.09f);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Output, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(image);
            AssetDatabase.Refresh();
            Debug.Log("CATAPULT_PROOF_OK output=" + Output);
        }

        private static void TraceBounds(string id, GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[Flow:TroopVisual] PROOF id=" + id + " has no renderer");
                return;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Transform visual = root.transform.childCount > 0 ? root.transform.GetChild(0) : root.transform;
            Debug.Log("[Flow:TroopVisual] PROOF id=" + id +
                      " rootScale=" + root.transform.localScale +
                      " visual='" + visual.name + "'" +
                      " visualEuler=" + visual.localEulerAngles +
                      " visualScale=" + visual.localScale +
                      " bounds=" + bounds.size);
        }

        private static void AddLabel(Vector3 position, string value)
        {
            var go = new GameObject(value + "_Label");
            go.transform.position = position;
            var text = go.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.08f;
            text.color = Color.white;
        }
    }
}
