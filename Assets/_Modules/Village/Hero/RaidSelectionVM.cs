// =============================================================================
// RaidSelectionVM — the pure ViewModel behind RaidSelectionScreen (the raid grid).
// Strict-MVVM migration Silo D.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Owns the SceneConfigCatalog projection: the FOUR raid targets (fallback to all
// enemy raids) as ItemVM cards + per-id helpers (difficulty / target time / reward
// hint / description / unlockVictories). The View (RaidSelectionScreen) binds this,
// renders the card grid from vm.Raids + the helpers, and routes a card tap through
// vm.DefFor(id) to open the deploy screen — it never touches the gameplay catalog.
//
// 2026-09-04 — THE ESCALATION GATE (economy map §4). Before today this VM hard-listed
// three ids and emitted EVERY card unlocked at EVERY victory count: ItemVM has always
// carried Locked + LockReason and the VM passed neither, so "upgrade -> unlock a harder
// raid" had no reader. It now compares each def's authored unlockVictories (0/3/10/20)
// against an INJECTED victory count, and refuses a target whose scene this build cannot
// load. Names/copy: docs/CREATIVE_CANON_ELARION_2026-09-04.md §3.
//
// SEPARATE from RaidDeployVM by design (different domain: this is the browse grid,
// that is the pre-raid deploy math). They only share the SceneConfigDef formatting.
// PURE C#: no UnityEngine UI types; unit-testable over a fake def list (§2c).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village;

namespace DeNelle.Village.Hero
{
    /// <summary>Pure ViewModel for the Raids-tab card grid.</summary>
    public sealed class RaidSelectionVM : IPanelViewModel, IDisposable
    {
        /// <summary>Icon role key on each raid card (the View maps it to art; no game state).</summary>
        public const string IconRoleRaid = "raid";

        // The FOUR raid targets, in escalation order (mirrors the View's grid).
        //
        // - THESE ARE LIVE SAVE KEYS. RaidClaimService persists PlayerPrefs
        // "dotr-raid-owner-<id>" / "dotr-raid-crystalday-<id>" keyed on exactly these
        // strings (RaidClaimService.cs:53,62). NEVER rename one to match a display name:
        // the creative canon (docs/CREATIVE_CANON_ELARION_2026-09-04.md §3) renames the
        // CARD, via scene-configs.json displayName, and says so in its own rule.
        //
        // iron_bastion joined the list on 2026-09-04 (economy map §4, tier 4). Its scene
        // was baked 2026-08-21 and was reachable by nothing - see the availability gate
        // below and the ORPHAN note deleted in the same commit.
        private static readonly string[] FlagshipRaidIds =
        {
            "raider_camp_small",
            "fortified_garrison",
            "mage_enclave",
            "iron_bastion",
        };

        /// <summary>
        /// THE ESCALATION INPUT: how many raid victories the player has banked. Compared
        /// against each def's authored <c>unlockVictories</c> (0 / 3 / 10 / 20, economy map §4).
        ///
        /// <para>WHY A PROVIDER AND NOT A DIRECT READ. At git HEAD this session NO victory
        /// counter existed anywhere in the tree (grepped raidsWon / raidVictories / victoryCount /
        /// totalRaidVictories across Assets/ and api/: zero hits; RaidClaimService persists only
        /// per-camp one-time flags, never a total). The counter is a SIBLING LANE's file in this
        /// same release, so this VM neither invents a PlayerPrefs key nor guesses a field name -
        /// it reads through ONE injectable seam and stays pure C#.</para>
        ///
        /// <para>MEASURED IN THE WORKING TREE 2026-09-04: that lane landed
        /// <c>GameState.RaidVictories</c> (GameState.cs:629), incremented by
        /// RaidVictoryController.RecordVictory with a one-shot backfill for older saves.
        /// RaidSelectionScreen.OpenInternal wires this provider to it, and that is the ONLY
        /// wiring site. Unwired (headless, EditMode, a stateless probe) the default is 0, which
        /// locks the gated tiers VISIBLY with a reason - never silently open.</para>
        /// </summary>
        public static Func<int> VictoryCountProvider;

