// =============================================================================
// SceneLink — pure-data model for ONE data-driven scene-to-scene crossing (WO1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// A SceneLink describes a routable edge in the world graph
//   Castle(MainCastle_Hall) → OuterWorld → Outpost1 → Dungeon → Outpost2
//   (+ a portal link from the dungeon back to Outpost2).
// The resolver (DeNelle.Village.SceneLinkResolverHost, which implements the
// Core-defined ISceneLinkResolver) loads these rows from
// Resources/Data/scene-links.json via JsonUtility, then loads the target scene
// (additive or single), finds the entry spawn, and warps the hero with NavMesh
// validation. This file is PURE DATA — it never references DeNelle.Village.
//
// JsonUtility-friendly: flat scalars + a Vector3 (serialized as {x,y,z}).
// =============================================================================
using UnityEngine;

namespace DeNelle.Core.World
{
    /// <summary>
    /// One data-driven crossing between two scenes. Authored in
    /// Resources/Data/scene-links.json and resolved at runtime by the
    /// SceneLinkResolver. JsonUtility-compatible (flat fields + Vector3).
    /// </summary>
    [System.Serializable]
    public class SceneLink
    {
        /// <summary>Stable unique id used by TravelTo(linkId) (e.g. "castle_to_outerworld").</summary>
        public string id;

        /// <summary>Crossing kind: "seam" | "outpost" | "dungeon" | "portal". Cosmetic/diagnostic only.</summary>
        public string type;

        /// <summary>The scene this link departs FROM (diagnostic + optional unload source).</summary>
        public string fromScene;

        /// <summary>The scene this link arrives AT — loaded by the resolver.</summary>
        public string toScene;

        /// <summary>Load mode: "additive" | "single". Single carries the hero across via DontDestroyOnLoad.</summary>
        public string loadMode;

        /// <summary>Optional GameObject name to Find in the target scene for the landing spot; "" = use targetPosition.</summary>
        public string spawnPoint;

        /// <summary>Fallback landing position when spawnPoint is empty or not found in the target scene.</summary>
        public Vector3 targetPosition;

        /// <summary>Unload the fromScene after arrival (only honoured for additive loads of chained spaces).</summary>
        public bool unloadFrom;
    }

    /// <summary>
    /// Root object for scene-links.json — a flat array of SceneLink rows
    /// (JsonUtility requires a wrapper type; it cannot deserialize a top-level array).
    /// </summary>
    [System.Serializable]
    public class SceneLinkFile
    {
        public SceneLink[] links;
    }
}
