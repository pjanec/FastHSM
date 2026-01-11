# BATCH-02 Development Handoff

**Date:** 2026-01-11  
**From:** Tech Lead  
**To:** Developer  
**Status:** 🟢 Ready for Assignment

---

## 📋 Quick Summary

**BATCH-02: RAM Instance Structures** is ready for implementation. This batch builds on BATCH-01's ROM structures and implements the mutable runtime state for each HSM instance.

**Complexity:** Higher than BATCH-01 due to:
- Multiple tier variants (64B, 128B, 256B)
- Unsafe code with fixed buffers
- **Critical architectural fix** for queue layouts
- More complex testing requirements

---

## ⚠️ CRITICAL: Architect's Fix MUST Be Implemented

### The Issue
Original design suggested 3 separate queues for event priorities. **This is mathematically impossible for Tier 1:**

```
Tier 1 event queue space: 32 bytes
Single event size: 24 bytes
If we split into 3 queues: 32 ÷ 3 = 10.67 bytes per queue
10.67 bytes < 24 bytes = IMPOSSIBLE ❌
```

### The Solution (MANDATORY)
**Tier-specific queue strategies:**

| Tier | Strategy | Capacity |
|------|----------|----------|
| Tier 1 (64B) | Single shared FIFO | 1 event |
| Tier 2 (128B) | Hybrid (interrupt slot + shared ring) | 1 interrupt + 1-2 normal |
| Tier 3 (256B) | Hybrid (interrupt slot + shared ring) | 1 interrupt + 5-6 normal |

**Developer MUST implement this correctly or fail the batch.**

---

## 📦 What's Been Prepared

### 1. Batch Instructions ✅
**File:** `.dev-workstream/batches/BATCH-02-INSTRUCTIONS.md`

**Contents:**
- Complete onboarding with architect fix emphasis
- 5 detailed tasks with code templates
- 25+ test requirements
- 6 mandatory questions
- Math explanation for queue layouts
- Quality standards from BATCH-01

**Estimated Effort:** 12-14 hours (1.5 days)

### 2. Dependencies ✅
**Requires:** BATCH-01 (Complete ✅)
- Uses enums from BATCH-01 (`InstanceFlags`, `InstancePhase`)
- Follows same struct layout patterns

---

## 🎯 What Developer Will Build

### Files to Create (5 new files):

**In `src/Fhsm.Kernel/Data/`:**
1. `InstanceHeader.cs` - Common 16-byte header
2. `HsmInstance64.cs` - Tier 1: 64 bytes (single queue)
3. `HsmInstance128.cs` - Tier 2: 128 bytes (hybrid queue)
4. `HsmInstance256.cs` - Tier 3: 256 bytes (hybrid queue)

**In `tests/Fhsm.Tests/Data/`:**
5. `InstanceStructuresTests.cs` - 25+ unit tests

