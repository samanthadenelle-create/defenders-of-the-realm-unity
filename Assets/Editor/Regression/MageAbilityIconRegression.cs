// =============================================================================
// MageAbilityIconRegression — pins that NO mage ability medallion renders KNIGHT
// iconography, by RESOLVING each mage ability through the REAL resolution order.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
// Marker  : MAGE_ICON_OK / MAGE_ICON_FAIL   (suite tag [mage-ability-icons])
//
// THE DEFECT THIS EXISTS FOR (ICON_CATALOG 2026-08-16, sections 1.2 + 5.1):
// the mage authored NO ability-id row in concept-icons.json, so all four of his
// medallions fell through to their EFFECT and painted the Knight's art —
//   q  'strike'    -> abilities/attack_sword   (a SWORD under the caption "Cast")
//   w  'shield'    -> icons/icon_shield
//   e  'drainshot' -> unmapped -> the icons/icon_combat crossed-swords DEFAULT
// and the same trap catches the learnable pool (mage.arcane-bolt / mage.thunder
// both 'strike' -> the sword; mage.manaweave unmapped -> the default).
//
// WHY IT ASSERTS BY SPRITE AND NOT BY STRING: HudModelProducers.cs:594 calls
// ConceptIconResolver.ResolveKey(def.Id, def.Effect) — VERB IS NEVER CONSULTED —
// and the view re-resolves that key, backstopping a null with
// ConceptIconResolver.DefaultSprite() (ElarionUiKitObsidian.cs:923). This suite
// walks that EXACT chain and compares the resulting UnityEngine.Sprite REFERENCE
// against the three knight-default sprites loaded from the same catalog. It never
// string-matches concept-icons.json, so re-pointing a row to different art, or
// deleting a row, or renaming an icon file, is all caught the same way: by what
// the player would actually see.
//
// GREEN-TODAY CONTRACT: the ids in KnownGaps still resolve knight art and are
// waiting on an OWNER icon tag (memory rule `vfx-map-owner-tags-no-creative-pick`
// — the CLI never picks the art). They are EXEMPTED, listed by name in the report,
// and every OTHER mage ability is pinned hard. So a new mage ability shipped with
// no concept row, or a wired row that regresses back onto the sword, FAILS.
// A gap that has since been tagged is reported as a NOTE telling the reader to
// delete its entry — never as a failure, so the correct fix is never blocked.
//
// NO HOLLOW PASS: if the catalog yields zero mage abilities, or if every mage
// ability turns out to be exempt (so nothing was actually pinned), the suite FAILS.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class MageAbilityIconRegression
    {
        /// <summary>The class blocks in abilities.json that hold mage-castable defs.</summary>
        private static readonly string[] MageClassBlocks = { "mage", "mage-skills" };

        /// <summary>
        /// Mage ability ids that STILL resolve knight-default art and are knowingly parked
        /// pending an OWNER icon tag. id -> why. Delete an entry the moment its row is
        /// authored; the suite tells you when that has happened.
        /// </summary>
        private static readonly Dictionary<string, string> KnownGaps =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "mage.drain",     "effect 'drainshot' maps to nothing, so it paints the icon_combat default. " +
                                "No mirrored icon literally depicts life-drain - every candidate is an " +
                                "interpretation, so the pick is the owner's." },
            { "mage.manaweave", "effect 'manaweave' maps to nothing, so it paints the icon_combat default. " +
                                "Arcanist6 is the owner's existing Manaweave TALENT tag but is already bound " +
                                "to arcane-bolt here; sharing it is a call only she should make." },
            { "mage.thunder",   "effect 'strike' -> abilities/attack_sword. Electromancer9 (knight.thunderbolt) " +
                                "is the obvious look-alike and abilities.json rules VERBATIM that reusing that " +
                                "ability's keys 'is exactly the creative pick the rule forbids'." },
        };

        public static bool Run(out string report)
        {
            var failures = new List<string>();
            var notes = new List<string>();

#if UNITY_EDITOR
            // Read the JSON as it is on disk right now, not a stale domain-reload cache.
            ConceptIconResolver.ClearCache();
#endif
            AbilityCatalog.Reload();

            // ── The three knight-default sprites, loaded through the SAME catalog the HUD uses ──
            Sprite knightSword = RpgUiCatalog.Get("abilities", "attack_sword");
            Sprite knightShield = RpgUiCatalog.Get("icons", "icon_shield");
            Sprite crossedSwords = ConceptIconResolver.DefaultSprite();

            if (knightSword == null && knightShield == null && crossedSwords == null)
            {
                report = "MAGE_ICON_FAIL: none of the three knight-default sprites (abilities/attack_sword, " +
                         "icons/icon_shield, the concept-icons 'default' block) could be loaded, so this suite " +
                         "cannot tell a knight icon from a mage one and every check below would pass vacuously. " +
                         "Fix the art/roles before trusting a green here.";
                return false;
            }

            var banned = new List<KeyValuePair<Sprite, string>>();
            if (knightSword != null) banned.Add(new KeyValuePair<Sprite, string>(knightSword, "abilities/attack_sword (the Knight's sword)"));
            if (knightShield != null) banned.Add(new KeyValuePair<Sprite, string>(knightShield, "icons/icon_shield (the Knight's shield)"));
            if (crossedSwords != null) banned.Add(new KeyValuePair<Sprite, string>(crossedSwords, "icons/icon_combat (the crossed-swords DEFAULT - nothing mapped)"));

            // ── Collect every mage-castable def, de-duplicated by id ──
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var defs = new List<AbilityDef>();
            for (int b = 0; b < MageClassBlocks.Length; b++)
            {
                var loadout = AbilityCatalog.GetLoadout(MageClassBlocks[b]);
                if (loadout == null) continue;
                for (int i = 0; i < loadout.Count; i++)
                {
                    var d = loadout[i];
                    if (d == null || string.IsNullOrEmpty(d.Id)) continue;
                    if (seen.Add(d.Id.Trim().ToLowerInvariant())) defs.Add(d);
                }
            }
            // GetLoadout only walks q/w/e/r; the learnable pool is keyed by name, so sweep it by id too.
            foreach (var id in PoolIdsFromCatalog())
            {
                var d = AbilityCatalog.FindById(id);
                if (d == null || string.IsNullOrEmpty(d.Id)) continue;
                if (seen.Add(d.Id.Trim().ToLowerInvariant())) defs.Add(d);
            }

            if (defs.Count == 0)
            {
                report = "MAGE_ICON_FAIL: AbilityCatalog yielded ZERO mage abilities (blocks '" +
                         string.Join("', '", MageClassBlocks) + "'), so nothing was pinned. A rename of the " +
                         "mage class block, or abilities.json failing to parse, lands here - never treat this " +
                         "as 'no mage icons to check'.";
                return false;
            }

            // ── Walk the real chain for each ──
            int pinned = 0, exempt = 0;
            foreach (var def in defs)
            {
                string id = def.Id.Trim();
                string effect = def.Effect;

                // EXACTLY what HudModelProducers.AbilityLoadoutProducer.Poll does (:594-595) ...
                string resolvedKey = ConceptIconResolver.ResolveKey(id, effect);
                // ... and what ActionSlotHandle.SetIcon does with the result (ElarionUiKitObsidian.cs:923).
                Sprite painted = resolvedKey != null ? ConceptIconResolver.Resolve(resolvedKey) : null;
                if (painted == null) painted = ConceptIconResolver.DefaultSprite();

                bool idAuthored = ConceptIconResolver.Resolve(id) != null;
                string bannedWhy = null;
                for (int i = 0; i < banned.Count; i++)
                    if (ReferenceEquals(painted, banned[i].Key)) { bannedWhy = banned[i].Value; break; }

                bool gapped = KnownGaps.TryGetValue(id, out string gapWhy);

                if (bannedWhy != null)
                {
                    if (gapped)
                    {
                        exempt++;
                        notes.Add("EXEMPT " + id + " -> " + bannedWhy + " | pending owner tag: " + gapWhy);
                    }
                    else
                    {
                        failures.Add("[mage-ability-icons] '" + id + "' (effect '" + (effect ?? "<none>") +
                                     "') paints " + bannedWhy + " on the mage's action bar. The medallion " +
                                     "shows KNIGHT art under a mage caption - 'verb' is never consulted for an " +
                                     "icon (HudModelProducers.cs:594 passes only id+effect), so ONLY a " +
                                     "concept-icons.json row keyed on this exact id can fix it. Author one " +
                                     "with owner-tagged art, or add the id to KnownGaps with a reason.");
                    }
                    continue;
                }

                if (gapped)
                {
                    notes.Add("GRADUATED " + id + " no longer paints knight art (now " +
                              (painted != null ? painted.name : "<null>") + ") - DELETE its KnownGaps entry so " +
                              "it becomes hard-pinned.");
                    pinned++;
                    continue;
                }

                if (painted == null)
                {
                    failures.Add("[mage-ability-icons] '" + id + "' resolves NO sprite at all - not even the " +
                                 "icon_combat default. The slot renders empty. Check that the row's role/name " +
                                 "addresses real art in Resources/RpgUi/.");
                    continue;
                }

                pinned++;
                notes.Add((idAuthored ? "AUTHORED " : "effect-fallback ") + id + " -> " + painted.name +
                          " (key '" + (resolvedKey ?? "<none>") + "', effect '" + (effect ?? "<none>") + "')");
            }

            // ── Hollow-pass guards ──
            if (pinned == 0)
                failures.Add("[mage-ability-icons] " + defs.Count + " mage ability/abilities were examined but " +
                             "NONE was actually pinned (" + exempt + " exempt). A suite that exempts everything " +
                             "proves nothing - shrink KnownGaps.");

            // The two rows this suite was written alongside must resolve REAL art, or a typo'd
            // icon name would silently fall back and still dodge the banned-sprite check above.
            AssertAuthoredArt("mage.fireball", failures);
            AssertAuthoredArt("mage.shell", failures);

            var sb = new StringBuilder();
            sb.Append(failures.Count == 0 ? "MAGE_ICON_OK" : "MAGE_ICON_FAIL")
              .Append(" ").Append(pinned).Append(" pinned / ").Append(exempt).Append(" exempt / ")
              .Append(defs.Count).Append(" mage abilities");
            for (int i = 0; i < notes.Count; i++) sb.Append("\n    ").Append(notes[i]);
            for (int i = 0; i < failures.Count; i++) sb.Append("\n  ").Append(failures[i]);

            report = sb.ToString();
            return failures.Count == 0;
        }

        /// <summary>
        /// Fails when <paramref name="id"/> has no concept row OR its row addresses art that is
        /// not on disk - the silent case where the id "looks wired" in JSON but still falls back.
        /// </summary>
        private static void AssertAuthoredArt(string id, List<string> failures)
        {
            if (ConceptIconResolver.Resolve(id) == null)
                failures.Add("[mage-ability-icons] '" + id + "' resolves no art by its OWN id. Either the " +
                             "concept-icons.json row was removed, or its role/name points at a sprite that is " +
                             "not in Resources/RpgUi/ - both put the mage back on the Knight's fallback art.");
        }

        /// <summary>
        /// The learnable mage-skills ids. Kept as an explicit list because GetLoadout only walks
        /// q/w/e/r while the pool is keyed by spell name; FindById validates each against the
        /// live catalog, so a renamed/removed id simply drops out rather than throwing.
        /// </summary>
        private static IEnumerable<string> PoolIdsFromCatalog()
        {
            yield return "mage.frost-nova";
            yield return "mage.arcane-bolt";
            yield return "mage.manaweave";
            yield return "mage.void-rift";
            yield return "mage.blink";
            yield return "mage.cataclysm";
            yield return "mage.thunder";
            yield return "mage.heal";
            yield return "mage.meteor";
        }
    }
}
