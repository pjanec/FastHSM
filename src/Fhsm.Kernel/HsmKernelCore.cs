using System;
using Fhsm.Kernel.Data;

namespace Fhsm.Kernel
{
    /// <summary>
    /// Non-generic kernel core. Compiled once, no generic expansion.
    /// Uses void* for type erasure.
    /// </summary>
    internal static unsafe class HsmKernelCore
    {
        private static HsmTraceBuffer? _traceBuffer = null;

        public static void SetTraceBuffer(HsmTraceBuffer? buffer)
        {
            _traceBuffer = buffer;
        }

        // Event ID for Timer Fired (System Reserved)
        private const ushort TimerEventId = 0xFFFE;

        // Configuration offsets for CurrentEventId scratch space
        private const int CurrentEventId_Offset_64 = 20;
        private const int CurrentEventId_Offset_128 = 58;
        private const int CurrentEventId_Offset_256 = 98;

        // Internal struct for LCA path
        private struct TransitionPath
        {
            public ushort LCA;
            public ushort ExitCount;  // Number of states to exit
            public ushort EntryCount; // Number of states to enter
            public fixed ushort ExitPath[16];  // Max depth 16
            public fixed ushort EntryPath[16];
        }

        /// <summary>
        /// Process instances through one tick.
        /// </summary>
        internal static void UpdateBatchCore(
            HsmDefinitionBlob definition,
            void* instancePtr,
            int instanceCount,
            int instanceSize,
            void* contextPtr,
            float deltaTime)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (instancePtr == null) throw new ArgumentNullException(nameof(instancePtr));
            if (instanceCount <= 0) return;
            
            // Process each instance
            for (int i = 0; i < instanceCount; i++)
            {
                byte* instPtr = (byte*)instancePtr + (i * instanceSize);
                InstanceHeader* header = (InstanceHeader*)instPtr;
                
                // Skip instances with invalid phase or wrong definition
                if (!ValidateInstance(header, definition))
                {
                    continue;
                }
                
                // Process based on current phase
                ProcessInstancePhase(
                    definition,
                    instPtr,
                    instanceSize,
                    contextPtr,
                    deltaTime,
                    header);
            }
        }
        
        private static bool ValidateInstance(InstanceHeader* header, HsmDefinitionBlob definition)
        {
            if (header->MachineId != definition.Header.StructureHash) return false;
            if (header->Phase > InstancePhase.Activity) return false;
            return true;
        }
        
        private static void ProcessInstancePhase(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            void* contextPtr,
            float deltaTime,
            InstanceHeader* header)
        {
            switch (header->Phase)
            {
                case InstancePhase.Idle:
                    // Timer Phase
                    ProcessTimerPhase(definition, instancePtr, instanceSize, deltaTime);
                    
                    // Check if any events in queue (triggered by timers or external)
                    if (HsmEventQueue.GetCount(instancePtr, instanceSize) > 0)
                    {
                        header->Phase = InstancePhase.Entry;
                    }
                    break;
                    
                case InstancePhase.Entry:
                    // Advance to Event processing
                    ProcessEventPhase(definition, instancePtr, instanceSize, contextPtr);
                    break;
                    
                case InstancePhase.RTC:
                    ushort eventId = GetCurrentEventId(instancePtr, instanceSize);
                    ProcessRTCPhase(definition, instancePtr, instanceSize, contextPtr, eventId);
                    break;
                    
                case InstancePhase.Activity:
                    ProcessActivityPhase(definition, instancePtr, instanceSize, contextPtr, deltaTime);
                    break;
            }
        }

        // --- Task 1: Timer Phase ---

        private static void ProcessTimerPhase(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            float deltaTime)
        {
            uint* timers = GetTimerArray(instancePtr, instanceSize, out int timerCount);
            if (timers == null || timerCount == 0) return;

            ushort* activeLeafIds = GetActiveLeafIds(instancePtr, instanceSize, out int regionCount);
            
            // Decrement all timers
            uint deltaTicks = (uint)(deltaTime * 1000f); // ms
            
            for (int i = 0; i < timerCount; i++)
            {
                if (timers[i] > 0)
                {
                    if (timers[i] > deltaTicks)
                    {
                        timers[i] -= deltaTicks;
                    }
                    else
                    {
                        timers[i] = 0;
                        // Timer fired
                        FireTimerEvent(definition, instancePtr, instanceSize, i, activeLeafIds, regionCount);
                    }
                }
            }
        }

