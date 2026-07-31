# WO-798 — WC3-style queue visual (build on live chip)

**WO:** `WorkOrders/WORK_ORDER_798_wc3_style_queue_visual.md` (**rewritten on code**)  
**Audience:** Claude (read-only design) → owner sign-off → CLI implement  
**Premise:** Upgrade shipped **right-column Builders chip + 5-deep text rows** — not a greenfield dock.

| File | Purpose |
|------|---------|
| **`CODE_AS_IS.md`** | **Read first** — live PublishStatus / FormatQueueRows / QueueStatus anchors |
| **`WIREFRAMES.md`** | **Layout A′** = same host, icons+rings; bottom dock = alternate only |
| `wireframe_A_production_dock.html` | Feel reference (icons/rings); placement default is **right column** |
| `layout_A.svg` | Vector feel reference |

**Claude next:** before/after of **QueueStatus** band → M1/M2/M3 multi-channel pick → image pairs.  
**CLI later:** extend `QueueEntry` + restyle `_queueRowsPlate` after sign-off.
