// =============================================================================
// QueueIconResolver — the ONE cached art lookup for a work-queue card (WO-864).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// WHY THIS EXISTS
// The queue card is designed VERB-FIRST (owner ruling 2026-08-03: "for now if no
// images we can use verbs"). The portrait is the ENHANCEMENT, so this resolver is
// allowed to return null and the card still reads correctly. That inversion is the
// whole point: a layout that assumed an icon and degraded to text would look broken
// on most cards, because most queueable things have no portrait.
//
// MEASURED COVERAGE (verified from disk 2026-08-03, not estimated):
//   Assets/Resources/Portraits/ holds 27 files.
//   With the BuildPaletteUI chain alone (id -> displayName-slug -> concept-icons):
//     structures 16/28, troops 0/7.
//   concept-icons.json carries 44 keys, ALL ability/UI/currency concepts — not one
//     structure id, structure slug, troop id or CatalogType token is a key, so that
//     third step contributes ZERO for queue jobs today.
//   With the chain below (adds the '_'->'-' form, the tier suffix, the leading
//   CATEGORY-TOKEN strip, the longest-token probe, and the troop iconId route):
//     structures 18/26 queueable, troops 7/7  ->  25/33 = ~76%.
//   The remaining 8 (wall_wood, wall_stone, gate_stone, fountain_healing, mill,
//   lumberyard, foundry, silo) have NO matching art on disk under ANY string
//   transform — no code change can reach them. They are reachable DATA-ONLY by
//   adding keys to concept-icons.json; that is deliberately NOT done here.
//
// The "tower_arcane_spire" case (the owner's live screen): strip "@15_7", '_'->'-'
// gives "tower-arcane-spire" (no file); dropping the leading category token yields
// "arcane-spire" -> arcane-spire.png. That single step is why the strip exists.
//
// CHEAP BY CONSTRUCTION (WO-864 §4b): every lookup is memoised, INCLUDING misses,
// so a portrait-less job costs exactly ONE failed probe for the whole session and a
// dictionary hit thereafter. Nothing here runs per frame.
//
// Constructs NO uGUI — this is a data/asset resolver, not a View.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;

namespace DeNelle.Core.UI
{
    /// <summary>Cached sprite lookup for <see cref="ObsidianQueueGate.QueueEntry"/> cards.</summary>
    public static class QueueIconResolver
    {
        // Leading catalog-category tokens that are NOT part of the art's file name.
        // "tower_arcane_spire" -> "arcane-spire"; "collector_farm" -> "farm".
        private static readonly string[] CategoryPrefixes =
        {
            "tower-", "wall-", "gate-", "mine-", "deco-", "collector-", "fountain-", "troop-",
        };

        // Resolved sprites, keyed on the full request (role|key|jobId|tier). NULLS ARE
        // CACHED — that is the point (one failed probe per distinct job, ever).
        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Art for a queue card, or null when none exists (the card then shows its verb +
        /// name, which is the designed default — never a blank card).
        /// </summary>
        public static Sprite Resolve(ObsidianQueueGate.QueueEntry e)
        {
            if (e.Free) return null;
            string ck = (e.IconRole ?? "") + "|" + (e.IconKey ?? "") + "|" + (e.JobId ?? "") + "|" + e.TargetTier;
            if (Cache.TryGetValue(ck, out var hit)) return hit;

            Sprite s = null;
            DeNelle.Core.Diagnostics.Guard.Try("QueueUi", "resolve card art for '" + (e.JobId ?? e.Label) + "'", () =>
            {
                // Explicit role/key wins — this is the TROOP route (RpgUi/icons/<iconId>),
                // the only one that reaches troops at all (0/7 via Portraits).
                if (!string.IsNullOrEmpty(e.IconRole))
                {
                    s = RpgUiCatalog.Get(e.IconRole, e.IconKey);
                    return;
                }
                s = ResolveStructure(e.JobId, e.TargetTier);
            });

            Cache[ck] = s;   // cache misses too
            return s;
        }

        /// <summary>Portraits chain for a build/upgrade job id (see the header for coverage).</summary>
        private static Sprite ResolveStructure(string jobId, int targetTier)
        {
            if (string.IsNullOrEmpty(jobId)) return null;

            // 1. Strip the placement suffix ("forge@1_2" -> "forge"); lower-case.
            string core = jobId;
            int at = core.IndexOf('@');
            if (at > 0) core = core.Substring(0, at);
            core = core.Trim().ToLowerInvariant();
            if (core.Length == 0) return null;

            string hyphen = core.Replace('_', '-');

            // 2. The catalog's authored display name is the single richest key (9 of the
            //    16 baseline hits come from it) — e.g. "tower_ground_archer" -> "Archer
            //    Tower" -> archer-tower.png, which the raw id never reaches.
            string slug = null;
            var entry = CatalogRegistry.Get(core);
            if (entry != null && !string.IsNullOrEmpty(entry.displayName))
                slug = Slug(entry.displayName);

            // 3. Tier art first when this is an upgrade to L2+ ("archer-tower-3.png").
            if (targetTier >= 2)
            {
                var tiered = LoadPortrait(slug != null ? slug + "-" + targetTier : null)
                          ?? LoadPortrait(hyphen + "-" + targetTier);
                if (tiered != null) return tiered;
            }

            var s = LoadPortrait(core)          // raw id ("barracks", "market", "pet-house")
                 ?? LoadPortrait(hyphen)        // underscore form ("wall_torch" -> "wall-torch")
                 ?? LoadPortrait(slug);         // display-name slug ("Archer Tower")
            if (s != null) return s;

            // 4. Punctuation-stripped slug ("Sky Ballista (Anti-Air)" -> "sky-ballista-anti-air").
            if (slug != null)
            {
                s = LoadPortrait(Clean(slug));
                if (s != null) return s;
            }

            // 5. Drop a leading CATEGORY token — the tower_arcane_spire case.
            foreach (var p in CategoryPrefixes)
            {
                if (!hyphen.StartsWith(p, System.StringComparison.Ordinal)) continue;
                s = LoadPortrait(hyphen.Substring(p.Length));
                if (s != null) return s;
                break;
            }

            // 6. Longest remaining token ("tower-siege-tower" -> "siege"; the catalog slug
            //    "Ballista" already covers that entry, so this is a genuine last resort).
            s = LongestTokenPortrait(slug) ?? LongestTokenPortrait(hyphen);
            if (s != null) return s;

            // 7. Concept icons — contributes nothing for today's catalog (see header), but
            //    it is the DATA-ONLY extension point: add a key and this starts hitting.
            return ConceptIconResolver.ResolveAny(core, hyphen, slug ?? core);
        }

        private static Sprite LongestTokenPortrait(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split('-');
            string best = null;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 3 && (best == null || parts[i].Length > best.Length)) best = parts[i];
            return best == null ? null : LoadPortrait(best);
        }

        /// <summary>"Archer Tower" -> "archer-tower" (the Portraits/ file convention).</summary>
        private static string Slug(string name)
            => string.IsNullOrEmpty(name) ? null : name.Trim().ToLowerInvariant().Replace(' ', '-');

        /// <summary>Drop everything outside [a-z0-9-] and collapse repeat separators.</summary>
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (c == '-' && sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }

        // Portraits import as plain Texture2D (NOT Sprite), so a bare Resources.Load<Sprite>
        // returns null for most of them — the same wrap BuildPaletteUI.LoadPortrait does.
        private static Sprite LoadPortrait(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string path = "Portraits/" + key;
            if (Cache.TryGetValue(path, out var cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f));
            }
            Cache[path] = sprite;   // cache the miss too
            return sprite;
        }
    }
}