        private static void FireTimerEvent(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            int timerIndex,
            ushort* activeLeafIds,
            int regionCount)
        {
            var timerEvent = new HsmEvent
            {
                EventId = TimerEventId,
                Priority = EventPriority.Normal,
                Timestamp = 0
            };
            
            HsmEventQueue.TryEnqueue(instancePtr, instanceSize, timerEvent);
        }

        // --- Task 2: Event Phase ---

        private static void ProcessEventPhase(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            void* contextPtr)
        {
            const int MaxEventsPerTick = 10;
            int eventsProcessed = 0;
            InstanceHeader* header = (InstanceHeader*)instancePtr;
            
            while (eventsProcessed < MaxEventsPerTick)
            {
                if (!HsmEventQueue.TryDequeue(instancePtr, instanceSize, out HsmEvent evt))
                {
                    break;
                }
                
                eventsProcessed++;
                
                // Trace event handled
                if (_traceBuffer != null && (header->Flags & InstanceFlags.DebugTrace) != 0)
                {
                    byte result = 0; // 0=consumed
                    if ((evt.Flags & EventFlags.IsDeferred) != 0)
                        result = 1; // 1=deferred
                    
                    _traceBuffer.WriteEventHandled(header->MachineId, evt.EventId, result);
                }

                if ((evt.Flags & EventFlags.IsDeferred) != 0)
                {
                    // Re-enqueue deferred event with Low priority
                    evt.Priority = EventPriority.Low;
                    evt.Flags &= ~EventFlags.IsDeferred;
                    HsmEventQueue.TryEnqueue(instancePtr, instanceSize, evt);
                    continue;
                }
                
                StoreCurrentEventId(instancePtr, instanceSize, evt.EventId);
                header->Phase = InstancePhase.RTC;
                return; // Go to RTC phase
            }
            
            // No more events, go to Idle
            header->Phase = InstancePhase.Idle;
        }

        // --- Task 4: Activity Phase ---

        private static void ProcessActivityPhase(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            void* contextPtr,
            float deltaTime)
        {
            InstanceHeader* header = (InstanceHeader*)instancePtr;
            ushort* activeLeafIds = GetActiveLeafIds(instancePtr, instanceSize, out int regionCount);
            
            // Execute activities for all active states
            for (int r = 0; r < regionCount; r++)
            {
                ushort leafId = activeLeafIds[r];
                if (leafId == 0xFFFF) continue;
                
                // Walk up from leaf to root, executing activities
                ushort current = leafId;
                while (current != 0xFFFF)
                {
                    ref readonly var state = ref definition.GetState(current);
                    
                    // Execute activity if present
                    if (state.ActivityActionId != 0 && state.ActivityActionId != 0xFFFF)
                    {
                        ExecuteAction(state.ActivityActionId, instancePtr, contextPtr, 0);
                    }
                    
                    current = state.ParentIndex;
                }
            }
            
            // Return to Idle
            header->Phase = InstancePhase.Idle;
        }

        // --- Task 3: RTC Phase ---

        private static void ProcessRTCPhase(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            void* contextPtr,
            ushort currentEventId)
        {
            const int MaxRTCIterations = 100;
            InstanceHeader* header = (InstanceHeader*)instancePtr;
            ushort* activeLeafIds = GetActiveLeafIds(instancePtr, instanceSize, out int regionCount);
            
            int iteration = 0;
            while (iteration < MaxRTCIterations)
            {
                iteration++;

                TransitionDef? selectedTransition = SelectTransition(
                    definition,
                    instancePtr,
                    instanceSize,
                    activeLeafIds,
                    regionCount,
                    currentEventId,
                    contextPtr);
                
                if (selectedTransition == null)
                {
                    // Consumed
                    break;
                }
                
                ExecuteTransition(definition, instancePtr, instanceSize, selectedTransition.Value, activeLeafIds, regionCount, contextPtr);
                
                // Break after one transition per event (Standard Run-to-Completion step)
                break;
            }
            
            // Advance to Activity
            header->Phase = InstancePhase.Activity;
        }

