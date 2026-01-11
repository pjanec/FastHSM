# FastHSM Implementation - Task Definitions

**Purpose:** Complete implementation plan with scope, deliverables, and design references.  
**For:** Future developers, code reviewers, project managers.

---

## Architecture Overview

FastHSM is a **high-performance Hierarchical State Machine library** designed for ECS game engines. It follows a "bytecode interpreter" pattern similar to FastBTree.

**Core Design Principles:**
- **Data-Oriented Design:** Immutable ROM (definitions) + mutable RAM (instances)
- **Zero-Allocation Runtime:** No GC pressure in hot path
- **Cache-Friendly:** Fixed-size blittable structs (64B/128B/256B tiers)
- **Deterministic Execution:** Reproducible across machines/builds
- **ECS-Compatible:** Unmanaged components, batched updates, void* context

**Architecture Layers:**
1. **Data Layer:** ROM/RAM structs, memory layout
2. **Compiler:** User API → flat bytecode blob
3. **Kernel:** Tick execution, LCA transitions, RTC loop
4. **Tooling:** Hot reload, debug tracing

**Key References:**
- `docs/design/HSM-design-talk.md` - Requirements & architecture decisions
- `docs/design/HSM-Implementation-Design.md` - Detailed implementation spec
- `docs/design/ARCHITECT-REVIEW-SUMMARY.md` - Critical fixes & directives
- `docs/btree-design-inspiration/` - Design pattern inspiration

---

## Phase 1: Data Layer (5 days)

**Goal:** Define all ROM/RAM structures with exact byte layouts. Verify sizes, offsets, blittability.

**Design Ref:** `HSM-Implementation-Design.md` Section 1 (Data Layer)

---

### BATCH-01: ROM Data Structures (Core)
**Effort:** 1.5 days → **Actual: 0.5 days** ✅

**Scope:**
Implement immutable definition structures (ROM) that will be stored in `HsmDefinitionBlob`.

**Deliverables:**
- `Enums.cs` - StateFlags, TransitionFlags, EventPriority, InstancePhase, InstanceFlags
- `StateDef.cs` - 32-byte state definition
- `TransitionDef.cs` - 16-byte transition definition
- `RegionDef.cs` - 8-byte orthogonal region definition
- `GlobalTransitionDef.cs` - 16-byte global transition (interrupts)
- 20+ unit tests verifying sizes, offsets, flag manipulation

**Critical Constraints:**
- Exact sizes MUST match spec (cache line optimization)
- `LayoutKind.Explicit` for deterministic layout
- Blittable (no GC references)
- `ushort` indices for 64K state/transition limit

**Design Details:**
- **StateDef (32B):** ParentIndex, FirstChildIndex, NextSiblingIndex, FirstTransitionIndex, Depth, Flags, ActionBindings (Entry/Exit/Activity/Timer), HistorySlotIndex, Reserved
- **TransitionDef (16B):** SourceStateIndex, TargetStateIndex, EventId, GuardId, ActionId, Flags (includes 4-bit priority), Cost
- **Architect Decision Q6:** Transition cost is structural-only (LCA distance)

**Testing Focus:**
- Size validation (Marshal.SizeOf)
- Field offset verification (pointer arithmetic)
- Flag extraction (priority from TransitionFlags)
- Edge cases (max indices, max depth)

---

### BATCH-02: RAM Instance Structures
**Effort:** 1.5 days → **Actual: 0.5 days** ✅

**Scope:**
Implement mutable per-instance state (RAM) with tier-specific memory layouts.

**Deliverables:**
- `InstanceHeader.cs` - 16-byte common header (all tiers)
- `HsmInstance64.cs` - 64-byte instance (Tier 1: Crowd)
- `HsmInstance128.cs` - 128-byte instance (Tier 2: Standard)
- `HsmInstance256.cs` - 256-byte instance (Tier 3: Complex)
- 18+ unit tests verifying layouts, fixed arrays, event buffers

**Critical Constraints:**
- **ARCHITECT CRITICAL FIX (Q1):** Tier-specific event queue strategies
  - **Tier 1 (64B):** Single shared queue (24B = 1 event). Interrupt overwrites oldest normal if full.
  - **Tier 2 (128B):** Hybrid: 24B reserved slot for interrupt + 44B shared ring (1-2 events)
  - **Tier 3 (256B):** Hybrid: 24B reserved slot for interrupt + 132B shared ring (5-6 events)

