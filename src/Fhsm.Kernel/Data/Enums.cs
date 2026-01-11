using System;

namespace Fhsm.Kernel.Data
{
    /// <summary>
    /// State behavior flags (packed into 16 bits).
    /// </summary>
    [Flags]
    public enum StateFlags : ushort
    {
        None = 0,
        IsComposite = 1 << 0,       // Has child states
        HasHistory = 1 << 1,        // Tracks last active child
        IsDeepHistory = 1 << 2,     // Deep history vs shallow
        HasRegions = 1 << 3,        // Has orthogonal regions
        HasOnEntry = 1 << 4,        // Has entry action
        HasOnExit = 1 << 5,         // Has exit action
        HasOnUpdate = 1 << 6,       // Has update/activity
        IsInitial = 1 << 7,         // Initial state of parent
        IsFinal = 1 << 8,           // Final state (terminates)
        
        // Reserved bits for future use
        Reserved9 = 1 << 9,
        Reserved10 = 1 << 10,
        Reserved11 = 1 << 11,
        Reserved12 = 1 << 12,
        Reserved13 = 1 << 13,
        Reserved14 = 1 << 14,
        Reserved15 = 1 << 15,
    }

    /// <summary>
    /// Transition behavior flags (packed into 16 bits).
    /// Includes priority in high bits (bits 12-15 = 4-bit priority).
    /// </summary>
    [Flags]
    public enum TransitionFlags : ushort
    {
        None = 0,
        
        // Behavior flags (bits 0-11)
        IsExternal = 1 << 0,        // External transition (exit + enter)
        IsInternal = 1 << 1,        // Internal (no exit/entry)
        HasGuard = 1 << 2,          // Has guard condition
        HasEffect = 1 << 3,         // Has effect action
        IsInterrupt = 1 << 4,       // Interrupt-class (high priority)
        IsSynchronized = 1 << 5,    // Part of sync group
        
        // Reserved (bits 6-11)
        Reserved6 = 1 << 6,
        Reserved7 = 1 << 7,
        Reserved8 = 1 << 8,
        Reserved9 = 1 << 9,
        Reserved10 = 1 << 10,
        Reserved11 = 1 << 11,
        
        // Priority (bits 12-15): 0 = lowest, 15 = highest
        Priority_Mask = 0xF000,     // Bits 12-15
    }

    /// <summary>
    /// Event priority classes.
    /// </summary>
    public enum EventPriority : byte
    {
        Low = 0,
        Normal = 1,
        Interrupt = 2,
    }

    /// <summary>
    /// Instance lifecycle phase (for RTC execution tracking).
    /// </summary>
    public enum InstancePhase : byte
    {
        Idle = 0,           // Not executing
        Setup = 1,          // Phase 0: Validation
        Timers = 2,         // Phase 1: Timer processing
        RTC = 3,            // Phase 2: Run-to-completion
        Update = 4,         // Phase 3: Activities
        Complete = 5,       // Tick finished
    }

    /// <summary>
    /// Instance flags (status and error conditions).
    /// </summary>
    [Flags]
    public enum InstanceFlags : byte
    {
        None = 0,
        EventOverflow = 1 << 0,         // Event queue overflow
        CommandOverflow = 1 << 1,       // Command buffer overflow
        CriticalCommandOverflow = 1 << 2, // Critical lane overflow
        BudgetExceeded = 1 << 3,        // Microstep budget exceeded
        Terminated = 1 << 4,            // Reached final state
        Error = 1 << 5,                 // Unrecoverable error
        
        Reserved6 = 1 << 6,
        Reserved7 = 1 << 7,
    }
}
