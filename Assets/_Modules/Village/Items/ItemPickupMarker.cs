// =============================================================================
// ItemPickupMarker + ItemPickupSpawner - the code-built world drop mote for the
// item-drops lane. On a kill the watcher spawns a cheap primitive at the death
// point; the hero walking within pickup range collects its rolled materials into
// ItemInventory (-> the persisted larder). "fight -> harvest" made tangible.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// HARD ISOLATION CONTRACT (mirrors CampVisual / the lane):
//   * NEW types. Touch NO existing file. The mote is a CODE-BUILT primitive
//     (GameObject.CreatePrimitive) with NO prefab hard-dep, NO scene edit, NO
//     .meta authoring. It only READS the hero transform (tag "Player" /
//     "HeroTarget") and grants via ItemInventory.GrantDrop (lane API).
//   * SHIPS DARK. ItemPickupSpawner.Spawn no-ops unless ItemDropSystem.Enabled,
//     and the marker self-destructs if the flag flips off - so nothing spawns,
//     bobs, or grants when the lane is off. Zero footprint in the build.
//   * Missing optional art = LogWarning, never error (none required: primitive).
//
// ASCII strings only. Canon: village is Elarion.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village.Items
{
    /// <summary>Spawns the code-built world drop motes (dark behind the lane flag).</summary>
    public static class ItemPickupSpawner
    {
        /// <summary>
        /// Spawn a collectible mote at <paramref name="at"/> carrying <paramref name="lines"/>
        /// (materialId -> count). No-op when the lane is off or there is nothing to carry.
        /// </summary>
        public static void Spawn(Vector3 at, Dictionary<string, int> lines)
        {
            if (!ItemDropSystem.Enabled) return;            // SHIPS DARK.
            if (lines == null || lines.Count == 0) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ItemDropMote";
            go.transform.position = at + Vector3.up * 0.5f;
            go.transform.localScale = Vector3.one * 0.45f;

            // Mote is a trigger so it never blocks the hero or NavMesh agents.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            // Cheap loot-gold tint so it reads as loot without any art dependency.
            // CreatePrimitive ships the built-in Standard shader, which URP cannot
            // render -> it falls back to Hidden/InternalErrorShader (MAGENTA). Build a
            // URP-compatible material explicitly (URP/Lit uses "_BaseColor", legacy
            // "_Color"); Sprites/Default is the always-present last resort. Same class
            // of fix as the "pink floor" URP/Lit lesson.
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var gold = new Color(1f, 0.84f, 0.32f, 1f);
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gold);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", gold);
                    rend.material = mat;
                }
            }

            go.AddComponent<ItemPickupMarker>().Init(lines);
        }
    }

    /// <summary>
    /// A single world drop mote: bobs gently, and grants its carried materials to
    /// <see cref="ItemInventory"/> when the hero comes within pickup range. Cleans
    /// itself up on pickup, on timeout, or if the lane flag flips off.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemPickupMarker : MonoBehaviour
    {
        private const float PickupRange = 2.0f;
        private const float Lifetime = 60f;     // motes self-clean so kills don't litter

        private Dictionary<string, int> _carried;
        private Transform _hero;
        private float _born;
        private float _baseY;
        private bool _collected;

        public void Init(Dictionary<string, int> lines)
        {
            _carried = lines;
            _born = Time.time;
            _baseY = transform.position.y;
        }

        private void Update()
        {
            // Lane off -> remove silently (keeps the build inert if toggled at runtime).
            if (!ItemDropSystem.Enabled) { Destroy(gameObject); return; }
            if (_collected) return;

            // Timeout cleanup.
            if (Time.time - _born > Lifetime) { Destroy(gameObject); return; }

            // Gentle bob for readability (no art needed).
            float bob = Mathf.Sin((Time.time - _born) * 2.5f) * 0.12f;
            var p = transform.position;
            transform.position = new Vector3(p.x, _baseY + 0.5f + bob, p.z);
            transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);

            EnsureHero();
            if (_hero == null) return;

            float sqr = (_hero.position - transform.position).sqrMagnitude;
            if (sqr <= PickupRange * PickupRange)
                Collect();
        }

        private void Collect()
        {
            _collected = true;
            if (_carried != null)
            {
                foreach (var kv in _carried)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                    ItemInventory.GrantDrop(kv.Key, kv.Value);
                }
            }
            Destroy(gameObject);
        }

        private void EnsureHero()
        {
            if (_hero != null) return;
            var p = GameObject.FindWithTag("Player");
            if (p == null)
            {
                // "HeroTarget" may be undefined (FindWithTag throws on an undefined tag).
                var ht = SafeFindWithTag("HeroTarget");
                if (ht != null) p = ht;
            }
            _hero = p != null ? p.transform : null;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }
    }
}
