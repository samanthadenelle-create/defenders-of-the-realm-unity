// =============================================================================
// PetPortraitRenderer — bakes Resources/PetPortraits/<id>.png from the starter
// pet FBXs so the game can render pets as lightweight camera-facing sprite
// billboards instead of shipping their 208 MB of Tripo 3D meshes (WO-211
// Phase 2 "lite pet visuals"; the pet meshes are the dominant WebGL bloat).
// -----------------------------------------------------------------------------
// Adapts HeroPortraitRenderer's proven render-FBX-to-PNG path:
//   1. Instantiate Resources/Pets/<species>.fbx into a temp off-stage root.
//   2. Rebuild every Renderer's materials as URP/Lit, carrying the embedded
//      texture across (matches the runtime TripoMaterialFixer) so the pet reads
//      textured instead of as a white/magenta silhouette.
//   3. Place a portrait camera + key/fill lights, render to a 256x256
//      RenderTexture with a TRANSPARENT background via the SRP render-request
//      API (Camera.Render() does not run URP in batchmode).
//   4. Encode to PNG, save under Assets/Resources/PetPortraits/<id>.png, where
//      <id> is the PetCatalog id ("pet-<species>", e.g. pet-aether-sprite).
//   5. Reimport with textureType=Sprite, alphaIsTransparency, max 256, compressed.
//
// Does NOT delete the source FBX — the gatekeeper removes the meshes after the
// render. Batchmode entry: DeNelle.Editor.PetPortraitRenderer.Render
//   (MenuItem: Defenders → Art → Render Pet Portraits).
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class PetPortraitRenderer
    {
        // Must live under a Resources/ folder so the runtime can Resources.Load
        // the sprite by id ("PetPortraits/pet-<species>"). PetSelectController
        // and the lite-pet billboard both load from this path.
        private const string ResourcesDir = "Assets/Resources/PetPortraits";
        private const int PortraitSize = 256;

        // The three starter species. The PetCatalog id is "pet-<species>"
        // (confirmed against pets.json: pet-aether-sprite / pet-flame-pup /
        // pet-ice-wolf), so the PNG file name is "pet-<species>.png".
        private static readonly string[] Species =
        {
            "aether-sprite",
            "flame-pup",
            "ice-wolf",
        };

        [MenuItem("Defenders/Art/Render Pet Portraits")]
        public static void Render()
        {
            Directory.CreateDirectory(ResourcesDir);
            int wrote = 0;
            foreach (var species in Species)
            {
                if (RenderOne(species)) wrote++;
            }
            AssetDatabase.Refresh();
            Debug.Log("PET_PORTRAITS_OK :: wrote=" + wrote);
        }

        private static bool RenderOne(string species)
        {
            string id = "pet-" + species; // PetCatalog id == "pet-<species>"
            string fbxPath = "Assets/Resources/Pets/" + species + ".fbx";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError("[PetPortraitRenderer] FBX not found: " + fbxPath);
                return false;
            }

            // Temp stage root — destroyed before this function returns.
            var stage = new GameObject("_PetPortraitStage");
            stage.transform.position = new Vector3(1000f, 1000f, 1000f); // off-stage

            var body = (GameObject)PrefabUtility.InstantiatePrefab(fbx, stage.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.Euler(0f, 200f, 0f); // 3/4 view
            RetargetToUrp(body);
            NormalizeHeight(body, 1.4f);

            // Three-point-ish light: a strong key light, a softer fill.
            var keyLightGo = new GameObject("KeyLight");
            keyLightGo.transform.SetParent(stage.transform, false);
            keyLightGo.transform.localPosition = new Vector3(1.5f, 2.5f, -2f);
            keyLightGo.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            var keyLight = keyLightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.96f, 0.88f);
            keyLight.intensity = 1.4f;

            var fillLightGo = new GameObject("FillLight");
            fillLightGo.transform.SetParent(stage.transform, false);
            fillLightGo.transform.localPosition = new Vector3(-1.5f, 1.8f, -1.5f);
            fillLightGo.transform.localRotation = Quaternion.Euler(25f, 30f, 0f);
            var fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.65f, 0.70f, 0.95f);
            fillLight.intensity = 0.5f;

            // Camera + framing. Re-centre on the body's bounds so a small/odd
            // pivot (Tripo meshes pivot at the mesh centre) still frames cleanly,
            // then back the camera off to fit the whole body in view.
            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(stage.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            // TRANSPARENT background — a SolidColor clear at alpha 0 + the ARGB32
            // RenderTexture + EncodeToPNG carries the alpha through so the pet
            // reads on its own card / in-world without a baked dark box.
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = false;
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;

            FrameBody(cam, body);

            // Render to a RenderTexture, read back, encode PNG, save.
            var rt = new RenderTexture(PortraitSize, PortraitSize, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            // Camera.Render() does NOT draw under URP in batchmode — it only emits
            // the transparent clear, producing a blank PNG. Use the SRP
            // render-request API (Unity 2022.2+/6) which actually runs the URP
            // pipeline into the target; fall back to the legacy call otherwise.
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, PortraitSize, PortraitSize), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            byte[] pngBytes = tex.EncodeToPNG();
            string outPath = ResourcesDir + "/" + id + ".png";
            File.WriteAllBytes(outPath, pngBytes);
            Debug.Log("[PetPortraitRenderer] Wrote " + outPath + " (" + pngBytes.Length + " bytes)");

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(stage);

            // Import the PNG as a transparent sprite (after Refresh so the asset
            // exists). Done per-file rather than in a batch so the texture
            // settings are applied even if a later species fails.
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(outPath);
            return true;
        }

        // Re-centre + back the camera off so the full body fits the frame using
        // the body's renderer bounds — robust to Tripo's mesh-centre pivots and
        // varying species sizes.
        private static void FrameBody(Camera cam, GameObject body)
        {
            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                cam.transform.localPosition = new Vector3(0f, 0.8f, -2.5f);
                cam.transform.localRotation = Quaternion.identity;
                return;
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float radius = b.extents.magnitude;
            if (radius <= 0.01f) radius = 0.7f;
            // Distance to fit the bounding sphere in the vertical FOV, with a
            // small margin so nothing clips the frame edge.
            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float dist = (radius / Mathf.Tan(halfFov)) * 1.25f;

            Vector3 center = b.center;
            cam.transform.position = center + new Vector3(0f, 0f, -dist);
            cam.transform.LookAt(center);
        }

        private static void ConfigureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[PetPortraitRenderer] No TextureImporter for " + assetPath);
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.maxTextureSize = PortraitSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void RetargetToUrp(GameObject body)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (src.shader != null && src.shader.name != null &&
                        src.shader.name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
                        continue;
                    Texture tex = null;
                    if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                    if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                    Color col = src.HasProperty("_Color") ? src.color : Color.white;

                    var newMat = new Material(lit);
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", col);
                    if (newMat.HasProperty("_Color"))     newMat.SetColor("_Color", col);
                    if (tex != null)
                    {
                        if (newMat.HasProperty("_BaseMap")) newMat.SetTexture("_BaseMap", tex);
                        if (newMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", tex);
                    }
                    if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", 0.2f);
                    if (newMat.HasProperty("_Metallic"))   newMat.SetFloat("_Metallic", 0f);
                    mats[i] = newMat;
                }
                r.sharedMaterials = mats;
            }
        }

        private static void NormalizeHeight(GameObject body, float target)
        {
            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.01f) return;
            body.transform.localScale *= (target / b.size.y);
        }
    }
}