**Design Details:**
- **InstanceHeader (16B):** MachineId, Generation, Phase, Flags, RandomSeed, QueueHead/Tail, CurrentDepth
- **Tier capacities:**
  - Tier 1: 2 regions, 2 timers, 4 history slots, 24B event buffer
  - Tier 2: 4 regions, 4 timers, 8 history slots, 68B event buffer
  - Tier 3: 8 regions, 8 timers, 16 history slots, 156B event buffer

**Testing Focus:**
- Exact sizes (64/128/256 bytes)
- Field offsets match spec
- Fixed array access (`fixed ushort ActiveLeafIds[]`)
- Event buffer capacities correct

**Design Ref:** `ARCHITECT-REVIEW-SUMMARY.md` Q1 (Event Queue Fix)

---

### BATCH-03: Event & Command Buffers
**Effort:** 1 day → **Actual: 0.5 days** ✅

**Scope:**
Implement I/O protocol structures for event queuing and command emission.

**Deliverables:**
- `HsmEvent.cs` - 24-byte fixed-size event
- `EventFlags` enum - IsDeferred, IsIndirect, IsConsumed
- `CommandPage.cs` - 4096-byte command buffer page
- `HsmCommandWriter.cs` - ref struct for zero-alloc command writing
- 20+ unit tests verifying event payload, command writer overflow protection

**Critical Constraints:**
- **HsmEvent:** Fixed 24B (8B header + 16B payload). Larger data uses indirection (ID-only).
- **CommandPage:** 4KB (OS page size, Architect Decision Q2)
- **HsmCommandWriter:** `ref struct` (stack-only, prevents dangling pointers)

**Design Details:**
- **Event header:** EventId (ushort), Priority (byte), Flags (byte), Timestamp (uint)
- **Payload:** 16-byte inline buffer. Larger data → store ID, set `IsIndirect` flag
- **CommandPage:** 16B header (BytesUsed, PageIndex, NextPageOffset) + 4080B data area
- **Writer:** TryWriteCommand returns false on overflow (no allocation, no exceptions)

**Testing Focus:**
- Event size exactly 24 bytes
- Payload read/write (int, float, struct)
- Command writer capacity tracking
- Overflow protection works

**Design Ref:** `HSM-Implementation-Design.md` Section 1.4 (Events & Commands)

---

### BATCH-04: Definition Blob & Instance Management
**Effort:** 1 day → **Actual: 2 days** ⚠️ CHANGES REQUIRED

**Scope:**
Complete data layer with ROM container, instance lifecycle, event queue operations, validation.