        private static TransitionDef? SelectTransition(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            ushort* activeLeafIds,
            int regionCount,
            ushort eventId,
            void* contextPtr)
        {
            // 1. Global transitions
            var globalSpan = definition.GlobalTransitions;
            for (int i = 0; i < globalSpan.Length; i++)
            {
                ref readonly var gt = ref globalSpan[i];
                if (gt.EventId == eventId)
                {
                    if (gt.GuardId == 0 || EvaluateGuard(gt.GuardId, instancePtr, contextPtr, eventId))
                    {
                        return new TransitionDef
                        {
                            SourceStateIndex = activeLeafIds[0],
                            TargetStateIndex = gt.TargetStateIndex,
                            EventId = gt.EventId,
                            GuardId = gt.GuardId,
                            ActionId = gt.ActionId,
                            Flags = gt.Flags,
                            Cost = 0
                        };
                    }
                }
            }

            // 2. Active state transitions
            TransitionDef? bestTransition = null;
            byte highestPriority = 0;

            for (int r = 0; r < regionCount; r++)
            {
                ushort leafId = activeLeafIds[r];
                if (leafId == 0xFFFF) continue;

                ushort current = leafId;
                while (current != 0xFFFF)
                {
                    ref readonly var state = ref definition.GetState(current);

                    if (state.FirstTransitionIndex != 0xFFFF)
                    {
                        for (ushort t = 0; t < state.TransitionCount; t++)
                        {
                            ushort transIndex = (ushort)(state.FirstTransitionIndex + t);
                            ref readonly var trans = ref definition.GetTransition(transIndex);

                            // Match event
                            if (trans.EventId == eventId)
                            {
                                // Priority is top 4 bits (12-15) of Flags
                                byte priority = (byte)((ushort)(trans.Flags) >> 12);

                                if (trans.GuardId == 0 || EvaluateGuard(trans.GuardId, instancePtr, contextPtr, eventId))
                                {
                                    if (bestTransition == null || priority > highestPriority)
                                    {
                                        bestTransition = trans;
                                        highestPriority = priority;
                                    }
                                }
                            }
                        }
                    }

                    current = state.ParentIndex;
                }
            }

            return bestTransition;
        }
        
        private static bool EvaluateGuard(ushort guardId, byte* instancePtr, void* contextPtr, ushort eventId)
        {
            bool result = HsmActionDispatcher.EvaluateGuard(guardId, instancePtr, contextPtr, eventId);

            if (_traceBuffer != null)
            {
                InstanceHeader* header = (InstanceHeader*)instancePtr;
                if ((header->Flags & InstanceFlags.DebugTrace) != 0)
                {
                    _traceBuffer.WriteGuardEvaluated(header->MachineId, guardId, result, 0);
                }
            }

            return result;
        }

        // --- Transition Execution Logic (BATCH-10) ---

