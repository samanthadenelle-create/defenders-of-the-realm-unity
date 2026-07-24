> **SOURCE: Grok execution package 2026-07-12** (owner-relayed, built from the docs/SME dossier fleet). Slotted into the WO numbering by CLI; reconcile against docs/SME/WO677_PHASE0_APPLICABILITY.md (the code-verified assessment).

# Dragon Decision: Create Own vs License the Existing One

> ## ✅ DECISION 2026-07-23 (Option B.2) → SWAP ACTUALLY LANDED 2026-07-24 (WO-760)
> ⚠ **The 07-23 "REMOVED/RESOLVED" claim below was PREMATURE** — that commit only repointed
> comments; the CC-BY-NC model still SHIPPED (Resources includes unused assets) until the
> 2026-07-24 builder-run + git-rm. As of 2026-07-24 the swap is REAL: licensed rig git-tracked
> at `Assets/Dragon/`, ships as `Resources/Enemies/Boss_Dragon.prefab` (built by
> `DragonAnimatorSetup` + force-tracked `SyndrathDragon.controller`); old CC-BY-NC files + the
> orphan `Prefabs/Village/Generated/Boss_Dragon.prefab` git-rm'd; `RedDragon 1.2` deleted;
> `EnemyFactory` dragon keys repointed to `Boss_Dragon`; fly->land->burn->Tree behavior built.
>
> The old CC-BY-NC 3DHaupt dragon has been **REMOVED and REPLACED** by a licensed
> Asset-Store dragon: **Unity Asset Store product 71047 "Dragon Animated" (WDallgraphics),
> commercial license**, now living at **`Assets/Dragon/`**. The old model + its files
> (`Resources/Enemies/Dragon.fbx` + `Dragon.controller` + `Materials/Dragon_Bump_Col2.*` +
> `Dragon_Nor_mirror2.jpg` + both `Boss_Dragon.prefab`s) were git-rm'd 2026-07-24 (WO-760).
> **The CC-BY-NC ship blocker (KEY_FACTS "L1") is CLEARED.** The boss's creative name
> ("Syndrath the Devourer", key `bossSyndrath`) is retained — only the model changed.
> The prose below is the pre-decision options memo, kept for provenance.

**Current Asset:** ~~Syndrath the Devourer (wave-20 boss)~~ → licensed Asset-Store dragon (Assets/Dragon, product 71047)  
**Source:** ~~Dennis Haupt free Sketchfab dragon~~ → WDallgraphics, Unity Asset Store (commercial)  
**License:** ~~**CC BY-NC** (Non-Commercial only)~~ → **commercial (Asset Store)** — blocker cleared 2026-07-23  
**Status:** ~~**Ship blocker**~~ → **RESOLVED**

---

## Option A — License the Existing Model (Recommended if you like the look)

### What it involves
1. Contact the artist (Dennis Haupt) via Sketchfab or his website.
2. Request a commercial license for “Syndrath the Devourer” (or the specific model name).
3. Pay whatever fee he asks (typical range for a high-quality dragon: $50–$300, sometimes more for exclusivity).
4. Get written permission + any required credit line.
5. Replace the current free version with the licensed file (or keep the same file once you have the license).

### Pros
- Fastest path
- You already have the model, animations, and code working
- Boss is already tuned

### Cons
- Costs money
- Still dependent on a third-party license

### How to do it
I can draft the exact email / message you should send the artist if you want.

---

## Option B — Create Your Own Dragon (Full ownership)

### What it involves
You have three realistic paths:

1. **AI Generation + Cleanup** (fastest, what you’ve been doing with Tripo3D)
   - Generate a dragon in Tripo3D / Meshy / Rodin / etc.
   - Clean topology, retarget to your humanoid or custom rig
   - Create or retarget animations
   - Time: 1–3 days of focused work if you already have a pipeline

2. **Buy a commercial dragon pack**
   - Many high-quality dragons exist on the Asset Store, CGTrader, Sketchfab marketplace (commercial licenses).
   - Instant drop-in if the skeleton matches.

3. **Commission an artist**
   - Hire someone on Fiverr / ArtStation / Discord for a custom dragon.
   - Cost: $200–$1500+ depending on quality and animation needs.

### Pros
- 100% owned, no license risk forever
- Can match your exact art style
- Can make it unique

### Cons
- Takes time and/or money
- You have to re-do any boss-specific setup (materials, VFX attachments, hit boxes, etc.)

---

## My Recommendation for You Right Now

Because you said you want to do 3–6 **first**, I recommend:

1. Finish items 3–6.
2. Then decide:
   - If the current dragon still looks great and you want to ship soon → **license it** (Option A).
   - If you want full ownership and are willing to spend a few days → **create/replace with your own** using Tripo3D + your existing pipeline (Option B).

I can prepare a full work order for either path the moment you choose.

---

**Next step when you’re ready:**  
Just tell me “license the dragon” or “create our own dragon” and I’ll give you the exact next actions + work order.