**Deliverables:**
- `HsmDefinitionHeader.cs` - 32-byte blob header (magic, hashes, counts)
- `HsmDefinitionBlob.cs` - ROM container with span accessors
- `HsmInstanceManager.cs` - Initialize/Reset/SelectTier
- `HsmEventQueue.cs` - Tier-specific enqueue/dequeue (implements Architect's fix)
- `HsmValidator.cs` - Definition/instance validation
- 30+ integration tests

**Critical Constraints:**
- **Header:** Magic 0x4D534846 ('FHSM'), StructureHash (topology), ParameterHash (logic)
- **Blob:** `sealed class`, arrays `private readonly`, expose `ReadOnlySpan<T>` for zero-alloc
- **Event Queue:** MUST implement tier-specific strategies from Architect Q1
- **Tier Selection:** Heuristics based on StateCount, MaxDepth, HistorySlots, RegionCount

**Design Details:**
- **Tier Thresholds:**
  - Tier 1 (64B): States ≤8, Depth ≤3, History ≤2, Regions ≤1
  - Tier 2 (128B): States ≤32, Depth ≤6, History ≤4, Regions ≤2
  - Tier 3 (256B): Everything else
- **Event Queue Implementation:**
  - Tier 1: Single queue, interrupt overwrites oldest normal
  - Tier 2/3: Check reserved slot first (interrupts), then shared ring (normal/low)
  - Ring buffer with wraparound, head/tail cursors

**Testing Focus:**
- Blob span accessors zero-allocation
- Tier selection logic (threshold tests)
- Tier 1 overwrite behavior
- Tier 2/3 reserved slot priority
- Ring wraparound works
- Validation catches invalid definitions

**Current Issues (BATCH-04):**
- Blob not sealed, arrays exposed as public
- Missing ActionIds/GuardIds dispatch tables
- Event queue ring doesn't enforce priority ordering within ring

**Design Ref:** `HSM-Implementation-Design.md` Sections 1.2 (Blob), 1.3 (Instance Layout), 3.2 (Event Processing)

---

## Phase 2: Compiler (6 days)

**Goal:** Transform user-defined state machines into `HsmDefinitionBlob` bytecode.

**Design Ref:** `HSM-Implementation-Design.md` Section 2 (Compiler Pipeline)

**Pipeline Stages:**
1. Builder API → Graph
2. Normalizer (assign IDs, resolve implicit states)
3. Validator (check rules)
4. Flattener (tree → flat arrays)
5. Emitter (serialize to blob)

---

### BATCH-05: Compiler - Graph Builder & Parser
**Effort:** 2 days

**Scope:**
Create fluent C# API for defining state machines + intermediate graph representation.

**Deliverables:**
- `Graph/StateNode.cs` - Mutable state node with children, transitions, regions
- `Graph/TransitionNode.cs` - Source/target, event, guard, action
- `Graph/RegionNode.cs` - Orthogonal region container
- `Graph/StateMachineGraph.cs` - Root container with state dictionary, event registry
- `HsmBuilder.cs` - Public fluent API (State().OnEntry().On().GoTo())
- 15+ tests verifying API creates correct graph structures

**Critical Constraints:**
- **StableId (Guid):** Each state gets GUID for hot reload stability (Architect Q3)
- **Function names as strings:** Resolved to function pointers by source gen later
- **Fluent API:** Chainable methods for ergonomic definition

**Design Details:**
- **StateNode:** Name, StableId, Parent, Children[], Transitions[], Regions[], IsInitial, IsHistory, Actions (OnEntry/Exit/Activity/Timer)
- **Builder pattern:** HsmBuilder → StateBuilder → TransitionBuilder
- **Event registry:** Map event names to ushort IDs
- **Function registry:** Track registered action/guard names for validation

**Example Usage:**
```csharp
var builder = new HsmBuilder("Enemy")
    .Event("Damaged", 1)
    .Event("Death", 2)
    .RegisterAction("TakeDamage")
    .RegisterAction("Die")
    .State("Idle")
        .OnEntry("Initialize")
        .On("Damaged").GoTo("Hurt").Action("TakeDamage")
        .Child("Patrolling")
            .Activity("Patrol");
```

**Testing Focus:**
- Builder creates graph with correct structure
- State hierarchy preserved
- Transitions connect correct states
- Events registered with IDs
- Duplicate state names throw
- Unknown event/state references throw

**Design Ref:** `HSM-Implementation-Design.md` Section 2.1 (Builder Graph)

---

### BATCH-06: Compiler - Normalizer & Validator
**Effort:** 2 days

**Scope:**
Normalize graph (assign flat indices, compute depths) and validate correctness.

**Deliverables:**
- `HsmNormalizer.cs` - Assign FlatIndex, compute Depth, resolve initial states
- `HsmGraphValidator.cs` - Structural validation rules
- Validator rules (~20 checks)
- 20+ tests covering normalization and validation

**Critical Constraints:**
- **History slot assignment:** MUST sort by StableId (Guid) for hot reload stability (Architect Q3)
- **Initial state resolution:** Each composite needs initial child (explicit or first)
- **Depth computation:** Parent depth + 1
- **FlatIndex assignment:** Breadth-first for cache locality

**Validation Rules:**
- No orphan states (all have path to root)
- No circular parent chains
- All transitions target valid states
- Initial states exist where required
- Function names registered (actions/guards)
- No duplicate state names
- Event IDs unique
- State depth ≤ max (from tier limits)
- Region count ≤ max (from tier limits)
- History states have valid parent

**Testing Focus:**
- FlatIndex assigned in correct order
- Depth computed correctly for nested states
- History slots sorted by StableId (not name)
- Validation catches circular dependencies
- Validation catches missing initial states
- Validation catches invalid transitions

**Design Ref:** `HSM-Implementation-Design.md` Section 2.2 (Normalization), 2.3 (Validation)

---

### BATCH-07: Compiler - Flattener & Emitter
**Effort:** 2 days

**Scope:**
Convert normalized graph to flat arrays and emit `HsmDefinitionBlob`.

**Deliverables:**
- `HsmFlattener.cs` - Graph → flat StateDef[], TransitionDef[], etc.
- `HsmEmitter.cs` - Create HsmDefinitionBlob from flat arrays
- Hash computation (StructureHash, ParameterHash)
- 15+ tests verifying correct blob emission

**Critical Constraints:**
- **StructureHash:** Hash topology only (parent/child structure, state count)
- **ParameterHash:** Hash logic (actions, guards, event IDs)
- **Transition cost:** Compute LCA structural distance (Architect Q6)
- **Global transitions:** Separate table (Architect Q7)

**Design Details:**
- **Flattening:**
  - Walk graph in BFS order (assigned FlatIndex)
  - Convert StateNode → StateDef (indices replace pointers)
  - Convert TransitionNode → TransitionDef
  - Build dispatch tables (ActionIds[], GuardIds[])
- **Hashing:**
  - StructureHash: Hash(StateCount, ParentIndices[], Depths[])
  - ParameterHash: Hash(ActionBindings[], GuardIds[], EventIds[])
- **Header population:** StateCount, TransitionCount, RegionCount, etc.

**Testing Focus:**
- Flattened arrays match graph structure
- Parent/child indices correct
- Transition indices point to correct states
- StructureHash stable across renames
- ParameterHash changes when logic changes
- Blob validates with HsmValidator

**Design Ref:** `HSM-Implementation-Design.md` Section 2.4 (Flattening), 2.5 (Emission)

---

## Phase 3: Kernel (8.5 days)

**Goal:** Implement runtime execution engine (tick, events, transitions, LCA).

**Design Ref:** `HSM-Implementation-Design.md` Section 3 (Runtime Kernel)

**Kernel Phases (per tick):**
1. Timer Decrement
2. Event Processing (priority-sorted)
3. Run-To-Completion (RTC) loop
4. Activity Execution

---

### BATCH-08: Kernel - Entry Point & Setup
**Effort:** 1.5 days

**Scope:**
Core kernel API with phase management and instance linking.

**Deliverables:**
- `HsmKernel.cs` - UpdateBatch API (thin shim pattern)
- `HsmKernelCore.cs` - Void* core (non-generic, Architect Q9)
- Instance phase transitions (Idle → Entry → RTC → Activity → Idle)
- Definition-instance linking
- 10+ tests

**Critical Constraints:**
- **Thin Shim Pattern (Architect Directive 1):**
  ```csharp
  // Non-generic core (compiled once)
  private static unsafe void UpdateBatchCore(void* instancePtr, void* contextPtr, ...)
  
  // Generic wrapper (inlined)
  [MethodImpl(AggressiveInlining)]
  public static unsafe void UpdateBatch<T, TContext>(...) where T : unmanaged where TContext : unmanaged
  {
      fixed (void* ctx = &context)
      fixed (void* inst = instances)
      {
          UpdateBatchCore(inst, ctx, ...);
      }
  }
  ```
- **Deterministic execution:** No implicit ordering, explicit phase sequencing

**Design Details:**
- **UpdateBatch signature:** `UpdateBatch<T>(HsmDefinitionBlob def, Span<T> instances, in TContext context)`
- **Phase flow:** Check phase, execute corresponding kernel pass, advance phase
- **Batch processing:** Process multiple instances in one call (ECS-style)

**Testing Focus:**
- Phase transitions correct
- Batch processes all instances
- Generic wrapper eliminates overhead (benchmark?)
- Invalid phase throws/fails gracefully

**Design Ref:** `HSM-Implementation-Design.md` Section 3.1 (Entry Point), Architect Q9

---

### BATCH-09: Kernel - Timer & Event Processing
**Effort:** 2 days

**Scope:**
Implement timer decrement and priority-based event processing.

**Deliverables:**
- Timer decrement (Phase 1)
- Event dequeue with priority (Interrupt > Normal > Low)
- Deferred event handling
- Budget-gated catch-up (prevent starvation)
- 15+ tests

**Critical Constraints:**
- **Timer semantics:** Decrement all timers by delta, fire event when deadline ≤ 0
- **Priority ordering:** Always process highest priority first (Architect Q1)
- **Budget limit:** Max N events per tick to prevent infinite loops
- **Deferred events:** Re-enqueue at end of tick if IsDeferred flag set

**Design Details:**
- **Phase 1 (Timer):**
  - Iterate TimerDeadlines[] in instance
  - Decrement by deltaTime
  - If deadline ≤ 0, enqueue timer event (from state definition)
- **Phase 2 (Event):**
  - Dequeue highest priority event (HsmEventQueue.TryDequeue)
  - Check budget (remaining event processing quota)
  - Process event → find matching transitions
  - If consumed, continue. If deferred, re-enqueue at low priority

**Testing Focus:**
- Timers decrement correctly
- Timer fires event at deadline
- Event priority ordering (interrupt processed first)
- Deferred events re-enqueued
- Budget prevents runaway event loops

**Design Ref:** `HSM-Implementation-Design.md` Section 3.2 (Event Processing)

---

### BATCH-10: Kernel - RTC Loop & Transitions
**Effort:** 3 days

**Scope:**
Run-To-Completion loop with transition selection and execution.

**Deliverables:**
- RTC loop (bounded iteration)
- Transition selection (priority, guards)
- Guard evaluation with context
- Action execution
- Fail-safe state (prevent infinite loops)
- 20+ tests

**Critical Constraints:**
- **RTC semantics:** Process transition chains until stable or max iterations
- **Guard evaluation:** Call user-provided guard function with context
- **RNG in guards (Architect Q4):** Allowed if `[HsmGuard(UsesRNG=true)]`, debug-only access tracking
- **Max iterations:** Clamp at ~100 to prevent infinite loops, enter fail-safe state if exceeded
- **Deterministic selection:** Priority-sorted, first matching guard wins

**Design Details:**
- **RTC loop:**
  ```
  for (int i = 0; i < MaxRTCIterations; i++) {
      transition = SelectTransition(currentState, event, context);
      if (transition == null) break;  // Stable
      ExecuteTransition(transition, context);
  }
  if (i >= MaxRTCIterations) EnterFailSafeState();
  ```
- **Transition selection:**
  - Get transitions for current state (FirstTransitionIndex, range)
  - Filter by EventId
  - Sort by priority (high → low)
  - Evaluate guards in order
  - Return first where guard returns true
- **Guard evaluation:**
  - Cast void* context to TContext*
  - Call function pointer from dispatch table
  - Pass: instance, context, event payload

**Testing Focus:**
- Simple transition works (A → B)
- Guard blocks transition
- Priority determines selection order
- RTC executes chain (A → B → C)
- Max iterations triggers fail-safe
- Multiple guards evaluated in priority order

**Design Ref:** `HSM-Implementation-Design.md` Section 3.3 (RTC Loop), Architect Q4

---

### BATCH-11: Kernel - LCA & Execution
**Effort:** 2 days

**Scope:**
Least Common Ancestor algorithm and hierarchical transition execution (exit/enter actions).

**Deliverables:**
- LCA computation
- Exit action execution (bottom-up)
- Entry action execution (top-down)
- History state restore
- Activity execution (Phase 4)
- 20+ tests

**Critical Constraints:**
- **LCA algorithm:** Find common ancestor of source and target states
- **Exit order:** Bottom-up (leaf to LCA)
- **Enter order:** Top-down (LCA to target)
- **History restore:** Load HistorySlots, validate with IsAncestor (Architect Q3)
- **Internal transitions:** Skip exit/entry, only run transition action

**Design Details:**
- **LCA computation:**
  ```
  LCA(source, target):
      Walk source up to root, mark ancestors
      Walk target up until hit marked ancestor
  ```
- **Exit path:** [currentLeaf, parent, ..., LCA]
- **Enter path:** [LCA, ..., parent, targetLeaf]
- **Action execution:** Call via function pointer from dispatch table
- **History:** Read HistorySlots[stateHistoryIndex], check IsValidStateId + IsAncestor

**Testing Focus:**
- LCA of siblings correct (parent)
- LCA of parent-child (parent)
- Exit actions run bottom-up
- Entry actions run top-down
- History state restores correct leaf
- Internal transition skips exit/entry
- Activity runs in Phase 4

**Design Ref:** `HSM-Implementation-Design.md` Section 3.4 (LCA Resolution), 3.5 (Execution), Architect Q3

---

## Phase 4: Tooling (3 days)

**Goal:** Hot reload and debug tracing for development workflow.

**Design Ref:** `HSM-Implementation-Design.md` Section 4 (Tooling & Observability)

---

### BATCH-12: Hot Reload Manager
**Effort:** 1.5 days

**Scope:**
Detect blob changes and safely reload or reset instances.

**Deliverables:**
- `HsmHotReloadManager.cs` - Compare hashes, decide reload strategy
- Structure change detection (StructureHash)
- Parameter change detection (ParameterHash)
- Instance migration or reset
- 10+ tests

**Critical Constraints:**
- **Structure change → Hard reset:** Memory layout changed, unsafe to migrate
- **Parameter change → Preserve state:** Only logic changed, keep active states
- **Hash comparison:** Compare new blob header with running instance MachineId

**Design Details:**
- **Reload decision:**
  ```
  if (newBlob.StructureHash != instance.MachineId) → HardReset
  else if (newBlob.ParameterHash != old.ParameterHash) → SoftReload
  else → NoChange
  ```
- **Hard reset:** Call HsmInstanceManager.Reset, loses state
- **Soft reload:** Update MachineId, preserve ActiveLeafIds/History

**Testing Focus:**
- Structure change triggers hard reset
- Parameter change preserves state
- Hash comparison correct
- Multiple instances handled

**Design Ref:** `HSM-Implementation-Design.md` Section 4.1 (Hot Reload), Architect Q3, Q8

---

### BATCH-13: Debug Tracing
**Effort:** 1.5 days

**Scope:**
Ring buffer trace recording for debugging and replay.

**Deliverables:**
- `HsmTraceBuffer.cs` - Per-thread ring buffer
- Trace record types (StateEntry, StateExit, Transition, Event, GuardEval)
- Trace filtering (by entity ID, event type, mode)
- Trace export (binary format)
- 10+ tests

**Critical Constraints:**
- **Zero-allocation:** Pre-allocated ring buffer, no GC pressure
- **Thread-local:** One buffer per thread, no locking
- **Filtering:** Runtime enable/disable by mode (Architect Q8)
- **Binary format:** Efficient, no text parsing

**Design Details:**
- **Trace record:** Timestamp, InstanceId, RecordType, Data (state/event IDs)
- **Ring buffer:** Fixed size (e.g., 4096 records), wraparound on overflow
- **Filtering modes:** All, EntityId, EventType, StateTransitions, GuardsOnly
- **Export:** Dump to byte array for external analysis

**Testing Focus:**
- Records added to buffer
- Ring wraparound works
- Filtering excludes correct records
- Export produces valid binary
- Thread-local buffers don't conflict

**Design Ref:** `HSM-Implementation-Design.md` Section 4.2 (Debug Tracing), Architect Q8

---

## Phase 5: Examples & Polish (3 days)

**Goal:** Working example + documentation for users.

---

### BATCH-14: Console Example Application
**Effort:** 1 day

**Scope:**
Complete, runnable example demonstrating all features.

**Deliverables:**
- `Fhsm.Examples.Console` - Traffic light or enemy AI example
- Define machine with builder API
- Compile to blob
- Run kernel with instances
- Print state transitions
- README with explanation

**Design Details:**
- **Example machine:** Traffic light (Red → Yellow → Green) or simple enemy AI
- **Demonstrate:**
  - Hierarchical states (nested)
  - Transitions with guards
  - Timers
  - Events (external triggers)
  - Actions (console output)
- **Code walkthrough:** Commented to explain each part

**Testing Focus:**
- Example compiles
- Example runs without errors
- State transitions visible in output
- README clear for new users

---

### BATCH-15: Documentation & Polish
**Effort:** 2 days

**Scope:**
API documentation, performance notes, final cleanup.

**Deliverables:**
- API reference (XML doc comments)
- Performance guide (tier selection, memory layout)
- Architecture overview document
- Migration guide (from other HSM libs)
- Final code review and cleanup

**Testing Focus:**
- All public APIs have XML docs
- No compiler warnings
- Code style consistent
- README.md at root with quick start

---

## Summary

**Total:** 15 batches, ~24 days estimated

**Critical Path:**
- Phase 1 (Data) → Phase 2 (Compiler) → Phase 3 (Kernel) → Phase 4/5 (Tooling/Examples)

**Architect's Critical Decisions:**
- Q1: Event queue tier-specific strategies (BATCH-02, BATCH-04)
- Q3: History slot StableId sorting (BATCH-06)
- Q4: RNG in guards allowed with attribute (BATCH-10)
- Q6: Structural-only transition cost (BATCH-07)
- Q7: Separate global transition table (BATCH-07)
- Q8: All trace filtering modes (BATCH-13)
- Q9: Thin shim pattern with AggressiveInlining (BATCH-08)

**Current Status:** 
- Phase 1: 3 batches done, 1 needs fixes (blob refactor)
- Phase 2: Starting BATCH-05

---

**For questions about task scope, see:**
- This document (TASK-DEFINITIONS.md)
- Design: `docs/design/HSM-Implementation-Design.md`
- Architect: `docs/design/ARCHITECT-REVIEW-SUMMARY.md`
- Status: `.dev-workstream/TASK-TRACKER.md`
- Batches: `.dev-workstream/batches/BATCH-XX-INSTRUCTIONS.md`