        private static void ExecuteTransition(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            TransitionDef transition,
            ushort* activeLeafIds,
            int regionCount,
            void* contextPtr)
        {
            ushort sourceStateId = transition.SourceStateIndex;
            ushort targetStateId = transition.TargetStateIndex;
            
            // Compute LCA path
            TransitionPath path = ComputeLCA(definition, sourceStateId, targetStateId);
            
            InstanceHeader* header = (InstanceHeader*)instancePtr;
            if (_traceBuffer != null && (header->Flags & InstanceFlags.DebugTrace) != 0)
            {
                _traceBuffer.WriteTransition(
                    header->MachineId,
                    transition.SourceStateIndex,
                    transition.TargetStateIndex,
                    transition.EventId);
            }
            
            // 1. Execute exit actions (leaf -> LCA)
            for (int i = 0; i < path.ExitCount; i++)
            {
                ushort stateId = path.ExitPath[i];
                
                if (_traceBuffer != null && (header->Flags & InstanceFlags.DebugTrace) != 0)
                {
                    _traceBuffer.WriteStateChange(header->MachineId, stateId, false);
                }

                ref readonly var state = ref definition.GetState(stateId);
                
                if (state.OnExitActionId != 0 && state.OnExitActionId != 0xFFFF)
                {
                    ExecuteAction(state.OnExitActionId, instancePtr, contextPtr, transition.EventId);
                }
                
                // Save history if this state has history
                if ((state.Flags & StateFlags.IsHistory) != 0 || state.HistorySlotIndex != 0xFFFF)
                {
                    SaveHistory(instancePtr, instanceSize, stateId, activeLeafIds[0]);
                }
            }
            
            // 2. Execute transition action
            if (transition.ActionId != 0 && transition.ActionId != 0xFFFF)
            {
                ExecuteAction(transition.ActionId, instancePtr, contextPtr, transition.EventId);
            }
            
            // 3. Execute entry actions (LCA -> leaf)
            ushort finalLeafId = targetStateId;
            
            for (int i = 0; i < path.EntryCount; i++)
            {
                ushort stateId = path.EntryPath[i];
                
                if (_traceBuffer != null && (header->Flags & InstanceFlags.DebugTrace) != 0)
                {
                    _traceBuffer.WriteStateChange(header->MachineId, stateId, true);
                }

                ref readonly var state = ref definition.GetState(stateId);
                
                // Check if history state
                if ((state.Flags & StateFlags.IsHistory) != 0)
                {
                    // Restore history
                    ushort restoredLeaf = RestoreHistory(instancePtr, instanceSize, stateId);
                    if (restoredLeaf != 0xFFFF)
                    {
                        finalLeafId = restoredLeaf;
                        break; 
                    }
                }
                
                if (state.OnEntryActionId != 0 && state.OnEntryActionId != 0xFFFF)
                {
                    ExecuteAction(state.OnEntryActionId, instancePtr, contextPtr, transition.EventId);
                }
                
                // If composite, resolve to initial child
                if ((state.Flags & StateFlags.IsComposite) != 0)
                {
                    ushort initialChild = state.FirstChildIndex;
                    if (initialChild != 0xFFFF)
                    {
                        finalLeafId = initialChild;
                    }
                }
            }
            
            // 4. Update active state
            activeLeafIds[0] = finalLeafId;
        }

        private static void ExecuteAction(
            ushort actionId,
            byte* instancePtr,
            void* contextPtr,
            ushort eventId)
        {
            if (_traceBuffer != null)
            {
                InstanceHeader* header = (InstanceHeader*)instancePtr;
                if ((header->Flags & InstanceFlags.DebugTrace) != 0)
                {
                    _traceBuffer.WriteActionExecuted(header->MachineId, actionId);
                }
            }

            HsmActionDispatcher.ExecuteAction(actionId, instancePtr, contextPtr, eventId);
        }

        private static TransitionPath ComputeLCA(
            HsmDefinitionBlob definition,
            ushort sourceStateId,
            ushort targetStateId)
        {
            var path = new TransitionPath();
            
            if (sourceStateId == targetStateId)
            {
                path.LCA = sourceStateId;
                path.ExitCount = 0;
                path.EntryCount = 0;
                return path;
            }
            
            ushort* sourceChain = stackalloc ushort[16];
            ushort* targetChain = stackalloc ushort[16];
            
            int sourceDepth = BuildAncestorChain(definition, sourceStateId, sourceChain);
            int targetDepth = BuildAncestorChain(definition, targetStateId, targetChain);
            
            ushort lca = 0xFFFF;
            int commonDepth = 0;
            
            int minDepth = sourceDepth < targetDepth ? sourceDepth : targetDepth;
            for (int i = 0; i < minDepth; i++)
            {
                if (sourceChain[i] == targetChain[i])
                {
                    lca = sourceChain[i];
                    commonDepth = i + 1;
                }
                else
                {
                    break;
                }
            }
            
            path.LCA = lca;
            
            path.ExitCount = (ushort)(sourceDepth - commonDepth);
            for (int i = 0; i < path.ExitCount; i++)
            {
                path.ExitPath[i] = sourceChain[sourceDepth - 1 - i];
            }
            
            path.EntryCount = (ushort)(targetDepth - commonDepth);
            for (int i = 0; i < path.EntryCount; i++)
            {
                path.EntryPath[i] = targetChain[commonDepth + i];
            }
            
            return path;
        }

