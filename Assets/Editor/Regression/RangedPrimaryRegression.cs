// =============================================================================
// RangedPrimaryRegression — pins WO-1105 (Sylas plays as an ARCHER).
// -----------------------------------------------------------------------------
// regression-registry: registered by the committer (do NOT self-register here —
// DataRegression.cs is lane-fenced; the orchestrator adds the [ranged-primary] row).
//
// ⭐ REVISED 2026-08-16 (owner ruling, verbatim: "change the bow and arrow attack to
// the action bar and leave the attack as the dagger attack"). THE ASSERTIONS BELOW
// CHANGED SIDES, THEY WERE NOT WEAKENED. This suite used to assert the bow was the
// PRIMARY ATTACK INPUT; the owner reversed that, so it now asserts the ARRANGEMENT
// she asked for, and Case 5 + Case 6 FAIL if the old arrangement is ever restored:
//
//   * the PRIMARY attack is the class-agnostic melee/dagger sweep, for every class
//     including the ranger (PlayerAttackController must not route the primary input
//     through a ranged cast);
//   * the BOW is an ACTION-BAR ABILITY - ranger.q, in the Q slot the bar already
//     renders - fired deliberately, wearing its authored verb + bow icon;
//   * the bow slot GREYS OUT under its cooldown like every other ability. The
//     morning's deliberate deviation (the face kept `interactable = true` through the
//     sweep, because a cooling bow would otherwise leave the archer inputless) is
//     MOOT - the dagger is always available - and Case 6 pins that it is gone.
//
// WHY: WO-1105 seats a bow's grip on the ROUNDED EDGE - the apex of the riser's bulge
// in Z at mid-Y (GripAnchor.BowGrip) - instead of the bounds centre, and the archer's
// whole presentation rests on DATA that a catalog regeneration can silently rewrite.
// This suite pins that data:
//
//   Case 1 — CROSSBOW EXCLUSION (owner ruling R4a, the load-bearing guard).
//            The RUNTIME weapons catalog (the Resources copy, the one that WINS at
//            runtime) must contain NO weapon whose id/mesh/name carries the token
//            "crossbow". Measured 2026-08-16: Resources = 0 crossbows,
//            StreamingAssets = 125. A crossbow inverts R4's axis rule (widest -> X,
//            narrowest -> Y; it is held across the body, not upright), so every one
//            of those 125 rows would seat WRONG under the bow grip derivation. The
//            editor menu Defenders/Catalog/Generate Gear Catalog re-inflates the
//            Resources copy from ~96 rows to ~431 and writes BOTH copies — running
//            it would pull all 125 in at once. This case makes that loud instead of
//            silent. DELETE THIS CASE ONLY when the inverted crossbow mapping is
//            implemented AND proven on device (the owner's "for simplicity, let's
//            not include any crossbows until we have verified" is the gate).
//
//   Case 2 — the RANGER's basic is still a RANGED basic. classes.ranger.abilities.q
//            must be effect 'strike' with range > 0 and a cooldown > 0 (R3: "an
//            archer is not a click-spam weapon" — the bow primary's cooldown is read
//            from THIS number, never a literal in code).
//
//   Case 4 — the BOW GRIP lands on the ROUNDED EDGE (owner correction 2026-08-16).
//            Built against a SYNTHETIC bow whose apex is known in closed form, so
//            the assertion is arithmetic, not art: a 1.0 m Y-long stave, 0.02 m
//            thick on X, with a dead-straight string edge at z=0 and a limb/riser
//            curve z(y) = D*(1-(2y)^2), D = 0.30 m, apex exactly at mid-Y. The
//            derived grip must be (0, 0, +D) - the apex - NOT (0, 0, 0), which is
//            what the FIRST-SURFACE rule this morning returned (it stopped on the
//            straight edge / string) and what the owner rejected: "You wanna follow
//            that perpendicular from the y axis over to the rounded hilt. The round
//            part of the bow is where the grip is." The two answers are 0.30 m
//            apart on a 1 m bow, so this case cannot pass under the old rule.
//
//   Case 3 — the ranged-basic DISCRIMINATOR still separates the classes it must.
//            HeroAbilities.TryGetRangedPrimary accepts a basic whose effect is
//            strike/drainshot AND whose range exceeds the hero's melee reach by more
//            than 2x. It no longer picks the primary INPUT (the owner's revision), but
//            it is still the one derived "does this class shoot?" test, and both
//            HeroTargetIndicator's auto-acquire/tap-override range gate (R1/R2) and
//            the Focus no-double-refund rule in PlayerAttackController.ResolveAttack
//            read it. Pinned against a reference melee reach of 3.2 m: ranger must
//            PASS and knight must FAIL.
//
//   Case 5 — the BOW IS AN ACTION-BAR ABILITY AND THE PRIMARY IS THE DAGGER.
//            ranger.q must be authored into the "q" bar slot (the bar renders the four
//            locked/loadout slots Q/W/E/R and casts the resolved def), and
//            PlayerAttackController's primary input must NOT route through a ranged
//            cast: no FirePrimary/FireRangedPrimary/ResolveRangedTarget, and Update
//            must call StartAttack directly. This is the owner's ruling in assertion
//            form — restoring the morning's arrangement fails here first.
//
//   Case 6 — the COOLDOWN SPECIAL CASE IS GONE. HudKitController must not force
//            `interactable = true` on the primary-attack face, and must still gate the
//            ability medallions on `!cooling`. The bow greys out while it cools like
//            every other ability; the justification for the exception (a cooling bow
//            leaving the ranger with no input) died with the dagger primary.
//
// Parsed straight from the JSON (never through a live catalog), so a copy that
// parses but was only half-regenerated is still caught.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RangedPrimaryRegression
    {
        private const string AbilitiesResourcesPath = "Assets/Resources/Data/Canonical/abilities.json";
        private const string WeaponsResourcesPath   = "Assets/Resources/Data/Canonical/weapons.json";

        /// <summary>The name token that identifies an excluded weapon (owner R4a, case-insensitive).</summary>
        private const string ExcludedWeaponToken = "crossbow";

        /// <summary>PlayerAttackController's serialized fallback melee reach (m) — the reference the
        /// discriminator is pinned against. Not a gameplay value; a test fixture.</summary>
        private const float ReferenceMeleeReach = 3.2f;

        /// <summary>Mirrors HeroAbilities.RangedPrimaryReachFactor.</summary>
        private const float RangedPrimaryReachFactor = 2f;

        /// <summary>Standalone batch entry — prints the RANGED_PRIMARY_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("RANGED_PRIMARY_OK - " + reason);
            else Debug.LogError("RANGED_PRIMARY_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([ranged-primary]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Case(failures, "crossbow-exclusion", () => Case1_NoCrossbowInRuntimeCatalog(failures, notes));

            JObject abilities = null;
            Case(failures, "parse-abilities", () =>
            {
                string json = ReadText(AbilitiesResourcesPath);
                if (json == null) { failures.Add("[parse-abilities] cannot read " + AbilitiesResourcesPath); return; }
                abilities = JObject.Parse(json);
            });

            if (abilities != null)
            {
                Case(failures, "ranger-basic-ranged", () => Case2_RangerBasicIsRanged(abilities, failures, notes));
                Case(failures, "discriminator", () => Case3_DiscriminatorStillSeparates(abilities, failures, notes));
                Case(failures, "bow-on-action-bar", () => Case5_BowIsAnActionBarAbility(abilities, failures, notes));
            }

            Case(failures, "bow-grip-apex", () => Case4_BowGripSeatsOnRoundedEdge(failures, notes));
            Case(failures, "cooldown-greys-out", () => Case6_NoCooldownSpecialCase(failures, notes));

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "RANGED PRIMARY OK - 6/6 cases pass (no crossbow can reach the runtime weapons " +
                         "catalog while the R4a exclusion stands, the ranger basic is still a costed-" +
                         "cooldown ranged strike carrying its verb + bow icon, the ranged-basic " +
                         "discriminator still admits the ranger while rejecting the knight, the bow grip " +
                         "still seats on the ROUNDED EDGE apex rather than the straight/string edge, the " +
                         "BOW is an ACTION-BAR ability while the PRIMARY attack is the melee/dagger " +
                         "sweep, and the bow slot greys out under its cooldown with no special case)" + noteStr;
                return true;
            }
            reason = "RANGED PRIMARY FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // =====================================================================
        //  CASE 1 — no crossbow may reach the RUNTIME (Resources) weapons catalog
        // =====================================================================
        private static void Case1_NoCrossbowInRuntimeCatalog(List<string> failures, List<string> notes)
        {
            string json = ReadText(WeaponsResourcesPath);
            if (json == null)
            {
                failures.Add("[crossbow-exclusion] cannot read " + WeaponsResourcesPath +
                             " - the runtime weapons catalog is the copy that WINS at runtime; " +
                             "without it this guard cannot prove the exclusion holds.");
                return;
            }

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex)
            {
                failures.Add("[crossbow-exclusion] " + WeaponsResourcesPath + " does not parse: " + ex.Message);
                return;
            }

            var weapons = root["weapons"] as JArray;
            if (weapons == null)
            {
                failures.Add("[crossbow-exclusion] " + WeaponsResourcesPath + " has no 'weapons' array.");
                return;
            }

            var offenders = new List<string>();
            foreach (var w in weapons)
            {
                string id = (string)w["id"] ?? string.Empty;
                string mesh = (string)w["mesh"] ?? string.Empty;
                string name = (string)w["name"] ?? string.Empty;
                // 'category' is the field the 431-row side actually keys crossbows on (measured:
                // the StreamingAssets copy carries category='crossbow'), so it is scanned too.
                string category = (string)w["category"] ?? string.Empty;
                if (Contains(id) || Contains(mesh) || Contains(name) || Contains(category))
                    offenders.Add(string.IsNullOrEmpty(id) ? (string.IsNullOrEmpty(mesh) ? name : mesh) : id);
            }

            if (offenders.Count > 0)
            {
                failures.Add("[crossbow-exclusion] the RUNTIME weapons catalog carries " + offenders.Count +
                             " crossbow row(s) (e.g. " + string.Join(", ", offenders.GetRange(0, Math.Min(5, offenders.Count))) +
                             ") - owner ruling R4a EXCLUDES crossbows until the plain-bow grip is proven " +
                             "on device. A crossbow is widest on X and narrowest on Y and is held across " +
                             "the body, so WeaponBoundsOrient's longest-to-+Y premise (and therefore the " +
                             "BowGrip derivation) is wrong for it by construction and it would seat wrong. " +
                             "Most likely cause: Defenders/Catalog/Generate Gear Catalog was run, which " +
                             "re-inflates this copy and pulls the StreamingAssets crossbow rows in.");
                return;
            }

            notes.Add("weapons(runtime)=" + weapons.Count + " rows, 0 crossbows");
        }

        private const string ConceptIconsResourcesPath = "Assets/Resources/Data/Canonical/concept-icons.json";

        /// <summary>True when the RUNTIME concept-icon map binds <paramref name="conceptId"/>.</summary>
        private static bool ConceptIconMapHas(string conceptId)
        {
            string json = ReadText(ConceptIconsResourcesPath);
            if (json == null) return false;
            try { return JObject.Parse(json)["map"]?[conceptId] != null; }
            catch { return false; }
        }

        /// <summary>ASCII-only guard (CLAUDE.md string rule) for player-facing authored text.</summary>
        private static bool IsAscii(string s)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] > 0x7E || s[i] < 0x20) return false;
            return true;
        }

        private static bool Contains(string s)
            => !string.IsNullOrEmpty(s) &&
               s.IndexOf(ExcludedWeaponToken, StringComparison.OrdinalIgnoreCase) >= 0;

        // =====================================================================
        //  CASE 2 — the ranger's basic is still a ranged strike with a real cooldown
        // =====================================================================
        private static void Case2_RangerBasicIsRanged(JObject root, List<string> failures, List<string> notes)
        {
            JToken q = root.SelectToken("classes.ranger.abilities.q");
            if (q == null)
            {
                failures.Add("[ranger-basic-ranged] classes.ranger.abilities.q is missing - the ranged " +
                             "primary resolves through the LOCKED Q def; with no def the archer falls " +
                             "back to the melee sweep, which is the exact WO-1105 defect.");
                return;
            }

            string effect = ((string)q["effect"] ?? string.Empty).Trim().ToLowerInvariant();
            float range = (float?)q["range"] ?? 0f;
            float cooldown = (float?)q["cooldown"] ?? 0f;
            string id = (string)q["id"] ?? "(no id)";

            if (effect != "strike" && effect != "drainshot")
                failures.Add("[ranger-basic-ranged] ranger.q effect='" + effect + "' is not a projectile " +
                             "shape - only 'strike'/'drainshot' route through ResolveStrikeLike -> " +
                             "LaunchProjectile (damage on ARRIVAL), which is what makes an arrow read as " +
                             "an arrow and what carries the WO-997 Focus hit-confirm restore.");
            if (range <= 0f)
                failures.Add("[ranger-basic-ranged] ranger.q range=" + range + " must be > 0 - the auto-" +
                             "target engage radius (R2) and the shot's reach are both READ from it.");
            if (cooldown <= 0f)
                failures.Add("[ranger-basic-ranged] ranger.q cooldown=" + cooldown + " must be > 0 - owner " +
                             "ruling R3: the bow primary carries a REAL cooldown (an archer is not a " +
                             "click-spam weapon), and the offhand dagger is what covers while it cools.");

            // The BOW'S ACTION-BAR FACE is derived from this def, so its two presentation fields are
            // data and are pinned here. Losing either sends the archer back to a generic face -
            // the owner's "the action bars seem to reflect something more generic" - and no C#
            // change would be needed to cause it, which is precisely why it needs a guard.
            string verb = ((string)q["verb"] ?? string.Empty).Trim();
            if (verb.Length == 0)
                failures.Add("[ranger-basic-ranged] ranger.q has no 'verb' - the bow's action-bar " +
                             "medallion reads its caption from THIS field (owner 2026-08-16: 'It should " +
                             "be the word shoot'). With it absent the slot shows the icon alone and the " +
                             "word she asked for is gone, with no C# change needed to cause it.");
            else if (!IsAscii(verb))
                failures.Add("[ranger-basic-ranged] ranger.q verb '" + verb + "' is not ASCII.");

            if (!ConceptIconMapHas("ranger.q"))
                failures.Add("[ranger-basic-ranged] concept-icons.json has no 'ranger.q' entry - the " +
                             "bow's action-bar medallion resolves its icon through this concept map, and " +
                             "with no entry it falls through to 'strike' -> abilities/attack_sword. That " +
                             "is literally the reported defect: the archer showing a SWORD. Bind it to a " +
                             "bow silhouette (today: spellicons/Hunter12).");

            notes.Add("ranger.q=" + id + " effect=" + effect + " range=" + range.ToString("0.##") +
                      "m cd=" + cooldown.ToString("0.##") + "s verb='" + verb + "'");
        }

        // =====================================================================
        //  CASE 3 — the discriminator still admits the ranger and rejects the knight
        // =====================================================================
        private static void Case3_DiscriminatorStillSeparates(JObject root, List<string> failures, List<string> notes)
        {
            bool ranger = IsRangedPrimary(root, "ranger", out string rWhy);
            bool knight = IsRangedPrimary(root, "knight", out string kWhy);

            if (!ranger)
                failures.Add("[discriminator] ranger.q no longer qualifies as a RANGED basic (" + rWhy +
                             ") - HeroTargetIndicator gates the archer's auto-acquire + sticky tap " +
                             "override on this test (R1/R2), and PlayerAttackController's Focus " +
                             "no-double-refund rule reads it, so Sylas would lose his targeting AND " +
                             "start double-earning Focus off the dagger.");
            if (knight)
                failures.Add("[discriminator] knight.q NOW qualifies as a ranged basic (" + kWhy +
                             ") - the Knight would inherit the archer's target-acquire ring and stop " +
                             "earning his on-hit restore. WO-1105 requires the Knight path unaffected.");

            notes.Add("discriminator: ranger=" + (ranger ? "ranged" : "melee") + " (" + rWhy + "), knight=" +
                      (knight ? "ranged" : "melee") + " (" + kWhy + ")");
        }

        /// <summary>Mirrors HeroAbilities.TryGetRangedPrimary against the authored JSON.</summary>
        private static bool IsRangedPrimary(JObject root, string cls, out string why)
        {
            JToken q = root.SelectToken("classes." + cls + ".abilities.q");
            if (q == null) { why = "no q def"; return false; }
            string effect = ((string)q["effect"] ?? string.Empty).Trim().ToLowerInvariant();
            float range = (float?)q["range"] ?? 0f;
            if (effect != "strike" && effect != "drainshot")
            {
                why = "effect='" + effect + "' is not a projectile shape";
                return false;
            }
            float threshold = ReferenceMeleeReach * RangedPrimaryReachFactor;
            if (range <= threshold)
            {
                why = "range " + range.ToString("0.##") + "m <= " + threshold.ToString("0.##") + "m threshold";
                return false;
            }
            why = "effect='" + effect + "' range " + range.ToString("0.##") + "m > " +
                  threshold.ToString("0.##") + "m threshold";
            return true;
        }

        // =====================================================================
        //  CASE 5 — the BOW is an ACTION-BAR ability; the PRIMARY attack is the dagger
        // =====================================================================
        //
        // Owner ruling 2026-08-16, verbatim: "change the bow and arrow attack to the action bar and
        // leave the attack as the dagger attack." Two halves, both asserted:
        //
        //   (a) DATA — ranger.q must be authored into the "q" BAR SLOT. The action bar renders the
        //       four resolved slots Q/W/E/R (HudModelProducers.ResolveSlotDef -> HeroAbilities
        //       .ResolvedDef) and a tap casts that def, so an authored "q" IS the bow being on the
        //       bar. If the slot key ever moved, the bow would leave the bar with no C# change.
        //
        //   (b) SOURCE — PlayerAttackController's primary input must not route through a ranged
        //       cast. Read as TEXT rather than executed because the alternative needs a live hero
        //       rig in play mode, which this batchmode suite has no way to stand up; the three
        //       member names it forbids are the exact ones the reverted arrangement introduced, so
        //       re-adding it cannot pass. Paired with (a) so a rename alone cannot silence it: the
        //       positive `StartAttack()` call in Update is asserted PRESENT as well.
        private const string PlayerAttackControllerPath =
            "Assets/_Modules/Village/Enemies/PlayerAttackController.cs";

        private static void Case5_BowIsAnActionBarAbility(JObject root, List<string> failures, List<string> notes)
        {
            // ── (a) the bow occupies the Q action-bar slot ────────────────────────────────────
            JToken q = root.SelectToken("classes.ranger.abilities.q");
            string slotKey = q != null ? ((string)q["slot"] ?? string.Empty).Trim().ToLowerInvariant() : null;
            if (q == null)
                failures.Add("[bow-on-action-bar] classes.ranger.abilities.q is missing - the bow IS " +
                             "the Q action-bar slot; with no def the archer has no bow on the bar at all.");
            else if (slotKey != "q")
                failures.Add("[bow-on-action-bar] ranger.q authors slot='" + slotKey + "', expected 'q' - " +
                             "the action bar renders the four resolved slots Q/W/E/R and casts the def it " +
                             "resolves, so moving the slot key takes the owner's bow OFF the bar with no " +
                             "code change (ruling: 'change the bow and arrow attack to the action bar').");

            // ── (b) the primary attack input is the melee/dagger sweep ────────────────────────
            string src = ReadText(PlayerAttackControllerPath);
            if (src == null)
            {
                failures.Add("[bow-on-action-bar] cannot read " + PlayerAttackControllerPath +
                             " - the primary-attack half of this case cannot be proved.");
                return;
            }

            string[] forbidden = { "FireRangedPrimary", "ResolveRangedTarget", "private void FirePrimary" };
            var found = new List<string>();
            foreach (string token in forbidden)
                if (src.IndexOf(token, StringComparison.Ordinal) >= 0) found.Add(token);

            if (found.Count > 0)
                failures.Add("[bow-on-action-bar] PlayerAttackController still carries " +
                             string.Join(", ", found) + " - that is the REVERTED arrangement, where the " +
                             "primary attack input fired the bow. Owner ruling 2026-08-16: 'leave the " +
                             "attack as the dagger attack'. The primary verb is the class-agnostic melee " +
                             "sweep for every class; the bow is fired from the action bar.");

            if (src.IndexOf("StartAttack();", StringComparison.Ordinal) < 0)
                failures.Add("[bow-on-action-bar] PlayerAttackController no longer calls StartAttack() - " +
                             "the melee/dagger sweep IS the primary attack, so this is not a rename-proof " +
                             "detail: with no call the primary input does nothing at all.");

            notes.Add("bow-on-action-bar: ranger.q slot='" + (slotKey ?? "?") + "', primary input = melee sweep");
        }

        // =====================================================================
        //  CASE 6 — the bow slot greys out on cooldown; no special case survives
        // =====================================================================
        //
        // The 2026-08-16 morning pass shipped ONE deliberate deviation: the attack face kept
        // `button.interactable = true` through its cooldown sweep, reasoning that disabling it would
        // leave the ranger with no input while the bow cooled. With the dagger as the primary attack
        // that reasoning is GONE — the player always has an attack — so the bow, like every other
        // ability, greys out while it cools. A special case whose justification has died is exactly
        // the kind of thing that survives silently in a large file, so it is pinned rather than
        // trusted to a comment.
        private const string HudKitControllerPath = "Assets/_Modules/HUD/Kit/HudKitController.cs";

        private static void Case6_NoCooldownSpecialCase(List<string> failures, List<string> notes)
        {
            string src = ReadText(HudKitControllerPath);
            if (src == null)
            {
                failures.Add("[cooldown-greys-out] cannot read " + HudKitControllerPath +
                             " - cannot prove the cooldown special case is gone.");
                return;
            }

            if (src.IndexOf("_attackSlot.button.interactable = true", StringComparison.Ordinal) >= 0 ||
                src.IndexOf("DrivePrimaryFace", StringComparison.Ordinal) >= 0)
                failures.Add("[cooldown-greys-out] HudKitController still forces the primary-attack face " +
                             "interactable through its cooldown sweep (DrivePrimaryFace / " +
                             "'_attackSlot.button.interactable = true'). That deviation existed ONLY " +
                             "because a cooling BOW would have left the archer inputless; the owner's " +
                             "2026-08-16 ruling made the dagger the primary attack, so the reason is gone " +
                             "and the standard SetCooldown gate must stand unmodified.");

            // The positive half: the ability medallions - which is where the bow now lives - must
            // still gate their tap on the cooldown. Both HUD shapes are asserted, because the bow
            // renders as a soft-glow medallion when CombatHud611 is ON and a radial-sweep slot when
            // it is OFF, and only the medallion branch sets interactable explicitly.
            if (src.IndexOf("interactable = !cooling", StringComparison.Ordinal) < 0)
                failures.Add("[cooldown-greys-out] no ability slot in HudKitController gates " +
                             "`interactable` on `!cooling` - the bow (ranger.q, the Q medallion) would " +
                             "stay tappable while it cools, which is the opposite of the ruling: it must " +
                             "grey out like every other ability in the game.");

            notes.Add("cooldown-greys-out: no primary-face interactable override; medallions gate on !cooling");
        }

        // =====================================================================
        //  CASE 4 — the bow grip seats on the ROUNDED EDGE, not the straight one
        // =====================================================================

        /// <summary>Synthetic bow dimensions (metres). Chosen so every expected number is exact.</summary>
        private const float SynthBowLength = 1.00f;   // Y span  - the LONGEST axis (R4 premise)
        private const float SynthBowThick  = 0.02f;   // X span  - the NARROWEST axis
        private const float SynthBowBulge  = 0.30f;   // Z apex  - the DEPTH the rounded edge raises out
        /// <summary>Tolerance on the derived seat. 1 mm on a 1 m bow; the WRONG answer (the straight
        /// edge) is 0.30 m away, so this can never pass by luck.</summary>
        private const float SynthBowTolerance = 0.001f;

        private static void Case4_BowGripSeatsOnRoundedEdge(List<string> failures, List<string> notes)
        {
            GameObject rig = null;
            try
            {
                // A parent to measure in, and a prop the solver owns.
                rig = new GameObject("BowGripProbe");
                var parent = new GameObject("Anchor").transform;
                parent.SetParent(rig.transform, false);

                var prop = new GameObject("SynthBow", typeof(MeshFilter), typeof(MeshRenderer));
                prop.GetComponent<MeshFilter>().sharedMesh = BuildSyntheticBowMesh();
                if (!prop.GetComponent<MeshFilter>().sharedMesh.isReadable)
                {
                    notes.Add("bow-grip-apex SKIPPED: procedural mesh reported not readable");
                    return;
                }

                DeNelle.Core.Geometry.WeaponBoundsOrient.NormalizeInto(
                    prop, parent, SynthBowLength,
                    DeNelle.Core.Geometry.WeaponBoundsOrient.GripAnchor.BowGrip,
                    resolveBladeUpFromHilt: false);

                // NormalizeInto subtracts the derived grip from the prop's local position, so the
                // grip point lands ON the anchor origin. Read the seat back off the transform: the
                // prop must have been pushed -Z by the full bulge. The straight-edge answer (the
                // pre-correction first-surface rule) would leave z at ~0.
                Vector3 seat = prop.transform.localPosition;
                float expectedZ = -SynthBowBulge;
                float err = Mathf.Abs(seat.z - expectedZ);

                notes.Add("bow-grip-apex: seat=(" + seat.x.ToString("0.####") + "," +
                          seat.y.ToString("0.####") + "," + seat.z.ToString("0.####") +
                          ") expectedZ=" + expectedZ.ToString("0.####") + " err=" + err.ToString("0.#####") + "m");

                if (err > SynthBowTolerance)
                    failures.Add("[bow-grip-apex] the derived grip seated at z=" + seat.z.ToString("0.####") +
                                 "m, expected " + expectedZ.ToString("0.####") + "m (the apex of the rounded " +
                                 "edge). z near 0 means the solve stopped on the STRAIGHT/string edge - that " +
                                 "is the exact seat the owner rejected on 2026-08-16: 'You wanna follow that " +
                                 "perpendicular from the y axis over to the rounded hilt. The round part of " +
                                 "the bow is where the grip is.'");

                if (Mathf.Abs(seat.y) > SynthBowTolerance)
                    failures.Add("[bow-grip-apex] the grip left mid-Y (y=" + seat.y.ToString("0.####") +
                                 "m, expected 0) - the mid-Y start point is CONFIRMED CORRECT by the owner " +
                                 "and must not move; only the termination was ever wrong.");
                if (Mathf.Abs(seat.x) > SynthBowTolerance)
                    failures.Add("[bow-grip-apex] the grip left the X centre-line (x=" + seat.x.ToString("0.####") +
                                 "m, expected 0) - X is the NARROW axis; the hand closes around the stave, " +
                                 "not on one of its faces.");
            }
            finally
            {
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        /// <summary>
        /// A bow with a known closed-form apex: Y-long over <see cref="SynthBowLength"/>, X-thin,
        /// a dead-straight edge at z=0 (the string) and a limb curve z(y) = D*(1-(2y/L)^2) whose
        /// maximum D sits EXACTLY at mid-Y (Rows is ODD so a vertex row lands there). Real
        /// triangles are emitted, not a bare point cloud, so the MeshRenderer reports a valid
        /// submesh and Renderer.bounds - which is what WeaponBoundsOrient.TryLocalBounds measures.
        /// </summary>
        private static Mesh BuildSyntheticBowMesh()
        {
            const int Rows = 41;                       // odd => a vertex row lands exactly on mid-Y
            float halfX = SynthBowThick * 0.5f;
            var verts = new List<Vector3>(Rows * 4);
            for (int i = 0; i < Rows; i++)
            {
                float t = i / (float)(Rows - 1);                       // 0..1
                float y = (t - 0.5f) * SynthBowLength;                 // -L/2 .. +L/2
                float u = 2f * y / SynthBowLength;                     // -1..1
                float z = SynthBowBulge * (1f - u * u);                // apex D at y=0
                verts.Add(new Vector3(-halfX, y, 0f));                 // 0: straight (string) edge
                verts.Add(new Vector3(+halfX, y, 0f));                 // 1
                verts.Add(new Vector3(-halfX, y, z));                  // 2: rounded (riser) edge
                verts.Add(new Vector3(+halfX, y, z));                  // 3
            }

            var tris = new List<int>((Rows - 1) * 12);
            for (int i = 0; i + 1 < Rows; i++)
            {
                int a = i * 4, c = (i + 1) * 4;
                Quad(tris, a + 0, a + 1, c + 1, c + 0);   // string face
                Quad(tris, a + 2, a + 3, c + 3, c + 2);   // rounded face
                Quad(tris, a + 0, a + 2, c + 2, c + 0);   // -X side
            }

            var mesh = new Mesh { name = "SynthBow" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Quad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        // ── plumbing ─────────────────────────────────────────────────────────
        private static void Case(List<string> failures, string label, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + label + "] threw: " + ex.Message); }
        }

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }
    }
}
