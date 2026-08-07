// =============================================================================
// TownBankCapacity -- THE town bank cap (WO-857 / WO-901 Phase F). ONE reader.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Economy
//
// WHAT THIS IS
//   The single authority for "how much of resource R can the town hold, and how is
//   what it holds distributed across its containers". Every system that needs a cap,
//   a headroom check, or a per-container fill level asks THIS class. Nothing else
//   computes capacity -- scattering cap lookups is exactly how two ceilings disagree.
//
//   Max(R) = baseCap(R) + sum( storageCapacity of BUILT containers whose
//                              repo.storageResource == R, scaled by placed level )
//
//   This wires the seam RepoProps.cs:155 (storageCapacity) + :174 (IsStorageContainer)
//   have carried with ZERO consumers since WO-707 -- :169 literally reads
//   "TODO(WO-707/WO-672): wire this seam".
//
// THE FOUR LAWS THIS FILE ENFORCES STRUCTURALLY (not by comment, not by hope)
//
//   1. CRYSTALS AND COINS ARE UNCAPPED, BY DESIGN. Owner ruling 2026-08-04 (WO-901
//      Sec.6): premium/bottleneck currency is never storage-gated -- the CoC precedent
//      is gems uncapped, gold/elixir capped by storages. The exemption is the named
//      constant UncappableResources below; storage-caps.json CANNOT add crystals or
//      coins to the capped set (an authored key is ignored with a warn), and
//      TownBankCapRegression case [no-crystal-cap] FAILS if a crystal cap ever appears.
//
//   2. A CAP CAN NEVER RESOLVE TO ZERO. BaseCapOf floors every answer at
//      AbsoluteMinBaseCap regardless of what the data says, so a missing / truncated /
//      zeroed storage-caps.json degrades to a playable wallet instead of a save whose
//      every grant clamps to nothing. Guarded structurally, not by trusting a number.
//
//   3. A SPEND IS NEVER UPPER-CLAMPED. ClampGrant refuses to touch a non-positive
//      request and returns it unchanged. The cap is a ceiling on INCOME; the existing
//      Mathf.Max(0, ...) floors stay the only bound on outgo.
//
//   5. A PURCHASED OR PROMISED QUANTITY IS NEVER CLAMPED. The owner's clamp-and-warn
//      ruling was about the ECONOMY -- what the player EARNS. A pack that advertises
//      5,000 food and delivers 1,920 is not balance, it is selling something and not
//      delivering it. The exemption axis is the named enum BankGrantKind + IsClampable,
//      never an incidental code path, and TownBankCapRegression case
//      [purchased-grant-never-clamped] fails the build if a paid grant is ever clamped.
//
//   4. AN EXISTING SAVE OVER THE CAP IS GRANDFATHERED, NEVER DRAINED. Nothing here
//      writes a wallet. Over-cap totals are reported as Apportionment.Overflow and are
//      spent down normally; RoomFor simply reads 0 until the total falls under Max.
//      Retroactively deleting a player's resources on load is not a cap, it is a bug.
//
// THE FILL / DRAIN ORDER (owner ruling 2026-08-04)
//   "By capacity. Fill smallest first, so pallets drain last."
//
//   MECHANIC. Containers are ordered by CAPACITY ASCENDING -- never by current contents,
//   because capacity only moves when a building is placed or upgraded, so the order is
//   stable and the props cannot flicker frame to frame as balances move. Occupancy is a
//   PURE FUNCTION of the single authoritative total: fill each container to its capacity
//   in that order until the total is exhausted. DRAINING IS THE SAME FUNCTION evaluated at
//   a lower total -- there is no separate drain path, no per-container balance, and
//   therefore nothing that can disagree with the wallet.
//
//   THE CONSEQUENCE, STATED EXACTLY, because it is easy to get backwards: under a pure
//   ascending fill the SMALLEST container fills FIRST and therefore empties LAST (it only
//   gives anything up once everything above it is already empty), and the LARGEST fills
//   LAST and empties FIRST. "Fill smallest first, SO pallets drain last" is one sentence,
//   not two rules: it holds precisely when the PALLETS ARE THE SMALLER CONTAINERS.
//
//   THE DATA DEPENDENCY THAT MAKES THE OWNER'S OUTCOME TRUE. The pallets stay visibly
//   stocked while the abstract base store churns ONLY while every container's capacity at
//   its MAX level stays BELOW baseCap. Today: baseCap 2000 vs containers 500 x [1,2,3] =
//   500/1000/1500. Raising structures-catalog storageCapacity above baseCap would silently
//   invert the look -- the pallets would start draining first. TownBankCapRegression case
//   [order-intent-pallets-last] FAILS if that inversion is ever authored, so the flip can
//   never ship as a visible bug the owner has to spot on the props.
//
//   ** THIS IS NOT A PER-CONTAINER WALLET. ** GameState.Resources / GameState.Wood /
//   GameState.Iron remain the SINGLE authority (WO-842 unified them after two authorities
//   produced the captured "985k can't afford 800"). Occupancy here is DERIVED and stored
//   nowhere.
//
// Sec.12: the cap resolution traces [Flow:Bank] (throttled -- it is a hot path) and every
// clamped grant raises an UNTHROTTLED FlowTrace.Warn naming the resource and the units
// lost, plus an Overflowed event for presentation. The warn is load-bearing: it is the
// only thing between the player and silently vaporised resources (WO-901 Sec.5).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Core.Economy
{
    /// <summary>
    /// WHY a grant is happening. This is the NAMED exemption axis for the town bank cap -- not an
    /// incidental code path, so the rule can be read, tested, and never re-discovered by accident.
    /// <para>The owner's clamp-and-warn ruling (WO-901 §5) was given about the ECONOMY: what the
    /// player EARNS. It was never a ruling about what the player BUYS. A pack that advertises 5,000
    /// food and delivers 1,920 is not a balance decision, it is selling something and not delivering
    /// it -- a refund/chargeback and a store-policy problem, and no toast makes it acceptable.</para>
    /// </summary>
    public enum BankGrantKind
    {
        /// <summary>
        /// EARNED in-game income: wave rewards, collector collects, quest rewards, raid loot, Echo
        /// dumps, offline harvest, kill trickle. CLAMPED AND WARNED -- the owner's ruling, unchanged.
        /// This is the default and every new income path should use it.
        /// </summary>
        EarnedIncome = 0,

        /// <summary>
        /// A quantity the player PAID FOR or was PROMISED AN EXACT NUMBER OF: IAP / pack-store
        /// entitlements, promo-code redemptions, referral payouts, battle-pass tier rewards.
        /// NEVER CLAMPED -- an advertised quantity always arrives in full. Storage pressure is a
        /// gameplay dial; it must never become a mechanism that under-delivers a purchase.
        /// </summary>
        PurchasedOrPromised = 1,

        /// <summary>
        /// Dev tools and the headless AutoPilot fleet funding a gate they are only walking THROUGH.
        /// NEVER CLAMPED. Never reachable from a player-facing path.
        /// </summary>
        DevHarness = 2,
    }

    /// <summary>The town-bank resource axes. Wood/Iron/Food are storage-capped; Crystals and
    /// Coins are UNCAPPED by design (see <see cref="TownBankCapacity.UncappableResources"/>).</summary>
    public enum BankResource
    {
        Wood = 0,
        Iron = 1,
        Food = 2,
        Crystals = 3,
        Coins = 4,
    }

    /// <summary>
    /// One container in the ordered bank. Slot 0 is always the non-building BASE STORE
    /// (the <c>baseCap</c> baseline that exists before any pallet is built); the rest are
    /// built lumberyard / foundry / silo instances. <see cref="Contents"/> and
    /// <see cref="Fill01"/> are DERIVED from the one authoritative wallet total -- they are
    /// not stored anywhere and must never be written back.
    /// </summary>
    public struct StorageSlot
    {
        /// <summary>Catalog id ("lumberyard"), or empty for the base store.</summary>
        public string StructureId;
        /// <summary>Stable per-instance key: "lumberyard@12,-4" (id + grid cell), or "base".
        /// This is what WO-903's pallet fill-stack matches on to find ITS slot.</summary>
        public string InstanceKey;
        /// <summary>Placed upgrade level (1-based). 1 for the base store.</summary>
        public int Level;
        /// <summary>This container's capacity at its level (units).</summary>
        public int Capacity;
        /// <summary>Derived units currently housed here (0..Capacity).</summary>
        public int Contents;
        /// <summary>Derived Contents/Capacity in 0..1 (0 when Capacity is 0).</summary>
        public float Fill01;
        /// <summary>True for the single non-building baseline slot.</summary>
        public bool IsBaseStore;
        /// <summary>Grid cell of the placed structure (0,0 for the base store) -- part of the tie-break key.</summary>
        public int CellX;
        /// <summary>Grid cell of the placed structure (0,0 for the base store) -- part of the tie-break key.</summary>
        public int CellZ;
    }

    /// <summary>A full derived answer for one resource: the authoritative total, the cap, any
    /// grandfathered overflow, and the ordered container occupancy. Pure -- reading it mutates nothing.</summary>
    public struct BankApportionment
    {
        public BankResource Resource;
        /// <summary>The one authoritative wallet total (GameState).</summary>
        public int Total;
        /// <summary>baseCap + sum of built container capacities. int.MaxValue for an uncapped resource.</summary>
        public int Max;
        /// <summary>max(0, Total - sum of container capacities) -- a grandfathered legacy save that
        /// already held more than the cap. NEVER deleted; it drains by being spent.</summary>
        public int Overflow;
        /// <summary>Containers ordered by CAPACITY ASCENDING (the fill AND drain order), tie-broken
        /// deterministically. Never null; always at least the base store.</summary>
        public StorageSlot[] Slots;
    }

    /// <summary>A clamped-grant event: what was asked for, what fit, and what was lost.</summary>
    public struct BankOverflowStatus
    {
        /// <summary>False until the first clamped grant of the session.</summary>
        public bool Available;
        public BankResource Resource;
        /// <summary>Player-facing resource word ("Wood").</summary>
        public string ResourceName;
        /// <summary>Player-facing container to build ("Lumberyard").</summary>
        public string ContainerName;
        public int Requested;
        public int Granted;
        /// <summary>Units LOST to the cap (Requested - Granted). Always &gt; 0 on a real event.</summary>
        public int Lost;
        public int Max;
        /// <summary>Short tag naming the income path that overflowed ("Grant", "OfflineHarvest").</summary>
        public string Source;
        /// <summary>Bumps on every publish -- cheap change-detect for a polling HUD
        /// (the ObsidianQueueGate snapshot pattern).</summary>
        public int Version;
    }

    /// <summary>The ONE reader for town bank capacity. Everything queries this; nothing duplicates it.</summary>
    public static class TownBankCapacity
    {
        // ── Law 2: a cap can never resolve to zero ────────────────────────────
        /// <summary>
        /// The absolute floor under any baseCap, applied AFTER the data is read and regardless of
        /// what it says. The most expensive single structure in structures-catalog costs 160 wood /
        /// 100 iron / 60 food, and the cheapest capacity-raising container (lumberyard) costs
        /// 50 wood + 20 iron -- so a wallet this size can always afford the next step out of any
        /// state. A missing, truncated, or zeroed storage-caps.json therefore degrades to a
        /// PLAYABLE save instead of one where every grant clamps to nothing.
        /// </summary>
        public const int AbsoluteMinBaseCap = 1000;

        // ── Law 1: crystals + coins are uncapped BY DESIGN ────────────────────
        /// <summary>
        /// Owner ruling 2026-08-04 (WO-901 Sec.6): premium / bottleneck currency is NEVER
        /// storage-gated. This is the named constant, not a comment -- <see cref="IsCapped"/> reads
        /// it, storage-caps.json cannot override it, and TownBankCapRegression [no-crystal-cap]
        /// fails the build if a crystal or coin cap is ever introduced.
        /// </summary>
        public static readonly BankResource[] UncappableResources =
        {
            BankResource.Crystals,
            BankResource.Coins,
        };

        /// <summary>The base-store slot key (slot 0 of every apportionment).</summary>
        public const string BaseStoreKey = "base";

        /// <summary>Raised on every clamped grant, immediately after the FlowTrace.Warn. Presentation
        /// subscribes (BankOverflowToastPresenter); gameplay never does.</summary>
        public static event Action<BankOverflowStatus> Overflowed;

        /// <summary>Latest clamped-grant snapshot (Available=false until the first one). Poll this
        /// from the HUD -- the ObsidianQueueGate pattern, no cross-assembly read.</summary>
        public static BankOverflowStatus LastOverflow { get; private set; }

        private static int _overflowVersion;

        // =====================================================================
        //  Resource identity
        // =====================================================================

        /// <summary>
        /// Law 5 -- THE ONE PLACE that decides whether a grant is subject to the cap at all.
        /// Only <see cref="BankGrantKind.EarnedIncome"/> is clamped. A purchased or promised
        /// quantity, and dev/harness funding, always land in full.
        /// <para>Pinned by TownBankCapRegression case [purchased-grant-never-clamped]: if this
        /// ever starts returning true for <see cref="BankGrantKind.PurchasedOrPromised"/>, a store
        /// pack can silently under-deliver what the player paid for and the build fails.</para>
        /// </summary>
        public static bool IsClampable(BankGrantKind kind) => kind == BankGrantKind.EarnedIncome;

        /// <summary>
        /// Is this structure a STORAGE CONTAINER (the owner's "pallets" - lumberyard / foundry /
        /// silo)? The one place outside this file that may ask.
        /// -------------------------------------------------------------------------------
        /// Added 2026-08-07 because the first-build 5s grace needs the owner's carve-out ("other
        /// than the pallets") and reached for repo.IsStorageContainer directly - which the
        /// [one-reader] guard correctly flagged. That guard exists so capacity math cannot be
        /// re-derived in two places and disagree.
        ///
        /// This is a CLASSIFICATION passthrough, not capacity math - it answers "is this a
        /// container", never "how much does it hold". Routing it here keeps the raw seam
        /// (repo.storageCapacity / repo.IsStorageContainer) read in exactly one file while letting
        /// callers ask the question they actually have. A future container needs no caller change.
        /// </summary>
        public static bool IsStorageContainer(DeNelle.Core.Catalog.RepoProps repo)
            => repo != null && repo.IsStorageContainer;

        /// <summary>True when this resource has a storage ceiling at all. False for Crystals/Coins,
        /// unconditionally and un-overridably (Law 1).</summary>
        public static bool IsCapped(BankResource r)
        {
            for (int i = 0; i < UncappableResources.Length; i++)
                if (UncappableResources[i] == r) return false;
            return true;
        }

        /// <summary>The lowercase catalog word for a resource -- the same token
        /// structures-catalog authors in <c>repo.storageResource</c>.</summary>
        public static string WordOf(BankResource r)
        {
            switch (r)
            {
                case BankResource.Wood:     return "wood";
                case BankResource.Iron:     return "iron";
                case BankResource.Food:     return "food";
                case BankResource.Crystals: return "crystals";
                case BankResource.Coins:    return "coins";
            }
            return "";
        }

        /// <summary>Parse a catalog/save resource word ("wood", "iron", "food", "crystals",
        /// "coins") into a BankResource. Case-insensitive; false when unknown or empty.</summary>
        public static bool TryParseResource(string word, out BankResource r)
        {
            r = BankResource.Wood;
            if (string.IsNullOrEmpty(word)) return false;
            switch (word.Trim().ToLowerInvariant())
            {
                case "wood":          r = BankResource.Wood;     return true;
                case "iron":          r = BankResource.Iron;     return true;
                case "food":          r = BankResource.Food;     return true;
                case "grain":         r = BankResource.Food;     return true;
                case "crystal":
                case "crystals":
                case "aethercrystal": r = BankResource.Crystals; return true;
                case "coin":
                case "coins":
                case "gold":          r = BankResource.Coins;    return true;
            }
            return false;
        }

        /// <summary>Player-facing resource name (ASCII, text-encoded state -- never colour alone).</summary>
        public static string DisplayName(BankResource r)
        {
            switch (r)
            {
                case BankResource.Wood:     return "Wood";
                case BankResource.Iron:     return "Iron";
                case BankResource.Food:     return "Food";
                case BankResource.Crystals: return "Crystals";
                case BankResource.Coins:    return "Gold";
            }
            return "Resource";
        }

        /// <summary>
        /// The player-facing container that raises this resource's cap, resolved FROM THE CATALOG
        /// (the first row whose repo.storageResource matches) so a data rename never strands the
        /// copy. Falls back to the generic word rather than a wrong building name.
        /// </summary>
        public static string ContainerNameFor(BankResource r)
        {
            var entries = CatalogRegistry.All();
            if (entries != null)
            {
                string want = WordOf(r);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var repo = e != null ? e.repo : null;
                    if (repo == null || !repo.IsStorageContainer) continue;
                    if (!string.Equals(repo.storageResource, want, StringComparison.OrdinalIgnoreCase)) continue;
                    return !string.IsNullOrEmpty(e.displayName) ? e.displayName : e.id;
                }
            }
            return "storage building";
        }

        // =====================================================================
        //  Caps
        // =====================================================================

        /// <summary>
        /// The baseline cap before any container is built. Reads storage-caps.json and then applies
        /// <see cref="AbsoluteMinBaseCap"/> as a hard floor (Law 2). Returns int.MaxValue for an
        /// uncapped resource so callers that forget to check <see cref="IsCapped"/> still cannot
        /// accidentally clamp crystals.
        /// </summary>
        public static int BaseCapOf(BankResource r)
        {
            if (!IsCapped(r)) return int.MaxValue;
            int authored = StorageCapsCatalog.RawBaseCap(WordOf(r));
            if (authored < AbsoluteMinBaseCap)
            {
                FlowTrace.Throttle("Bank", "basecap-floor-" + WordOf(r), 60f,
                    $"baseCap for {WordOf(r)} authored as {authored} -- FLOORED to AbsoluteMinBaseCap {AbsoluteMinBaseCap} " +
                    "(a cap of zero would clamp every grant to nothing and soft-lock the save).");
                return AbsoluteMinBaseCap;
            }
            return authored;
        }

        /// <summary>A container's capacity at a placed level (level is 1-based and clamped &gt;= 1).</summary>
        public static int CapacityAtLevel(int authoredStorageCapacity, int level)
        {
            if (authoredStorageCapacity <= 0) return 0;
            float mult = StorageCapsCatalog.LevelMultiplier(Mathf.Max(1, level));
            return Mathf.Max(authoredStorageCapacity, Mathf.RoundToInt(authoredStorageCapacity * mult));
        }

        /// <summary>
        /// The town bank ceiling for a resource = baseCap + every BUILT container of that resource,
        /// scaled by its placed level. int.MaxValue when the resource is uncapped.
        /// </summary>
        public static int MaxOf(BankResource r)
        {
            if (!IsCapped(r)) return int.MaxValue;
            var slots = BuildSlots(r, out int containerCapacity);
            int max = BaseCapOf(r) + containerCapacity;
            FlowTrace.Throttle("Bank", "max-" + WordOf(r), 30f,
                $"MaxOf({WordOf(r)}) = base {BaseCapOf(r)} + containers {containerCapacity} across {slots.Length - 1} built container(s) = {max}.");
            return max;
        }

        /// <summary>The one authoritative wallet total for a resource (GameState -- WO-842).
        /// 0 when no save service exists.</summary>
        public static int CurrentOf(BankResource r)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return 0;
            switch (r)
            {
                case BankResource.Wood:     return state.Wood;
                case BankResource.Iron:     return state.Iron;
                case BankResource.Food:     return state.Resources.Food;
                case BankResource.Crystals: return state.Resources.Crystals;
                case BankResource.Coins:    return state.Resources.Coins;
            }
            return 0;
        }

        /// <summary>Units of headroom left for a resource given an explicit current total. Never
        /// negative (a grandfathered over-cap save reads 0, it is NOT drained). int.MaxValue when uncapped.</summary>
        public static int RoomFor(BankResource r, int current)
        {
            if (!IsCapped(r)) return int.MaxValue;
            return Mathf.Max(0, MaxOf(r) - Mathf.Max(0, current));
        }

        /// <summary>Live-state overload of <see cref="RoomFor(BankResource,int)"/>.</summary>
        public static int RoomFor(BankResource r) => RoomFor(r, CurrentOf(r));

        /// <summary>
        /// WO-900 SEAM (WO-901 Sec.4). True when the bank can accept at least
        /// <paramref name="amount"/> more units. The collector "tap to collect" tell MUST read this
        /// and say "Bank full" instead of "tap to collect" when it is false -- otherwise the tell
        /// invites a tap that vaporises the pending pool under the clamp-and-warn ruling.
        /// </summary>
        public static bool HasHeadroom(BankResource r, int amount = 1)
        {
            if (!IsCapped(r)) return true;
            return RoomFor(r) >= Mathf.Max(1, amount);
        }

        // =====================================================================
        //  The clamp (Law 3 + the load-bearing warn)
        // =====================================================================

        /// <summary>
        /// THE clamp. Returns how much of <paramref name="requested"/> actually fits in the bank on
        /// top of <paramref name="current"/>, and reports the units LOST.
        ///
        /// <para>PURE with respect to the wallet -- it writes nothing. Callers apply the returned
        /// amount. <paramref name="current"/> is passed in rather than read so the EconomyService
        /// fallback pool (no GameState) clamps against its own store and so the function is
        /// unit-testable without a save service.</para>
        ///
        /// <para>A NON-POSITIVE request is returned UNCHANGED (Law 3) -- a spend routed through an
        /// "add" API must never be upper-clamped. An UNCAPPED resource is returned unchanged.</para>
        ///
        /// <para>When anything is lost this raises an UNTHROTTLED <c>FlowTrace.Warn</c> naming the
        /// resource and the amount, publishes <see cref="LastOverflow"/>, and fires
        /// <see cref="Overflowed"/>. That warn is the only thing standing between the player and
        /// silently vaporised resources (WO-901 Sec.5) -- it must never be swallowed or throttled.</para>
        /// </summary>
        public static int ClampGrant(BankResource r, int current, int requested, string sourceTag, out int lost)
        {
            lost = 0;
            if (requested <= 0) return requested;      // Law 3 -- spends and no-ops pass straight through
            if (!IsCapped(r)) return requested;        // Law 1 -- crystals/coins are never clamped

            // One capacity resolution for the whole call -- Grant is a hot path (per-kill trickle,
            // harvest ticks), so do NOT re-walk the layout for the room, the max and the trace.
            int max = MaxOf(r);
            int room = Mathf.Max(0, max - Mathf.Max(0, current));
            if (requested <= room) return requested;

            int granted = room;
            lost = requested - granted;

            string resName = DisplayName(r);
            string container = ContainerNameFor(r);

            // Sec.12 -- NEVER throttled, NEVER swallowed. If this line is missing, resources
            // vanished silently and that is the defect the owner's ruling is one line away from.
            FlowTrace.Warn("Bank",
                $"BANK FULL [{sourceTag ?? "?"}] {resName}: requested {requested}, banked {granted}, " +
                $"LOST {lost} (wallet {current}/{max}). Build or upgrade a {container}, or spend {resName.ToLowerInvariant()}.");

            var status = new BankOverflowStatus
            {
                Available = true,
                Resource = r,
                ResourceName = resName,
                ContainerName = container,
                Requested = requested,
                Granted = granted,
                Lost = lost,
                Max = max,
                Source = sourceTag ?? "?",
                Version = ++_overflowVersion,
            };
            LastOverflow = status;

            var handler = Overflowed;
            if (handler != null)
            {
                // Guard so a bad presentation subscriber can never swallow or abort the income path.
                Guard.Try("Bank", "raise Overflowed", () => handler(status));
            }

            return granted;
        }

        // =====================================================================
        //  Apportionment -- the derived per-container occupancy (WO-903 seam)
        // =====================================================================

        /// <summary>
        /// PURE fill -- and, because it is a pure function of the total, PURE DRAIN as well.
        /// Distributes <paramref name="total"/> across <paramref name="slots"/> in the order given
        /// -- which MUST already be capacity-ascending (<see cref="OrderSlots"/>) -- filling each to
        /// its capacity before moving outward, and reports what did not fit.
        ///
        /// <para>Draining is THIS SAME FUNCTION evaluated at a lower total. There is deliberately no
        /// separate drain path and no stored per-container balance: two code paths is exactly how the
        /// pallets end up showing a state the wallet does not agree with.</para>
        /// </summary>
        public static void Fill(int total, StorageSlot[] slots, out int overflow)
        {
            int remaining = Mathf.Max(0, total);
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    int cap = Mathf.Max(0, slots[i].Capacity);
                    int take = Mathf.Min(remaining, cap);
                    slots[i].Contents = take;
                    slots[i].Fill01 = cap > 0 ? (float)take / cap : 0f;
                    remaining -= take;
                }
            }
            overflow = remaining;
        }

        /// <summary>
        /// The ordering law (owner 2026-08-04): CAPACITY ASCENDING. The smallest container fills
        /// FIRST and therefore drains LAST; the largest fills last and drains first. Capacity --
        /// never current contents -- because capacity only changes when a building is placed or
        /// upgraded, so the order is stable and the props cannot flicker as balances move. Ties
        /// break on a stable key (base store first, then catalog id, then cell X, then cell Z),
        /// never on dictionary or FindObjectsByType iteration order.
        /// <para>The owner's stated outcome ("pallets drain last") therefore requires the pallets to
        /// be the SMALLER containers -- see the file header and case [order-intent-pallets-last].</para>
        /// </summary>
        public static void OrderSlots(StorageSlot[] slots)
        {
            if (slots == null || slots.Length < 2) return;
            Array.Sort(slots, CompareSlots);
        }

        private static int CompareSlots(StorageSlot a, StorageSlot b)
        {
            int c = a.Capacity.CompareTo(b.Capacity);
            if (c != 0) return c;
            // Deterministic tie-break -- the base store always sorts ahead of a same-capacity pallet.
            c = (a.IsBaseStore ? 0 : 1).CompareTo(b.IsBaseStore ? 0 : 1);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.StructureId ?? "", b.StructureId ?? "");
            if (c != 0) return c;
            c = a.CellX.CompareTo(b.CellX);
            if (c != 0) return c;
            c = a.CellZ.CompareTo(b.CellZ);
            if (c != 0) return c;
            return string.CompareOrdinal(a.InstanceKey ?? "", b.InstanceKey ?? "");
        }

        /// <summary>
        /// WO-903 SEAM. The full derived answer for one resource: authoritative total, cap,
        /// grandfathered overflow, and per-container occupancy in fill/drain order.
        ///
        /// <para>A pallet fill-stack finds ITS slot by <see cref="StorageSlot.InstanceKey"/>
        /// (<c>"lumberyard@12,-4"</c>) or by <see cref="TryGetSlot"/>, then reads
        /// <see cref="StorageSlot.Fill01"/> -- e.g. <c>steps = Mathf.RoundToInt(slot.Fill01 * 20)</c>.
        /// Nothing is stored per container; call this again and it re-derives.</para>
        /// </summary>
        public static BankApportionment Apportion(BankResource r)
        {
            int total = CurrentOf(r);
            var slots = BuildSlots(r, out int containerCapacity);
            OrderSlots(slots);
            Fill(total, slots, out int overflow);
            return new BankApportionment
            {
                Resource = r,
                Total = total,
                Max = IsCapped(r) ? BaseCapOf(r) + containerCapacity : int.MaxValue,
                Overflow = overflow,
                Slots = slots,
            };
        }

        /// <summary>
        /// WHAT-IF seam. The same derived answer as <see cref="Apportion(BankResource)"/> but at a
        /// HYPOTHETICAL total (<c>current + delta</c>, floored at 0) -- so a caller can ask
        /// "if N units are removed, which containers empty and by how much?" without touching the
        /// wallet. Intended readers: the WO-903 pallet fill-stacks (to animate a drop) and a future
        /// raid-steal presentation pass.
        ///
        /// <para>IMPORTANT: this is a PREVIEW, never a mutation. An actual steal or spend must move
        /// the ONE authoritative total (GameState) exactly like any other spend --
        /// <c>total -= amount</c> -- and let the apportionment re-derive. There is no per-container
        /// balance to debit and none may be introduced (WO-842: two authorities produced the
        /// captured "985k can't afford 800").</para>
        /// </summary>
        public static BankApportionment Preview(BankResource r, int delta)
        {
            int hypothetical = Mathf.Max(0, CurrentOf(r) + delta);
            var slots = BuildSlots(r, out int containerCapacity);
            OrderSlots(slots);
            Fill(hypothetical, slots, out int overflow);
            return new BankApportionment
            {
                Resource = r,
                Total = hypothetical,
                Max = IsCapped(r) ? BaseCapOf(r) + containerCapacity : int.MaxValue,
                Overflow = overflow,
                Slots = slots,
            };
        }

        /// <summary>Find one container's derived state by its stable instance key. False when the
        /// key names no live container (sold / never built).</summary>
        public static bool TryGetSlot(BankResource r, string instanceKey, out StorageSlot slot)
        {
            slot = default;
            if (string.IsNullOrEmpty(instanceKey)) return false;
            var a = Apportion(r);
            for (int i = 0; i < a.Slots.Length; i++)
            {
                if (string.Equals(a.Slots[i].InstanceKey, instanceKey, StringComparison.OrdinalIgnoreCase))
                {
                    slot = a.Slots[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>Build the stable instance key for a placed container. Public so WO-903 can build
        /// the same key from its own PlacedStructure without duplicating the format.</summary>
        public static string InstanceKeyOf(string itemId, int cellX, int cellZ)
            => $"{itemId}@{cellX},{cellZ}";

        // =====================================================================
        //  Built-container enumeration -- the SAME authority the build/sell seam uses
        // =====================================================================

        /// <summary>
        /// The base store plus every BUILT storage container for this resource.
        ///
        /// <para>"BUILT" is <c>GameState.BaseLayout</c> (the placed-structure record the ONE commit
        /// seam writes at BuildModeController.cs:1888-1892, that
        /// StrategicPlacementMigration converts template towns into, and that sell REMOVES) AND
        /// <c>GameState.HasEverBuilt</c> (the monotonic WO-834 v36 ledger). Both are existing
        /// notions -- no third one is invented, and the P0 that let ResourceCollectorBootstrap
        /// create income for an unbuilt building by consulting only a registry is not repeated.
        /// BaseLayout is what makes the cap fall again when a container is SOLD; the ever-built
        /// co-gate is what stops anything that fabricates a layout row from opening the seam.</para>
        /// </summary>
        private static StorageSlot[] BuildSlots(BankResource r, out int containerCapacity)
        {
            containerCapacity = 0;
            var list = new List<StorageSlot>(4)
            {
                new StorageSlot
                {
                    StructureId = "",
                    InstanceKey = BaseStoreKey,
                    Level = 1,
                    Capacity = IsCapped(r) ? BaseCapOf(r) : 0,
                    IsBaseStore = true,
                },
            };

            if (!IsCapped(r)) return list.ToArray();

            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var layout = state != null ? state.BaseLayout : null;
            if (layout == null) return list.ToArray();

            string want = WordOf(r);
            for (int i = 0; i < layout.Count; i++)
            {
                var d = layout[i];
                if (string.IsNullOrEmpty(d.itemId)) continue;

                var entry = CatalogRegistry.Get(d.itemId);
                var repo = entry != null ? entry.repo : null;
                if (repo == null || !repo.IsStorageContainer) continue;
                if (!string.Equals(repo.storageResource, want, StringComparison.OrdinalIgnoreCase)) continue;
                if (state != null && !state.HasEverBuilt(d.itemId)) continue;   // existence co-gate

                int level = Mathf.Max(1, d.level);
                int cap = CapacityAtLevel(repo.storageCapacity, level);
                if (cap <= 0) continue;

                containerCapacity += cap;
                list.Add(new StorageSlot
                {
                    StructureId = d.itemId,
                    InstanceKey = InstanceKeyOf(d.itemId, d.cellX, d.cellZ),
                    Level = level,
                    Capacity = cap,
                    IsBaseStore = false,
                    CellX = d.cellX,
                    CellZ = d.cellZ,
                });
            }

            return list.ToArray();
        }
    }
}