        /// <summary>
        /// Second gate: can this def's scene actually be LOADED in this build? Injected so the
        /// VM stays pure C# (no UnityEngine); <see cref="CreateDefault"/> wires it to
        /// <c>SceneRouter.IsSceneInBuild</c>. Null = assume every scene is loadable, which is
        /// what the EditMode tests and headless projections want.
        ///
        /// <para>THIS EXISTS BECAUSE OF A MEASURED HOLE, not as defensive decoration.
        /// RaidBase_IronBastion.unity bakes 127 GameObjects and NO
        /// HeroStartPoint_PlayerSpawn marker (measured 2026-09-04 against
        /// RaidBase_mage_enclave's 270 GameObjects + 1 marker). HeroControlEnsurer seats the
        /// carried hero at that marker, so entering that scene today strands the hero at its
        /// TOWN world pose. The scene is therefore registered in Build Settings DISABLED,
        /// Application.CanStreamedLevelBeLoaded returns false for it, and this predicate turns
        /// that into a locked card with a sentence instead of a dead tap.</para>
        /// </summary>
        public static Func<string, bool> SceneAvailableProvider;

        private readonly List<SceneConfigDef> _defs = new List<SceneConfigDef>();
        private readonly List<ItemVM> _raids = new List<ItemVM>();
        private readonly Dictionary<string, SceneConfigDef> _byId =
            new Dictionary<string, SceneConfigDef>(StringComparer.OrdinalIgnoreCase);
        private readonly Action _onClose;
        private readonly int _victories;
        private readonly Func<string, bool> _sceneAvailable;
        private bool _disposed;

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "RAIDS";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>One card per raid (Name = raw displayName, may be empty — the View
        /// falls back to a spaced id; Locked + LockReason carry the escalation gate).
        /// Never null.</summary>
        public IReadOnlyList<ItemVM> Raids => _raids;

        /// <summary>The raw SceneConfigDef for a card id (the View forwards it to the deploy
        /// screen so it never re-pulls the catalog itself), or null.</summary>
        public SceneConfigDef DefFor(string id) =>
            id != null && _byId.TryGetValue(id, out var d) ? d : null;

        // Per-card presentation inputs (raw values; the View formats colour/time/hint).
        public string DifficultyFor(string id) { var d = DefFor(id); return d != null ? d.difficulty : null; }
        public float TargetTimeFor(string id) { var d = DefFor(id); return d != null ? d.recommendedClearTime : 0f; }
        public float RewardMultiplierFor(string id) { var d = DefFor(id); return d != null ? d.rewardMultiplier : 1f; }
        public float ShardChanceFor(string id) { var d = DefFor(id); return d != null ? d.shardDropChance : 0f; }
        /// <summary>The creative canon's one-line card copy for this target (may be null/empty).</summary>
        public string DescriptionFor(string id) { var d = DefFor(id); return d != null ? d.description : null; }
        /// <summary>Authored victory threshold for this target (0 = always available).</summary>
        public int UnlockVictoriesFor(string id) { var d = DefFor(id); return d != null ? d.unlockVictories : 0; }

        /// <summary>
        /// WO-1389 pressure point 4 - what the wins BUY, before the player can enter: the scout
        /// line for a card, "Iron walls . 15 defenders" (wall tier + garrison headcount from the
        /// def, the same two facts RaidDeployVM's scout report opens with). Null when the def
        /// authors neither, so an unauthored row paints nothing. Pure string work.
        /// </summary>
        public string ScoutLineFor(string id) => ScoutLine(DefFor(id));

        /// <summary>
        /// WO-1389 - the NEXT camp the ladder has not yet opened for <paramref name="victories"/>
        /// banked wins: the first FLAGSHIP def (the same ordered list CreateDefault renders) whose
        /// authored unlockVictories exceeds the count. Null when every camp is open (the ladder is
        /// climbed) or the catalog resolves nothing - the post-raid dialogue then drops its camp
        /// sentence rather than inventing one. ONE resolution site, shared by the dialogue text
        /// tokens (PostRaidBeatTokens) and the regression oracle, so the card and the sentence can
        /// never name different camps.
        /// </summary>
        public static SceneConfigDef NextLockedCamp(int victories)
        {
            if (victories < 0) victories = 0;
            SceneConfigDef best = null;
            foreach (var id in FlagshipRaidIds)
            {
                var def = SceneConfigCatalog.Find(id);
                if (def == null)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "NextLockedCamp: flagship raid id '" + id + "' does not resolve in scene-configs.json - skipped.");
                    continue;
                }
                if (def.unlockVictories <= victories) continue;
                if (best == null || def.unlockVictories < best.unlockVictories) best = def;
            }
            DeNelle.Core.Diagnostics.FlowTrace.Step("Raid", "NextLockedCamp(victories=" + victories + ") -> " +
                (best != null ? "'" + best.id + "' at " + best.unlockVictories + " wins" : "<none - ladder climbed>"));
            return best;
        }

