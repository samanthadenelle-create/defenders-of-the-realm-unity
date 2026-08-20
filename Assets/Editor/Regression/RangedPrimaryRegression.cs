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
//   Case 7 — the LONGEST axis still seats on +Y. Case 4's synthetic bow is authored
//            Y-long, so it cannot exercise the axis solve; this one hands the solver
//            the same mesh rotated 90 deg about X (longest extent arriving on the
//            prop's Z) and requires it to come back onto +Y. Guards WO-970, where the
//            align could only YAW and any mesh not already Y-long "stayed lying flat".
//
//   Case 8 — the HELD bow STANDS UPRIGHT (owner defect 2026-08-16: "the bow LYING
//            HORIZONTALLY across his body ... it must stand UPRIGHT ... rotated roughly
//            90 degrees about the grip point"). HeroBowAttachment parented the correctly
//            seated bow to the LeftHand bone with an IDENTITY hand-local rotation, which
//            maps the bow's +Y onto the BONE's +Y - the "out of the fist" axis, right for
//            a sword and 90 deg wrong for a bow, whose hand closes AROUND the riser.
//            Pinned against a hostile fixture (hand pitched 90 deg + rolled 53, body yawed
//            37) and it asserts the identity seat WOULD have failed, so it cannot pass
//            vacuously.
//
//   Case 9 — the COMPANION archer's bow is DERIVED TOO, on BOTH axes. Case 8 tests the
//            SOLVER; this tests the CALLER. A companion has no HeroBowAttachment, so its
//            bow is seated by EquipmentController.AttachLoadedProp, which fell to the raw
//            `Quaternion.Euler(_baseGripEuler)` with the Bow preset's gripEuler == (0,0,0)
//            - so Case 8 stayed green while the companion bow was still horizontal. Half
//            source-lint (the branch derives; ApplyGlobalWeaponYaw is withheld from it and
//            from nothing else), half geometry: the real composition is run on a hostile
//            fixture and all four clauses of the owner's rule are MEASURED OFF THE MESH.
//
//  Case 10 — the SHEATHED bow holds the SAME POSE as the drawn one (owner ruling
//            2026-08-16, verbatim: "both sheathed and drawn bow stay in this same
//            pose"). Corrects a call made that same night: a capture proving the HELD
//            seat was 0 deg off vertical was generalised to the diagonally-slung back
//            bow, a transform it never covered. Bow-only - melee keeps its OWN derived
//            sheathe carry and this case fails if the bow branch ever swallows it.
//            (The melee EXPRESSION of that carry - a diagonal baldric on the back - was
//            superseded by owner instruction on 2026-08-20; see Case10's header. The BOW
//            half of the 08-16 ruling is untouched and still binding.)
//
// ★ THE OWNER'S CANONICAL BOW RULE — HER EXACT WORDS, 2026-08-16, BINDING FOR EVERY BOW.
// Recorded verbatim (never paraphrased) because a rule without its reasoning gets "fixed"
// by the next reader — a dialed +91 Z was shipped and reverted once, and a confident
// comment then preserved the wrong conclusion for months:
//
//   "For bows, the rule would always be y is the longest distance on any two points of a
//    mesh bow. the straight edge runs parallel to the person holding it with the arm
//    crossing that straight line perpendicular, landing with the hand clasping on the
//    curved edge furthest from the person."
//
// The four clauses, and the case that asserts each:
//   1. Y IS THE LONGEST DISTANCE BETWEEN ANY TWO POINTS  -> Case 7 (a bow rotated 90 deg
//      must come back onto +Y, so the align must MEASURE rather than trust the import) and
//      Case 9's held-frame check that the greatest vertex-pair distance IS the limb span.
//   2. THE STRAIGHT EDGE RUNS PARALLEL TO THE PERSON     -> Case 8 (belly on body.forward)
//      and Case 9, which measures the string LINE's skew out of the archer's body plane.
//   3. THE ARM CROSSES THAT LINE PERPENDICULAR at mid-Y  -> Case 4 (the seat is derived on
//      the perpendicular from the straight edge's midpoint, never along the string).
//   4. THE HAND CLASPS THE CURVED EDGE FURTHEST FROM THE PERSON -> Case 4 (apex, not the
//      first surface) and Case 9, which requires the grip to sit a full bulge FARTHER
//      downrange than the string.
// Clauses 2 and 4 together ARE the belly axis. A fix that satisfies 1 and 3 but not 2 and 4
// stands the bow upright with the curve facing BACKWARD - string at the target, curve at the
// archer. It photographs as nearly right, which is why Case 9 ends with an independence probe
// proving the 180-degree yaw moves the belly WITHOUT moving the limb: an upright-only
// assertion, and any dialed Z-roll constant, cannot tell those two poses apart.
//
// ⚠ CASES 4 / 7 / 8 ARE THREE DIFFERENT FAILURES AND MUST STAY SEPARATE. Case 4 is the
// grip POSITION (where on the bow the hand sits), Case 7 is the seated AXIS, Case 8 is
// the ORIENTATION ONCE IN HAND. On 2026-08-16 the grip position measured exactly right
// (bow-grip-apex err=0m, commit 14a2c66e) while the bow still lay horizontal - so a suite
// that measured only the grip called the defect green. Never merge them, never relax one
// to make another pass, and never "fix" an orientation failure by moving the grip.
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
            Case(failures, "bow-long-axis-y", () => Case7_LongAxisSeatsOnY(failures, notes));
            Case(failures, "bow-upright-in-hand", () => Case8_BowStandsUprightInHand(failures, notes));
            Case(failures, "companion-bow-derived", () => Case9_CompanionBowSeatIsDerived(failures, notes));
            Case(failures, "sheathed-bow-matches-drawn", () => Case10_SheathedBowMatchesDrawn(failures, notes));

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "RANGED PRIMARY OK - 10/10 cases pass (no crossbow can reach the runtime weapons " +
                         "catalog while the R4a exclusion stands, the ranger basic is still a costed-" +
                         "cooldown ranged strike carrying its verb + bow icon, the ranged-basic " +
                         "discriminator still admits the ranger while rejecting the knight, the bow grip " +
                         "still seats on the ROUNDED EDGE apex rather than the straight/string edge, the " +
                         "BOW is an ACTION-BAR ability while the PRIMARY attack is the melee/dagger " +
                         "sweep, the bow slot greys out under its cooldown with no special case, the " +
                         "LONGEST axis still seats on +Y whatever axis the source mesh authored it on, " +
                         "the held bow still stands UPRIGHT in a hand whose bone axes are nowhere " +
                         "near vertical, and the COMPANION archer's bow is DERIVED through " +
                         "ComputeBowHeldRotation with the global yaw withheld, satisfying all four " +
                         "clauses of the owner's canonical bow rule measured off the mesh, and a " +
                         "SHEATHED bow holds the same world pose as the drawn one while the melee " +
                         "diagonal back-carry is untouched)" + noteStr;
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
                    notes.Add(DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                        "bow-grip-apex", "procedural mesh reported not readable"));
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

        // =====================================================================
        //  CASE 7 — the LONGEST axis seats on +Y (the premise the whole bow rule stands on)
        // =====================================================================
        //
        // DISTINCT FROM CASE 4 ON PURPOSE. Case 4 pins WHERE ON the bow the hand sits (the grip
        // POSITION — the riser apex, not the string edge). This case pins WHICH WAY the bow is
        // seated (the ORIENTATION — long axis on +Y). They are different failures with different
        // fixes and they must not be able to regress into one another: on 2026-08-16 the grip
        // position measured perfect (err=0m) while the bow still lay HORIZONTALLY in hand, and a
        // suite that only measured the grip called that state green.
        //
        // Case 4's synthetic bow is authored Y-long already, so it cannot test the axis solve —
        // AlignAxesYLongXNarrowZWide is near-identity for it. This case therefore hands the solver
        // the same mesh rotated 90 degrees about X inside the prop, so its longest extent arrives
        // on the prop's Z. The solver MUST rotate it back onto +Y. WO-970 records the exact
        // regression this guards: the align could only ever YAW, so every prop whose source mesh
        // was not already Y-long "stayed lying flat" — a Z-long mesh reading horizontal is that
        // bug, and it is one line of geometry away from the owner's reported defect.
        private const float AxisDotTolerance = 0.999f;   // ~2.6 deg off +Y

        private static void Case7_LongAxisSeatsOnY(List<string> failures, List<string> notes)
        {
            GameObject rig = null;
            try
            {
                rig = new GameObject("BowAxisProbe");
                var parent = new GameObject("Anchor").transform;
                parent.SetParent(rig.transform, false);

                // prop (empty) -> child (mesh, pre-rotated 90 about X). The child rotation makes the
                // mesh's own long axis land on the PROP's Z, so "longest -> +Y" has real work to do.
                var prop = new GameObject("SynthBowRoot");
                var child = new GameObject("SynthBowMesh", typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(prop.transform, false);
                child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var mesh = BuildSyntheticBowMesh();
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                if (!mesh.isReadable)
                {
                    notes.Add(DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                        "bow-long-axis-y", "procedural mesh reported not readable"));
                    return;
                }

                DeNelle.Core.Geometry.WeaponBoundsOrient.NormalizeInto(
                    prop, parent, SynthBowLength,
                    DeNelle.Core.Geometry.WeaponBoundsOrient.GripAnchor.BowGrip,
                    resolveBladeUpFromHilt: false);

                // The mesh's long axis is its own +Y; carry it up through the child and the prop to
                // read where the solver actually put it, in the anchor's frame.
                Vector3 longAxis = prop.transform.localRotation * child.transform.localRotation * Vector3.up;
                float dotY = Mathf.Abs(Vector3.Dot(longAxis.normalized, Vector3.up));

                notes.Add("bow-long-axis-y: longAxis=(" + longAxis.x.ToString("0.###") + "," +
                          longAxis.y.ToString("0.###") + "," + longAxis.z.ToString("0.###") +
                          ") |dot(+Y)|=" + dotY.ToString("0.####"));

                if (dotY < AxisDotTolerance)
                    failures.Add("[bow-long-axis-y] after NormalizeInto the mesh's LONGEST axis points " +
                                 longAxis.ToString("0.###") + " in the grip root's frame, |dot(+Y)|=" +
                                 dotY.ToString("0.####") + " (needs >= " + AxisDotTolerance.ToString("0.###") +
                                 "). The owner's rule is binding for EVERY bow: 'the longest piece is gonna " +
                                 "be the y axis'. A long axis on X or Z is the bow lying HORIZONTALLY - the " +
                                 "2026-08-16 defect - and it is a DIFFERENT failure from the grip POSITION " +
                                 "that Case 4 measures, which is why it is pinned separately. See WO-970: " +
                                 "the align could once only YAW, so a mesh not already Y-long stayed flat.");
            }
            finally
            {
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        // =====================================================================
        //  CASE 8 — the held bow STANDS UPRIGHT in a hand whose bone axes are not vertical
        // =====================================================================
        //
        // ROOT CAUSE THIS PINS (owner defect 2026-08-16, "the bow lies horizontally across his
        // body ... rotated roughly 90 degrees about the grip point"): HeroBowAttachment parented
        // the correctly-seated bow to the LeftHand bone with an IDENTITY hand-local rotation
        // (GripLocalEuler == 0), mapping the bow's prop-local +Y onto the BONE's own +Y. On this
        // rig that axis is "points out of the fist" - right for a sword, which continues the fist,
        // and wrong by ~90 degrees for a bow, whose hand closes AROUND the riser so the limbs run
        // PERPENDICULAR to the fist. WeaponBoundsOrient.ComputeBowHeldRotation now derives the seat
        // from the BODY's axes instead (limbs -> body.up, belly -> body.forward), the same
        // construction EquipmentController.ComputeSheathRotation uses.
        //
        // The fixture is deliberately HOSTILE: the hand bone is pitched 90 degrees (so its own +Y
        // lies along the body's forward - exactly the real defect's shape) plus a 53-degree roll,
        // and the body is yawed 37 degrees off world forward so nothing can pass by accidentally
        // agreeing with a world axis. Assertion 3 proves the fixture is doing work: the identity
        // seat MUST be badly tilted here, so this case can never pass vacuously and can never
        // quietly become a restatement of Case 4's grip-position measurement.
        private const float UprightToleranceDeg = 0.5f;
        /// <summary>The identity seat must be at least this far off vertical, or the fixture is
        /// not exercising the bug and the case would prove nothing.</summary>
        private const float FixtureMinIdentityTiltDeg = 45f;

        private static void Case8_BowStandsUprightInHand(List<string> failures, List<string> notes)
        {
            GameObject rig = null;
            try
            {
                rig = new GameObject("BowUprightProbe");
                var body = new GameObject("Body").transform;
                body.SetParent(rig.transform, false);
                body.localRotation = Quaternion.Euler(0f, 37f, 0f);      // hero facing, upright

                var hand = new GameObject("LeftHand").transform;
                hand.SetParent(body, false);
                hand.localRotation = Quaternion.Euler(90f, 0f, 53f);     // bone +Y along forward, rolled

                Quaternion handLocal =
                    DeNelle.Core.Geometry.WeaponBoundsOrient.ComputeBowHeldRotation(hand, body);

                Quaternion composed = hand.rotation * handLocal;
                Vector3 limbWorld  = composed * Vector3.up;        // prop +Y = the limb-to-limb span
                Vector3 bellyWorld = composed * Vector3.forward;   // prop +Z = the riser belly / aim

                float limbTilt     = Vector3.Angle(limbWorld, body.up);
                float bellyOff     = Vector3.Angle(bellyWorld, body.forward);
                float identityTilt = Vector3.Angle(hand.rotation * Vector3.up, body.up);

                notes.Add("bow-upright-in-hand: limbTiltFromVertical=" + limbTilt.ToString("0.##") +
                          "deg bellyOffAim=" + bellyOff.ToString("0.##") +
                          "deg identitySeatWouldTilt=" + identityTilt.ToString("0.##") + "deg");

                if (identityTilt < FixtureMinIdentityTiltDeg)
                    failures.Add("[bow-upright-in-hand] FIXTURE BROKEN: the identity seat is only " +
                                 identityTilt.ToString("0.##") + "deg off vertical (needs >= " +
                                 FixtureMinIdentityTiltDeg.ToString("0.#") + "deg). The hand bone must be " +
                                 "pitched away from vertical or this case passes without exercising the " +
                                 "bug, and it would silently degrade into a restatement of Case 4.");

                if (limbTilt > UprightToleranceDeg)
                    failures.Add("[bow-upright-in-hand] the held bow's LONG axis sits " +
                                 limbTilt.ToString("0.##") + "deg off the body's vertical (allowed " +
                                 UprightToleranceDeg.ToString("0.##") + "deg). That is the owner's " +
                                 "2026-08-16 defect: 'the bow LYING HORIZONTALLY across his body ... it " +
                                 "must stand UPRIGHT'. ~" + identityTilt.ToString("0.#") + "deg means the " +
                                 "hand-local seat went back to IDENTITY, which maps the limb span onto the " +
                                 "hand BONE's +Y (the fist axis - correct for a sword, 90deg wrong for a " +
                                 "bow). This is NOT the grip-position bug; do not 'fix' it by moving the " +
                                 "grip, and do not weaken Case 4's bow-grip-apex to compensate.");

                if (bellyOff > UprightToleranceDeg)
                    failures.Add("[bow-upright-in-hand] the bow's BELLY (prop +Z, the riser face the grip " +
                                 "apex sits on - the side AWAY from the string) points " +
                                 bellyOff.ToString("0.##") + "deg off the body's forward (allowed " +
                                 UprightToleranceDeg.ToString("0.##") + "deg). The curved limbs must open " +
                                 "AWAY from the target, so the belly faces the aim. ~180deg here means a " +
                                 "global yaw was composed onto the derived seat - ApplyGlobalWeaponYaw " +
                                 "corrects grips that INHERITED raw bone axes and must NOT be applied on " +
                                 "top of this derivation.");
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

        // =====================================================================
        //  CASE 9 — the COMPANION bow seat is DERIVED, on BOTH axes
        // =====================================================================
        //
        // WHAT THIS PINS THAT CASE 8 DOES NOT. Case 8 exercises the SOLVER
        // (WeaponBoundsOrient.ComputeBowHeldRotation) in isolation. It stayed green all the while
        // the COMPANION archer's bow was still horizontal, because the companion bow never goes
        // through HeroBowAttachment: a companion has no HeroBowAttachment, so DeferBowToBowAttachment
        // is false and its bow is seated by EquipmentController.AttachLoadedProp, which fell to the
        // raw `Quaternion.Euler(_baseGripEuler)` with the Bow preset's gripEuler == (0,0,0). A solver
        // that is correct and a caller that does not call it read identically in a suite that only
        // tests the solver. This case tests the CALLER and the COMPOSITION.
        //
        // TWO AXES, MEASURED OFF THE MESH — not off a quaternion convention. The owner's archer
        // reference has the STRING as the straight edge NEAREST the body and the limbs curving AWAY
        // toward the target, so the seat owes two answers:
        //   * the LIMB line (nock to nock) stands along the body's UP, and
        //   * the BELLY (the bulged riser face the grip apex sits on, opposite the string) faces the
        //     body's FORWARD.
        // Both are read from the synthetic bow's actual vertices after the real composition: the limb
        // line is the world segment between the two most separated vertices (which on this mesh are
        // exactly the two nock tips), and the belly direction is the world vector from the mesh
        // centroid to the seated grip origin, since GripAnchor.BowGrip seats the grip ON the bulge
        // apex while the centroid sits between apex and string. Neither number can be satisfied by
        // agreeing with an axis naming convention.
        //
        // WHY BOTH, STATED AS THE FAILURE IT CATCHES: a dialed Z-roll constant - the tempting
        // (0,0,-90) that was weighed and rejected - stands the bow upright while leaving the belly
        // free to face BACKWARD (string downrange, curve at the archer). That reads as nearly right
        // in a screenshot. Assertion (d) below PROVES the axes are independent by composing the
        // 180-degree global yaw onto the derived seat and requiring that it leaves the limb axis
        // untouched while flipping the belly - i.e. a limb-only assertion would have passed the
        // broken pose. That is also why ApplyGlobalWeaponYaw must NOT be composed on this path, and
        // assertion (b) pins the code that withholds it.
        private const string EquipmentControllerPath =
            "Assets/_Modules/Village/Hero/EquipmentController.cs";

        /// <summary>Angular tolerance (deg) on the measured axes. The wrong answers are 90 deg
        /// (horizontal) and 180 deg (belly reversed) away, so this can never pass by luck.
        /// <para>
        /// ⛔ DO NOT WIDEN THIS. It has been attacked once already and the bound was NOT the problem.
        /// On its first gate run this case failed at 1.15 deg against this 1.0, and 1.15 is not a
        /// near-miss that wants slack - it is exactly atan(SynthBowThick / SynthBowLength), the tilt
        /// of the corner-to-corner vertex pair the old estimator was picking as the limb line. The
        /// ESTIMATOR was fixed (see DominantAxis); the bound stayed. If this case goes
        /// red again, the residual is telling you something true - measure it, do not raise this.
        /// </para></summary>
        private const float CompanionBowToleranceDeg = 1.0f;

        /// <summary>The yaw-composed belly must be at least this far off the aim, or assertion (d)
        /// is not proving that the two axes are independent.</summary>
        private const float YawFlipsBellyMinDeg = 150f;

        private static void Case9_CompanionBowSeatIsDerived(List<string> failures, List<string> notes)
        {
            // ── (a)+(b) SOURCE: the caller derives, and withholds the global yaw ─────────────────
            string raw = ReadText(EquipmentControllerPath);
            if (raw == null)
            {
                failures.Add("[companion-bow-derived] cannot read " + EquipmentControllerPath +
                             " - the companion bow's seat is composed there, so without it this case " +
                             "cannot prove the path derives rather than falling back to a raw euler.");
                return;
            }
            // Strip comments AND string literals first: this file DOCUMENTS the defect at length, so
            // an unstripped match would be satisfied by prose describing the bug it is meant to catch.
            string src = StripCommentsAndStrings(raw);

            if (src.IndexOf("ComputeBowHeldRotation", StringComparison.Ordinal) < 0)
                failures.Add("[companion-bow-derived] EquipmentController contains no live call to " +
                             "ComputeBowHeldRotation (comments and string literals stripped). The " +
                             "companion/non-ranger bow branch has reverted to a raw euler seat - the " +
                             "2026-08-16 defect, where gripEuler (0,0,0) maps the limb span onto the " +
                             "hand BONE's +Y (the fist axis) and the bow lies HORIZONTALLY across the " +
                             "body. The fix is the DERIVATION, never a dialed constant: a constant can " +
                             "stand the bow up while leaving the belly facing backwards, which this " +
                             "suite's assertion (c) exists to reject.");

            if (src.IndexOf("WeaponClass.Bow", StringComparison.Ordinal) < 0)
                failures.Add("[companion-bow-derived] EquipmentController no longer branches on " +
                             "WeaponClass.Bow. The bow seat must be scoped to BOWS - the shield and " +
                             "every melee family keep their own felt-approved seats untouched.");

            int yawIdx = src.IndexOf("ApplyGlobalWeaponYaw(_baseGripRot)", StringComparison.Ordinal);
            if (yawIdx < 0)
                failures.Add("[companion-bow-derived] the main-hand global-yaw line " +
                             "(ApplyGlobalWeaponYaw(_baseGripRot)) is gone entirely. It must SURVIVE " +
                             "for every raw-euler seat - removing it changes every melee family's " +
                             "look - and be withheld ONLY from the derived bow.");
            else
            {
                // The guard must sit immediately before the yaw, so the yaw is skipped for the
                // derived bow and applied to everything else.
                int from = Math.Max(0, yawIdx - 160);
                string window = src.Substring(from, yawIdx - from);
                if (window.IndexOf("bowDerivedSeat", StringComparison.Ordinal) < 0)
                    failures.Add("[companion-bow-derived] ApplyGlobalWeaponYaw(_baseGripRot) is not " +
                                 "guarded by bowDerivedSeat. The 180-degree yaw corrects grips that " +
                                 "INHERITED the raw bone axes; composed onto a world-derived bow seat " +
                                 "it swings the BELLY to face backward - string toward the target, " +
                                 "curve toward the archer - which is upright and still wrong. Precedent: " +
                                 "the derived ComputeSheathRotation result is likewise consumed without " +
                                 "the yaw, and HeroBowAttachment drops it for this same reason.");
            }

            // ── (c)+(d) GEOMETRY: run the real composition and measure the mesh ─────────────────
            GameObject rig = null;
            try
            {
                rig = new GameObject("CompanionBowProbe");
                var body = new GameObject("Body").transform;
                body.SetParent(rig.transform, false);
                body.localRotation = Quaternion.Euler(0f, 37f, 0f);      // companion facing, upright

                var hand = new GameObject("LeftHand").transform;
                hand.SetParent(body, false);
                hand.localRotation = Quaternion.Euler(90f, 0f, 53f);     // bone +Y along forward, rolled

                // ORDER MATTERS AND IT MIRRORS THE RUNTIME. AttachLoadedProp creates the grip root
                // UNPARENTED, runs NormalizeInto on it, and only then SetParent(hand, false) -
                // HeroBowAttachment does the same. That is not incidental: TryLocalBounds measures
                // Renderer.bounds, a WORLD AABB, so normalizing under an already-rotated bone would
                // hand the axis solve an inflated box and could pick the wrong longest axis. A
                // fixture that parented first would be testing a path the game does not run.
                var mesh = BuildSyntheticBowMesh();
                if (!mesh.isReadable)
                {
                    notes.Add(DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                        "companion-bow-derived (geometry half)", "procedural mesh not readable"));
                    return;
                }

                // Created UNDER the probe root so nothing leaks into the scene if an assert throws;
                // NormalizeInto is still fed an unrotated parent, which is what the ordering above
                // is about (the probe root itself carries no rotation).
                var gripRoot = new GameObject("WeaponProp").transform;
                gripRoot.SetParent(rig.transform, false);

                var prop = new GameObject("SynthBow", typeof(MeshFilter), typeof(MeshRenderer));
                prop.GetComponent<MeshFilter>().sharedMesh = mesh;

                DeNelle.Core.Geometry.WeaponBoundsOrient.NormalizeInto(
                    prop, gripRoot, SynthBowLength,
                    DeNelle.Core.Geometry.WeaponBoundsOrient.GripAnchor.BowGrip,
                    resolveBladeUpFromHilt: false);

                gripRoot.SetParent(hand, false);   // ...and only now onto the bone, as the game does.

                // THE COMPOSITION UNDER TEST - byte-for-byte what AttachLoadedProp's bow branch does:
                // derived hand-local seat, then the authored gripEuler as a NUDGE on top, and NO
                // global yaw. The Bow preset's gripEuler is (0,0,0); it is written out rather than
                // dropped so that dialing it would move this measurement too.
                Vector3 presetGripEuler = Vector3.zero;
                Quaternion derived =
                    DeNelle.Core.Geometry.WeaponBoundsOrient.ComputeBowHeldRotation(hand, body);
                gripRoot.localPosition = Vector3.zero;
                gripRoot.localRotation = derived * Quaternion.Euler(presetGripEuler);

                // ── LIMB LINE, measured off the mesh ────────────────────────────────────────────
                // ⚠ THE ESTIMATOR IS THE SUBTLE PART. READ THIS BEFORE TOUCHING THE TOLERANCE.
                // The first version of this case took the single most-separated VERTEX PAIR as the
                // limb line. That is biased, and provably so: the two nock tips are 1.0 m apart on Y
                // but the stave is SynthBowThick (0.02 m) wide on X, so the winning pair is the
                // CORNER-TO-CORNER diagonal (-halfX,-L/2) -> (+halfX,+L/2), not the two tip centres.
                // Its tilt off true +Y is a closed form:
                //     atan(SynthBowThick / SynthBowLength) = atan(0.02 / 1.0) = 1.1458 deg
                // and the case duly failed at "1.15deg off vertical" against a 1.0 deg bound. That
                // 1.15 was the FIXTURE'S OWN MEASUREMENT ERROR, not the seat's: the residual is a
                // property of which two vertices get picked, it is invariant under any rigid rotation
                // of the rig, and it would not shrink by one thousandth of a degree if the derivation
                // were made perfect. Widening the bound to 2 deg would have "fixed" it by hiding a
                // real 1.15 deg of blindness in the very assertion that guards the owner's rule.
                //
                // So the ESTIMATOR is fixed instead, not the bound. The extreme pair is kept only as
                // a SEED; the limb line is then the cloud's PRINCIPAL AXIS (see DominantAxis), which
                // weighs every vertex correctly and returns the true axis - exactly (0,1,0) in the
                // prop frame - for any stave thickness. The tolerance stays at 1 deg.
                Vector3[] verts = mesh.vertices;
                var world = new Vector3[verts.Length];
                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < verts.Length; i++)
                {
                    world[i] = prop.transform.TransformPoint(verts[i]);
                    centroid += world[i];
                }
                centroid /= Mathf.Max(1, verts.Length);

                float best = -1f; int ia = 0, ib = 0;
                for (int i = 0; i < world.Length; i++)
                    for (int j = i + 1; j < world.Length; j++)
                    {
                        float d = (world[i] - world[j]).sqrMagnitude;
                        if (d > best) { best = d; ia = i; ib = j; }
                    }
                Vector3 limbSeed  = (world[ib] - world[ia]).normalized;   // biased by ~1.15 deg
                Vector3 limbWorld = DominantAxis(world, limbSeed);        // unbiased

                // BELLY, measured off the mesh: GripAnchor.BowGrip seats the grip ON the bulge apex
                // while the centroid lies between apex and string, so centroid -> grip points along
                // the belly. Projected perpendicular to the limb line so a residual along-limb
                // component cannot flatter the angle.
                Vector3 bellyWorld = gripRoot.position - centroid;
                bellyWorld -= Vector3.Dot(bellyWorld, limbWorld) * limbWorld;
                float bellyLen = bellyWorld.magnitude;
                bellyWorld = bellyWorld.normalized;

                // The limb LINE is undirected (either nock may be "first"), so fold the angle.
                float limbTilt = Vector3.Angle(limbWorld, body.up);
                if (limbTilt > 90f) limbTilt = 180f - limbTilt;
                float bellyOff = Vector3.Angle(bellyWorld, body.forward);
                float identityTilt = Vector3.Angle(hand.rotation * Vector3.up, body.up);

                // The seed's own tilt, reported so the estimator PROVES it is doing work rather than
                // being trusted. Expected ~1.15 deg = atan(SynthBowThick / SynthBowLength). If this
                // ever prints ~0 the fixture stopped having a cross-section and the fit is no longer
                // being exercised; if the fitted limbTilt ever creeps toward this value, the fit has
                // been removed and the bound must NOT be widened to accommodate it.
                float seedTilt = Vector3.Angle(limbSeed, body.up);
                if (seedTilt > 90f) seedTilt = 180f - seedTilt;
                float seedBiasClosedForm =
                    Mathf.Atan2(SynthBowThick, SynthBowLength) * Mathf.Rad2Deg;

                // ── CLAUSE 1, in the HELD pose: "y is the longest distance on any two points of a
                // mesh bow". `best` IS that greatest-distance-between-any-two-points, measured over
                // the vertices after the solve - the owner's definition applied literally, not the
                // AABB extent the solver reads. It must equal the authored limb span AND it must be
                // the axis that ended up vertical (asserted just below), which is the two halves of
                // her first clause: Y is the longest distance, and that is the axis that stands up.
                float longestSpan = Mathf.Sqrt(best);

                // ── CLAUSES 2 + 4, stated the way SHE states them - as distances FROM THE PERSON.
                // The straight (string) edge is the fixture's z==0 rows. It must end up NEARER the
                // archer than the grip, which sits on "the curved edge furthest from the person".
                var stringWorld = new List<Vector3>(verts.Length);
                Vector3 stringCentroid = Vector3.zero;
                for (int i = 0; i < verts.Length; i++)
                    if (Mathf.Abs(verts[i].z) < 1e-5f) { stringWorld.Add(world[i]); stringCentroid += world[i]; }
                int stringCount = stringWorld.Count;
                float stringDepth = 0f, gripDepth = 0f, straightEdgeSkew = 0f;
                if (stringCount > 0)
                {
                    stringCentroid /= stringCount;
                    // Depth = distance from the person, measured along the body's forward.
                    stringDepth = Vector3.Dot(stringCentroid - body.position, body.forward);
                    gripDepth = Vector3.Dot(gripRoot.position - body.position, body.forward);
                    // "the straight edge runs parallel to the person holding it": the string LINE
                    // must lie in the archer's body plane, i.e. carry no component along forward.
                    // Same principal-axis estimator as the limb line, for the same reason - the
                    // string edge is also SynthBowThick wide on X, so picking single extreme
                    // vertices would tilt this line by the identical atan(0.02/1.0) = 1.15 deg.
                    Vector3 stringLine = DominantAxis(stringWorld.ToArray(), limbWorld);
                    straightEdgeSkew = Mathf.Abs(90f - Vector3.Angle(stringLine, body.forward));
                }

                notes.Add("companion-bow-derived: limbTiltFromVertical=" + limbTilt.ToString("0.##") +
                          "deg bellyOffAim=" + bellyOff.ToString("0.##") + "deg bellyArm=" +
                          bellyLen.ToString("0.###") + "m longestSpan=" + longestSpan.ToString("0.###") +
                          "m stringDepth=" + stringDepth.ToString("0.###") + "m gripDepth=" +
                          gripDepth.ToString("0.###") + "m straightEdgeSkew=" +
                          straightEdgeSkew.ToString("0.##") + "deg identitySeatWouldTilt=" +
                          identityTilt.ToString("0.##") + "deg extremePairSeedTilt=" +
                          seedTilt.ToString("0.###") + "deg (closed form " +
                          seedBiasClosedForm.ToString("0.###") + "deg - the fixture bias the " +
                          "end-centroid refinement removes; NOT a seat error)");

                // CLAUSE 1 - "y is the longest distance on any two points of a mesh bow".
                if (Mathf.Abs(longestSpan - SynthBowLength) > 0.01f)
                    failures.Add("[companion-bow-derived] clause 1 (owner, 2026-08-16: 'y is the " +
                                 "longest distance on any two points of a mesh bow'): the greatest " +
                                 "distance between any two vertices measured " + longestSpan.ToString("0.####") +
                                 "m, expected the " + SynthBowLength.ToString("0.##") + "m limb span. " +
                                 "The solve either rescaled the bow or seated the length on a different " +
                                 "pair of points than the two nocks - see also bow-long-axis-y, which " +
                                 "pins that the align MEASURES the longest axis rather than trusting " +
                                 "whatever axis the FBX authored it on.");

                if (stringCount == 0)
                    failures.Add("[companion-bow-derived] FIXTURE BROKEN: no straight-edge (string) " +
                                 "vertices found, so clauses 2 and 4 - the ones that decide which way " +
                                 "the curve faces - cannot be measured at all.");
                else
                {
                    // CLAUSE 2 - "the straight edge runs parallel to the person holding it".
                    if (straightEdgeSkew > CompanionBowToleranceDeg)
                        failures.Add("[companion-bow-derived] clause 2 (owner: 'the straight edge runs " +
                                     "parallel to the person holding it'): the string line sits " +
                                     straightEdgeSkew.ToString("0.##") + "deg out of the archer's body " +
                                     "plane (allowed " + CompanionBowToleranceDeg.ToString("0.##") +
                                     "deg). The string must run along the person, not out toward the " +
                                     "target.");

                    // CLAUSE 4 - "the hand clasping on the curved edge furthest from the person".
                    // The grip must be FARTHER downrange than the string, by the full bulge depth.
                    float clasp = gripDepth - stringDepth;
                    if (clasp <= 0f || clasp < SynthBowBulge * 0.5f)
                        failures.Add("[companion-bow-derived] clause 4 (owner: 'landing with the hand " +
                                     "clasping on the curved edge furthest from the person'): the grip " +
                                     "sits only " + clasp.ToString("0.####") + "m farther from the archer " +
                                     "than the string (expected about the full bulge, " +
                                     SynthBowBulge.ToString("0.##") + "m). A value at or below zero is " +
                                     "the bow held BACKWARD - the hand on the string side, the curve " +
                                     "toward the archer. That is the pose a dialed Z-roll constant " +
                                     "produces while still standing the bow perfectly upright, and it is " +
                                     "the whole reason clause 2 and clause 4 are asserted separately " +
                                     "from the limb axis.");
                }

                if (bellyLen < 0.02f)
                    failures.Add("[companion-bow-derived] FIXTURE BROKEN: the measured belly arm is " +
                                 bellyLen.ToString("0.####") + "m, too short to give a meaningful " +
                                 "direction. The grip is supposed to sit on the bulge APEX while the " +
                                 "centroid sits between apex and string; a near-zero arm means the " +
                                 "BowGrip seat moved (see case bow-grip-apex) and this case is no " +
                                 "longer measuring the belly at all.");

                // The refinement must be EXERCISED, not merely present: the seed it corrects has to
                // carry the bias the closed form predicts. Tracks the fixture automatically - change
                // SynthBowThick and both sides move together.
                if (Mathf.Abs(seedTilt - seedBiasClosedForm) > 0.05f)
                    failures.Add("[companion-bow-derived] FIXTURE DRIFT: the extreme-pair seed tilts " +
                                 seedTilt.ToString("0.###") + "deg off vertical but the closed form " +
                                 "atan(SynthBowThick/SynthBowLength) says " +
                                 seedBiasClosedForm.ToString("0.###") + "deg. Either the synthetic bow " +
                                 "changed shape or the seed is no longer the corner-to-corner pair, " +
                                 "and in both cases the end-centroid refinement is no longer being " +
                                 "exercised - so a real limb-axis error could now hide inside a " +
                                 "measurement artefact nobody is watching.");

                if (identityTilt < FixtureMinIdentityTiltDeg)
                    failures.Add("[companion-bow-derived] FIXTURE BROKEN: the identity seat is only " +
                                 identityTilt.ToString("0.##") + "deg off vertical. The hand bone must " +
                                 "be pitched away from vertical or a raw-euler seat would pass this " +
                                 "case, which is precisely the regression it exists to catch.");

                if (limbTilt > CompanionBowToleranceDeg)
                    failures.Add("[companion-bow-derived] the COMPANION's seated bow has its limb line " +
                                 limbTilt.ToString("0.##") + "deg off the body's vertical (allowed " +
                                 CompanionBowToleranceDeg.ToString("0.##") + "deg). ~" +
                                 identityTilt.ToString("0.#") + "deg means EquipmentController's bow " +
                                 "branch went back to Quaternion.Euler(_baseGripEuler) - the horizontal " +
                                 "companion bow. Fix the branch to derive; do NOT dial gripEuler.");

                if (bellyOff > CompanionBowToleranceDeg)
                    failures.Add("[companion-bow-derived] the COMPANION's seated bow has its BELLY " +
                                 "(the bulged riser face opposite the string) " + bellyOff.ToString("0.##") +
                                 "deg off the body's forward (allowed " +
                                 CompanionBowToleranceDeg.ToString("0.##") + "deg). The archer holds the " +
                                 "STRING nearest the body with the limbs curving AWAY toward the target, " +
                                 "so the belly faces downrange. ~180deg means a global yaw was composed " +
                                 "onto the derived seat, which is upright and still wrong.");

                // (d) PROVE THE TWO AXES ARE INDEPENDENT - and with it, that the yaw decision matters.
                Quaternion yawed = gripRoot.localRotation * Quaternion.Euler(0f, 180f, 0f);
                Quaternion composedYawed = hand.rotation * yawed;
                Quaternion composedTrue  = hand.rotation * gripRoot.localRotation;
                float yawedLimbTilt = Vector3.Angle(composedYawed * Vector3.up, body.up);
                if (yawedLimbTilt > 90f) yawedLimbTilt = 180f - yawedLimbTilt;
                float yawedBellyOff = Vector3.Angle(composedYawed * Vector3.forward,
                                                    composedTrue * Vector3.forward);

                notes.Add("companion-bow-derived (yaw probe): yawedLimbTilt=" +
                          yawedLimbTilt.ToString("0.##") + "deg bellyMovedBy=" +
                          yawedBellyOff.ToString("0.##") + "deg");

                if (yawedLimbTilt > CompanionBowToleranceDeg || yawedBellyOff < YawFlipsBellyMinDeg)
                    failures.Add("[companion-bow-derived] the independence probe did not behave: " +
                                 "composing ApplyGlobalWeaponYaw's 180 degrees onto the derived seat " +
                                 "moved the limb by " + yawedLimbTilt.ToString("0.##") + "deg and the " +
                                 "belly by " + yawedBellyOff.ToString("0.##") + "deg (expected ~0 and " +
                                 ">=" + YawFlipsBellyMinDeg.ToString("0.#") + "). This probe is what " +
                                 "makes the two-axis requirement non-negotiable: the yaw leaves the bow " +
                                 "UPRIGHT while reversing the belly, so an upright-only assertion (and " +
                                 "any dialed Z-roll constant) cannot tell the correct seat from the " +
                                 "one with the string pointed at the target.");
            }
            finally
            {
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        /// <summary>
        /// The UNBIASED long axis of a vertex cloud: the principal axis (dominant eigenvector of the
        /// covariance), found by power iteration from an approximate <paramref name="seed"/>.
        /// Direction is unsigned - callers must fold the angle.
        /// <para>
        /// WHY THIS EXISTS, so nobody "simplifies" it back. The first version of the companion case
        /// took the most-separated VERTEX PAIR as the limb line. On any prop with a real
        /// cross-section that pair is the CORNER-TO-CORNER diagonal, not the axis: on the synthetic
        /// bow (1.0 m on Y, 0.02 m on X) it sits atan(0.02 / 1.0) = 1.1458 deg off true, and the case
        /// duly failed its gate at "1.15deg off vertical" against a 1 deg bound. That residual was
        /// the FIXTURE'S MEASUREMENT ERROR, not the seat's - it is a property of which two vertices
        /// win, invariant under rigid rotation, and unchanged by a perfect derivation. Widening the
        /// bound would have buried 1.15 deg of blindness inside the assertion guarding the rule.
        /// </para>
        /// <para>
        /// The second attempt averaged the centroids of the two END BANDS. Better (~0.24 deg) but
        /// still wrong, and instructively so: the band edge is anchored to `hi`, which is itself set
        /// by a +X corner vertex, so a row could straddle the edge with only its +X side admitted.
        /// It converged to a NON-ZERO fixed point rather than to the truth. A principal-axis fit has
        /// no band, no edge and no tie to break: it uses every vertex with its correct weight, and
        /// on a cloud symmetric about its own long axis it returns that axis exactly, for any
        /// thickness. That is why this is the third and final estimator.
        /// </para>
        /// </summary>
        private static Vector3 DominantAxis(Vector3[] world, Vector3 seed)
        {
            if (world == null || world.Length == 0 || seed.sqrMagnitude < 1e-12f) return seed;

            Vector3 c = Vector3.zero;
            for (int i = 0; i < world.Length; i++) c += world[i];
            c /= world.Length;

            // Symmetric covariance, six independent terms.
            float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
            for (int i = 0; i < world.Length; i++)
            {
                Vector3 d = world[i] - c;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }

            // Power iteration. The seed is already within ~1 deg, so this converges immediately;
            // the loop is generous because it is a few dozen float ops on a 164-vertex fixture.
            Vector3 v = seed.normalized;
            for (int iter = 0; iter < 32; iter++)
            {
                var n = new Vector3(
                    xx * v.x + xy * v.y + xz * v.z,
                    xy * v.x + yy * v.y + yz * v.z,
                    xz * v.x + yz * v.y + zz * v.z);
                if (n.sqrMagnitude < 1e-20f) return seed;
                v = n.normalized;
            }
            return v;
        }

        // =====================================================================
        //  CASE 10 — the SHEATHED bow holds the SAME POSE as the drawn one
        // =====================================================================
        //
        // THE RULING, verbatim (owner, 2026-08-16): "both sheathed and drawn bow stay in this same
        // pose". THE MISTAKE IT CORRECTS, recorded because it is the reason this case exists: a
        // capture showed the HELD bow at limbTiltFromVertical=0 and the hero's diagonally-slung back
        // bow was then reported as correct - by generalising a measurement of the DRAWN transform to
        // the SHEATHED one, which it never covered. A trace that proves one state proves one state.
        //
        // Melee keeps its OWN derived sheathe carry (ComputeSheathRotation); assertion (a) fails if
        // the bow branch ever swallows it. Only WeaponClass.Bow is diverted, and the diversion is not
        // a second solve: ComputeBowHeldRotation builds its target in WORLD from the body's axes and
        // merely EXPRESSES it in whatever anchor it is handed, so feeding it the SHEATHE SOCKET yields
        // the identical world orientation the hand does. Assertion (c) measures exactly that identity,
        // on a fixture where the socket and the hand are rotated nowhere near each other - which is the
        // ruling itself, and the assertion that stops the two paths drifting apart again.
        //
        // ⚠ THE MELEE HALF OF THIS CASE WAS SUPERSEDED ON 2026-08-20 — BY OWNER INSTRUCTION, NOT BY
        // A CLI DECISION. Her words: "sheathed should sit inverted with the longest mesh (y) up and
        // down attached to hip bone". So the sheathe ANCHOR moved from the chest/spine bone to two
        // per-slot HIP sockets, and the melee EXPRESSION moved from the diagonal baldric back-carry
        // to a vertical, inverted hip hang (_sheatheBladeDiagonalDeg 28 -> 0). The sentences that
        // stood here — "a baldric carry, blade up the spine and leaning toward the off shoulder …
        // right and felt-approved … DELIBERATELY LEFT ALONE" — described a pose the owner has since
        // replaced, so this case can no longer assert it without asserting a retired ruling. What it
        // asserts INSTEAD is the part of the 08-16 ruling that survives: melee still owns a derived
        // carry of its own, and the BOW still matches its drawn pose. The new melee contract (hips,
        // vertical, inverted) is owned by SheathePoseRegression (marker SHEATHE_POSE_OK) — asserted
        // there, not weakened away here.
        //
        // ⚠ AND BOTH SOURCE ASSERTIONS WERE PINNED TO A LOCAL VARIABLE'S NAME. They matched the
        // literal text "ComputeSheathRotation(back)" and "ComputeBowHeldRotation(back," — so when the
        // single shared `back` socket became `sheatheMain`/`sheatheOff`, both went red while the code
        // they guard was still doing exactly what they demand: the bow branch still calls
        // ComputeBowHeldRotation with the sheathe socket, and melee still falls to its own carry. A
        // guard pinned to an identifier cries on a rename and stays silent on a real repeal. They are
        // pinned to the CALL SHAPE and the ANCHOR IDENTITY now: inside ApplyHoldPose's bow ternary,
        // the bow and the melee arms must be handed the SAME anchor, and it must not be the hand.
        private const float SheathedDrawnAgreementDeg = 0.5f;
        /// <summary>The socket and hand must be at least this far apart, or "they agree" is trivial.</summary>
        private const float FixtureMinAnchorSpreadDeg = 45f;

        private static void Case10_SheathedBowMatchesDrawn(List<string> failures, List<string> notes)
        {
            // ── (a)+(b) SOURCE: bow-scoped, socket-anchored, melee carry untouched ──────────────
            string raw = ReadText(EquipmentControllerPath);
            if (raw == null)
            {
                failures.Add("[sheathed-bow-matches-drawn] cannot read " + EquipmentControllerPath +
                             " - the sheathed seat is composed there.");
                return;
            }
            string src = StripCommentsAndStrings(raw).Replace(" ", string.Empty)
                                                     .Replace("\t", string.Empty)
                                                     .Replace("\r", string.Empty)
                                                     .Replace("\n", string.Empty);

            // The ternary is read out of ApplyHoldPose itself - the LIVE sheathe path. Scoping it to
            // that method matters: the Seating-Editor preview carries the same shape, and a check
            // that accepted either could pass on the preview while the pose the player sees was
            // gutted. (The preview is guarded by its own suite's parity rule, not by this one.)
            string holdPose = MethodBody(src, "voidApplyHoldPose(");
            if (holdPose == null)
            {
                failures.Add("[sheathed-bow-matches-drawn] ApplyHoldPose was not found in " +
                             EquipmentControllerPath + " - the live sheathe path cannot be inspected, " +
                             "so neither half of the owner's bow ruling can be confirmed.");
            }
            else
            {
                string bowArm = BowTernary(holdPose);
                if (bowArm == null)
                {
                    failures.Add("[sheathed-bow-matches-drawn] ApplyHoldPose no longer branches on " +
                                 "_currentWeaponKind == WeaponClass.Bow with a '?' - the bow exception " +
                                 "is gone from the live sheathe path, so a slung bow now takes whatever " +
                                 "carry melee takes. Owner ruling 2026-08-16: 'both sheathed and drawn " +
                                 "bow stay in this same pose'.");
                }
                else
                {
                    string bowAnchor = FirstArgOf(bowArm, "ComputeBowHeldRotation(");
                    string meleeAnchor = FirstArgOf(bowArm, ":ComputeSheathRotation(");

                    if (meleeAnchor == null)
                        failures.Add("[sheathed-bow-matches-drawn] the bow branch's ELSE arm no longer " +
                                     "calls ComputeSheathRotation. The bow ruling must NOT take their own " +
                                     "derived sheathe carry away from swords, axes, hammers and staves - " +
                                     "the bow is the EXCEPTION on that expression, not a replacement for " +
                                     "it. (The melee carry's SHAPE - hips, vertical, inverted since the " +
                                     "owner's 2026-08-20 instruction - is asserted by SheathePoseRegression; " +
                                     "what is asserted HERE is only that melee still has one of its own.)");

                    if (bowAnchor == null)
                        failures.Add("[sheathed-bow-matches-drawn] no live call to ComputeBowHeldRotation " +
                                     "inside ApplyHoldPose's bow branch (comments and string literals " +
                                     "stripped). The sheathed bow has reverted to the melee carry, so it no " +
                                     "longer holds the same pose as the drawn bow - owner ruling 2026-08-16.");
                    else if (IsHandAnchor(bowAnchor))
                        failures.Add("[sheathed-bow-matches-drawn] ComputeBowHeldRotation is being handed '" +
                                     bowAnchor + "' - a HAND, not the sheathe socket. The anchor is the frame " +
                                     "the world target is EXPRESSED in; passing the hand seats the sheathed " +
                                     "prop in the hand's frame, which is a different bug wearing the same shape.");
                    else if (meleeAnchor != null && bowAnchor != meleeAnchor)
                        failures.Add("[sheathed-bow-matches-drawn] the bow arm anchors at '" + bowAnchor +
                                     "' while the melee arm anchors at '" + meleeAnchor + "'. Both arms of " +
                                     "ONE ternary seat ONE prop, so two different anchors means one of them " +
                                     "is not the socket the prop is actually parented to - the bow would hold " +
                                     "a pose measured against a transform it does not hang from.");
                }
            }

            if (src.IndexOf("_currentWeaponKind==WeaponClass.Bow", StringComparison.Ordinal) < 0)
                failures.Add("[sheathed-bow-matches-drawn] the sheathe path no longer gates on " +
                             "_currentWeaponKind == WeaponClass.Bow. Without that gate the change is " +
                             "either dead (no bow reaches it) or it has escaped its fence and is " +
                             "standing every melee weapon upright on the back.");

            // ── (c) GEOMETRY: the two anchors must produce the SAME world pose ──────────────────
            GameObject rig = null;
            try
            {
                rig = new GameObject("SheathedBowProbe");
                var body = new GameObject("Body").transform;
                body.SetParent(rig.transform, false);
                body.localRotation = Quaternion.Euler(0f, 37f, 0f);

                var hand = new GameObject("LeftHand").transform;
                hand.SetParent(body, false);
                hand.localRotation = Quaternion.Euler(90f, 0f, 53f);

                // The sheathe socket hangs off the HIPS (it hung off the chest until the owner's
                // 2026-08-20 instruction; the fixture angles below are unchanged, because the point
                // was never WHICH bone - it is that the socket is rotated nowhere near the hand. If
                // the derivation were anchor-dependent these two would disagree, and assertion (d)
                // proves they are far enough apart for that to bite.)
                var socket = new GameObject("SheatheSocket_HipMain").transform;
                socket.SetParent(body, false);
                socket.localRotation = Quaternion.Euler(-18f, 164f, 25f);
                socket.localPosition = new Vector3(-0.10f, 0.12f, -0.15f);

                Quaternion drawnLocal =
                    DeNelle.Core.Geometry.WeaponBoundsOrient.ComputeBowHeldRotation(hand, body);
                Quaternion sheathedLocal =
                    DeNelle.Core.Geometry.WeaponBoundsOrient.ComputeBowHeldRotation(socket, body);

                Quaternion drawnWorld    = hand.rotation * drawnLocal;
                Quaternion sheathedWorld = socket.rotation * sheathedLocal;

                float anchorSpread = Quaternion.Angle(hand.rotation, socket.rotation);
                float disagreement = Quaternion.Angle(drawnWorld, sheathedWorld);

                Vector3 limbWorld  = sheathedWorld * Vector3.up;
                Vector3 bellyWorld = sheathedWorld * Vector3.forward;
                float limbTilt = Vector3.Angle(limbWorld, body.up);
                float bellyOff = Vector3.Angle(bellyWorld, body.forward);

                notes.Add("sheathed-bow-matches-drawn: anchorSpread=" + anchorSpread.ToString("0.##") +
                          "deg drawnVsSheathed=" + disagreement.ToString("0.###") +
                          "deg sheathedLimbTilt=" + limbTilt.ToString("0.##") +
                          "deg sheathedBellyOff=" + bellyOff.ToString("0.##") + "deg");

                if (anchorSpread < FixtureMinAnchorSpreadDeg)
                    failures.Add("[sheathed-bow-matches-drawn] FIXTURE BROKEN: the hand and the sheathe " +
                                 "socket are only " + anchorSpread.ToString("0.##") + "deg apart " +
                                 "(needs >= " + FixtureMinAnchorSpreadDeg.ToString("0.#") + "deg). Two " +
                                 "nearly-aligned anchors would agree by accident and this case would " +
                                 "prove nothing about anchor-independence.");

                // THE RULING, as one number.
                if (disagreement > SheathedDrawnAgreementDeg)
                    failures.Add("[sheathed-bow-matches-drawn] the sheathed bow's world orientation is " +
                                 disagreement.ToString("0.##") + "deg away from the drawn one (allowed " +
                                 SheathedDrawnAgreementDeg.ToString("0.##") + "deg). Owner ruling " +
                                 "2026-08-16: 'both sheathed and drawn bow stay in this same pose'. " +
                                 "A large value means the sheathed seat fell back to the MELEE carry " +
                                 "(ComputeSheathRotation - the vertical, inverted hip hang since the " +
                                 "owner's 2026-08-20 instruction, a diagonal baldric before it), which " +
                                 "is correct for a sword and wrong for a bow in either expression.");

                // ...and as the four clauses, so a future 'agreement' between two WRONG poses cannot
                // pass. Both halves are required: agreeing and correct are different properties.
                if (limbTilt > SheathedDrawnAgreementDeg)
                    failures.Add("[sheathed-bow-matches-drawn] the SHEATHED bow's limb line sits " +
                                 limbTilt.ToString("0.##") + "deg off the body's vertical. Clause 1 of " +
                                 "the owner's rule applies to the slung bow exactly as it does to the " +
                                 "held one - the two states hold the same pose.");

                if (bellyOff > SheathedDrawnAgreementDeg)
                    failures.Add("[sheathed-bow-matches-drawn] the SHEATHED bow's BELLY is " +
                                 bellyOff.ToString("0.##") + "deg off the body's forward. Clauses 2 and " +
                                 "4 - string parallel to the person, curve furthest from the person - " +
                                 "govern the slung bow too. Note this case would still pass its " +
                                 "agreement assertion if BOTH states were wrong the same way, which is " +
                                 "why the axes are asserted as well.");
            }
            finally
            {
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        // ── SOURCE-SHAPE HELPERS (2026-08-20) ────────────────────────────────────────────────────
        // Added when the two literal matches above ("...(back)") went red on a RENAME while the code
        // still honoured the ruling. Everything here works on the whitespace-free, comment-free,
        // string-free projection of the file, so it reads STRUCTURE - which method, which branch,
        // which argument - instead of a spelling that any refactor is entitled to change.

        /// <summary>The brace-balanced body following <paramref name="signature"/>, or null.</summary>
        private static string MethodBody(string src, string signature)
        {
            if (string.IsNullOrEmpty(src)) return null;
            int at = src.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int open = src.IndexOf('{', at);
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            return null;
        }

        /// <summary>
        /// The single statement in which the sheathe path branches on WeaponClass.Bow: from the gate
        /// to the terminating ';'. Both arms of one ternary seat ONE prop, which is what lets the
        /// anchor-identity assertion above be meaningful rather than a coincidence of naming.
        /// </summary>
        private static string BowTernary(string methodBody)
        {
            if (string.IsNullOrEmpty(methodBody)) return null;
            int at = methodBody.IndexOf("_currentWeaponKind==WeaponClass.Bow?", StringComparison.Ordinal);
            if (at < 0) return null;
            int end = methodBody.IndexOf(';', at);
            return end < 0 ? methodBody.Substring(at) : methodBody.Substring(at, end - at);
        }

        /// <summary>First argument of the first <paramref name="call"/> in <paramref name="region"/>,
        /// or null when the call is absent. Stops at the first ',' or ')'.</summary>
        private static string FirstArgOf(string region, string call)
        {
            if (string.IsNullOrEmpty(region)) return null;
            int at = region.IndexOf(call, StringComparison.Ordinal);
            if (at < 0) return null;
            int start = at + call.Length;
            int comma = region.IndexOf(',', start);
            int close = region.IndexOf(')', start);
            int end = comma >= 0 && (close < 0 || comma < close) ? comma : close;
            if (end < 0) return null;
            string arg = region.Substring(start, end - start);
            return arg.Length == 0 ? null : arg;
        }

        /// <summary>True when an anchor identifier names a HAND bone rather than a sheathe socket.
        /// Named, not enumerated: the hands are _weaponHand / _offHandHand today and the point is the
        /// ROLE, not those two spellings.</summary>
        private static bool IsHandAnchor(string anchor)
            => !string.IsNullOrEmpty(anchor) &&
               anchor.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Source with // and /* */ comments AND every string literal (plain, verbatim, interpolated)
        /// blanked out, so a source-lint match cannot be satisfied by prose or by a log message. The
        /// files this suite lints document their own defects at length - EquipmentController names
        /// ComputeBowHeldRotation in three separate comment blocks - so an unstripped IndexOf would
        /// pass on the documentation of the bug rather than on the code that fixes it.
        /// </summary>
        private static string StripCommentsAndStrings(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];

                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i = Math.Min(n, i + 2);
                    sb.Append(' ');
                    continue;
                }
                if (c == '\'')
                {
                    i++;   // char literal - short, and never carries a token we lint for
                    while (i < n && src[i] != '\'')
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    i = Math.Min(n, i + 1);
                    sb.Append(' ');
                    continue;
                }
                if (c == '@' && i + 1 < n && src[i + 1] == '"')
                {
                    i += 2;   // verbatim: "" is an escaped quote
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                if (c == '"')
                {
                    i++;   // plain or interpolated - the braces inside are blanked with the rest,
                    while (i < n)   // which is deliberate: an interpolated call is not the live call
                    {               // this lint is looking for.
                        if (src[i] == '\\') { i += 2; continue; }
                        if (src[i] == '"') { i++; break; }
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
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
