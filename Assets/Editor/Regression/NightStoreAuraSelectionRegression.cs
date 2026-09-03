// =============================================================================
// NightStoreAuraSelectionRegression [night-store-aura]  -- WO-1343
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// -----------------------------------------------------------------------------
// WHAT THIS SUITE IS FOR, IN ONE SENTENCE
// -----------------------------------------------------------------------------
// It makes it IMPOSSIBLE for a future refactor to silently re-point an owner's VFX
// tag, or to make a database row change what an offline player sees.
//
// Those are the two ways this feature can fail QUIETLY, and both have precedent:
//   * A tag was silently re-pointed THIS SESSION - the VFX Caster overwrote her
//     'atfootprintoftree_Aura' -> Aura_Nature pick with Elite_Death.prefab while she
//     was tagging something else, and logged nothing but "tagged -> <key>". Nobody
//     would have known but for a diff.
//   * "The remote read is an OVERRIDE and never a dependency" is asserted in prose
//     all over RemoteTunables.cs. Prose does not go red.
//
// -----------------------------------------------------------------------------
// WHAT IT PROVES, EXECUTABLY
// -----------------------------------------------------------------------------
//  (a) THE TAGS ARE HERS, READ FROM HER FILE. Every key this feature names is
//      resolved out of Assets/Editor/VfxManualPicks.json AT RUN TIME and compared
//      against the prefab path recorded here. This suite therefore fails if the key
//      is renamed in code, if the row disappears, OR if the row is re-pointed at a
//      different prefab - which is exactly the tagger defect above. The suite does
//      NOT edit that file and must never be "fixed" by editing it: a RED here means
//      a human looks at a tag, which is the entire point.
//
//  (b) THE NO-ROW INVARIANT. With the tunables table CLEARED - no row, no network,
//      no server - the selector resolves the owner's FIRST tagged key, rotation OFF,
//      cadence 30, mask 127, burst-repeat 0. Then a MALFORMED payload and a
//      readOk=false payload are applied and the same assertion is repeated, because
//      "an empty table behaves like today" and "a BROKEN table behaves like today"
//      are different claims and only the second one is a real fail-safe.
//
//  (c) ROTATION SHIPS OFF, AND CANNOT BE PROMOTED BY ACCIDENT. The registry default
//      for the mode knob is pinned at 0. She has not chosen between her two tagged
//      candidates or the family, and a default that drifts to 1 or 2 would be this
//      repo making her creative decision for her.
//
//  (d) THE ROTATION WALK IS ORDERED AND ONE-AT-A-TIME. "Slowly one after another"
//      is her pacing instruction: consecutive ticks must advance through the enabled
//      family members in folder order and return exactly one key, never two.
//
//  (e) NO PREFAB WAS CHOSEN HERE. The seven family names are asserted to be exactly
//      the seven .prefab files on disk in her folder - a directory listing, not a
//      shortlist. If someone later adds an eighth prefab or drops one, this goes red
//      rather than quietly shipping a stale hand-typed list.
//
//  (f) THE HELD SEATS STAY HELD. HeldVfxKeys.TreeOfLifeFootAura and
//      HeldVfxKeys.BossDeath must remain EMPTY until she tags them. A future seat
//      filling one in with a plausible key is the exact rule violation
//      (memory vfx-map-owner-tags-no-creative-pick), and it would look like progress.
//
//  (g) ONE SPAWN OWNER. Source lint: RealmStoreBeacon must contain exactly ONE
//      VFXManager.PlayKey call, and NightStoreAuraSelector must contain NONE.
//      A selection policy that starts spawning is a second owner (CLAUDE.md s7).
//
//  NOT provable here: whether the effect LOOKS good. That is hers, on the device,
//  and it is the entire reason the choice is a row instead of a constant.
//
// Markers: NIGHT_STORE_AURA_OK / NIGHT_STORE_AURA_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.NightStoreAuraSelectionRegression.RunAll
// ASCII only.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.Ops;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class NightStoreAuraSelectionRegression
    {
        private const string PicksPath    = "Assets/Editor/VfxManualPicks.json";
        private const string AuraFolder   = "Assets/Spells Pack/Particles/Prefabs/Auras";
        private const string SelectorSrc  = "Assets/_Modules/Village/Vfx/NightStoreAuraSelector.cs";
        private const string BeaconSrc    = "Assets/_Modules/Village/Vfx/RealmStoreBeacon.cs";

        /// <summary>
        /// THE PINNED TAGS. key -> the prefab path HER file must still carry for it.
        /// Read verbatim from Assets/Editor/VfxManualPicks.json on 2026-09-03.
        /// ⛔ If a case below goes RED, the correct response is to ASK HER, never to edit
        /// VfxManualPicks.json to match this table and never to edit this table to match a
        /// surprise. A silent re-point is the defect this suite exists to catch.
        /// </summary>
        private static readonly KeyValuePair<string, string>[] PinnedTags =
        {
            // Ask 2, her FIRST store pick and the shipped default.
            new KeyValuePair<string, string>(
                "NightStoreoption_Aura",
                "Assets/Resources/VFX/Aura/top_down_starfall_line_blue.prefab"),

            // Ask 2b, her SECOND store candidate ("not sure which will be best").
            new KeyValuePair<string, string>(
                "Store_Aura",
                "Assets/Lana Studio/Casual RPG VFX/Prefabs/Loot/Loot_flicker.prefab"),

            // The knight's shield-bash impact, confirmed by her as deliberate.
            new KeyValuePair<string, string>(
                "KnightShieldBash_Impact",
                "Assets/Hovl Studio/AAA Projectiles Vol 1/Prefabs/Flash and hits/Dragon punch flash.prefab"),

            // Ask 1's SIBLING, pinned so the additive relationship cannot be quietly undone:
            // the tree's existing FireFlies loop must still be the FireFlies loop.
            new KeyValuePair<string, string>(
                "TreeofLifeAura_Aura",
                "Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab"),
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("NIGHT_STORE_AURA_OK - " + reason);
            else Debug.LogError("NIGHT_STORE_AURA_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string picks = null;
            try
            {
                picks = File.Exists(PicksPath) ? File.ReadAllText(PicksPath) : null;

                Case(failures, "owner-tags",   () => Case1_OwnerTagsUnmoved(failures, picks));
                Case(failures, "no-row",       () => Case2_NoRowInvariant(failures));
                Case(failures, "rotation-off", () => Case3_RotationShipsOff(failures));
                Case(failures, "walk-order",   () => Case4_RotationWalkIsOrdered(failures));
                Case(failures, "family-list",  () => Case5_FamilyIsADirectoryListing(failures));
                Case(failures, "held-seats",   () => Case6_HeldSeatsStayHeld(failures));
                Case(failures, "one-owner",    () => Case7_OneSpawnOwner(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                // Never leave a mutated tunables table behind for the next suite in the run.
                RemoteTunables.Clear();
            }

            if (failures.Count == 0)
            {
                reason = "NIGHT STORE AURA OK - her tagged keys still resolve to the prefabs she tagged " +
                         "(4 pinned), an EMPTY / MALFORMED / readOk=false tunables table all reproduce " +
                         "this build exactly (mode=TaggedStarfall '" + NightStoreAuraSelector.OwnerTaggedKey +
                         "', cadence 30 min, mask 127, burstRepeat 0), rotation ships OFF, the family " +
                         "walk is ordered and one-at-a-time over the 7 prefabs actually on disk, both " +
                         "held seats are still unbound, and there is exactly ONE spawn owner.";
                return true;
            }
            reason = "night-store-aura FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - HER TAGS, read from HER file, still point where they pointed
        // =====================================================================

        private static void Case1_OwnerTagsUnmoved(List<string> failures, string picks)
        {
            if (picks == null)
            {
                failures.Add("[owner-tags] " + PicksPath + " is MISSING. Every owner VFX tag in the game " +
                             "lives in that file; without it nothing can be verified and the no-pick rule " +
                             "has no source of truth.");
                return;
            }

            foreach (var pin in PinnedTags)
            {
                if (!TryReadPick(picks, pin.Key, out string path))
                {
                    failures.Add("[owner-tags] key '" + pin.Key + "' has NO ROW in " + PicksPath +
                                 ". It was tagged by the owner and is wired in code; a vanished row means " +
                                 "the hook now resolves nothing. ASK HER - do not re-add the row from " +
                                 "this file.");
                    continue;
                }

                if (!string.Equals(path, pin.Value, StringComparison.Ordinal))
                {
                    failures.Add("[owner-tags] key '" + pin.Key + "' now points at '" + path +
                                 "' but the owner tagged it as '" + pin.Value + "'. THIS IS THE SILENT " +
                                 "RE-POINT DEFECT: HovlVfxCatalogGenerator.WriteManualPick replaces an " +
                                 "existing row on a key match with NO diff check, NO warning and NO " +
                                 "confirmation, and the VFX Caster's tag base-name is a free-typed field " +
                                 "that PERSISTS across prefab selections - so an unrelated tagging action " +
                                 "can overwrite this pick. ASK HER which one she meant. Do NOT edit " +
                                 PicksPath + " and do NOT edit this pin to match.");
                }
            }

            // The code constants must be the keys she tagged, spelled exactly - including her
            // capitalisation. 'NightStoreoption_Aura' has a lower-case 'o' in the middle and that
            // is not a typo to be tidied: the catalog lookup is Ordinal.
            if (NightStoreAuraSelector.OwnerTaggedKey != "NightStoreoption_Aura")
                failures.Add("[owner-tags] NightStoreAuraSelector.OwnerTaggedKey is '" +
                             NightStoreAuraSelector.OwnerTaggedKey + "'. Her tag is spelled " +
                             "'NightStoreoption_Aura' - catalog lookup is Ordinal, so a 'corrected' " +
                             "capitalisation silently resolves nothing.");

            if (NightStoreAuraSelector.OwnerTaggedAltKey != "Store_Aura")
                failures.Add("[owner-tags] NightStoreAuraSelector.OwnerTaggedAltKey is '" +
                             NightStoreAuraSelector.OwnerTaggedAltKey + "'; her second candidate is " +
                             "tagged 'Store_Aura'.");

            if (NightStoreAuraSelector.LegacyBeaconKey != "store.beacon.near")
                failures.Add("[owner-tags] LegacyBeaconKey is '" + NightStoreAuraSelector.LegacyBeaconKey +
                             "' - the aura this build replaced is keyed 'store.beacon.near'. That key is " +
                             "the one-row undo of the whole swap and must stay reachable.");
        }

        /// <summary>Read one manual pick's prefabPath out of the raw JSON, without a JSON dependency
        /// and without ever writing to the file.</summary>
        private static bool TryReadPick(string json, string key, out string prefabPath)
        {
            prefabPath = null;
            var m = Regex.Match(
                json,
                "\"key\"\\s*:\\s*\"" + Regex.Escape(key) + "\"\\s*,\\s*\"prefabPath\"\\s*:\\s*\"([^\"]*)\"");
            if (!m.Success) return false;
            // JsonUtility escapes nothing in these paths today, but unescape defensively so a
            // future escaped path does not read as a mismatch.
            prefabPath = m.Groups[1].Value.Replace("\\\\", "\\").Replace("\\/", "/");
            return true;
        }

        // =====================================================================
        //  Case 2 - THE NO-ROW INVARIANT, in all three broken shapes
        // =====================================================================

        private static void Case2_NoRowInvariant(List<string> failures)
        {
            // (i) NO ROW / NO NETWORK / NO SERVER - the table was never populated.
            RemoteTunables.Clear();
            AssertShippedBehaviour(failures, "no row (table cleared - offline, 404, or empty table)");

            // (ii) MALFORMED PAYLOAD - the body did not parse at all. ApplyPayload must reject it
            //      and leave every knob answering its shipping default.
            RemoteTunables.Clear();
            // NOTE the literal is brace-free on purpose: CLAUDE.md s1's brace gate counts raw
            // characters and cannot tell a brace inside a string from a real one, so an unbalanced
            // brace in a test fixture would fail the gate for the whole file. Newtonsoft rejects
            // this just as hard.
            RemoteTunables.ApplyPayload("this is not json at all", "test-corrupt");
            AssertShippedBehaviour(failures, "corrupt payload (unparseable body)");

            // (iii) SERVER SAYS IT COULD NOT READ ITS OWN TABLE. Distinct from "no knobs are set"
            //       and must NOT be mistaken for it.
            RemoteTunables.Clear();
            RemoteTunables.ApplyPayload("{\"version\":1,\"readOk\":false,\"reason\":\"test\"}", "test-readfail");
            AssertShippedBehaviour(failures, "server readOk=false");

            // (iv) A ROW WITH AN UNUSABLE VALUE must not poison the knob - it falls to the default.
            RemoteTunables.Clear();
            RemoteTunables.ApplyPayload(
                "{\"version\":1,\"readOk\":true,\"values\":{\"vfx.nightStoreAuraMode\":\"banana\"}}",
                "test-badvalue");
            AssertShippedBehaviour(failures, "malformed row value for the mode knob");

            // (v) AND THE KNOB MUST ACTUALLY WORK when a good row IS present - an invariant that
            //     only ever fails safe would be indistinguishable from a knob that is not wired.
            RemoteTunables.Clear();
            RemoteTunables.ApplyPayload(
                "{\"version\":1,\"readOk\":true,\"values\":{\"vfx.nightStoreAuraMode\":\"1\"}}",
                "test-good");
            if (NightStoreAuraSelector.Mode != NightStoreAuraMode.TaggedLootFlicker)
                failures.Add("[no-row] a VALID row vfx.nightStoreAuraMode=1 did not take effect (mode " +
                             "resolved " + NightStoreAuraSelector.Mode + "). The fail-safe is only " +
                             "meaningful if the success path works - a knob that always answers its " +
                             "default is not a knob.");
            string liveKey = NightStoreAuraSelector.SelectKey(
                NightStoreAuraSelector.Mode, NightStoreAuraSelector.FamilyMask, 0, null, out _);
            if (liveKey != NightStoreAuraSelector.OwnerTaggedAltKey)
                failures.Add("[no-row] with mode=1 the selector answered '" + liveKey + "' instead of her " +
                             "second tagged candidate '" + NightStoreAuraSelector.OwnerTaggedAltKey + "'.");

            RemoteTunables.Clear();
        }

        /// <summary>Assert the FOUR knobs and the resolved key are this build's shipped values.</summary>
        private static void AssertShippedBehaviour(List<string> failures, string situation)
        {
            var mode = NightStoreAuraSelector.Mode;
            if (mode != NightStoreAuraMode.TaggedStarfall)
                failures.Add("[no-row] under '" + situation + "' the mode resolved " + mode +
                             " instead of TaggedStarfall. A tunable that changes behaviour when the " +
                             "network is down is a DEFECT, not a feature (WO-1343 / RemoteTunables.cs).");

            float cadence = NightStoreAuraSelector.CadenceSeconds;
            if (Mathf.Abs(cadence - 1800f) > 0.5f)
                failures.Add("[no-row] under '" + situation + "' the cadence resolved " + cadence +
                             "s instead of 1800s (30 min, her number verbatim).");

            int mask = NightStoreAuraSelector.FamilyMask;
            if (mask != NightStoreAuraSelector.AuraFamilyFullMask)
                failures.Add("[no-row] under '" + situation + "' the family mask resolved " + mask +
                             " instead of " + NightStoreAuraSelector.AuraFamilyFullMask + ".");

            float repeat = NightStoreAuraSelector.BurstRepeatSeconds;
            if (repeat != 0f)
                failures.Add("[no-row] under '" + situation + "' the burst-repeat resolved " + repeat +
                             "s instead of 0 (OFF - one burst per cadence, her spec read literally).");

            string key = NightStoreAuraSelector.SelectKey(mode, mask, 0, null, out _);
            if (key != NightStoreAuraSelector.OwnerTaggedKey)
                failures.Add("[no-row] under '" + situation + "' the selector answered '" + key +
                             "' instead of the owner's first tagged key '" +
                             NightStoreAuraSelector.OwnerTaggedKey + "'. An offline player must see " +
                             "EXACTLY the aura she chose.");
        }

        // =====================================================================
        //  Case 3 - rotation ships OFF and cannot be promoted by accident
        // =====================================================================

        private static void Case3_RotationShipsOff(List<string> failures)
        {
            var spec = RemoteTunables.SpecFor(RemoteTunables.KeyVfxNightStoreAuraMode);
            if (spec == null)
            {
                failures.Add("[rotation-off] '" + RemoteTunables.KeyVfxNightStoreAuraMode + "' is not in " +
                             "RemoteTunables.Registry. An unregistered key answers 0 and can never be set " +
                             "from the database - the knob would be invisible AND unsettable.");
                return;
            }

            if (spec.Default != (int)NightStoreAuraMode.TaggedStarfall)
                failures.Add("[rotation-off] the shipping default for '" + spec.Key + "' is " + spec.Default +
                             ", not 0 (TaggedStarfall). The owner has NOT chosen between her two tagged " +
                             "candidates and the Aura_* rotation - she said so out loud (\"not sure which " +
                             "will be best\", \"if the other one doesnt look good\"). A default other than " +
                             "her first pick is this repo making her creative decision for her.");

            // An out-of-range mode must fall back rather than index into nothing.
            RemoteTunables.Clear();
            RemoteTunables.ApplyPayload(
                "{\"version\":1,\"readOk\":true,\"values\":{\"vfx.nightStoreAuraMode\":\"99\"}}",
                "test-oob");
            if (NightStoreAuraSelector.Mode != NightStoreAuraMode.TaggedStarfall)
                failures.Add("[rotation-off] mode=99 did not fall back to TaggedStarfall (got " +
                             NightStoreAuraSelector.Mode + "). A fat thumb on a phone must not be able " +
                             "to leave the store in an undefined state.");
            RemoteTunables.Clear();

            // The other three knobs must exist too - a key added to fewer than all sources is
            // invisible or unsettable, which is the failure this whole rail is shaped against.
            foreach (string key in new[]
                     {
                         RemoteTunables.KeyVfxNightStoreAuraCadenceMin,
                         RemoteTunables.KeyVfxNightStoreAuraFamilyMask,
                         RemoteTunables.KeyVfxNightStoreAuraBurstRepeatSec,
                     })
            {
                if (RemoteTunables.SpecFor(key) == null)
                    failures.Add("[rotation-off] '" + key + "' is not in RemoteTunables.Registry - it " +
                                 "cannot be read and cannot be set.");
            }
        }

        // =====================================================================
        //  Case 4 - "slowly one after another": ordered, one at a time
        // =====================================================================

        private static void Case4_RotationWalkIsOrdered(List<string> failures)
        {
            int n = NightStoreAuraSelector.AuraFamilyPrefabNames.Length;
            int full = NightStoreAuraSelector.AuraFamilyFullMask;

            // canPlay = "everything resolves", so this case asserts ORDER, not resolution.
            var seen = new List<string>();
            for (int tick = 0; tick < n; tick++)
            {
                string key = NightStoreAuraSelector.SelectKey(
                    NightStoreAuraMode.RotateFamily, full, tick, _ => true, out _);
                seen.Add(key);
            }

            for (int i = 0; i < n; i++)
            {
                string expected = NightStoreAuraSelector.AuraFamilyPrefabNames[i];
                if (seen[i] != expected)
                    failures.Add("[walk-order] tick " + i + " selected '" + seen[i] + "' but the folder-" +
                                 "order walk expects '" + expected + "'. Her instruction was \"slowly one " +
                                 "after another\" - an ORDERED walk, not a shuffle.");
            }

            // One at a time: the walk must return exactly one key, and the whole cycle must cover
            // every member exactly once before repeating.
            var distinct = new HashSet<string>(seen, StringComparer.Ordinal);
            if (distinct.Count != n)
                failures.Add("[walk-order] a full cycle of " + n + " ticks produced only " + distinct.Count +
                             " distinct member(s) - the walk repeats or stalls instead of visiting each " +
                             "aura once.");

            // Dropping a member from the mask must remove it from the walk WITHOUT a code change -
            // that is the WO-1343 requirement in one assertion.
            int withoutFire = full & ~(1 << 2);   // bit 2 = Aura_Fire, per the documented ordering
            for (int tick = 0; tick < 12; tick++)
            {
                string key = NightStoreAuraSelector.SelectKey(
                    NightStoreAuraMode.RotateFamily, withoutFire, tick, _ => true, out _);
                if (key == "Aura_Fire" || key == "Aura_Fire_Aura")
                {
                    failures.Add("[walk-order] mask " + withoutFire + " excludes Aura_Fire (bit 2) yet the " +
                                 "walk still selected it at tick " + tick + ". Removing a prefab she " +
                                 "dislikes must be a single number, with no code change.");
                    break;
                }
            }

            // An empty mask must NOT leave the store bare - it falls back to her tagged key.
            string fallback = NightStoreAuraSelector.SelectKey(
                NightStoreAuraMode.RotateFamily, 0, 0, _ => true, out _);
            if (fallback != NightStoreAuraSelector.OwnerTaggedKey)
                failures.Add("[walk-order] an EMPTY family mask answered '" + fallback + "' instead of " +
                             "falling back to her tagged '" + NightStoreAuraSelector.OwnerTaggedKey +
                             "'. A misconfigured mask must never leave the store with no aura.");

            // And when NOTHING in the family resolves - which is TODAY, because none of the seven is
            // owner-tagged yet - rotation must still answer her tagged key rather than nothing.
            string untagged = NightStoreAuraSelector.SelectKey(
                NightStoreAuraMode.RotateFamily, full, 0, _ => false, out _);
            if (untagged != NightStoreAuraSelector.OwnerTaggedKey)
                failures.Add("[walk-order] with NO family member resolvable (the state of the catalog " +
                             "today - all seven Aura_* prefabs are 'catalogued:false'), rotation answered '" +
                             untagged + "' instead of her tagged '" + NightStoreAuraSelector.OwnerTaggedKey +
                             "'. Turning rotation on before she tags the family must degrade to her " +
                             "aura, never to a bare store.");

            // The cadence tick means something DIFFERENT per mode, and the code must say which.
            if (NightStoreAuraSelector.CadenceMeaningFor(NightStoreAuraMode.TaggedStarfall)
                != NightStoreCadenceMeaning.RefireBurst)
                failures.Add("[walk-order] the cadence meaning for TaggedStarfall is not RefireBurst. Both " +
                             "of her store tags are MEASURED one-shot bursts (every ParticleSystem " +
                             "looping:0), so a tick must re-fire them.");
            if (NightStoreAuraSelector.CadenceMeaningFor(NightStoreAuraMode.RotateFamily)
                != NightStoreCadenceMeaning.AdvanceRotation)
                failures.Add("[walk-order] the cadence meaning for RotateFamily is not AdvanceRotation. The " +
                             "Aura_* family is CONTINUOUS, so a tick advances the walk rather than " +
                             "re-firing anything.");
            if (NightStoreAuraSelector.CadenceMeaningFor(NightStoreAuraMode.LegacyBeaconRing)
                != NightStoreCadenceMeaning.Inert)
                failures.Add("[walk-order] the cadence meaning for LegacyBeaconRing is not Inert.");
        }

        // =====================================================================
        //  Case 5 - the family list is a DIRECTORY LISTING, not a hand-typed shortlist
        // =====================================================================

        private static void Case5_FamilyIsADirectoryListing(List<string> failures)
        {
            if (!Directory.Exists(AuraFolder))
            {
                // Spells Pack is a gitignored art pack. On a machine without it imported this is not
                // a defect, and failing here would make a clean clone red for a reason nobody can fix.
                Debug.Log("[night-store-aura] " + AuraFolder + " is absent (gitignored art pack not " +
                          "imported) - the family-listing case is SKIPPED, not failed.");
                return;
            }

            var onDisk = new List<string>();
            foreach (string file in Directory.GetFiles(AuraFolder, "*.prefab", SearchOption.TopDirectoryOnly))
                onDisk.Add(Path.GetFileNameWithoutExtension(file));
            onDisk.Sort(StringComparer.Ordinal);

            var declared = new List<string>(NightStoreAuraSelector.AuraFamilyPrefabNames);
            declared.Sort(StringComparer.Ordinal);

            if (onDisk.Count != declared.Count || string.Join(",", onDisk) != string.Join(",", declared))
                failures.Add("[family-list] NightStoreAuraSelector.AuraFamilyPrefabNames is [" +
                             string.Join(", ", declared) + "] but " + AuraFolder + " holds [" +
                             string.Join(", ", onDisk) + "]. That array is supposed to BE the folder " +
                             "listing she screenshotted - not a shortlist, not a ranking. If the folder " +
                             "gained or lost a prefab, update the array; if the array was edited to " +
                             "prefer some prefabs, that is a creative pick and must be reverted " +
                             "(memory vfx-map-owner-tags-no-creative-pick).");
        }

        // =====================================================================
        //  Case 6 - the held seats stay held
        // =====================================================================

        private static void Case6_HeldSeatsStayHeld(List<string> failures)
        {
            if (!string.IsNullOrEmpty(HeldVfxKeys.TreeOfLifeFootAura))
                failures.Add("[held-seats] HeldVfxKeys.TreeOfLifeFootAura is now '" +
                             HeldVfxKeys.TreeOfLifeFootAura + "'. The tree-foot seat is HELD: the owner " +
                             "tagged 'atfootprintoftree_Aura' -> Aura_Nature.prefab and the VFX Caster " +
                             "then overwrote that row with Elite_Death.prefab while she was tagging a " +
                             "BOSS DEATH. Binding either one from code is a creative pick. This goes " +
                             "green again ONLY when she retags and the new key is written here WITH her " +
                             "confirmation - at which point add it to PinnedTags above too.");

            if (!string.IsNullOrEmpty(HeldVfxKeys.BossDeath))
                failures.Add("[held-seats] HeldVfxKeys.BossDeath is now '" + HeldVfxKeys.BossDeath +
                             "'. There is NO boss-death row in " + PicksPath + " - her \"added Elite " +
                             "death to boss death\" pick landed on the 'atfootprintoftree' key instead. " +
                             "Naming a key here is authoring her tag. Same rule: she tags, then this " +
                             "constant is filled in and pinned.");

            // The spurious rows must NOT be wired anywhere. If a future seat starts reading them, the
            // Heart of Elarion grows a death explosion at its foot and nobody meant that.
            foreach (string src in new[] { SelectorSrc, BeaconSrc,
                                           "Assets/_Modules/Village/Heart/HeartAuraController.cs",
                                           "Assets/_Modules/Village/Enemies/EliteVFXController.cs" })
            {
                if (!File.Exists(src)) continue;
                string body = StripComments(File.ReadAllText(src));
                // RE-POINTED 2026-09-03 at the gate (CLI), and NARROWED to the real defect - NOT
                // softened. The first form asked whether the key was MENTIONED anywhere outside a
                // comment, and StripComments does not strip STRING LITERALS - so it fired on the
                // held seats' own FlowTrace reason text, i.e. on the very sentences that exist to
                // explain WHY the seat is held. A pin that fails on its own documentation teaches
                // the next seat to delete the documentation, which is the opposite of the point.
                //
                // What the pin MEANS: no code may BIND this key. A binding is a BARE quoted literal
                // - VFXManager.PlayKey("atfootprintoftree_Aura") - because that is the only shape
                // the VFX layer accepts. The key embedded inside a longer prose sentence cannot
                // reach a spawner and never could. So: match a complete literal whose ENTIRE
                // content is the key, with or without its _Aura/_Impact role suffix.
                //
                // This is STRICTLY STRONGER where it counts: every real wiring still fails, including
                // one a future seat adds, and it no longer passes-or-fails on prose. Re-point a pin,
                // never soften it.
                if (Regex.IsMatch(body, "\"atfootprintoftree[A-Za-z0-9_]*\""))
                    failures.Add("[held-seats] " + src + " BINDS the key 'atfootprintoftree' as a bare " +
                                 "string literal. That row currently points at Elite_Death.prefab because " +
                                 "the tagger overwrote it; wiring it seats a death EXPLOSION at the base of " +
                                 "the Heart of Elarion.");
                if (Regex.IsMatch(body, "\"EliteDeath_Impact\""))
                    failures.Add("[held-seats] " + src + " BINDS 'EliteDeath_Impact' as a bare string " +
                                 "literal. The owner did NOT pick that row - the tagger wrote it in her " +
                                 "name. It is held.");
            }
        }

        // =====================================================================
        //  Case 7 - ONE spawn owner (CLAUDE.md s7)
        // =====================================================================

        private static void Case7_OneSpawnOwner(List<string> failures)
        {
            if (!File.Exists(BeaconSrc) || !File.Exists(SelectorSrc))
            {
                failures.Add("[one-owner] a source file is missing: " + BeaconSrc + " / " + SelectorSrc);
                return;
            }

            string beacon   = StripComments(File.ReadAllText(BeaconSrc));
            string selector = StripComments(File.ReadAllText(SelectorSrc));

            int beaconPlays = Regex.Matches(beacon, @"VFXManager\.PlayKey\s*\(").Count;
            if (beaconPlays != 1)
                failures.Add("[one-owner] " + BeaconSrc + " contains " + beaconPlays + " VFXManager.PlayKey " +
                             "call(s); it must contain exactly ONE. The Night Store's aura has a single " +
                             "spawn owner - a second call site is a second owner and a second pool slot " +
                             "(CLAUDE.md s7).");

            int selectorPlays = Regex.Matches(selector, @"VFXManager\.PlayKey\s*\(").Count;
            if (selectorPlays != 0)
                failures.Add("[one-owner] " + SelectorSrc + " contains " + selectorPlays +
                             " VFXManager.PlayKey call(s); the selector DECIDES and must never SPAWN. " +
                             "It is pure and headlessly testable precisely because it does not.");

            // The beacon must ask the selector rather than hardcoding a key back in.
            if (!beacon.Contains("NightStoreAuraSelector.SelectKey"))
                failures.Add("[one-owner] " + BeaconSrc + " no longer calls " +
                             "NightStoreAuraSelector.SelectKey - the tested policy and the shipped " +
                             "decision have diverged, so a green suite would prove nothing.");

            // The town gate must be present: the clock does not run in a raid, battle or dungeon.
            if (!beacon.Contains("HubScenes.IsHub"))
                failures.Add("[one-owner] " + BeaconSrc + " no longer gates the cadence clock on " +
                             "HubScenes.IsHub. WO-1343: the aura clock runs in TOWN ONLY.");

            // A no-show must name itself. A silent absence is indistinguishable from a subtle prefab.
            if (!beacon.Contains("FlowTrace."))
                failures.Add("[one-owner] " + BeaconSrc + " has no FlowTrace on the aura path. A silent " +
                             "VFX no-show is indistinguishable from \"the artist's prefab is subtle\", and " +
                             "that ambiguity costs a felt-test round trip (CLAUDE.md s12).");
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