        private static int BuildAncestorChain(
            HsmDefinitionBlob definition,
            ushort stateId,
            ushort* chain)
        {
            int depth = 0;
            ushort current = stateId;
            ushort* tempChain = stackalloc ushort[16];
            
            while (current != 0xFFFF && depth < 16)
            {
                tempChain[depth++] = current;
                ref readonly var state = ref definition.GetState(current);
                current = state.ParentIndex;
            }
            
            for (int i = 0; i < depth; i++)
            {
                chain[i] = tempChain[depth - 1 - i];
            }
            
            return depth;
        }

        private static void SaveHistory(
            byte* instancePtr,
            int instanceSize,
            ushort compositeStateId,
            ushort activeLeafId)
        {
            ushort* historySlots = GetHistorySlots(instancePtr, instanceSize, out int slotCount);
            if (historySlots == null) return;
            
            int slotIndex = compositeStateId % slotCount;
            
            if (slotIndex < slotCount)
            {
                historySlots[slotIndex] = activeLeafId;
            }
        }

        private static ushort RestoreHistory(
            byte* instancePtr,
            int instanceSize,
            ushort historyStateId)
        {
            ushort* historySlots = GetHistorySlots(instancePtr, instanceSize, out int slotCount);
            if (historySlots == null) return 0xFFFF;
            
            int slotIndex = historyStateId % slotCount;
            
            if (slotIndex < slotCount)
            {
                return historySlots[slotIndex];
            }
            
            return 0xFFFF;
        }

        private static ushort* GetHistorySlots(byte* instancePtr, int instanceSize, out int count)
        {
            switch (instanceSize)
            {
                case 64: count = 2; return (ushort*)(instancePtr + 32);
                case 128: count = 8; return (ushort*)(instancePtr + 40);
                case 256: count = 16; return (ushort*)(instancePtr + 64);
                default: count = 0; return null;
            }
        }

        // --- Helpers ---

        private static uint* GetTimerArray(byte* instancePtr, int instanceSize, out int count)
        {
            switch (instanceSize)
            {
                case 64: count = 2; return (uint*)(instancePtr + 24);
                case 128: count = 4; return (uint*)(instancePtr + 24);
                case 256: count = 8; return (uint*)(instancePtr + 32);
                default: count = 0; return null;
            }
        }

        private static ushort* GetActiveLeafIds(byte* instancePtr, int instanceSize, out int count)
        {
            switch (instanceSize)
            {
                case 64: count = 2; return (ushort*)(instancePtr + 16);
                case 128: count = 4; return (ushort*)(instancePtr + 16);
                case 256: count = 8; return (ushort*)(instancePtr + 16);
                default: count = 0; return null;
            }
        }
        
        private static void StoreCurrentEventId(byte* instancePtr, int instanceSize, ushort eventId)
        {
            ushort* ptr = null;
            if (instanceSize == 64) ptr = (ushort*)(instancePtr + CurrentEventId_Offset_64);
            else if (instanceSize == 128) ptr = (ushort*)(instancePtr + CurrentEventId_Offset_128);
            else if (instanceSize == 256) ptr = (ushort*)(instancePtr + CurrentEventId_Offset_256);
            
            if (ptr != null) *ptr = eventId;
        }
        
        private static ushort GetCurrentEventId(byte* instancePtr, int instanceSize)
        {
            ushort* ptr = null;
            if (instanceSize == 64) ptr = (ushort*)(instancePtr + CurrentEventId_Offset_64);
            else if (instanceSize == 128) ptr = (ushort*)(instancePtr + CurrentEventId_Offset_128);
            else if (instanceSize == 256) ptr = (ushort*)(instancePtr + CurrentEventId_Offset_256);

            return ptr != null ? *ptr : (ushort)0;
        }
    }
}
