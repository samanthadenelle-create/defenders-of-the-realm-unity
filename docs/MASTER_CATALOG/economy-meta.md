# Master Catalog — Area: economy-meta

> ⚠ **STALE 2026-07-22 — corrections (live anchor `CANON_GROUND_TRUTH_2026-07-22.md`):** `packs.json` now has **13 packs** (not 5); the pet active-slot now persists (save v34); the 3 pack→cosmetic ECON P1s are fixed + guarded (PACK_GRANT / PACK_COSMETIC_INTEGRITY). Body below is the 2026-06-12 point-in-time map; trust these lines + the anchor over it.

Scope: `Assets/_Modules/{Pets,Cosmetics,Wallet,Web3,Economy}` — pet system, wallet/crypto
payment, pack store, Glimmer, battle pass, cosmetics, and the Solana/SKR/web3 (Jupiter) layer.
Verified by reading every `.cs`, `.asmdef`, canonical `.json`, README, and test in scope.

Legend: **[LIVE]** wired & functional · **[STUB]** intentionally scaffolded · **[DEAD]** unused/superseded ·
**[FLAG]** see FLAGS section.

---

## Assemblies (asmdef)

| Assembly | refs | notable |
|---|---|---|
| `DeNelle.Pets` | Core, Data, Unity.Localization, UniTask | NO ref to Village/Cosmetics → all cross-module via reflection bridges. autoReferenced. |
| `DeNelle.Wallet` | Core, Data, Unity.Localization, UniTask | versionDefine `com.solana.unity_sdk` **with empty expression** → `SOLANA_SDK` define activates whenever the package is present (any version). autoReferenced. |
| `DeNelle.Cosmetics` | Core, UniTask | autoReferenced. |
| `DeNelle.Web3` | Core, **Wallet**, UniTask | `autoReferenced:false`. Only module that directly references Wallet. |
| `DeNelle.Wallet.Tests` | Wallet, Core, Data, UniTask, TestRunner(s) | EditMode-only, `UNITY_INCLUDE_TESTS`. |
| **Economy** | *(none — no asmdef)* | Folder has README only; code described in README lives in Assembly-CSharp / Village, not here. See FLAGS. |

Cross-module bridge pattern (recurring): Pets/Cosmetics cannot reference Village (circular asmdef),
so they resolve Village/Cosmetics types by `AppDomain` type-name reflection, cache members, best-effort invoke.

---

## WALLET — `DeNelle.Wallet`  (namespace `DeNelle.Wallet`)

### WalletService.cs  [LIVE]
- `WalletService : IWalletSigner` — app-facing wallet surface (React `useGameWallet` analog). Plain C# class (not MonoBehaviour). Implements `DeNelle.Core.Web3.IWalletSigner` for backend save-auth (WO-121).
- Enums/structs (all here): `WalletNetwork{Devnet=0,Mainnet=1}`, `CurrencyKind{Sol,Usdc,Skr}`, `WalletStatus{Disconnected,Connecting,Connected}`, `WalletAccount`, `WalletBalance`, `PaymentResult`. Interface `IWalletProvider`.
- Const `DefaultNetwork = Devnet` (owner-gated to Mainnet, spec Part 10). `RewardsDistributorAddress` → `WalletRegistry`.
- Ctors: `WalletService(IWalletProvider)` (null→stub); `WalletService()` auto-selects `SolanaWalletProvider` if `SolanaWalletProvider.IsSdkAvailable` else `StubWalletProvider`; static `Create(bool useStub=false)`.
- Public: `UniTask<WalletAccount> Connect()`; `UniTask Disconnect()`; `UniTask<WalletBalance> GetBalance()`; `UniTask<PaymentResult> Pay(PackDef, CurrencyKind)`; `UniTask<PaymentResult> PayFlat(string txId, CurrencyKind, double)` (WO-7, non-pack); `void SetNetwork(WalletNetwork)` (warns on Mainnet); event `StatusChanged`.
- On valid Connect → `CoreServices.RegisterWalletSigner(this)`; Disconnect → unregister. IWalletSigner: `CanSign` = connected && provider.CanSignMessages, `WalletAddress`, `SignMessageBase58`.
- Deps: IWalletProvider, WalletRegistry, PackDef, CoreServices, CanonicalJson(indirect).

### IWalletProvider (interface, in WalletService.cs)  [LIVE]
Seam: `ProviderName, IsConnected, Account, Connect, Disconnect, GetBalance, SendPayment, CanSignMessages, SignMessageBase58`.