        /// <summary>Static composer so the dialogue token and the oracle read the SAME sentence.</summary>
        public static string ScoutLine(SceneConfigDef d)
        {
            if (d == null) return null;
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(d.wallTier)) parts.Add(SpaceCamelCase(d.wallTier) + " walls");
            int defenders = GarrisonCount(d);
            if (defenders > 0) parts.Add(defenders + (defenders == 1 ? " defender" : " defenders"));
            return parts.Count == 0 ? null : string.Join(" . ", parts);
        }

        /// <summary>Garrison headcount authored on a def (sum of composition counts), 0 when none.</summary>
        public static int GarrisonCount(SceneConfigDef d)
        {
            if (d == null || d.garrison == null || d.garrison.composition == null) return 0;
            int n = 0;
            foreach (var u in d.garrison.composition)
                if (u != null && u.count > 0) n += u.count;
            return n;
        }

        /// <summary>"ReinforcedSteel" -> "Reinforced Steel" (mirrors RaidDeployVM; no regex).</summary>
        private static string SpaceCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        // ── Construction / resolution ───────────────────────────────────────────

        /// <summary>The ONLY resolution site: pulls the flagship raids (fallback to all
        /// enemy raids) from <see cref="SceneConfigCatalog"/> so the View never touches it.</summary>
        public static RaidSelectionVM CreateDefault(Action onClose = null)
        {
            var list = new List<SceneConfigDef>();
            foreach (var id in FlagshipRaidIds)
            {
                var def = SceneConfigCatalog.Find(id);
                if (def != null) list.Add(def);
            }
            if (list.Count == 0)
                foreach (var def in SceneConfigCatalog.All)
                    if (def != null && def.IsEnemy) list.Add(def);

            int victories = 0;
            var provider = VictoryCountProvider;
            if (provider != null)
            {
                // Guarded: a provider fault must never blank the raid grid, and must never be
                // swallowed without a log (CLAUDE.md §12).
                try { victories = provider(); }
                catch (Exception ex)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "RaidSelectionVM: VictoryCountProvider threw (" + ex.GetType().Name + ": " +
                        ex.Message + ") - treating the player as 0 victories, so every gated camp " +
                        "shows LOCKED with its reason rather than silently unlocking.");
                    victories = 0;
                }
            }
            else
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "RaidSelectionVM: no VictoryCountProvider wired - the raid-victory counter is " +
                    "another lane's file in this release. Treating the player as 0 victories, so " +
                    "camps gated above 0 read LOCKED with their reason. Wire it with " +
                    "RaidSelectionVM.VictoryCountProvider = () => theirCounter.");
            }

            return new RaidSelectionVM(list, onClose, victories, SceneAvailableProvider);
        }

        public RaidSelectionVM(IReadOnlyList<SceneConfigDef> defs, Action onClose,
                               int victories = 0, Func<string, bool> sceneAvailable = null)
        {
            _onClose = onClose;
            _victories = victories < 0 ? 0 : victories;
            _sceneAvailable = sceneAvailable;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null) continue;
                    _defs.Add(d);
                    if (!string.IsNullOrEmpty(d.id)) _byId[d.id] = d;
                }
            Rebuild();
        }

        /// <summary>Banked raid victories this projection was built against (read-only, for traces).</summary>
        public int Victories => _victories;

        /// <summary>True when this card is gated shut. Mirrors the ItemVM the View renders.</summary>
        public bool IsLocked(string id)
        {
            var d = DefFor(id);
            return d != null && ResolveLock(d) != null;
        }

        /// <summary>The player-facing sentence for a locked card, or null when it is open.</summary>
        public string LockReasonFor(string id)
        {
            var d = DefFor(id);
            return d != null ? ResolveLock(d) : null;
        }

        /// <summary>
        /// THE ONE LOCK RESOLVER - returns the player-facing reason, or null when the card is open.
        ///
        /// <para>Order matters and is deliberate: the PROGRESSION gate is checked first, so a
        /// player who has not earned a target is told to go earn it (the actionable sentence)
        /// rather than told the expedition is not ready (true, but nothing they can do).
        /// Availability is the fallback for a target the player HAS earned whose scene this
        /// build cannot load.</para>
        ///
        /// <para>NEVER A BARE "Locked". Both sentences name the missing thing AND the remedy,
        /// and both stand on their own in greyscale - the owner is red/green colourblind, so the
        /// state is carried by the WORDS (the same law RaidCooldownService's copy follows).
        /// Voice: docs/CREATIVE_CANON_ELARION_2026-09-04.md §0/§3. ASCII only.</para>
        /// </summary>
        private string ResolveLock(SceneConfigDef d)
        {
            int need = d.unlockVictories;
            if (need > 0 && _victories < need)
            {
                int remaining = need - _victories;
                return "The Heart cannot reach this far yet - win " + remaining +
                       (remaining == 1 ? " more raid" : " more raids") + " to press on.";
            }

            var avail = _sceneAvailable;
            if (avail != null && !string.IsNullOrEmpty(d.sceneName))
            {
                bool ok;
                try { ok = avail(d.sceneName); }
                catch (Exception ex)
                {
                    // Never swallow (CLAUDE.md §12). Fail OPEN on a probe fault: SceneRouter's own
                    // IsSceneRegistered gate still refuses an unloadable scene, so the worst case
                    // is a refusal one screen later - strictly better than hiding an earned target.
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "RaidSelectionVM: scene-availability probe threw for '" + d.sceneName + "' (" +
                        ex.GetType().Name + ": " + ex.Message + ") - treating it as available.");
                    ok = true;
                }
                if (!ok)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "RaidSelectionVM: '" + d.id + "' is EARNED (" + _victories + " of " + need +
                        " victories) but its scene '" + d.sceneName + "' cannot be loaded in this " +
                        "build - the card stays locked with a sentence instead of dead-ending the " +
                        "player. Register the scene ENABLED in Build Settings once it bakes a " +
                        "HeroStartPoint_PlayerSpawn marker.");
                    return "The Heart remembers no fortress here. This expedition is not ready.";
                }
            }

            return null;
        }

        private void Rebuild()
        {
            _raids.Clear();
            foreach (var d in _defs)
            {
                if (d == null) continue;
                // Name carries the RAW displayName (may be empty); the View falls back to a
                // kit-spaced id so the VM never references the presentation kit.
                string name = string.IsNullOrEmpty(d.displayName) ? "" : d.displayName;
                string lockReason = ResolveLock(d);
                // ItemVM has ALWAYS carried Locked + LockReason (ItemVM.cs:32,35); this VM passed
                // neither, so every card shipped UNLOCKED at every victory count. Named args
                // because both sit behind rarity/equipped in the positional list.
                _raids.Add(new ItemVM(d.id, name, IconRoleRaid, d.id, 0, "", true,
                                      rarity: null, equipped: false,
                                      locked: lockReason != null, lockReason: lockReason));
            }

            DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                "RaidSelectionVM projected " + _raids.Count + " raid card(s) at " + _victories +
                " victories; locked=" + CountLocked() + ".");
        }

        private int CountLocked()
        {
            int n = 0;
            for (int i = 0; i < _raids.Count; i++) if (_raids[i].Locked) n++;
            return n;
        }
    }
}
