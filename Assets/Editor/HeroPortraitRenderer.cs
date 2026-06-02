// =============================================================================
// HeroPortraitRenderer — bakes Resources/HeroPortraits/{knight,ranger}.png from
// the textured FBXes so the Title + HeroSelect flanks read as proper portraits
// instead of white silhouettes.
// -----------------------------------------------------------------------------
// Owner 2026-05-20 (screenshot): Knight + Ranger flanks rendered as
// untextured white silhouettes because the earlier HeroPortraitGenerator used
// AssetPreview, and Tripo FBXs have Phong materials that AssetPreview can't
// resolve in URP.
//
// This editor utility takes a different path:
//   1. Instantiate the FBX into a temp scene root.
//   2. Walk every Renderer and rebuild its materials as URP/Lit, carrying
//      the embedded texture across (matches the runtime TripoMaterialFixer).
//   3. Place a portrait camera + light, render to a RenderTexture.
//   4. Encode to PNG, save under Resources/HeroPortraits/.
// -----------------------------------------------------------------------------
// Entry point: Defenders → Heroes → Render Knight + Ranger Portraits.
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroPortraitRenderer
    {
        private const string ResourcesDir = "Assets/_Modules/Onboarding/Resources/HeroPortraits";
        private const int PortraitWidth = 512;
        private const int PortraitHeight = 700;

        [MenuItem("Defenders/Heroes/Render Hero Portraits (transparent)")]
        public static void RenderBoth()
        {
            RenderOne("Assets/Resources/Heroes/Knight.fbx", "knight.png");
            RenderOne("Assets/Resources/Heroes/Ranger.fbx", "ranger.png");
            RenderOne("Assets/Resources/Heroes/Mage.fbx", "mage.png");
            // 4th hero — Elara the Cleric shares the Mage body (SlugFor Cleric->Mage);
            // a warm white-gold tint sets her apart from Thrain's icy-blue Mage portrait.
            RenderOne("Assets/Resources/Heroes/Mage.fbx", "cleric.png", new Color(1f, 0.93f, 0.70f));
            AssetDatabase.Refresh();
        }

        /// <summary>Multiplies every renderer's base colour by <paramref name="tint"/> — used
        /// to recolour Elara's shared Mage body to a warm healer gold so her portrait reads
        /// distinct from Thrain's icy-blue Mage.</summary>
        private static void ApplyTint(GameObject body, Color tint)
        {
            foreach (var r in body.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                    else if (m.HasProperty("_Color")) m.SetColor("_Color", m.color * tint);
                }
            }
        }

        private static void RenderOne(string fbxPath, string pngName, Color? tint = null)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) { Debug.LogError($"[HeroPortraitRenderer] FBX not found: {fbxPath}"); return; }

            // Temp stage root — destroyed before this function returns.
            var stage = new GameObject("_PortraitStage");
            stage.transform.position = new Vector3(1000f, 1000f, 1000f); // off-stage

            var body = (GameObject)PrefabUtility.InstantiatePrefab(fbx, stage.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.Euler(0f, 200f, 0f); // 3/4 view
            RetargetToUrp(body);
            NormalizeHeight(body, 1.7f);
            if (tint.HasValue) ApplyTint(body, tint.Value);

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

            // Camera + framing — slight zoom on the upper body.
            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.05f, -2.4f);
            camGo.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            // Owner 2026-05-25: hero-select portraits need a TRANSPARENT
            // background so each hero reads on the card's own styling instead
            // of a baked dark box. A SolidColor clear at alpha 0 + the ARGB32
            // RenderTexture + EncodeToPNG carries the alpha through.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = false;
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;

            // Render to a RenderTexture, read back, encode PNG, save.
            var rt = new RenderTexture(PortraitWidth, PortraitHeight, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            // CRITICAL (owner 2026-05-25: portraits came out blank): the legacy
            // Camera.Render() does NOT draw under URP in batchmode — the camera
            // only emitted its transparent clear colour, so every portrait was an
            // empty PNG. Use the SRP render-request API (Unity 2022.2+/6) which
            // actually runs the URP pipeline into the target. Fall back to the
            // legacy call if a render request isn't supported (non-SRP project).
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(PortraitWidth, PortraitHeight, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, PortraitWidth, PortraitHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            byte[] pngBytes = tex.EncodeToPNG();
            Directory.CreateDirectory(ResourcesDir);
            string outPath = ResourcesDir + "/" + pngName;
            File.WriteAllBytes(outPath, pngBytes);
            Debug.Log("[HeroPortraitRenderer] Wrote " + outPath + " (" + pngBytes.Length + " bytes)");

            // Remove any stale opaque .jpg of the same slug — HeroSelectController
            // loads the portrait by name (Resources.Load("HeroPortraits/<slug>")),
            // so a leftover knight.jpg/ranger.jpg alongside the new .png makes the
            // load ambiguous and can win over the transparent .png.
            string slug = Path.GetFileNameWithoutExtension(pngName);
            string staleJpg = ResourcesDir + "/" + slug + ".jpg";
            if (File.Exists(staleJpg))
            {
                AssetDatabase.DeleteAsset(staleJpg);
                Debug.Log("[HeroPortraitRenderer] Removed stale opaque portrait " + staleJpg);
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(stage);
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
            // After scaling, re-centre so feet land at y=0 in the camera frame.
            var pivot = body.transform.position;
            float bottom = b.min.y;
            body.transform.position = new Vector3(pivot.x, pivot.y - bottom, pivot.z);
        }
    }
}