### StubWalletProvider.cs  [LIVE — default provider today]
- `StubWalletProvider : IWalletProvider` — devnet mock; no SDK. Generates deterministic base58 addr (44ch) / tx sig (88ch) via seeded `System.Random(0xDEFEED)`. Starting mock balance Sol=5/Usdc=250/Skr=2000 (generous so all 5 packs buyable). `SendPayment` simulates ~1.1s finality, debits mock balance. `CanSignMessages => false` (no key → backend auth headers skipped, offline-safe).

### SolanaWalletProvider.cs  [STUB until SDK present / integrator-verify]
- `SolanaWalletProvider : IWalletProvider` — real Solana Unity SDK seam. ALL SDK calls inside `#if SOLANA_SDK`; compiles with define off (`IsSdkAvailable` false, methods throw `InvalidOperationException`). Android/Seeker → MWA `LoginWalletAdapter`; desktop/iOS → `LoginPhantom`. Builds SystemProgram (SOL) / TokenProgram (USDC/SKR) transfers, wallet signs+sends, polls confirmation ~30s. `SignMessageBase58` via wallet ed25519. **Every SDK type/method tagged `// SDK-VERIFY:`** — names are best-knowledge, integrator must confirm against resolved package. Recipient = `WalletRegistry.DevnetPurchaseRecipientAddress`. Pure helpers `LamportsToUi/UiToBaseUnits` always compiled.

### WalletEndpoints.cs  [LIVE — config constants]
- Static `WalletEndpoints` — RPC/WS URLs (devnet/mainnet), SPL mints, decimals, MWA chain/app-identity. Devnet RPC `api.devnet.solana.com`. USDC devnet `4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU`, USDC mainnet `EPjFW...Dt1v`. **`SkrMintDevnet=""` and `SkrMintMainnet=""` — empty: integrator must fill devnet SKR mint or SKR transfers fail cleanly.** Decimals Sol=9, Usdc=6, Skr=6. App identity name "Defenders of the Realm", uri `https://defenders-of-the-realm.app`.

### WalletRegistry.cs  [LIVE]
- Typed loader for `wallets.json` (public addresses only). Types `WalletEntry`, `WalletRegistryData`. Static `WalletRegistry`: `RewardsDistributor`, `DevnetPurchaseRecipient`, `*Address` convenience, `Reload()`. WebGL-safe via `CanonicalJson.Read`. Hard fallback consts: RewardsDistributor `2JRmE...nmNi`, DevnetRecipient `3Eeww...gaHe`. `Fill()` backfills missing entries.

### PackCatalog.cs  [LIVE]
- Typed model + loader for `packs.json`. Types `PackPricing, PackEconomy, ConvenienceItemDef, PackContents, PackDef, PackCatalogData`. Static `PackCatalog`: `Packs`, `CurrencyDisclaimer`, `Find(sku)`, `FindByTier(int)`, `Reload()`. WebGL-safe load. `PackDef.AmountFor/UsdReference/AmountLabel`.

