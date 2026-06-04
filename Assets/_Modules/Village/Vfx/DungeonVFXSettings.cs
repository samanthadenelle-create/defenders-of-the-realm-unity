// =============================================================================
// DungeonVFXSettings — WO-59: ScriptableObject holding darker prefab overrides
// for dungeon scenes.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Create via: Assets → Create → Defenders/VFX/Dungeon VFX Settings
// Assign the resulting asset to VFXManager.dungeonSettings in the Inspector.
// Call VFXManager.Instance.ApplyDungeonMode(true) when a dungeon scene loads,
// and ApplyDungeonMode(false) when returning to the village.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Holds darker, higher-contrast prefab overrides for dungeon scenes.
    /// Assign to VFXManager.dungeonSettings and call
    /// <see cref="VFXManager.ApplyDungeonMode"/> on dungeon scene load.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/VFX/Dungeon VFX Settings",
                     fileName = "DungeonVFXSettings")]
    public class DungeonVFXSettings : ScriptableObject
    {
        [Serializable]
        public struct Override
        {
            public VFXType type;
            [Tooltip("Replacement prefab — darker colours, stronger lights, more sparks.")]
            public GameObject prefab;
        }

        [Tooltip("Overrides applied in dungeon mode. Village prefabs are used for any " +
                 "type not listed here.")]
        public List<Override> overrides = new List<Override>();

        [Header("Post-Processing Overrides")]
        [Range(0f, 2f)] public float bloomIntensityMultiplier = 1.3f;
        [Range(0f, 2f)] public float contrastBoost            = 1.15f;
        public bool increasedVignette = true;
    }
}
