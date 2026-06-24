// =============================================================================
// VfxParade.VfxParadeManifest - runtime-loadable manifest of effect prefabs.
// -----------------------------------------------------------------------------
// A ScriptableObject holding DIRECT GameObject references to the Spells Pack
// effect prefabs the owner wants to parade in a standalone build. Direct
// references FORCE the referenced prefabs into the player build even though the
// Spells Pack folder is gitignored and lives OUTSIDE Resources (a path-based
// Resources.Load could never reach them). An editor step
// (DeNelle.Editor.VfxParadeManifestBuilder.Build) scans the pack, fills this
// asset, and writes it to Assets/Resources/VfxParade/VfxParadeManifest.asset so
// the runtime overlay can load it with Resources.Load.
//
// Lives in the VfxParade.Runtime asmdef so the shipped game can reference the
// type. ASCII-only strings throughout.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VfxParade
{
    /// <summary>One paraded effect: a DIRECT prefab reference plus its source
    /// path and display name (captured at build time for labelling + picks).</summary>
    [Serializable]
    public sealed class VfxParadeEntry
    {
        public GameObject prefab; // DIRECT ref - forces the prefab into the build
        public string path;       // original AssetDatabase path (for the picks file)
        public string name;       // prefab file name without extension (label)
    }

    [CreateAssetMenu(menuName = "Defenders/VFX Parade Manifest", fileName = "VfxParadeManifest")]
    public sealed class VfxParadeManifest : ScriptableObject
    {
        /// <summary>The resources-relative path the runtime loads this asset from
        /// (no extension, no "Resources/" prefix), per Unity's Resources.Load.</summary>
        public const string ResourcesPath = "VfxParade/VfxParadeManifest";

        public List<VfxParadeEntry> entries = new List<VfxParadeEntry>();
    }
}
