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
using DeNelle.Core.UI;            // CombatText / CombatTextKind - the ONE bounded stamp seam
using DeNelle.Core.Diagnostics;   // FlowTrace (CLAUDE.md sec.12)

namespace DeNelle.Village.Items
{
    /// <summary>
    /// WO-1589 - the ONE producer of the "what did I just bank?" reward stamp for LOOT.
    /// <para>
    /// THE DEFECT IT CLOSES (owner, Seeker, 2026-09-07: "when i open a chest no toast to
    /// what i found"): the chest path ended at
    /// <c>[Flow:Loot] Chest_crate opened -&gt; dropped 2 loot line(s) as a world mote</c> and
    /// said nothing, while a kill 25 seconds later toasted through
    /// <c>CombatText(Reward)</c> in the same session. Two feedback rules on one screen.
    /// </para>
    /// <para>
    /// ⛔ IT IS NOT A SECOND TOAST SYSTEM. It composes a label and hands it to the SAME
    /// bounded, pooled, deduped screen-space stamp Enemy.ShowFieldKillReward uses
    /// (<c>CombatText.Show(CombatTextKind.Reward, ...)</c>, WO-1103 §1.8). Building a
    /// parallel loot toast is the inferior fix; do not.
    /// </para>
    /// <para>
    /// ⛔ IT FIRES AT THE BANK, NEVER AT THE DROP. A chest that dropped a mote the player
    /// walked past has granted NOTHING - toasting at the open would claim what is not yet
    /// held. The call sites are therefore ItemPickupMarker.Collect (the mote
    /// is walked over) and BreakableContainer's direct-deposit fallback (the roll banked
    /// straight to the larder because world pickups are off). Both are banks.
    /// </para>
    /// </summary>
    public static class LootRewardToast
    {
        private const string Sys = "Loot";

        /// <summary>Most lines named individually before the label spills into "+N more".
        /// The stamp is capped at 44 reference px (CombatTextLayer) - an unbounded label
        /// from a fat chest roll would run off a Seeker screen.</summary>
        private const int MaxNamedLines = 4;

        /// <summary>How many reward stamps this producer has routed this session. Permanent
        /// instrumentation (sec.12), and the counter the [chest-loot-toast] oracle reads to
        /// prove "one toast per pickup, zero without".</summary>
        public static int AnnouncedCount { get; private set; }

        /// <summary>The last label routed, verbatim. Diagnostics + oracle read.</summary>
        public static string LastLabel { get; private set; }

        /// <summary>Reset the session counters (oracle setup; never called by gameplay).</summary>
        public static void ResetCounters()
        {
            AnnouncedCount = 0;
            LastLabel = null;
        }

        /// <summary>
        /// Compose the player-facing label for <paramref name="lines"/>:
        /// <c>"+1 Oil Flask  +1 Tattered Cloth"</c>. PURE and DETERMINISTIC - ordered by
        /// count descending then ordinal id, because a <see cref="Dictionary{TKey,TValue}"/>
        /// has no stable order and the same roll must never read two different ways.
        /// Names come from <see cref="ItemIdentity.DisplayName"/> (the authored
        /// consumables/materials row), never from the raw id when a row exists.
        /// Returns an empty string when there is nothing bankable to name.
        /// </summary>
        public static string ComposeLabel(IDictionary<string, int> lines)
        {
            if (lines == null || lines.Count == 0) return string.Empty;

            var ordered = new List<KeyValuePair<string, int>>();
            foreach (var kv in lines)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                ordered.Add(kv);
            }
            if (ordered.Count == 0) return string.Empty;

            ordered.Sort((a, b) =>
            {
                int byCount = b.Value.CompareTo(a.Value);
                return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
            });

            var sb = new System.Text.StringBuilder(64);
            int named = 0;
            for (int i = 0; i < ordered.Count && named < MaxNamedLines; i++, named++)
            {
                if (named > 0) sb.Append("  ");
                string display = ItemIdentity.DisplayName(ordered[i].Key);
                if (string.IsNullOrEmpty(display)) display = ordered[i].Key;
                sb.Append('+').Append(ordered[i].Value).Append(' ').Append(display);
            }
            int spilled = ordered.Count - named;
            if (spilled > 0) sb.Append("  +").Append(spilled).Append(" more");
            return sb.ToString();
        }