### PackStore.cs  [LIVE — but scene-wiring DISABLED, see FLAGS]
- `PackStore : MonoBehaviour [RequireComponent(UIDocument)]` — 5-pack store UI + purchase flow. `Awake` builds its own `new WalletService()` (stub default); `SetWalletService` injects shared. **`BindElements()` IGNORES the UXML entirely and builds the whole panel in code (`ShopTheme`)** because UXML renders empty in player builds (documented trap). Public: `Render()`, `SetWalletService`, `UniTask<PaymentResult> Purchase(PackDef,CurrencyKind)`, event `PackPurchased`. `Purchase` → connect → `WalletService.Pay` → `ApplyPackContents` (writes Crystals/Food/Coins to `GameStateService.State.Resources`, records owned SKUs, `Save()`). `CloseStore()` closes the Village `MarketplaceInteractor` **via reflection** (Wallet can't ref Village). Analytics: `EventTracker.Track("bundle_viewed"/"purchase_completed")`. Convenience tokens NOT applied (flagged "Week-8 inventory pass"). Renders verbatim covenant line "You are never required to spend anything. Ever."

### CryptoPaymentManager.cs  [LIVE — thin bridge, reconciled WO-74]
- `CryptoPaymentManager : MonoBehaviour` singleton (DontDestroyOnLoad). Simple SOL/SKR/USDC top-up entry points for ShopUI/BattlePass; delegates to shared `WalletService.PayFlat`. SKR path applies `skrBonusMultiplier` (1.25) + optional `StakingBonusManager` (WO-76, resolved by reflection — **does not exist yet**). On success grants **Glimmer** (not "Aether Shards" which don't exist) via `GlimmerCurrencyService.TryAddGlimmer` **by reflection** (Wallet→Cosmetics would be circular). Public: `ConnectWallet`, `PayWith{SOL,SKR,USDC}(int)`→`UniTask<bool>`, sync `BuyWith*` (.Forget()). Inspector conversion rates aetherToSol/Usdc/Skr.

### WalletConnectDialog.cs  [LIVE]
- `WalletConnectDialog : MonoBehaviour [RequireComponent(UIDocument)]` — connect/account control (React `ConnectWalletButton`). Binds UXML by element NAME (`wallet-connect-button` etc.) — headless if absent (plain `Connect()/Disconnect()` API still works). `Awake` builds own stub `WalletService`; `SetWalletService` injects. Exposes `Wallet`, events `Connected`/`Disconnected`. NOTE: binds real UXML (unlike PackStore which code-builds) — would render empty in a player build (see FLAGS).

### Tests/ (EditMode)  [LIVE]
- `WalletServiceTest` — `FakeWalletProvider` (synchronous double) + real stub; devnet-default, connect/balance/pay guards, status events, full stub flow.
- `StubWalletProviderTest` — connect/balance/pay/debit/insufficient/reconnect over the shipped stub ([UnityTest] for UniTask.Delay).
- `WalletRegistryTest` — public addresses present, base58-only, **scans wallets.json for forbidden secret keywords** (privatekey/seedphrase/keypair/signer…), two distinct wallets.

---

## WEB3 — `DeNelle.Web3`  (namespace `DeNelle.Web3`)  — Jupiter SKR swap

### JupiterSwapService.cs  [LIVE quote / STUB swap-signing]
- `JupiterSwapService : MonoBehaviour, IJupiterService [RequireComponent(UIDocument)]`. Registers with `CoreServices.RegisterJupiter` on Awake. **REAL**: Jupiter `/v6/quote` fetch (UnityWebRequest, JsonUtility DTO parse), fee math, panel show/hide, connected-wallet lookup through `DeNelle.Wallet.WalletService` (via `WalletConnectDialog`). **STUB**: `ExecuteSwapAsync` fetches `/v6/swap` tx then hands to `WalletBridgeStub` (no real signing). Public: `OpenSwapPanel(decimal minSkr)`, `CloseSwapPanel`, `GetQuoteAsync`, `ExecuteSwapAsync`, `ConnectedWalletKey`, `FeeConfig`. Inspector `_skrMint="REPLACE_WITH_SKR_MINT_ADDRESS"` (placeholder). USDC/SOL mints sourced from `WalletEndpoints`. **CONTRADICTION: wallet stack is DEVNET-only but public Jupiter aggregator is MAINNET — flagged in file header, unreconciled.**

### JupiterSwapPanelController.cs  [LIVE]
- `: MonoBehaviour [RequireComponent(JupiterSwapService)]`. Drives `JupiterSwapPanel.uxml` by element name; debounces input (0.6s), fetches quote, refreshes fee breakdown, confirm → `ExecuteSwapAsync`. Public `Initialise(decimal minimumSkr, SwapFeeConfig)`. Hardcoded English strings (swap.* keys reserved for later loc).

### SwapFeeConfig.cs  [LIVE — ScriptableObject]
- `SwapFeeConfig : ScriptableObject` (`CreateAssetMenu "Defenders/Swap Fee Config"`). `PlatformFeeBps`(20=0.2%), `FeeWalletAddress`(""—OWNER-CONFIG SKR token-account), `SlippageBps`(50), `EnableSolInput`(false v1).

### WalletBridgeStub.cs  [STUB — hard-fails in release]
- Static `WalletBridgeStub.SignAndSendTransaction(json, onSuccess, onError)`. In `UNITY_EDITOR||DEVELOPMENT_BUILD` logs + fires fake `STUB_SIG_<guid8>`; in release build `LogError` + onError. Signs/submits nothing. Replace with real signer before shipping.

### JupiterSwapBootstrap.cs  [LIVE — `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`]
- Static. Self-spawns a `JupiterSwapHost` GameObject (UIDocument+Service+Controller) per allowed scene, borrowing PanelSettings from an existing scene UIDocument (never loads by name → no black panel). Loads `JupiterSwapPanel` UXML via Resources. Idempotent, scene-scoped, graceful-degrade (logs once, no crash). Allowed scenes: `Title, HeroSelect, PetSelect, Village2` + `Dungeon_*`. **NOTE allowed-list says `PetSelect` and `Village2`; the integrator-notes comment block at file foot mentions "Village/Dungeon" — minor doc/code drift.**

---

## COSMETICS — `DeNelle.Cosmetics`  (namespace `DeNelle.Cosmetics`)

### GlimmerCurrencyService.cs  [LIVE — `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`]
- `GlimmerCurrencyService : MonoBehaviour` singleton (DontDestroyOnLoad). Soft-currency wallet + cosmetic ownership/equip. Persists to **PlayerPrefs key `dotr-cosmetics-v1`** (Newtonsoft JSON) — SEPARATE from GameState (spec: "Crystals→Glimmer not allowed"). `StartingGlimmer=25`. Types `GlimmerSaveData`. Public: `Glimmer`, `OwnedCosmetics`, `Owns(id)`, `EquippedFor(category)`, `bool TryPurchase(id)`, `Equip(id)`, `UnequipCategory(cat)`, `bool TryAddGlimmer(int)`, `bool SpendGlimmer(int)`, `bool GrantAchievement(id)`, event `Changed`. Consumed by CryptoPaymentManager + PetDeployer + BattlePassManager (all via reflection from other asmdefs).

### CosmeticCatalog.cs  [LIVE]
- Typed model + loader for `cosmetics.json`. Types `CosmeticDef` (Id, Category, AppliesTo, DisplayName, Description, GlimmerCost, UnlockMethod, PreviewColor; derived IsAchievement/IsBuyable/PreviewUnityColor), `CosmeticCatalogData`. Static `CosmeticCatalog`: `All`, `Find(id)`, `ByCategory(cat)`, `Reload()`. WebGL-safe via CanonicalJson (DEF-212 fix). **CosmeticDef does NOT model the `meshPath`/`specialSale` fields present on one row of cosmetics.json — silently ignored (see FLAGS).**

### CosmeticApplier.cs  [LIVE — reconciled WO-73]
- `CosmeticApplier : MonoBehaviour [RequireComponent(MeshRenderer)]`. Applies visuals: material tint (first-pass = tint MeshRenderer to `PreviewUnityColor`), prefab override, VFX attach. Inspector slots `materialOverride/prefabOverride/vfxPrefab` for art later. Public: `ApplyCosmetic(string id)`, `ApplyCosmetic(CosmeticDef)`, `ResetToDefault()`, `EquippedCosmeticId`. Ownership queried at call site via GlimmerCurrencyService. **No automatic caller wires it yet — art hookup pending.**

### BattlePassManager.cs  [LIVE — reconciled WO-73; needs SO]
- `BattlePassManager : MonoBehaviour` singleton (DontDestroyOnLoad). XP/level + free/premium tracks. Persists to **PlayerPrefs (`BP_Level`,`BP_XP`,`BP_HasPremium`)** — NOT a unified save. Needs `BattlePassData` SO (`Core/Data`) assigned in Inspector or it's a no-op (warns). Public: `AddXP(int)`, `bool PurchasePremiumPass()` (spends `premiumCostGlimmer=2400` via GlimmerCurrencyService.SpendGlimmer; back-dates rewards), `HasPremium`. Reward kinds: Crystals→`GameStateService.AddCrystals`; Cosmetic→`GlimmerCurrencyService.GrantAchievement`; Resource→log only (hook pending). LevelUpVFX called via reflection (Cosmetics→Village barred). seasonName "Season 1 - Shadow Realms".

---

## PETS — `DeNelle.Pets`  (namespace `DeNelle.Pets`)

### Pet.cs  [LIVE]  ⚠ stale comment, see FLAGS
- `Pet : MonoBehaviour` — one in-village guardian pet. Hunts nearest hostile `IDamageable` via `Physics.OverlapSphereNonAlloc` (enemy LayerMask) — never names Village Enemy. Enum `PetMode{Idle,Defend,Fortify}`. Configured from `PetDef` + bond rank + home post + optional `PetData` SO (SO wins, WO-86). Public: `Configure(def,bondRank,homePost,mode)`, `TakeDamage(float)`, `Heal(float)`, `SetHomePost(Vector3)`, `SetEnemyMask`, `SetProgressionMultipliers(dmg,hp)`, props `PetId/Species/BondRank/Mode/Hp/MaxHp/IsAlive/HomePost/HasHostileInRange/Def`. WO-128 anti-ranged: prioritises `IRangedThreat`, dashes + applies `StatusEffect.Slow`. WO-187: self-adds & drives a **NavMeshAgent** in Awake (`_agent.Move(displacement)`) to stay wall-constrained. WO-163: animator param-presence guards (Tripo pets lack KayKit params). Records kill-XP via `DamageAttribution`, hit VFX via `PetAttackVfxBridge`.

### PetCatalog.cs  [LIVE]
- Typed model + loader for `pets.json`. Types `PetBondRank, PetDef, PetCatalogData`. Static `PetCatalog`: `Pets`, `DeployRadius`(11), `Find(id)`, `FindBySpecies(species)`, `DeploySlotPosition(slotIndex, heartPos)` (ring port of React `petPost()`), `Reload()`. WebGL-safe. PetDef: id/species/name/element/archetype/tints/huntSpeed(4.4)/attackRange(2.7)/attackCooldown(0.75)/slotIndex/bondRanks[5]; `RankAt`, `TintColor`, `GlowUnityColor`.

### PetDeployer.cs  [LIVE]
- `PetDeployer : MonoBehaviour` — spawns starter pets on the Heart ring. **`UseLitePetVisuals = true` (const)** → never loads heavy Tripo FBX (~208MB), renders a `PetBillboard` sprite quad from `Resources/PetPortraits/<id>.png` (WO-211 Phase-2 WebGL bloat fix). `DIAG_SKIP_ALL_PETS=false`. Public: `DeployStarterPets()` (deploy-once guard; single chosen starter via `GameState.StarterPetId`, default `ice-wolf`), `DeployChosen(species)`, `SyncDeployedToSlots(IReadOnlyList<string>)` (WO-297 multi-slot), `Pet SummonAt(Vector3,mode)` (WO-360 Echo at outpost), `ClearDeployed()`, setters `SetHeartPosition/SetEnemyMask/SetBondRanks`, `DeployedPets`. Auto-attaches per-pet: `PetHeroLeash`, `PetHarvester`, `PetIdleRoutines`, `PetProgression`. Cosmetic pet-skin resolved by reflection to GlimmerCurrencyService. Contains big inactive `TryLoadPetMesh`/`WirePetAnimator`/`NormalizePetHeight`/`TripoMaterialFixer` path (only runs if `UseLitePetVisuals=false`). `TintPlaceholder` is **defined but currently unreferenced** ([DEAD] under lite-visuals).

### PetAcquisitionService.cs  [LIVE — `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`]
- `PetAcquisitionService : MonoBehaviour` singleton (DontDestroyOnLoad). The 3 acquisition paths + active-slot model (WO-297). Enum `PetAcquisitionSource{Starter,Tame,Hatch,Rescue,Gift}`. `DefaultMaxSlots=1`, `AbsoluteMaxSlots=6`. Public: `Tame/Hatch/Rescue(species)`→`Acquire`, `bool Acquire(species,source)` (writes GameState.Pets + OwnedPets enum + auto-slot + Save + events), `Owns`, `SetMaxSlots(int)`, `AssignSlot/ClearSlot/TryAssignFreeSlot/SpeciesInSlot`, `MaxSlots/ActiveSlotSpecies/FilledSlotCount`, events `Changed`/`PetAcquired(string)`. Slot→species runtime map rebuilt from StarterPetId on load. **FLAG (own header): exact slot assignment NOT persisted — needs a save-layer field; only the starter is auto-restored.**

### PetProgression.cs  [LIVE]
- `PetProgression : MonoBehaviour, IXpEarner [RequireComponent(Pet)]`. Per-pet XP/level (`level*85+55` curve, same as hero). On level-up applies dmg/hp multipliers to Pet (`+7%`/`+8%` per level, cap 3x; overridable by `PetData` SO). Registers under `Pet.PetId` in `XpEarnerRegistry`. Public: `AddXp(float)`, `Level`, `EarnerId`, `WorldPosition`. In-memory only (not persisted).

### PetSkillTreeCatalog.cs  [LIVE]
- Typed loader for `pet-skill-trees.json`. Types `PetSkillDef` (incl. WO-298 additive fields branch/magnitude/magnitudeType/questItem/cost), `PetSkillTreeDef`, `PetSkillTreeData`. Static: `PetMaxLevel`(20), `PetLoadoutSize`(3), `RespecFoodCost`(50), `RespecGlimmerCost`(5), `SkillsInBranch`, `GetSignature`, `GetTree`, `AllTrees`, `FindSkill`, `bool CanUnlock(skillId,petLevel,unlocked)`, `Reload`. **Catalog ships 11 species trees but only 3 (aether/flame/ice) have PetDefs in pets.json + map to PetSpecies enum — see FLAGS.**

### PetHeroLeash.cs  [LIVE]
- `PetHeroLeash : MonoBehaviour [RequireComponent(Pet)]`. Natural wander-near-hero motion: drifting heading random-walk + a "carrot" HomePost projected ahead, curve-home past explore radius, "stop & sniff" beats. Inner 4.5 / explore 9 / return 13. Resolves hero `HeroLocomotion` transform **by reflection** (per-pet seeded RNG). Also declares internal `PetNameTagBillboard` (TextMesh→camera billboard). No public API (driven by Update).

### PetHarvester.cs  [LIVE]
- `PetHarvester : MonoBehaviour [RequireComponent(Pet)]` — the core "pet gathers while you defend" loop (WO-229). State machine Idle/MovingToNode/Harvesting. Steers pet by re-anchoring `Pet.SetHomePost` to a node (suspends `PetHeroLeash` while gathering, restores after). Banks via existing `MineNode.TryAutoExtract` (no new currency). Combat ALWAYS wins (`ShouldYieldToCombat` = Defend pet with `Pet.HasHostileInRange`). detectRadius 28, scanInterval 1s, carryCapacity 50. No public API. **Superseded duplicate exists — see FLAGS.**

### MineNodeBridge.cs  [LIVE — reflection seam]
- Internal `MineNodeHandle` (wrap one Village `MineNode` Component: Position, IsValid, IsDepleted, IsClaimed, `SetClaim(bool)`, `int TryAutoExtract()`) + static `MineNodeBridge` (resolve `DeNelle.Village.MineNode` type + members once; `Available`; `FindNearest(from,radius)` via throttled `FindObjectsOfType`). Lets Pets reach Village node API without an asmdef ref.

### PetAttackVfxBridge.cs  [LIVE — reflection seam]
- Internal static. `Strike(Color,Vector3)` → Village `AbilityVfxKit.SpawnAbilityVfx` (Strike effect) by reflection; no-op if absent (WO-35).

### PetClipPlayer.cs  [LIVE — WO-184 fallback]
- `PetClipPlayer : MonoBehaviour`. Plays one embedded `AnimationClip` via `PlayableGraph` (no AnimatorController needed) for the ice-wolf (Generic rig, no shipped controller). `Initialize(Animator,AnimationClip)`; eases playback speed idle↔move off displacement. Build-safe.

### PetAnimatorController.cs  [LIVE but partly UNWIRED — DEF-57]
- `PetAnimatorController : MonoBehaviour [RequireComponent(Animator)]`. Cached-hash driver: `UpdateMovement(speed)`, `PlayAttack/PlayHit/PlayDeath`, anim-event hooks `OnAttackHit()`/`OnFootstep()` (both **TODO stubs** — "wire to PetCombat/AudioService when ticket filed"). WO-163 param guards. NOTE: Pet.cs drives its own Animator directly with hashes "Speed/Attack/Hit/**Dead**"; this component uses "**Death**" and is not auto-attached by PetDeployer → **largely unused today** ([FLAG]).

### PetEmoteController.cs  [LIVE but WAVE-CLEAR WIRING STUBBED — DEF-57]
- `PetEmoteController : MonoBehaviour`. Idle "Happy" emote, "Alert" on nearby `IDamageable` hostile (OverlapSphere), `Celebrate()` public. **`TrySubscribeWaveClear()/TryUnsubscribeWaveClear()` are EMPTY stubs** — header claims WaveManager subscription via reflection but the body is "wired externally". WO-163 param guards. Not auto-attached by PetDeployer.

### PetBillboard.cs  [LIVE]
- `PetBillboard : MonoBehaviour`. Yaw-only camera-facing billboard for the lite-pet sprite quad (WO-211). LateUpdate. Attached by PetDeployer when `UseLitePetVisuals`.

### PetIdleRoutines.cs  [LIVE — self-disables, authoring gap]
- `PetIdleRoutines : MonoBehaviour`. Cute idle routines (Sit/LieDown/Shake) after idle settle; deterministic seeded picker; param-guarded. **GAP: the cute params/clips DO NOT EXIST on any shipped pet controller** → component self-disables + logs the authoring contract once. Attached by PetDeployer. No public API.

---

## ECONOMY — `Assets/_Modules/Economy/`  [DOC-ONLY in this scope]
- Contains ONLY `README.md` (+ .meta). **No `.cs`, no asmdef.** README documents the LIVE economy path which lives OUTSIDE this folder: `DeNelle.Village.MineNode`/`EconomyService`, `DeNelle.Pets.PetHarvester`+`MineNodeBridge`, `ClaimableCamp`/`Outpost`, `PetHarvestBootstrap`. States the in-folder code (ResourceNode family, old PetHarvester, ResourceInventory) is **superseded** — but those files are not present at this path (already removed/relocated). Canonical income API: `EconomyService.Instance.AddResource(ResourceType,amount)` / `Grant` / `TrySpend`.

---

## CANONICAL JSON DATA (`Assets/StreamingAssets/Data/Canonical/`)
Dual-copied to `Assets/Resources/Data/Canonical/` (Resources copy WINS at load, WebGL-safe via `CanonicalJson`).

| File | version | count | schema / notes |
|---|---|---|---|
| `packs.json` | 1 | 5 packs | hearth-spark(t1)→founders-vow(t5,founderOnly). Each: sku/tier/name/tagline/theme/pricing{usd,usdc,sol,skr}/contents{cosmetics[],economy{glimmer,crystals,food,coins},convenience[]}/packExclusiveCosmetic. Canon names verbatim. `currencyDisclaimer`="Token price moves with the market." Convenience = time-saving only (no combat power). |
| `wallets.json` | 1 | 2 entries | rewardsDistributor(`2JRmE…nmNi`, Seed Vault, transparency-only NOT a recipient) + devnetPurchaseRecipient(`3Eeww…gaHe`, Solflare devnet sink). PUBLIC addresses only, no keys. |
| `cosmetics.json` | 1 | 12 items | 4 hero / 4 pet / 4 village skins. Fields id/category/appliesTo/displayName/description/glimmerCost/unlockMethod(buy\|achievement)/previewColor. **`pet-aether-twilight` row carries extra `meshPath` + `specialSale` fields NOT in CosmeticDef** (ignored on load). Achievement items glimmerCost 0. |
| `pets.json` | 1 | 3 pets | deployRadius 11. aether-sprite(slot0)/flame-pup(slot1)/ice-wolf(slot2), each 5 bondRanks(0–4). |
| `pet-skill-trees.json` | 1 | **11 trees** | petMaxLevel 20, petLoadoutSize 3, respecFoodCost 50, respecGlimmerCost 5. Trees: aether-sprite, flame-pup, ice-wolf (12/12/11 skills) + sproutling, craghound, frostkit, emberpup, mirewing, glimmermoth, stoneback-calf, aether-fox (11 each). Skill fields: id/name/type(active\|passive)/tier/description/cooldownSeconds?/unlockLevel/prerequisites[] + WO-298 branch/magnitude/magnitudeType/questItem/cost. |

---

## DOCS (READMEs in scope)
- `Wallet/README.md` — "Monetization + crypto (~70% built, do NOT greenfield). Store scene-wiring DISABLED pending own PanelSettings." **CURRENT.**
- `Cosmetics/README.md` — catalog/applier/battlepass/glimmer; shop UI in `HUD/CosmeticShopPanel` (WO-236); open Q in `docs/GLIMMER_ECONOMY_OPEN_QUESTION.md`. **CURRENT.**
- `Web3/README.md` — Jupiter swap; lists the 4 files. **CURRENT but terse** (doesn't note the devnet/mainnet contradiction or stub-signer).
- `Pets/README.md` — file map; explicitly notes "a second PetHarvester.cs also exists in Economy/ (superseded — use this one)". **Mostly current but references in-Economy files that aren't physically present here** (see FLAGS).
- `Economy/README.md` — superseded-path doc; points to live Village+Pets economy. **CURRENT as a pointer, STALE as a file inventory** (describes files not in the folder).

---

## FLAGS

### Stale-comment-vs-code (the flagged class)
1. **Pet.cs movement comment is STALE.** Section header reads `"// Movement — kinematic drift; NavMeshAgent wiring is the integrator's."` (line ~582) — but `Awake()` self-adds and configures a `NavMeshAgent` and `MoveToward` drives it via `_agent.Move(displacement)` (WO-187). The agent IS wired by Pet itself, not the integrator. Same class as the HeroLocomotion "pure transform" trap. The accurate WO-187 comment block above it contradicts the older one-liner header.
2. **PetAnimatorController param-name mismatch.** Declares Death trigger `"Death"`; the live driver Pet.cs uses `"Dead"`. The two won't agree if both run; PetAnimatorController is not auto-attached, so this is latent, not active.
3. **PetEmoteController header vs body.** Header documents "subscribes to WaveManager.OnWaveCleared via reflection bridge"; `TrySubscribeWaveClear/TryUnsubscribeWaveClear` are **empty stubs** — celebration is "wired externally" only. Comment overstates what the code does.
4. **CosmeticApplier reconciliation comment** correctly notes `CosmeticData`/WO-72 SO "never built" — accurate, not stale, but worth knowing the material path is only a preview-color tint until art refs land.

### Dead / duplicate / unreferenced code
5. **Duplicate `PetHarvester`.** READMEs state a second (old) `PetHarvester.cs` exists in `Economy/` and is superseded — physically NOT present at `Assets/_Modules/Economy/` now (folder is README-only), so the dup is already gone but the docs still warn of it.
6. **PetDeployer dead branches under lite-visuals.** With `UseLitePetVisuals=true`, the entire Tripo-FBX path (`TryLoadPetMesh`, `WirePetAnimator`, `TryLoadEmbeddedClip`, `NormalizePetHeight`, `StripPetColliders`, Camera/Light/Particle strip, `AffinityGlow`) never executes. `TintPlaceholder` is defined but **referenced nowhere** (truly dead).
7. **PetAnimatorController + PetEmoteController not auto-attached** by PetDeployer (only Leash/Harvester/IdleRoutines/Progression are). Both are largely orphan presentation components today (DEF-57), with TODO stubs (`OnAttackHit`, `OnFootstep`, wave-clear).
8. **StakingBonusManager (WO-76) does not exist** — CryptoPaymentManager resolves it by reflection and no-ops; the SKR staking bonus is currently just the flat 1.25x multiplier.

### Scene-gated / disabled
9. **PackStore scene-wiring DISABLED** (README + CLAUDE.md §8 / PIPELINE_STATE): store needs its own PanelSettings before re-enabling. PackStore code-builds its UI to dodge the empty-UXML player-build trap; **WalletConnectDialog and JupiterSwapPanelController still bind real UXML by name** → they would render empty in a player build (uxml-in-builds trap, per memory).
10. **SOLANA_SDK define off by default** → all real on-chain wallet ops run through `StubWalletProvider` (devnet mock). Nothing transacts on a real chain today. Wallet asmdef versionDefine has an **empty version expression**, so the define flips on the moment the package resolves (intended) — but `SolanaWalletProvider`'s SDK calls are all `// SDK-VERIFY:` unconfirmed names.
11. **Convenience tokens not applied** — `PackStore.ApplyPackContents` grants crystals/food/coins + records SKUs but deliberately skips convenience tokens ("Week-8 inventory pass").

### Broken / contradictory
12. **Jupiter network contradiction.** `JupiterSwapService` targets the **mainnet** public Jupiter aggregator (`quote-api.jup.ag`) while the entire wallet stack is **devnet-only and owner-gated** (spec Part 10). Flagged in the file header, **unreconciled** — a real swap can't be both devnet-wallet and mainnet-Jupiter.
13. **SKR mint is empty everywhere.** `WalletEndpoints.SkrMintDevnet/Mainnet = ""` and `JupiterSwapService._skrMint = "REPLACE_WITH_SKR_MINT_ADDRESS"`. Any real SKR transfer or swap fails cleanly until the integrator supplies the live SKR SPL mint. (SKR pricing still works in-store via the stub.)
14. **Swap signing is a stub that hard-fails in release.** `WalletBridgeStub` logs a fake signature in editor/dev and `LogError`s in a release build. No real Jupiter-swap signer exists; `SolanaWalletProvider` signs SystemProgram/TokenProgram transfers but has **no Jupiter-swap deserialize-and-sign path**.
15. **pet-skill-trees.json over-specifies vs runtime.** 11 species trees authored, but only 3 species exist in `pets.json` + map to the `PetSpecies` enum (`PetAcquisitionService.TryToSpeciesEnum`). The other 8 (sproutling, craghound, …) can be acquired into `GameState.Pets` (by id) but **cannot be carried in the OwnedPets enum list** (acquisition's own FLAG) and have no PetDef to deploy from PetCatalog.
16. **Three separate persistence stores in this area** — PackStore→`GameStateService` (unified save); GlimmerCurrencyService→PlayerPrefs `dotr-cosmetics-v1`; BattlePassManager→PlayerPrefs `BP_*`. Ownership of pack-granted cosmetic SKUs lands in `GameState.OwnedItemIds`, but Glimmer-shop cosmetic ownership lands in the SEPARATE PlayerPrefs blob → two cosmetic-ownership sources of truth that are not reconciled.
17. **PetAcquisitionService active-slot assignment not persisted** (its own header FLAG) — only the StarterPetId slot survives a reload; multi-slot rosters reset.
