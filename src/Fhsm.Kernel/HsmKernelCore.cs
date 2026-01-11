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
        // Event ID for Timer Fired (System Reserved)
        private const ushort TimerEventId = 0xFFFE;

        // Configuration offsets for CurrentEventId scratch space
        private const int CurrentEventId_Offset_64 = 20;
        private const int CurrentEventId_Offset_128 = 58;
        private const int CurrentEventId_Offset_256 = 98;

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
                    // Activities would go here (Batch 10)
                    // Return to Idle
                    header->Phase = InstancePhase.Idle;
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
            
            for (int iteration = 0; iteration < MaxRTCIterations; iteration++)
            {
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
                
                ExecuteTransitionStub(definition, instancePtr, instanceSize, selectedTransition.Value, activeLeafIds, regionCount);
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
            return true;
        }

        private static void ExecuteTransitionStub(
            HsmDefinitionBlob definition,
            byte* instancePtr,
            int instanceSize,
            TransitionDef transition,
            ushort* activeLeafIds,
            int regionCount)
        {
             // Simple stub: update first region to target
             activeLeafIds[0] = transition.TargetStateIndex;
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
