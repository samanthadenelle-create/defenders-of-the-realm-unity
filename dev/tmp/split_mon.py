import io, os
from pathlib import Path

# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
# location, never hardcode a drive letter. dev/tmp/<script>.py -> parents[2].
ROOT = str(Path(__file__).resolve().parents[2])
SRC = os.path.join(ROOT, 'WorkOrders', 'WORK_ORDER_1146_MON_monetization_activation_purchases_and_ads.md')
lines = io.open(SRC, encoding='utf-8').read().split('\n')

def span(start_marker, end_marker):
    a = b = None
    for i, l in enumerate(lines):
        if a is None and l.startswith(start_marker):
            a = i
        elif a is not None and end_marker and l.startswith(end_marker):
            b = i
            break
    return a, (b if b is not None else len(lines))

head_a, head_b = span('## 0. Outcome', '## 3. Lane P')      # 0,1,2 shared preamble
laneP_a, laneP_b = span('## 3. Lane P', '## 4. Lane A')
laneA_a, laneA_b = span('## 4. Lane A', '## 5.')
tail_a = laneA_b                                             # 5..10 shared contract

shared_head = '\n'.join(lines[head_a:head_b]).rstrip()
lane_p = '\n'.join(lines[laneP_a:laneP_b]).rstrip()
lane_a = '\n'.join(lines[laneA_a:laneA_b]).rstrip()
shared_tail = '\n'.join(lines[tail_a:]).rstrip()

SPLIT_NOTE = """
> ### \u26a0 SPLIT FROM ONE TICKET, 2026-08-22 (owner: "split it")
> This was one 354-line WO carrying BOTH lanes. They share only the laws in section 1 and the
> release contract in sections 5-10; their EVIDENCE is completely different - chain/verifier and a
> backend on one side, physical device and an ad dashboard on the other - and so are their blockers.
> Held together, either lane stalling froze the other. They now run and land independently.
>
> **The shared halves are DUPLICATED into both files ON PURPOSE.** A seat must not have to open the
> sibling ticket to learn a non-negotiable law. \u26d4 If a law in section 1 changes, change it in BOTH.
"""

P_HEADER = """**Status:** READY TO IMPLEMENT - BLOCKED ON OWNER R5 FOR ACTIVATION ONLY (build may proceed)

# WO-1147 - MON - Purchasing: verified on-chain payment to durable entitlement

**Minted:** 2026-08-22 (CLI, banner bumped 1147 -> 1148 in the SAME edit)
**Lane:** **MON** - monetization, dedicated and prioritised.
**Split from:** WO-1146 (owner ruling 2026-08-22).
**Sibling:** `WORK_ORDER_1146_MON_rewarded_ads_activation.md` - Lane A, independent.

## \u2b50 WHAT R5 DOES AND DOES NOT BLOCK

R5 asks: *does the public Buy button stay OFF until payment is proven end to end, or go ON as soon
as the mint is ready?* The programme's own recommendation is **(A) OFF until the checklist is green**.

**R5 IS A SHIPPING DECISION, NOT A BUILD DECISION.** Every step below can be built, tested on devnet
and gated while `RealmStorePurchase` stays `defaultOn:false`. R5 gates only the moment the flag
flips. **Do not wait on it to start.**

The asymmetry that argues for (A), recorded so it is not re-litigated: under (A) the worst case is a
player who cannot buy yet. Under (B) the worst case is **a player who paid and got nothing**, on a
live storefront, with no entitlement record to reconcile from. A delay versus a refund, a support
thread and a store dispute.
"""

A_HEADER = """**Status:** READY TO IMPLEMENT - NO OWNER RULING OUTSTANDING

# WO-1146 - MON - Rewarded ads: activation behind earned-reward proof

**Minted:** 2026-08-22 (CLI; renamed in place from the combined ticket)
**Lane:** **MON** - monetization, dedicated and prioritised.
**Split from:** the combined WO-1146 (owner ruling 2026-08-22).
**Sibling:** `WORK_ORDER_1147_MON_purchasing_verified_entitlement.md` - Lane P, independent.

## \u2b50 WHY THIS LANE GOES FIRST

**Nothing here waits on an owner ruling.** Its blockers are physical-device and dashboard evidence,
which can be obtained today - where the purchasing lane's verifier work needs backend decisions that
are not made yet. `RewardedAdSkip` stays `defaultOn:false` until the owner signs the gate on that
evidence, but every step below can be built and proven before then.
"""

p_doc = (P_HEADER + SPLIT_NOTE + '\n\n---\n\n' + shared_head + '\n\n---\n\n'
         + lane_p.replace('## 3. Lane P', '## 3. Lane P', 1) + '\n\n---\n\n' + shared_tail + '\n')
a_doc = (A_HEADER + SPLIT_NOTE + '\n\n---\n\n' + shared_head + '\n\n---\n\n'
         + lane_a.replace('## 4. Lane A', '## 4. Lane A', 1) + '\n\n---\n\n' + shared_tail + '\n')

io.open(os.path.join(ROOT, 'WorkOrders', 'WORK_ORDER_1147_MON_purchasing_verified_entitlement.md'),
        'w', encoding='utf-8', newline='\n').write(p_doc)
io.open(os.path.join(ROOT, 'WorkOrders', 'WORK_ORDER_1146_MON_rewarded_ads_activation.md'),
        'w', encoding='utf-8', newline='\n').write(a_doc)
os.remove(SRC)

print('WO-1146 MON ads     : %d lines' % len(a_doc.split('\n')))
print('WO-1147 MON purchase: %d lines' % len(p_doc.split('\n')))
print('shared preamble %d, tail %d lines (duplicated into both on purpose)'
      % (len(shared_head.split('\n')), len(shared_tail.split('\n'))))
