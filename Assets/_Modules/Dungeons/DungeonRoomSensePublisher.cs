// =============================================================================
// DungeonRoomSensePublisher (WO-958) — feeds room bounds to the camera blackboard.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// The Dungeons half of the WO-958 room-sense seam (Core half: DungeonRoomSense).
// SmartMobileCamera (DeNelle.Village) needs the composed rooms' world AABBs for
// room-aware framing, but Village cannot reference Dungeons (circular asmdef) and
// reflection is banned — so this publisher pushes the data DOWN into Core where
// the camera can read it.
//
// Same self-arming idiom as DungeonRoomBinder (WO-797): one RuntimeInitialize
// hook, sceneLoaded thereafter. The room set is recomputed from the scene itself
// (RoomPrefabMeta children of the DungeonCompose_* root, measured by the ONE
// shared DungeonRoomBounds math) — never from a second copy of the layout.
//
// Emits [Flow:Camera] — WO-958's capture greps ONE tag for the whole camera story.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.World;
using DeNelle.Dungeons.RoomForge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Auto-installer: on every scene load, publishes the composed dungeon's room
    /// bounds into <see cref="DungeonRoomSense"/> (Core) so the Village-side camera
    /// can frame room-aware. Clears the blackboard when the world has no rooms.
    /// </summary>
    internal static class DungeonRoomSensePublisher
    {
        private const string Sys = "Camera";
        private const string ComposeRootPrefix = "DungeonCompose_";
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // A boot straight into a composed dungeon loaded its scene before this
            // hook existed — process the active scene once, too (binder idiom).
            Guard.Try(Sys, "room-sense publish (install)",
                () => PublishFor(SceneManager.GetActiveScene(), LoadSceneMode.Single));
            FlowTrace.Step(Sys, "room-sense publisher installed (WO-958 sceneLoaded hook)");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Guarded: a bad room prefab must never break a scene load.
            Guard.Try(Sys, $"room-sense publish '{scene.name}'", () => PublishFor(scene, mode));
        }

        private static void PublishFor(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject[] roots;
            try { roots = scene.GetRootGameObjects(); }
            catch (Exception ex)
            {
                FlowTrace.Warn(Sys, $"room-sense: GetRootGameObjects failed for '{scene.name}': {ex.Message}");
                return;
            }

            Transform composeRoot = null;
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go != null && go.name.StartsWith(ComposeRootPrefix, StringComparison.Ordinal))
                {
                    composeRoot = go.transform;
                    break;
                }
            }

            if (composeRoot == null)
            {
                // Additive loads (arena stages, overlays) must not wipe a live dungeon's
                // room set; a SINGLE load means the world changed — clear stale data.
                if (mode != LoadSceneMode.Single) return;
                bool hadData = DungeonRoomSense.RoomCount > 0;
                DungeonRoomSense.Clear();
                if (DeNelle.Core.HubScenes.IsDungeon(scene.name))
                    FlowTrace.Step(Sys, $"room-sense: dungeon '{scene.name}' has no compose root " +
                        "(hand-built/outpost pipeline) - rooms=0, camera uses profile defaults");
                else if (hadData)
                    FlowTrace.Step(Sys, $"room-sense: left dungeon -> cleared for '{scene.name}'");
                return;
            }

            var rooms = new List<DungeonRoomSense.Room>();
            float minExtent = float.MaxValue, maxExtent = 0f;
            foreach (Transform child in composeRoot)
            {
                if (child == null) continue;
                var meta = child.GetComponent<RoomPrefabMeta>();
                if (meta == null) continue;
                Bounds b = DungeonRoomBounds.Compute(child.gameObject);
                if (b.size.sqrMagnitude <= 0.01f)
                {
                    FlowTrace.Warn(Sys, $"room-sense: room '{child.name}' has zero-size bounds - skipped");
                    continue;
                }
                rooms.Add(new DungeonRoomSense.Room { Id = child.name, Bounds = b });
                float narrow = Mathf.Min(b.size.x, b.size.z);
                if (narrow < minExtent) minExtent = narrow;
                float wide = Mathf.Max(b.size.x, b.size.z);
                if (wide > maxExtent) maxExtent = wide;
            }

            DungeonRoomSense.Publish(scene.name, rooms);
            if (rooms.Count > 0)
                FlowTrace.Step(Sys, $"room-sense: published {rooms.Count} room(s) for '{scene.name}' " +
                    $"(narrowest {minExtent:F1}m, widest {maxExtent:F1}m, " +
                    $"smallRoomMaxExtent={DungeonCameraProfile.SmallRoomMaxExtent:F1}m)");
            else
                FlowTrace.Warn(Sys, $"room-sense: compose root found in '{scene.name}' but NO " +
                    "RoomPrefabMeta rooms - camera uses profile defaults");
        }
    }
}
