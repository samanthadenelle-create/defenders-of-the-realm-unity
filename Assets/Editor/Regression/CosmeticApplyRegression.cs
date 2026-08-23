// =============================================================================
// CosmeticApplyRegression [cosmetic-apply] — an equipped cosmetic REACHES A RENDERER.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Namespace: DeNelle.Editor.Regression.
// Marker: COSMETIC_APPLY_OK / COSMETIC_APPLY_FAIL.
// Standalone: run-unity-method -Method DeNelle.Editor.Regression.CosmeticApplyRegression.RunAll
// Registered in DataRegression.RunAll as the "cosmetic-apply suite".
//
// ── THE DEFECT THIS SUITE EXISTS BECAUSE OF (WO-992, found 2026-08-21) ───────
// CosmeticApplier.ApplyCosmetic was CALLED FROM NOWHERE and the component's GUID
// sat on ZERO prefabs and ZERO scenes. Every part of the economy around it worked:
// Glimmer was earned (TierSystem, Enemy, DailyQuestRewardBridge, WaveFeedbackDirector),
// spent (GlimmerCurrencyService.TryPurchase), equipped (Equip), shown in a shop
// (CosmeticShopPanel) — and SOLD FOR REAL MONEY (packs.json: 25 glimmer with Hearth
// Spark, 50 with Starter's Hand). The only broken link was the last one: the equipped
// id never reached anything the player could see.
//
// It shipped that way, for months, because NOTHING ASSERTED THE LAST LINK. Every
// oracle in the cosmetics area checked DATA — cosmetics.json rows, pack grantability,
// wallet arithmetic — and data was never the problem. That is the general lesson and
// it is why this suite proves the link WITH A REAL RENDERER instead of another JSON
// read: a state flag that changes nothing is indistinguishable from a working feature
// at the data layer.
//
// ── THE SIX RULES ────────────────────────────────────────────────────────────
//   1 [reaches]   THE LOAD-BEARING ONE, driven LIVE, not linted: build a real
//                 GameObject with a real Renderer and a real material, apply a real
//                 CosmeticDef through CosmeticApplier, and READ THE COLOUR BACK OFF
//                 THE RENDERER. If the apply path ever silently stops touching the
//                 renderer again, this is the line that goes red.
//   2 [seam]      The two body owners CALL the applier: HeroBodySwapper installs it
//                 (Attach) and HeroArmorVisual re-drives it (RefreshOn) after it swaps
//                 the visible mesh. Source-lint on CODE, comments dropped — a rule that
//                 matched its own tombstone would be worse than no rule
//                 (EchoWorldPresenceRegression learned this the hard way).
//   3 [one-owner] EXACTLY ONE type applies cosmetic appearance. A second applier /
//                 skinner / "CosmeticVisual" fails here. CLAUDE.md §7's one-appearance-
//                 owner rule, enforced instead of remembered.
//   4 [meshPath]  cosmetics.json authors `meshPath` and CosmeticDef must PARSE it. It
//                 was authored on the pet-aether-twilight row and silently discarded —
//                 the field did not exist — so every consumer re-invented the path.
//   5 [folders]   CosmeticApplier.ResourceFolderFor("pet") and the literal
//                 PetDeployer.TryLoadPetMesh loads from MUST agree. DeNelle.Pets cannot
//                 reference DeNelle.Cosmetics (it reaches the wallet by reflection), so
//                 this pair is a genuine duplicated constant that only a gate can hold
//                 together — §2/§5/§16's failure mode, caught at the seam.
//   6 [village]   The VILLAGE category reaches a STRUCTURE renderer. WO-992 fixed the
//                 HERO seam only; 4 of the 12 shipped cosmetics are village-category and
//                 had NO consumer at all — the applier was category-generic, but nothing
//                 called Attach(host,"village",...) at a structure body owner, so a
//                 building palette the player PAID FOR changed nothing. Same defect shape,
//                 one category over, and it survived rules 1-5 because every one of them
//                 was hero-shaped. Proven LIVE through a CHILD renderer, because a
//                 structure root owns no renderer — VisualFactory.Skin makes the model a
//                 child, so a root-only applier would pass every hero test and decorate
//                 nothing on a building. Plus the tier-reskin re-drive: an upgrade
//                 destroys the decorated renderers and skins new ones.
//
// ── TWO ABSENCES THAT MUST NEVER BE CONFLATED (2026-08-21 hollow-pass sweep) ──
// ART-ABSENT is EXPECTED and is ASSERTED THROUGH: no cosmetic art is staged, so every
// cosmetic lands on the preview-tint fallback (allowPreviewTintFallback) and the rules
// below read that tint back off a real renderer. That path is proven, not skipped.
// FIXTURE-ABSENT is a BROKEN GATE: a tracked source file missing from its hardcoded
// path, or a catalog that will not load / no longer carries the row it is measured on.
// Those FAIL, naming exactly what was missing. Only a genuine HARNESS capability gap
// (no shader in this editor session, a shader with no colour property) stands down —
// and it does so through RegressionOutcome.PartialSkip, whose token puts it in the
// THIRD column so it can never be read as green. The rule this file learned the hard
// way: `notes.Add("SKIPPED ...")` + `return` out of a null guard IS a pass, because the
// caller's only channel is the bool. See RegressionOutcome.cs.
//
// ⚠ WHAT THIS SUITE DELIBERATELY DOES NOT ASSERT: that the applied look is GOOD.
// No cosmetic ART exists in the tree (Resources/Cosmetics/Pets/ is an empty folder),
// so today rule 1 passes through the preview-colour placeholder path. Rule 1 is about
// REACHING the renderer, which is the thing that was broken; judging the look is the
// owner's, from a screenshot (memory: screenshots-are-primary-evidence).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeNelle.Cosmetics;
// NOTE: DeNelle.Village is deliberately NOT imported wholesale — StructureFactory is referenced
// fully-qualified below so this suite cannot pick up an ambiguity from that large namespace.
using UnityEngine;
using UnityEditor;

