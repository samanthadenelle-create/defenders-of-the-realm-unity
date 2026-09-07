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
//     .meta authoring. It only READS the hero transform (tag "Player", then a
//     HeroLocomotion lookup) and grants via ItemInventory.GrantDrop (lane API).
//   * SHIPS DARK. ItemPickupSpawner.Spawn no-ops unless ItemDropSystem.Enabled,
//     and the marker self-destructs if the flag flips off - so nothing spawns,
//     bobs, or grants when the lane is off. Zero footprint in the build.
//   * Missing optional art = LogWarning, never error (none required: primitive).
//
// -- PER-ITEM IDENTITY: SHAPE, not hue (WO-1132 follow-on, 2026-08-21) --------
// THE DEFECT: Spawn built ONE hardcoded gold sphere for EVERY drop. A chest that
// rolled Iron Scrap and one that rolled a Heartwood Bough left identical objects
// on the floor - the drop carried no identity at all, so a player could only find
// out what fell by walking over it.
//
// THE FIX follows the sibling defect proven the same day on IngredientPickup:
// the identity data was ALREADY AUTHORED and nothing read it. Every
// consumables.json / materials.json row carries a `glyph` char (pinned by the
// [item-identity] oracle) and ItemIdentity.GlyphOf already exposed it. The mote's
// SILHOUETTE is now built from that glyph via ItemMoteShapes - no new identity
// table was authored. Hue is deliberately NOT the cue: the owner is red/green
// colourblind, and the sibling proved that lit pastel primitives all wash to the
// same pellet anyway. The tint now carries only KIND (consumable / material /
// unauthored), in three luma-separated values that survive a greyscale pass.
//
// A mote carrying MORE THAN ONE distinct item shows its headline item's shape
// plus one small satellite pip per extra line (capped at 3) - so "a chest full"
// reads differently from "one herb" without any hue or text.
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

            string headline = ResolveHeadlineId(lines);
            char glyph = ItemMoteShapes.ResolveGlyph(headline);
            Color tint = ItemMoteShapes.TintFor(headline);

            // An id that no catalog owns still gets a distinct silhouette (hashed), but it
            // is CONTENT DEBT, so say so once instead of letting it pass silently.
            if (!ItemMoteShapes.HasAuthoredIdentity(headline))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Once(
                    "ItemDrop", "unauthored-drop-id:" + headline,
                    "drop id '" + headline + "' has no consumables.json/materials.json row - " +
                    "mote silhouette is hash-derived (family '" + ItemMoteShapes.FamilyName(glyph) +
                    "'). PO: author the row so the drop gets a real name + glyph.");
            }

            // ROOT is an EMPTY transform, never a primitive: the shape parts hang under a
            // body so the whole silhouette bobs/spins as one piece, and the root itself can
            // never carry a collider. ItemPickupMarker's bob/spin still drives this root, so
            // the mote sits at exactly the height it always did.
            var go = new GameObject("ItemDropMote_" +
                                    (string.IsNullOrEmpty(headline) ? "unknown" : headline));
            go.transform.position = at + Vector3.up * 0.5f;

            var body = new GameObject("Mote");
            body.transform.SetParent(go.transform, false);

            var parts = ItemMoteShapes.PartsFor(glyph);
            for (int i = 0; i < parts.Count; i++)
                BuildPart(body.transform, parts[i], tint);

            // Multi-line motes read as "more here" by SHAPE - satellite pips, capped so a
            // fat chest roll never turns into a ball of primitives.
            int extras = Mathf.Min(lines.Count - 1, 3);
            for (int i = 0; i < extras; i++)
            {
                var rot = Quaternion.Euler(0f, 90f + i * 120f, 0f);
                BuildPart(body.transform, new MotePartSpec(
                    "Extra" + i, PrimitiveType.Sphere,
                    rot * new Vector3(0f, -0.16f, 0.30f), Vector3.one * 0.09f), tint);
            }

            go.AddComponent<ItemPickupMarker>().Init(lines);
        }

        /// <summary>
        /// The item this mote LOOKS like: the largest line, tie-broken by ordinal id so the
        /// same roll always produces the same silhouette (a dictionary's order is not stable
        /// and a mote that changed shape between identical rolls would be worse than none).
        /// </summary>
        public static string ResolveHeadlineId(Dictionary<string, int> lines)
        {
            if (lines == null) return null;
            string best = null;
            int bestCount = int.MinValue;
            foreach (var kv in lines)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                if (kv.Value > bestCount ||
                    (kv.Value == bestCount && string.CompareOrdinal(kv.Key, best) < 0))
                {
                    best = kv.Key;
                    bestCount = kv.Value;
                }
            }
            return best;
        }

        /// <summary>
        /// One URP-safe, emissive, collider-free primitive of the mote body.
        /// <para>
        /// THREE things here are deliberate and must not be "cleaned up":
        /// (1) the primitive's collider is DESTROYED - pickup is a per-frame DISTANCE CHECK
        /// (ItemPickupMarker.Update), never a physics event, so a live collider would only
        /// block the hero or punch a hole in the NavMesh;
        /// (2) the material is built from URP/Lit with fallbacks, because CreatePrimitive
        /// ships the built-in Standard shader which URP renders MAGENTA (the "pink floor"
        /// lesson);
        /// (3) it is EMISSIVE - a lit-only primitive is invisible at low lantern oil, which
        /// is exactly where a dungeon chest drops.
        /// </para>
        /// </summary>
        private static void BuildPart(Transform body, MotePartSpec spec, Color tint,
                                      float emissiveMul = 0.75f)
        {
            var go = GameObject.CreatePrimitive(spec.Primitive);
            go.name = spec.Name;
            go.transform.SetParent(body, false);
            go.transform.localPosition = spec.LocalPosition;
            go.transform.localRotation = spec.LocalRotation;
            go.transform.localScale = spec.LocalScale;

            // (1) Distance-check pickup - the mote must never block hero or NavMesh.
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(col);
                else UnityEngine.Object.DestroyImmediate(col);
            }

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                // (2) URP-safe material construction - never leave the Standard shader on.
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                    // (3) Emissive so the mote reads in an unlit dungeon.
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", tint * emissiveMul);
                    rend.material = mat;
                }
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
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
                // WO-1513: the old fallback read the "HeroTarget" tag, which
                // TagManager.asset has never declared — a permanently dead branch.
                // The hero definitively carries HeroLocomotion (CLAUDE.md §7).
                var loco = FindFirstObjectByType<HeroLocomotion>();
                if (loco != null) p = loco.gameObject;
            }
            _hero = p != null ? p.transform : null;
        }
    }
}
