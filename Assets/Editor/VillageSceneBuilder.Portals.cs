using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    // WO-181: dungeon portals + portal pillar/material -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static void SpawnDungeonPortal()
        {
            var portalType = FindType(TypeDungeonPortal);
            if (portalType == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] DungeonPortal type not found — skipping.");
                return;
            }

            // Clean any pre-existing portals from prior builds.
            foreach (var name in new[] { "DungeonPortal", "DungeonPortal_HealersCottage",
                                         "DungeonPortal_FolksGranary" })
            {
                var existing = GameObject.Find(name);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            }

            // Owner feedback 2026-05-20 ("still doorway items that say
            // healers cottage in front of gate"): the stone-arch portals
            // were blocking the south-gate sightline. Relocated to the
            // EAST and WEST sides of the village interior, well off the
            // N-S gate spine, so they read as side attractions instead of
            // gate clutter.
            // dungeonId is the SHORT id — SceneRouter.GoDungeon prepends
            // "Dungeon_". Passing the full scene name double-prefixed and
            // routed to a missing scene (owner 2026-05-20: prior connection
            // error). Strict short ids only.
            // Folk's Granary authored by FolksGranaryBuilder — east portal
            // routes back to its own dungeon now.
            BuildOneDungeonPortal(portalType, "DungeonPortal_HealersCottage",
                new Vector3(-18f, 0f, 6f), "HealersCottage", "Healer's Cottage");
            BuildOneDungeonPortal(portalType, "DungeonPortal_FolksGranary",
                new Vector3( 18f, 0f, 6f), "FolksGranary",   "Folk's Old Granary");
        }

        private static void BuildOneDungeonPortal(System.Type portalType, string objectName,
                                                  Vector3 position, string dungeonId,
                                                  string displayName)
        {
            var root = new GameObject(objectName);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            const float archWidth = 4.5f;
            const float archHeight = 5.2f;

            // Owner 2026-05-20 ("cannot find entrance to healers cottage"):
            // the portal needs a visible marker so the player can find it.
            // Use a flat ground disc + always-visible floating sign — neither
            // depends on the painted-material shader-find that previously
            // turned the arch into a violet ghost.
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "PortalDisc";
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            disc.transform.localScale = new Vector3(3.5f, 0.05f, 3.5f);
            // Bright violet disc that reads at distance — URP/Lit with a
            // strong base colour (no texture, no shader-find risk).
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = "PortalDisc" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.55f, 0.30f, 0.95f, 1f));
                if (mat.HasProperty("_Color"))     mat.SetColor("_Color", new Color(0.55f, 0.30f, 0.95f, 1f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", new Color(0.55f, 0.30f, 0.95f) * 1.5f);
                    mat.EnableKeyword("_EMISSION");
                }
                disc.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // Owner 2026-05-25: stand the real Tripo Portal_To_Dungeon archway on
            // the disc as the dungeon ENTRANCE (the disc stays as a ground glow).
            // TripoMaterialFixer (ForceRebuildAll) colours it from its real basemap
            // with a violet tint fallback; -90°X stands it upright like the other
            // Tripo structures (verify orientation after the re-bake and flip if needed).
            var portalModel = LoadModel("Assets/Resources/Structures/Portal.fbx");
            if (portalModel != null)
            {
                var arch = (GameObject)PrefabUtility.InstantiatePrefab(portalModel);
                arch.name = "PortalArch";
                arch.transform.SetParent(root.transform, false);
                arch.transform.localPosition = Vector3.zero;
                // Recent owner Tripo exports import upright — leave identity (the old
                // -90°X tips the new exports onto their backs). Verify after re-bake.
                arch.transform.localRotation = Quaternion.identity;
                NormalizeProp(arch, archHeight);
                StripColliders(arch);
                StripRigidbodies(arch);
                SnapFeetToParent(arch);
                var fxType = FindType("DeNelle.Core.TripoMaterialFixer");
                if (fxType != null)
                {
                    var fx = arch.AddComponent(fxType);
                    fxType.GetMethod("SetFallbackTint")?.Invoke(fx, new object[] { new Color(0.55f, 0.30f, 0.95f) });
                    fxType.GetMethod("ForceRebuildAll")?.Invoke(fx, null);
                }
            }
            else
            {
                Debug.LogWarning("[VillageSceneBuilder] Portal.fbx not found at Resources/Structures — dungeon entrance keeps the bare disc.");
            }

            // DEF-10: floating TextMesh world-space sign removed — "▼ Healer's
            // Cottage ▼" was a placeholder readability aid visible in screenshot
            // review (2026-05-26). Proximity [F] prompt (DEF-26) replaces it.

            var trigger = root.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, archHeight * 0.5f, 0f);
            trigger.size = new Vector3(archWidth, archHeight, 0.6f);
            trigger.isTrigger = true;

            // Mount the portal logic.
            var portal = root.AddComponent(portalType);
            var cfgMethod = portalType.GetMethod("Configure");
            cfgMethod?.Invoke(portal, new object[] { dungeonId, displayName });
        }

        private static void BuildPortalPillar(Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pillar";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            PaintMaterial(go.GetComponent<Renderer>(), new Color(0.32f, 0.28f, 0.25f), false);
        }

        private static void PaintMaterial(Renderer renderer, Color colour, bool transparent)
        {
            if (renderer == null) return;
            Shader shader = transparent
                ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
                : (Shader.Find("Universal Render Pipeline/Lit")   ?? Shader.Find("Standard"));
            if (shader == null) return;
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", colour);
            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.renderQueue = 3000;
            }
            renderer.sharedMaterial = mat;
        }
    }
}