### Critical Requirements:
- ✅ Structs MUST be exact sizes (16B, 64B, 128B, 256B)
- ✅ Use `unsafe` keyword for fixed buffers
- ✅ Implement tier-specific queue layouts (architect's fix)
- ✅ Minimum 25 unit tests
- ✅ XML doc comments on all public types
- ✅ Zero compiler warnings

---

## 📚 Documentation Package

Developer will read (in order):

1. **BATCH-01 Review:** `.dev-workstream/reviews/BATCH-01-REVIEW.md` (15 min) - See what excellence looks like
2. **Architect Review:** `docs/design/ARCHITECT-REVIEW-SUMMARY.md` (20 min) ⚠️ CRITICAL FIX #1
3. **Implementation Spec:** `docs/design/HSM-Implementation-Design.md` Section 1.3 (1.5 hours)
4. **BTree Inspiration:** `docs/btree-design-inspiration/01-Data-Structures.md` (30 min)

**Total Reading:** ~2.5 hours (essential for understanding)

**⚠️ MUST READ:** Architect Review Section on "Tier 1 Event Queue Fragmentation Trap"

---

## 🎓 Key Learning Objectives

This batch teaches:
- Unsafe C# code with fixed buffers
- Tier-based memory optimization
- Cache-friendly struct design (64B, 128B, 256B = power of 2)
- Embedded ring buffer concepts
- Architectural constraint problem-solving

---

## ⚠️ Critical Success Factors

### What MUST Be Exact:
1. **InstanceHeader:** 16 bytes
2. **HsmInstance64:** 64 bytes (single queue!)
3. **HsmInstance128:** 128 bytes (hybrid queue)
4. **HsmInstance256:** 256 bytes (hybrid queue)

### What MUST Be Different From Original Design:
**Tier 1 Queue Strategy:**
- ❌ NOT 3 separate queues (math doesn't work)
- ✅ Single shared FIFO queue (architect's fix)

**Tier 2/3 Queue Strategy:**
- ✅ Hybrid: reserved interrupt slot + shared ring

### Quality Standards:
- Tests verify behavior (read/write fixed arrays, not just "exists")
- Field offset tests catch layout bugs
- Event capacity tests verify math
- All tests MUST pass

---

## 📊 Report Requirements

Developer must submit:
- Completion report: `.dev-workstream/reports/BATCH-02-REPORT.md`
- Must answer 6 specific technical questions
- Must explain architect's fix in own words
- Full test output included
- Any deviations documented

**Q1 Preview:** "Explain the architect's critical fix for Tier 1 event queues. Why is a separate queue per priority class impossible? Show the math."

---

## 🔄 Comparison to BATCH-01

### Similarities:
- Fixed-size structs with `LayoutKind.Explicit`
- Size tests are critical
- Same namespace and project structure
- XML doc comments required

### New Challenges:
- **Unsafe code:** Fixed buffers require `unsafe` keyword
- **Multiple variants:** 3 tier sizes instead of single structs
- **Embedded buffers:** Event queues inside structs
- **Architectural fix:** Must implement tier-specific strategies
- **More tests:** 25+ vs 20+ (more complexity)

---

## 🚨 Watch For

### Potential Issues:
- **Forgetting architect's fix** - Most likely mistake
- **Unsafe keyword missing** - Fixed buffers won't compile
- **Wrong buffer sizes** - Math errors in EventBuffer sizing
- **Shallow tests** - Testing "exists" instead of behavior
- **Field offset errors** - Gaps or overlaps in layout

### Red Flags in Review:
- Tier 1 has 3 queues (violates architect fix)
- Size tests fail
- Tests don't use `unsafe` to verify fixed arrays
- Report doesn't explain architect's fix
- EventBuffer sizes don't match capacity claims

---

## 📈 Success Metrics

**Batch will be approved if:**
- [ ] All 4 struct size tests pass (16B, 64B, 128B, 256B)
- [ ] 25+ quality unit tests written and passing
- [ ] Architect's queue fix correctly implemented
- [ ] Tier 1 uses SINGLE queue (not 3)
- [ ] Tier 2/3 use HYBRID queues
- [ ] No compiler warnings
- [ ] XML doc comments present
- [ ] Report thoroughly answers all 6 questions
- [ ] Code demonstrates understanding of architect's constraints

**Estimated Review Time:** 2-2.5 hours (more complex than BATCH-01)

---

## 🎯 Batch Context

**Phase:** 1 of 5 (Data Layer)  
**Dependencies:** BATCH-01 ✅ Complete  
**Blocks:** BATCH-03 (Event & Command Buffers), BATCH-04 (Definition Blob)  
**Priority:** HIGH - All runtime code depends on these structs

**Why This Matters:**
These structs define the runtime memory layout for every entity running a state machine. Wrong sizes or layouts = broken kernel. The architect's queue fix solves a critical design flaw discovered during review.

---

## 📞 Support

**For Developer:**
- Questions? Create `.dev-workstream/questions/BATCH-02-QUESTIONS.md`
- Unclear about queue fix? ASK BEFORE IMPLEMENTING
- Need clarification? Don't guess!

**For Tech Lead:**
- Monitor questions folder closely
- Be ready to explain tier-specific queue strategies
- Watch for understanding of architect's fix

---

## ✅ Handoff Checklist

- [x] Batch instructions written and comprehensive
- [x] Architect's fix prominently featured
- [x] Task breakdown clear and detailed
- [x] Success criteria explicit
- [x] Quality standards based on BATCH-01 excellence
- [x] Reference materials linked
- [x] Common pitfalls documented (architect fix emphasis)
- [x] Report requirements specified
- [x] 6 mandatory questions prepared
- [x] Code templates provided
- [x] Ready for developer assignment

---

**Status:** 🟢 READY TO ASSIGN

**Next Action:** Assign BATCH-02 to developer and emphasize reading the Architect Review FIRST

---

**This batch is more complex than BATCH-01, but the foundation from that batch will help. The key is understanding and implementing the architect's critical fix correctly. 🚀**

---

**Full instructions:** `.dev-workstream/batches/BATCH-02-INSTRUCTIONS.md`
