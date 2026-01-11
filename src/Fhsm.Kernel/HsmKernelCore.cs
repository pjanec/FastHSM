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
        /// <summary>
        /// Process instances through one tick.
        /// </summary>
        /// <param name="definition">State machine definition</param>
        /// <param name="instancePtr">Pointer to instance array</param>
        /// <param name="instanceCount">Number of instances</param>
        /// <param name="instanceSize">Size of each instance (64/128/256)</param>
        /// <param name="contextPtr">Context pointer (user data)</param>
        /// <param name="deltaTime">Time delta for this tick</param>
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
            // Check if instance belongs to this definition
            if (header->MachineId != definition.Header.StructureHash)
            {
                return false;
            }
            
            // Check phase is valid
            if (header->Phase < InstancePhase.Idle || header->Phase > InstancePhase.Activity)
            {
                return false;
            }
            
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
                    // Nothing to do, waiting for external trigger
                    break;
                    
                case InstancePhase.Entry:
                    // Process entry actions (will be implemented in later batch)
                    // For now, advance to RTC
                    header->Phase = InstancePhase.RTC;
                    break;
                    
                case InstancePhase.RTC:
                    // Run-to-completion loop (will be implemented in later batch)
                    // For now, advance to Activity
                    header->Phase = InstancePhase.Activity;
                    break;
                    
                case InstancePhase.Activity:
                    // Execute activities (will be implemented in later batch)
                    // For now, return to Idle
                    header->Phase = InstancePhase.Idle;
                    break;
            }
        }
    }
}