namespace DeNelle.Editor.Regression
{
    public static class CosmeticApplyRegression
    {
        private const string HeroBodySwapperPath  = "Assets/_Modules/Village/Hero/HeroBodySwapper.cs";
        private const string HeroArmorVisualPath  = "Assets/_Modules/Village/Hero/HeroArmorVisual.cs";
        private const string PetDeployerPath      = "Assets/_Modules/Pets/PetDeployer.cs";
        private const string ApplierPath          = "Assets/_Modules/Cosmetics/CosmeticApplier.cs";
        private const string StructureFactoryPath = "Assets/_Modules/Village/Catalog/StructureFactory.cs";

        [MenuItem("Defenders/Regression/Cosmetic Apply")]
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("COSMETIC_APPLY_OK: " + reason);
            else Debug.LogError("COSMETIC_APPLY_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            CheckReachesRenderer(failures, notes);
            CheckSeam(failures, notes);
            CheckOneOwner(failures, notes);
            CheckMeshPathParsed(failures, notes);
            CheckFolderAgreement(failures, notes);
            CheckVillageReachesStructure(failures, notes);

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures.ToArray());
                return false;
            }

            reason = "COSMETIC APPLY OK — an equipped cosmetic reaches a real renderer, both hero body " +
                     "owners drive the one applier, the village category reaches a structure renderer, " +
                     "and the pet folder constant still agrees. " +
                     string.Join("; ", notes.ToArray());
            return true;
        }

        // ── Rule 1 — LIVE: the colour lands on a real renderer ────────────────

        private static void CheckReachesRenderer(List<string> failures, List<string> notes)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                // DECLARED stand-down (RegressionOutcome.PartialSkip), not a note that reads green.
                // A shader is a HARNESS capability, not product data: with no shader at all no
                // renderer can be built, so this section genuinely cannot run. The token puts it in
                // the THIRD column so "0 red" can never be mistaken for "rule 1 was proven".
                notes.Add(RegressionOutcome.PartialSkip("[reaches] live renderer proof",
                          "no URP/Lit, Standard or Sprites/Default shader resolved in this editor " +
                          "session, so no renderer could be built to prove against"));
                return;
            }

            GameObject host = null;
            Material mat = null;
            try
            {
                host = GameObject.CreatePrimitive(PrimitiveType.Cube);
                host.name = "CosmeticApplyRegression_Host";
                host.hideFlags = HideFlags.HideAndDontSave;

                var renderer = host.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    failures.Add("[reaches] a primitive Cube produced no MeshRenderer — the harness itself " +
                                 "is broken, not the applier.");
                    return;
                }

                mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                renderer.sharedMaterial = mat;

                string prop = mat.HasProperty("_BaseColor") ? "_BaseColor"
                            : mat.HasProperty("_Color") ? "_Color"
                            : null;
                if (prop == null)
                {
                    // DECLARED stand-down — harness capability again (the resolved shader carries no
                    // colour property), never a green pass.
                    notes.Add(RegressionOutcome.PartialSkip("[reaches] live renderer proof",
                              "shader '" + shader.name + "' exposes neither _BaseColor nor _Color, so a " +
                              "tint cannot be proven through it"));
                    return;
                }

                // A cosmetic with a colour NOTHING else in the project uses, so a pass cannot be a
                // coincidence of some default. Deliberately given no meshPath: the whole point is to
                // exercise the path an ARTLESS cosmetic takes, which is every cosmetic today.
                var def = new CosmeticDef
                {
                    Id           = "regression-probe-cosmetic",
                    Category     = "hero",
                    AppliesTo    = "knight",
                    DisplayName  = "Regression Probe",
                    UnlockMethod = "achievement",
                    PreviewColor = "#123456",
                };
                Color want = def.PreviewUnityColor;

                var applier = host.AddComponent<CosmeticApplier>();
                applier.Bind("hero", "knight");
                applier.allowPreviewTintFallback = true;
                applier.ApplyCosmetic(def);

                if (applier.DecoratedRendererCount == 0)
                {
                    failures.Add("[reaches] the applier resolved ZERO renderers on a host that has a " +
                                 "MeshRenderer. This is the WO-992 defect signature: the equipped " +
                                 "cosmetic cannot reach anything the player sees.");
                    return;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Color got = block.GetColor(prop);

                bool took = Mathf.Approximately(got.r, want.r)
                         && Mathf.Approximately(got.g, want.g)
                         && Mathf.Approximately(got.b, want.b);

                if (!took)
                {
                    failures.Add("[reaches] applying cosmetic '" + def.Id + "' left the renderer's " + prop +
                                 " at " + got + ", not the cosmetic's " + want + ". The equip state changed " +
                                 "and the PIXELS DID NOT — a player can buy this and see nothing, which is " +
                                 "exactly the defect WO-992 found.");
                    return;
                }

                // And it must UNDO — a cosmetic you unequip has to come off.
                applier.ResetToDefault();
                var after = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(after);
                Color cleared = after.GetColor(prop);
                bool stillTinted = Mathf.Approximately(cleared.r, want.r)
                                && Mathf.Approximately(cleared.g, want.g)
                                && Mathf.Approximately(cleared.b, want.b);
                if (stillTinted)
                {
                    failures.Add("[reaches] ResetToDefault left " + prop + " at the cosmetic colour " + want +
                                 " — unequipping a skin would not remove it.");
                    return;
                }

                notes.Add("[reaches] live proof: cosmetic colour " + want + " read back off a real renderer " +
                          "via " + prop + " (" + applier.DecoratedRendererCount + " renderer(s)), and cleared on reset");
            }
            catch (Exception ex)
            {
                failures.Add("[reaches] threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (mat != null) UnityEngine.Object.DestroyImmediate(mat);
            }
        }

        // ── Rule 2 — the body owners drive the applier ────────────────────────

        private static void CheckSeam(List<string> failures, List<string> notes)
        {
            string swapper = CodeText(HeroBodySwapperPath);
            if (swapper == null)
                failures.Add("[seam] " + HeroBodySwapperPath + " not found — cannot prove the hero installs an applier.");
            else if (swapper.IndexOf("CosmeticApplier.Attach", StringComparison.Ordinal) < 0)
                failures.Add("[seam] HeroBodySwapper no longer calls CosmeticApplier.Attach. Without that " +
                             "call the hero cosmetics the player BOUGHT are invisible again — that call site " +
                             "IS the WO-992 fix. (Comment lines are ignored, so a tombstone will not satisfy this.)");
            else
                notes.Add("[seam] HeroBodySwapper installs the applier");

            string armor = CodeText(HeroArmorVisualPath);
            if (armor == null)
                failures.Add("[seam] " + HeroArmorVisualPath + " not found — cannot prove the armour swap re-drives the applier.");
            else if (armor.IndexOf("CosmeticApplier.RefreshOn", StringComparison.Ordinal) < 0)
                failures.Add("[seam] HeroArmorVisual no longer calls CosmeticApplier.RefreshOn. It REPLACES the " +
                             "visible mesh on an armour equip, so without the re-drive the paid-for skin is " +
                             "silently stripped the first time the player changes armour.");
            else
                notes.Add("[seam] HeroArmorVisual re-drives the applier after a body swap");
        }

        // ── Rule 3 — exactly one appearance owner ─────────────────────────────

        private static void CheckOneOwner(List<string> failures, List<string> notes)
        {
            // Reflection, not a filename scan: a second owner introduced under any name, in any
            // assembly, is what this has to catch.
            var owners = new List<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }
                if (types == null) continue;

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;

                    // GetMethod(name, flags) would throw AmbiguousMatchException here — the owner
                    // carries ApplyCosmetic(string) AND ApplyCosmetic(CosmeticDef). Enumerate instead.
                    bool applies = false;
                    MethodInfo[] methods;
                    try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly); }
                    catch { continue; }
                    foreach (var m in methods)
                    {
                        if (m != null && m.Name == "ApplyCosmetic") { applies = true; break; }
                    }
                    if (!applies) continue;
                    owners.Add(t.FullName);
                }
            }

            if (owners.Count == 0)
            {
                failures.Add("[one-owner] NO MonoBehaviour exposes a public ApplyCosmetic — the appearance " +
                             "owner is gone and every purchased cosmetic is inert again.");
                return;
            }
            if (owners.Count > 1)
            {
                failures.Add("[one-owner] " + owners.Count + " types apply cosmetic appearance (" +
                             string.Join(", ", owners.ToArray()) + "). CLAUDE.md §7: ONE appearance owner. " +
                             "Two owners is how a thing ends up half-skinned with no single place to fix it.");
                return;
            }
            notes.Add("[one-owner] " + owners[0]);
        }

        // ── Rule 4 — the authored meshPath is actually parsed ─────────────────

        private static void CheckMeshPathParsed(List<string> failures, List<string> notes)
        {
            var field = typeof(CosmeticDef).GetField("MeshPath", BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                failures.Add("[meshPath] CosmeticDef has no MeshPath field. cosmetics.json AUTHORS `meshPath` " +
                             "(the pet-aether-twilight row) — with no field, Newtonsoft parses the row and " +
                             "throws the key away, and every consumer has to re-invent the path.");
                return;
            }

            CosmeticDef def = null;
            try { def = CosmeticCatalog.Find("pet-aether-twilight"); }
            catch (Exception ex)
            {
                // NOT a stand-down: a catalog that THROWS is a broken dependency, not an absent
                // optional one. Rule 6b already reads CosmeticCatalog.All, so a catalog that cannot
                // load takes the whole suite's meaning with it. Name what threw and go red.
                failures.Add("[meshPath] CosmeticCatalog.Find(\"pet-aether-twilight\") threw " +
                             ex.GetType().Name + ": " + ex.Message + ". The catalog is the dependency " +
                             "this rule and rule 6b are both measured through — a throwing catalog " +
                             "cannot be reported as a pass.");
                return;
            }

            if (def == null)
            {
                // NOT a stand-down either. The row SHIPS in both authored copies of cosmetics.json
                // (Resources/Data/Canonical + StreamingAssets/Data/Canonical), so its absence from the
                // LOADED catalog is drift — either the id moved or the load path stopped reaching the
                // file — and that is exactly the thing this rule exists to notice.
                failures.Add("[meshPath] 'pet-aether-twilight' is NOT in the loaded catalog, yet the row " +
                             "is authored in cosmetics.json (both copies: Resources/Data/Canonical and " +
                             "StreamingAssets/Data/Canonical). Either the id was renamed or the catalog " +
                             "load path no longer reaches the file; the meshPath round-trip is unproven.");
                return;
            }

            if (string.IsNullOrEmpty(def.MeshPath))
            {
                failures.Add("[meshPath] 'pet-aether-twilight' loaded but its MeshPath is empty — the field " +
                             "exists yet the authored value is not surviving the parse. Check the JsonProperty " +
                             "name against the key in cosmetics.json (both copies: StreamingAssets AND Resources).");
                return;
            }

            notes.Add("[meshPath] round-trips as '" + def.MeshPath + "'");
        }

        // ── Rule 5 — the pet folder constant and PetDeployer's literal agree ──

        private static void CheckFolderAgreement(List<string> failures, List<string> notes)
        {
            string folder = CosmeticApplier.ResourceFolderFor("pet");
            if (string.IsNullOrEmpty(folder))
            {
                failures.Add("[folders] CosmeticApplier.ResourceFolderFor(\"pet\") returned nothing.");
                return;
            }

            string pet = CodeText(PetDeployerPath);
            if (pet == null)
            {
                // WAS A HOLLOW PASS (the RegressionMarkerRegression RULE 4 ratchet caught it): a
                // missing PetDeployer.cs made this rule assert NOTHING while the suite still reported
                // COSMETIC_APPLY_OK — and rule 5 exists precisely because these two constants live in
                // assemblies that cannot reference each other, so this gate is the ONLY thing holding
                // them together. A tracked source file at a hardcoded path is never an optional
                // dependency; if it is gone, the seam moved and that is the finding. Fail, naming it
                // — the same treatment [seam] and [village] already give a missing file.
                failures.Add("[folders] " + PetDeployerPath + " not found — the pet mesh loader this rule " +
                             "is measured against is missing or moved, so the folder constant shared with " +
                             "CosmeticApplier.ResourceFolderFor(\"pet\") is UNPROVEN. This cannot be a pass: " +
                             "DeNelle.Pets cannot reference DeNelle.Cosmetics, so nothing else checks it.");
                return;
            }

            // PetDeployer.TryLoadPetMesh builds the key as <folder>/ + equippedId.
            if (pet.IndexOf("\"" + folder + "/\"", StringComparison.Ordinal) < 0 &&
                pet.IndexOf("\"" + folder + "/", StringComparison.Ordinal) < 0)
            {
                failures.Add("[folders] PetDeployer does not load from \"" + folder + "/\" but " +
                             "CosmeticApplier.ResourceFolderFor(\"pet\") says it should. DeNelle.Pets cannot " +
                             "reference DeNelle.Cosmetics, so these two CANNOT be one declaration — this gate " +
                             "is the only thing holding them together. If the folder moved, move BOTH.");
                return;
            }

            notes.Add("[folders] PetDeployer and ResourceFolderFor agree on '" + folder + "'");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// File text with WHOLE-LINE comments dropped, so a source-lint reads CALLS and not the
        /// sentences that describe them. EchoWorldPresenceRegression's first run failed on exactly
        /// this: its rules matched their own removal tombstones and reported a defect against files
        /// that called nothing. Returns null when the file is missing.
        /// </summary>
        // ── Rule 6 — the VILLAGE category reaches a STRUCTURE renderer ────────
        //
        // WO-992 fixed the HERO seam only. Four of the twelve shipped cosmetics are
        // village-category and had NO CONSUMER AT ALL: the applier was category-generic, but
        // nothing ever called Attach(host, "village", ...) at a structure body owner, so
        // equipping a building palette the player had PAID FOR changed nothing anywhere. That
        // is the same defect shape as WO-992, one category over, and it survived the WO-992
        // suite precisely because every rule there was hero-shaped.
        //
        // Proven LIVE, and deliberately through a CHILD renderer: a structure root
        // (StructureFactory.Create) owns no renderer of its own — VisualFactory.Skin
        // instantiates the model as a CHILD. An applier that only ever looked at its own
        // GameObject would pass every hero test and decorate nothing on a building.
        private static void CheckVillageReachesStructure(List<string> failures, List<string> notes)
        {
            // 6a — the seam exists in the STRUCTURE body owner (source-lint, comments dropped).
            string factory = CodeText(StructureFactoryPath);
            if (factory == null)
            {
                failures.Add("[village] " + StructureFactoryPath + " not found — cannot prove a structure " +
                             "installs an applier.");
            }
            else
            {
                bool attaches = factory.IndexOf("CosmeticApplier.Attach", StringComparison.Ordinal) >= 0;
                bool bindsVillage = factory.IndexOf("\"village\"", StringComparison.Ordinal) >= 0;
                if (!attaches || !bindsVillage)
                    failures.Add("[village] StructureFactory no longer installs a village-bound applier " +
                                 "(CosmeticApplier.Attach=" + attaches + ", \"village\" literal=" + bindsVillage +
                                 "). Without it the village cosmetics the player BOUGHT are invisible — the " +
                                 "WO-992 defect, one category over. (Comments are stripped, so a tombstone " +
                                 "will not satisfy this.)");
                else
                    notes.Add("[village] StructureFactory installs the village-bound applier");

                if (factory.IndexOf("CosmeticApplier.RefreshOn", StringComparison.Ordinal) < 0)
                    failures.Add("[village] StructureFactory.ReskinForLevel no longer calls " +
                                 "CosmeticApplier.RefreshOn. A tier upgrade DESTROYS the decorated renderers " +
                                 "and skins new ones, so without the re-drive the paid-for palette is silently " +
                                 "stripped the first time the player upgrades a building.");
                else
                    notes.Add("[village] the tier reskin re-drives the applier");
            }

            // 6b — the factory's member mapping still names members the CATALOG actually ships.
            // Asserted against StructureFactory's OWN mapper, never a copy of its table.
            try
            {
                string member = DeNelle.Village.StructureFactory.VillageCosmeticMemberFor("forge");
                if (string.IsNullOrEmpty(member))
                {
                    failures.Add("[village] DeNelle.Village.StructureFactory.VillageCosmeticMemberFor(\"forge\") returned no " +
                                 "member — a plain trade building maps to no village cosmetic, so no building " +
                                 "would ever be decorated.");
                }
                else
                {
                    bool catalogHasIt = false;
                    foreach (var c in CosmeticCatalog.All)
                    {
                        if (c == null) continue;
                        if (string.Equals(c.Category, "village", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(c.AppliesTo, member, StringComparison.OrdinalIgnoreCase))
                        { catalogHasIt = true; break; }
                    }
                    if (!catalogHasIt)
                        failures.Add("[village] StructureFactory maps buildings to village cosmetic member '" +
                                     member + "', but cosmetics.json ships NO village cosmetic with that " +
                                     "appliesTo. The mapping and the catalog have drifted, so every village " +
                                     "cosmetic silently fails its AppliesTo match and decorates nothing.");
                    else
                        notes.Add("[village] factory member '" + member + "' is shipped by cosmetics.json");
                }
            }
            catch (Exception ex)
            {
                failures.Add("[village] member-mapping check threw " + ex.GetType().Name + ": " + ex.Message);
            }

            // 6c — LIVE: a village cosmetic's colour lands on a structure's CHILD renderer.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                // DECLARED stand-down — harness capability (see rule 1's twin). 6a and 6b above have
                // already asserted; only the LIVE half stands down, which is what PartialSkip says.
                notes.Add(RegressionOutcome.PartialSkip("[village] live structure-renderer proof",
                          "no URP/Lit, Standard or Sprites/Default shader resolved in this editor " +
                          "session, so no structure renderer could be built"));
                return;
            }

            GameObject root = null;
            GameObject visual = null;
            Material mat = null;
            try
            {
                // Mirror the real shape: an EMPTY root (no renderer) with the skinned model as a child.
                root = new GameObject("CosmeticApplyRegression_StructureRoot") { hideFlags = HideFlags.HideAndDontSave };
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "StructureVisual(Clone)";
                visual.hideFlags = HideFlags.HideAndDontSave;
                visual.transform.SetParent(root.transform, false);

                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    failures.Add("[village] a primitive Cube produced no MeshRenderer — the harness itself " +
                                 "is broken, not the applier.");
                    return;
                }

                mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                renderer.sharedMaterial = mat;

                string prop = mat.HasProperty("_BaseColor") ? "_BaseColor"
                            : mat.HasProperty("_Color") ? "_Color"
                            : null;
                if (prop == null)
                {
                    // DECLARED stand-down — harness capability, never a green pass.
                    notes.Add(RegressionOutcome.PartialSkip("[village] live structure-renderer proof",
                              "shader '" + shader.name + "' exposes neither _BaseColor nor _Color, so a " +
                              "tint cannot be proven through it"));
                    return;
                }

                string member = DeNelle.Village.StructureFactory.VillageCosmeticMemberFor("forge");
                if (string.IsNullOrEmpty(member))
                {
                    // 6b ALREADY recorded a failure for this exact condition, so the suite is red
                    // either way — this is a stand-down of the live half only, and it says so rather
                    // than returning silently and looking like the check ran.
                    notes.Add(RegressionOutcome.PartialSkip("[village] live structure-renderer proof",
                              "VillageCosmeticMemberFor(\"forge\") returned no member — 6b has already " +
                              "failed on that; the live proof cannot be built without a member to bind"));
                    return;
                }

                // A colour nothing else in the project uses, so a pass cannot be a coincidence.
                // No meshPath on purpose: NO village cosmetic art is staged, so this exercises the
                // preview-tint path every village cosmetic takes today. Rule 6 is about REACHING the
                // structure renderer — judging the look is the owner's, from a screenshot.
                var def = new CosmeticDef
                {
                    Id           = "regression-probe-village",
                    Category     = "village",
                    AppliesTo    = member,
                    DisplayName  = "Regression Probe Village",
                    UnlockMethod = "achievement",
                    PreviewColor = "#65431F",
                };
                Color want = def.PreviewUnityColor;

                var applier = root.AddComponent<CosmeticApplier>();
                applier.Bind("village", member);
                applier.allowPreviewTintFallback = true;
                applier.ApplyCosmetic(def);

                if (applier.DecoratedRendererCount == 0)
                {
                    failures.Add("[village] the applier resolved ZERO renderers on a structure root whose " +
                                 "VISUAL IS A CHILD. Structures never carry a renderer on the root — this is " +
                                 "the signature of an applier that only inspects its own GameObject, and it " +
                                 "means an equipped building palette reaches nothing the player sees.");
                    return;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Color got = block.GetColor(prop);

                bool took = Mathf.Approximately(got.r, want.r)
                         && Mathf.Approximately(got.g, want.g)
                         && Mathf.Approximately(got.b, want.b);

                if (!took)
                {
                    failures.Add("[village] applying village cosmetic '" + def.Id + "' (appliesTo '" + member +
                                 "') left the structure renderer's " + prop + " at " + got + ", not the " +
                                 "cosmetic's " + want + ". The equip state changed and the BUILDING DID NOT.");
                    return;
                }

                applier.ResetToDefault();
                var after = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(after);
                Color cleared = after.GetColor(prop);
                if (Mathf.Approximately(cleared.r, want.r)
                 && Mathf.Approximately(cleared.g, want.g)
                 && Mathf.Approximately(cleared.b, want.b))
                {
                    failures.Add("[village] ResetToDefault left the structure renderer's " + prop + " at the " +
                                 "cosmetic colour " + want + " — unequipping a building palette would not remove it.");
                    return;
                }

                notes.Add("[village] live proof: village cosmetic colour " + want + " read back off a " +
                          "structure CHILD renderer via " + prop + " (" + applier.DecoratedRendererCount +
                          " renderer(s)), and cleared on reset");
            }
            catch (Exception ex)
            {
                failures.Add("[village] threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (mat != null) UnityEngine.Object.DestroyImmediate(mat);
            }
        }

        private static string CodeText(string projectRelativePath)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? "",
                                       projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) return null;

            string[] lines;
            try { lines = File.ReadAllLines(full); }
            catch { return null; }

            var sb = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                if (t.StartsWith("*", StringComparison.Ordinal)) continue;
                if (t.StartsWith("/*", StringComparison.Ordinal)) continue;
                sb.AppendLine(line);
            }
            return sb.ToString();
        }
    }
}