        /// <summary>
        /// Say what was just banked, ONCE, at <paramref name="worldPos"/>, through the kill
        /// path's own stamp. <paramref name="source"/> is a trace token ("mote-pickup" /
        /// "chest-deposit") so the device log tells the two banks apart.
        /// Returns true when a stamp was routed (false = nothing nameable, so nothing said).
        /// </summary>
        public static bool Announce(IDictionary<string, int> lines, Vector3 worldPos, string source)
        {
            string label = ComposeLabel(lines);
            if (string.IsNullOrEmpty(label)) return false;

            AnnouncedCount++;
            LastLabel = label;

            // The SAME seam Enemy.ShowFieldKillReward uses - pooled, capped, deduped.
            CombatText.Show(CombatTextKind.Reward, label, worldPos);

            // sec.12 permanent trace, mirroring the kill line
            // ("[Flow:Reward] KILL REWARD TOAST '+17 XP  +7 gold' ... routed=CombatText(Reward)")
            // so a device log proves the chest now speaks too.
            FlowTrace.Step(Sys,
                $"CHEST REWARD TOAST '{label}' source={(string.IsNullOrEmpty(source) ? "?" : source)} " +
                $"lines={(lines != null ? lines.Count : 0)} routed=CombatText(Reward) at {worldPos}");
            return true;
        }
    }

    /// <summary>Spawns the code-built world drop motes (dark behind the lane flag).</summary>
    public static class ItemPickupSpawner
    {
        /// <summary>
        /// Spawn a collectible mote at <paramref name="at"/> carrying <paramref name="lines"/>
        /// (materialId -> count). No-op when the lane is off or there is nothing to carry.
        /// <para>
        /// <paramref name="source"/> (WO-1589) is a TRACE token only - "chest" from
        /// BreakableContainer, the default "drop" from the kill-drop watcher. It changes
        /// nothing about the mote; it exists so the device log can tell a chest pickup from a
        /// kill pickup, which is the whole acceptance evidence for WO-1589 ("CHEST REWARD
        /// TOAST follows the pickup, once per chest").
        /// </para>
        /// </summary>
        public static void Spawn(Vector3 at, Dictionary<string, int> lines, string source = "drop")
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

            go.AddComponent<ItemPickupMarker>().Init(lines, source);
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
        private string _source = "drop";   // WO-1589 trace token: "chest" / "drop"
        private Transform _hero;
        private float _born;
        private float _baseY;
        private bool _collected;

        public void Init(Dictionary<string, int> lines) => Init(lines, "drop");

        /// <summary><paramref name="source"/> is the WO-1589 trace token ("chest" / "drop")
        /// carried only so the reward-toast line names where the mote came from.</summary>
        public void Init(Dictionary<string, int> lines, string source)
        {
            _carried = lines;
            _source = string.IsNullOrEmpty(source) ? "drop" : source;
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

        /// <summary>
        /// THE BANK MOMENT (WO-1589). Everything the mote carries moves into the larder
        /// here - this is the first instant the player actually HOLDS it, which is why the
        /// reward stamp fires here and not at the chest open. A mote that is never walked
        /// over never reaches this method, so a chest the player ignored says nothing:
        /// correct, and the world mote stays where it fell.
        /// </summary>
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

            // Say what was found, ONCE, through the kill path's own bounded stamp.
            // Positioned slightly above the mote so the label rises off the pickup the
            // player is standing on rather than out of their feet.
            LootRewardToast.Announce(_carried, transform.position + Vector3.up * 0.6f, _source + "-pickup");

            // Editor-boundary-safe teardown, the SAME idiom BuildPart already uses above:
            // plain Destroy is a hard error outside play mode, and the [chest-loot-toast]
            // oracle drives this exact method from an EditMode batchmode run.
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
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
