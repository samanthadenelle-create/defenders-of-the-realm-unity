// =============================================================================
// OffsetForge.OffsetTable — dependency-free runtime data + loader for offsets.
// -----------------------------------------------------------------------------
// Plain C# / UnityEngine only. NO editor references, NO third-party deps.
// This is the OPTIONAL runtime loader the package ships so a consumer game can
// read the JSON the Offset Forge editor window exports, with zero coupling.
// Serializable via UnityEngine.JsonUtility.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OffsetForge
{
    /// <summary>Serializable 3-float vector (JsonUtility-friendly) with conversions to/from Vector3.</summary>
    [Serializable]
    public struct Vec3
    {
        public float x;
        public float y;
        public float z;

        public Vec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vec3(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        // Implicit Vec3 -> Vector3 (lossless, always valid).
        public static implicit operator Vector3(Vec3 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        // Explicit Vector3 -> Vec3 (narrowing into our serializable type).
        public static explicit operator Vec3(Vector3 v)
        {
            return new Vec3(v.x, v.y, v.z);
        }

        public override string ToString()
        {
            return string.Format("({0:0.##}, {1:0.##}, {2:0.##})", x, y, z);
        }
    }

    /// <summary>One authored offset record, keyed by a stable string id (typically the model name).</summary>
    [Serializable]
    public class OffsetEntry
    {
        public string id;
        public Vec3 rot;   // euler degrees (x,y,z)
        public Vec3 pos;   // local position
        public float scale = 1f;
        // Optional NON-uniform 3-axis scale. Additive: absent/zero in JSON => use the uniform `scale`
        // above (back-compat; hero/prop consumers ignore this). Lets a consumer that genuinely needs a
        // stretched axis (e.g. a widened bridge deck) persist it. Consumer picks scaleXyz when non-zero.
        public Vec3 scaleXyz = new Vec3(0f, 0f, 0f);
        // WO-577: when true, the consumer seats from the geometry VERTICAL baseline and treats
        // rot/pos/scale as the absolute in-hand delta (bypassing its own grip inference). Default
        // false = the offset is a nudge on top of the consumer's geometric grip. Additive field;
        // older JSON without the key reads false (back-compat).
        public bool fullOverride = false;
    }

    /// <summary>A flat table of offsets, serialized to/from JSON.</summary>
    [Serializable]
    public class OffsetTable
    {
        public List<OffsetEntry> offsets = new List<OffsetEntry>();

        /// <summary>Returns the entry whose id matches (ordinal), or null if none.</summary>
        public OffsetEntry Find(string id)
        {
            if (offsets == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < offsets.Count; i++)
            {
                var e = offsets[i];
                if (e != null && string.Equals(e.id, id, StringComparison.Ordinal))
                    return e;
            }
            return null;
        }

        /// <summary>Inserts a new entry, or overwrites the existing one with the same id.</summary>
        public void Upsert(OffsetEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) return;
            if (offsets == null) offsets = new List<OffsetEntry>();
            var existing = Find(entry.id);
            if (existing != null)
            {
                existing.rot = entry.rot;
                existing.pos = entry.pos;
                existing.scale = entry.scale;
                existing.scaleXyz = entry.scaleXyz;
                existing.fullOverride = entry.fullOverride;
            }
            else
            {
                offsets.Add(entry);
            }
        }
    }

    /// <summary>Static JSON helpers (JsonUtility). Safe on null/empty input.</summary>
    public static class OffsetTableIO
    {
        /// <summary>Parse JSON into an OffsetTable. Returns a fresh empty table on null/empty/invalid input.</summary>
        public static OffsetTable Load(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new OffsetTable();
            try
            {
                var table = JsonUtility.FromJson<OffsetTable>(json);
                if (table == null) table = new OffsetTable();
                if (table.offsets == null) table.offsets = new List<OffsetEntry>();
                return table;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OffsetForge] OffsetTableIO.Load failed to parse JSON: " + e.Message);
                return new OffsetTable();
            }
        }

        /// <summary>Serialize an OffsetTable to pretty-printed JSON. Returns an empty-table JSON on null.</summary>
        public static string ToJson(OffsetTable table)
        {
            if (table == null) table = new OffsetTable();
            return JsonUtility.ToJson(table, true);
        }
    }
}
