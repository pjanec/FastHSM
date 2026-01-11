# Task Tracker

**See:** `TASK-DEFINITIONS.md` for detailed scope and design references.

| # | Task | Status | Actual | Ref |
|---|------|--------|--------|-----|
| 01 | ROM Structs | 🟢 | 0.5d | Design §1.1 |
| 02 | RAM Instances | 🟢 | 0.5d | Design §1.3, Arch Q1 |
| 03 | Event/Command | 🟢 | 0.5d | Design §1.4 |
| 04 | Blob/Instance Mgmt | ⚠️ | 2d | Design §1.2, §3.2 |
| 05 | Compiler - Graph | 🟢 | 1d | Design §2.1 |
| 06 | Compiler - Normalize | 🟡 | - | Design §2.2-2.3, Arch Q3 |
| 07 | Compiler - Flatten | ⚪ | - | Design §2.4-2.5, Arch Q6-Q7 |
| 08 | Kernel - Entry | ⚪ | - | Design §3.1, Arch Q9 |
| 09 | Kernel - Events | ⚪ | - | Design §3.2 |
| 10 | Kernel - RTC | ⚪ | - | Design §3.3, Arch Q4 |
| 11 | Kernel - LCA | ⚪ | - | Design §3.4-3.5 |
| 12 | Hot Reload | ⚪ | - | Design §4.1, Arch Q8 |
| 13 | Debug Trace | ⚪ | - | Design §4.2 |
| 14 | Console Example | ⚪ | - | - |
| 15 | Docs/Polish | ⚪ | - | - |

**Progress:** 4 done, 1 needs fixes, 10 remaining  
**Current:** BATCH-06 (Compiler - Normalize/Validate)

**Phases:**
- ✅ Phase 1.1-1.3: Data Layer (3/4 done)
- ⚠️ Phase 1.4: BATCH-04 fixes needed
- 🟡 Phase 2: Compiler (1/3 done)
- ⚪ Phase 3: Kernel
- ⚪ Phase 4: Tooling
- ⚪ Phase 5: Examples

**Key:**
- Design = `docs/design/HSM-Implementation-Design.md`
- Arch = `docs/design/ARCHITECT-REVIEW-SUMMARY.md`
