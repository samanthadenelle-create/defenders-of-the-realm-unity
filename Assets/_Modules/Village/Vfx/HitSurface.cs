// =============================================================================
// HitSurface + HitSurfaceVfx (WO-887, surface half) — the RUNTIME HOME the five
// owner-tagged surface impacts never had, and the resolution that decides which
// one plays.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS FILE EXISTS, AND WHY IT DID NOT BEFORE.
//
// WO-887's surface half was REFUSED on 2026-08-05, correctly and with
// measurements: there was no SurfaceType / MaterialType / HitSurface enum
// anywhere in the tree, so nothing could name a surface, and the refusal noted
// that defining a surface taxonomy is DESIGN work belonging to the owner. That
// block is now cleared from both ends:
//
//   1. THE ART: the owner tagged all five surfaces herself in
//      Assets/Editor/VfxManualPicks.json (2026-08-21) — PP_FleshImpacts,
//      PP_MetalImpacts, PP_StoneImpacts, PP_WoodImpacts, PP_SandImpacts. Those
//      keys are mapped VERBATIM below. No prefab is chosen, substituted or
//      re-pointed here (standing rule: the OWNER tags the key, the CLI maps the
//      key to a named hook and never picks art).
//
//   2. THE TAXONOMY: the owner ruled the defaults on 2026-08-21, explicitly as a
//      low-stakes, reversible-on-a-felt-check call — wall tier 1 -> Wood, wall
//      tier 2-3 and gates -> Metal, every other structure -> Stone, enemies ->
//      Flesh, and SAND deliberately UNUSED.
//
// ⚠ THE PART OF THE ORIGINAL REFUSAL THAT WAS WRONG, corrected at source rather
//   than coded around: the refusal recorded that "wood palisades, stone walls and
//   steel gates share one Structure layer" and concluded THE SURFACE SIGNAL DOES
//   NOT EXIST. The LAYER conclusion is true and the SIGNAL conclusion is not —
//   WallSegment has carried its material the whole time. WallSegment.Tier is
//   public and returns 1..3 (WallSegment.cs:144), and WallTier names those exact
//   three values Wood=1 / Iron=2 / ReinforcedSteel=3
//   (Assets/_Modules/Village/Walls/WallTierData.cs:29). A physics layer was the
//   wrong place to look; the gameplay type was the right one.
//
// WHY A STRING KEY AND NOT A VFXType: appending to VFXType is a single-owner edit
// (WO-884 §0.2, and the enum's own header repeats it), and the enum is serialised
// BY ORDINAL into VFXCatalog.asset. WO-892 already established the sanctioned
// alternative for exactly this case — a VFX moment whose consumer is a string key
// declares that key and plays through VFXManager.PlayKey. StructureDamageVisuals
// is the precedent. So the five keys ARE the runtime home, and this file is what
// gives them one instead of leaving them stranded in editor tooling.
//
// COLOURBLIND LAW: the surface read is carried by the recipe's MOTION and DEBRIS
// SHAPE (splatter / spark / chip / splinter), never by hue, and it is ADDITIVE —
// it never replaces a health bar, a number, or any other channel. An unresolved
// surface falls back to the generic Impact_Physical the call site already played,
// so nothing is ever left with no feedback at all.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// What a blow LANDED ON. Deliberately a small, closed gameplay taxonomy — it is
    /// not a physics-material system and must not grow into one without an owner
    /// ruling. Ordinals are not serialised anywhere; this enum is a runtime-only
    /// dispatch key, so it is safe to reorder (unlike <see cref="VFXType"/>).
    /// </summary>
    public enum HitSurface
    {
        /// <summary>Unresolved — the caller keeps whatever generic impact it already plays.</summary>
        None = 0,
        /// <summary>Living bodies: enemies, the hero, troops, companions.</summary>
        Flesh,
        /// <summary>Iron / reinforced-steel walls and every gate (owner default 2026-08-21).</summary>
        Metal,
        /// <summary>Every other structure — buildings, towers, the Heart (owner default 2026-08-21).</summary>
        Stone,
        /// <summary>Tier-1 (wood) wall segments (owner default 2026-08-21).</summary>
        Wood,
        /// <summary>
        /// Ground / loose earth. DELIBERATELY UNUSED — the owner ruled on 2026-08-21 that
        /// there is no ground-impact case worth wiring, so nothing resolves to Sand today.
        /// The value and its catalogued art are kept because the art is already tagged and
        /// built; the day a ground-impact case exists it is a one-line resolution change,
        /// not a re-derivation. <see cref="HitSurfaceVfx.SandIsIntentionallyUnused"/>
        /// records that as an assertable fact rather than a comment nobody re-reads.
        /// </summary>
        Sand,
    }

    /// <summary>
    /// Maps a <see cref="HitSurface"/> to the owner-tagged catalog key and plays it.
    /// Pure lookup + one guarded play; no state, no allocation on the hot path.
    /// </summary>
    public static class HitSurfaceVfx
    {
        private const string FlowSys = "HitSurface";

        // ── The owner's 2026-08-21 tags, VERBATIM ────────────────────────────────
        // Each key is an owner row in Assets/Editor/VfxManualPicks.json. The catalog
        // row that backs it is authored by SurfaceImpactVfxMirrors (tracked mirror,
        // demo geometry stripped, forced one-shot) and reached through
        // HovlVfxCatalogGenerator's manual overlay. Renaming any of these strings
        // silently un-wires the owner's pick — they are data, not identifiers.
        public const string KeyFlesh = "PP_FleshImpacts";
        public const string KeyMetal = "PP_MetalImpacts";
        public const string KeyStone = "PP_StoneImpacts";
        public const string KeyWood  = "PP_WoodImpacts";
        public const string KeySand  = "PP_SandImpacts";

        /// <summary>
        /// Owner ruling 2026-08-21: no ground-impact case is worth wiring, so
        /// <see cref="HitSurface.Sand"/> is never RESOLVED even though its art is tagged
        /// and catalogued. Exposed so the regression can MEASURE that (by resolving every
        /// struck-target shape in the game and finding no Sand) rather than trusting a
        /// comment. Flip to false in the same edit that adds a ground-impact resolution.
        /// </summary>
        public const bool SandIsIntentionallyUnused = true;

        /// <summary>The owner-tagged catalog key for a surface; null for <see cref="HitSurface.None"/>.</summary>
        public static string KeyFor(HitSurface surface)
        {
            switch (surface)
            {
                case HitSurface.Flesh: return KeyFlesh;
                case HitSurface.Metal: return KeyMetal;
                case HitSurface.Stone: return KeyStone;
                case HitSurface.Wood:  return KeyWood;
                case HitSurface.Sand:  return KeySand;
                default:               return null;
            }
        }

        /// <summary>
        /// THE RESOLUTION — the owner's 2026-08-21 defaults, in the order they are
        /// decided. Order matters exactly once: <see cref="WallSegment"/> is tested before
        /// the generic-structure fallback, because a wall IS a structure and would
        /// otherwise read as Stone regardless of its tier.
        /// <para>
        /// Takes a <see cref="Component"/> (not a raw GameObject) because every call site
        /// already holds one — the struck <c>IDamageableStructure</c> cast to
        /// <c>MonoBehaviour</c>, or the struck <c>Enemy</c>. Resolution walks the component
        /// itself and then its hierarchy, so a hit reported on a child collider still finds
        /// the wall/gate/building that owns it.
        /// </para>
        /// Returns <see cref="HitSurface.None"/> when nothing matches — never a guess.
        /// </summary>
        public static HitSurface Resolve(Component struck)
        {
            if (struck == null) return HitSurface.None;
            return Resolve(struck.gameObject);
        }

        /// <summary>GameObject overload of <see cref="Resolve(Component)"/>.</summary>
        public static HitSurface Resolve(GameObject struck)
        {
            if (struck == null) return HitSurface.None;

            // 1. WALLS FIRST — the one case with a real per-instance material signal.
            //    WallSegment.Tier is public and 1..3, named by WallTier
            //    (Wood=1 / Iron=2 / ReinforcedSteel=3). Owner default: tier 1 reads WOOD,
            //    tiers 2 and 3 read METAL. Tested before the structure fallback below.
            // ⚠ EVERY LOOKUP BELOW PASSES includeInactive: true, DELIBERATELY. Unity's
            //   default GetComponentInParent SKIPS inactive GameObjects, which would make
            //   a target that is mid-disable (a structure being torn down, a pooled body
            //   between lives) silently resolve to None. It also lets the regression
            //   MEASURE this resolution on inactive fixtures — an inactive GameObject never
            //   runs Awake, so the suite can exercise Enemy/Gate/WallSegment without
            //   booting any of their lifecycles.
            const bool Inactive = true;

            var wall = struck.GetComponentInParent<WallSegment>(Inactive);
            if (wall != null)
                return wall.Tier <= (int)Walls.WallTier.Wood ? HitSurface.Wood : HitSurface.Metal;

            // 2. GATES — owner default: METAL, at every tier. A gate has no tier ladder of
            //    its own, so unlike a wall there is nothing to branch on.
            if (struck.GetComponentInParent<Gate>(Inactive) != null) return HitSurface.Metal;

            // 3. LIVING BODIES — flesh. Enemies are the overwhelming majority of hits;
            //    the hero and troops are here so a stray AoE reads correctly too.
            if (struck.GetComponentInParent<Enemy>(Inactive) != null)       return HitSurface.Flesh;
            if (struck.GetComponentInParent<HeroHealth>(Inactive) != null)  return HitSurface.Flesh;
            if (struck.GetComponentInParent<TroopController>(Inactive) != null) return HitSurface.Flesh;

            // 4. EVERY OTHER STRUCTURE — stone. Buildings, towers, collectors, the Heart.
            //    Resolved off the shared damage interface rather than a type list, so a
            //    structure type added tomorrow reads as stone instead of reading as
            //    nothing. That is the owner's default, stated as a default.
            if (struck.GetComponentInParent<DeNelle.Core.Combat.IDamageableStructure>(Inactive) != null)
                return HitSurface.Stone;

            // 5. NO MATCH. Not an error and not a guess — the caller keeps its generic
            //    impact. Throttled because a miss on a hot melee path would otherwise
            //    firehose the log.
            FlowTrace.Throttle(FlowSys, "unresolved", 5f,
                $"no surface resolved for '{struck.name}' — it is neither a wall, gate, living " +
                "body, nor an IDamageableStructure. The generic Impact_Physical still plays; " +
                "add a case here only on an owner ruling, never by guessing the material.");
            return HitSurface.None;
        }

        /// <summary>
        /// Play the surface burst for <paramref name="surface"/> at <paramref name="position"/>.
        /// Returns true when a key was dispatched (NOT that the catalog resolved it — an
        /// unmapped key no-ops with its own throttled trace inside VFXManager.PlayKey).
        /// <para>
        /// Fire-and-forget: the returned <c>VFXHandle</c> is deliberately discarded, which
        /// is safe ONLY because all five rows are forced one-shot at the mirror
        /// (SurfaceImpactVfxMirrors clears main.loop on every layer, so the shared
        /// derivation in VfxLoopFlagRegression stamps IsLoop=false). A loop-flagged row
        /// played this way permanently burns one of the 20 global loop slots — that is the
        /// documented leak the vfx-loop-flag oracle exists to stop, and it is why these
        /// five could not simply be catalogued as the owner tagged them (isLoop:true).
        /// </para>
        /// </summary>
        public static bool Play(HitSurface surface, Vector3 position, Quaternion rotation = default)
        {
            string key = KeyFor(surface);
            if (string.IsNullOrEmpty(key)) return false;

            bool dispatched = false;
            Guard.Try(FlowSys, "surface impact " + surface, () =>
            {
                VFXManager.PlayKey(key, position, rotation);
                dispatched = true;
            });

            FlowTrace.Throttle(FlowSys, "play:" + surface, 2f,
                $"surface impact '{surface}' -> key '{key}' at {position} (owner tag, mapped verbatim).");
            return dispatched;
        }

        /// <summary>Resolve then play, in one call. Returns the surface that was resolved.</summary>
        public static HitSurface ResolveAndPlay(Component struck, Vector3 position,
                                                Quaternion rotation = default)
        {
            HitSurface surface = Resolve(struck);
            Play(surface, position, rotation);
            return surface;
        }
    }
}
