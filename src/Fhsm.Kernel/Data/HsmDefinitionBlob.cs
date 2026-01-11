using System;

namespace Fhsm.Kernel.Data
{
    /// <summary>
    /// Container for the immutable ROM definition of a state machine.
    /// helds the defining structures (States, Transitions, Regions, etc.).
    /// </summary>
    public sealed class HsmDefinitionBlob
    {
        public HsmDefinitionHeader Header;
        
        private readonly StateDef[] _states;
        private readonly TransitionDef[] _transitions;
        private readonly RegionDef[] _regions;
        private readonly GlobalTransitionDef[] _globalTransitions;
        private readonly ushort[] _actionIds;
        private readonly ushort[] _guardIds;
        
        public HsmDefinitionBlob()
        {
            _states = Array.Empty<StateDef>();
            _transitions = Array.Empty<TransitionDef>();
            _regions = Array.Empty<RegionDef>();
            _globalTransitions = Array.Empty<GlobalTransitionDef>();
            _actionIds = Array.Empty<ushort>();
            _guardIds = Array.Empty<ushort>();
        }

        public HsmDefinitionBlob(
            HsmDefinitionHeader header,
            StateDef[] states,
            TransitionDef[] transitions,
            RegionDef[] regions,
            GlobalTransitionDef[] globalTransitions,
            ushort[] actionIds,
            ushort[] guardIds)
        {
            Header = header;
            _states = states ?? Array.Empty<StateDef>();
            _transitions = transitions ?? Array.Empty<TransitionDef>();
            _regions = regions ?? Array.Empty<RegionDef>();
            _globalTransitions = globalTransitions ?? Array.Empty<GlobalTransitionDef>();
            _actionIds = actionIds ?? Array.Empty<ushort>();
            _guardIds = guardIds ?? Array.Empty<ushort>();
        }
        
        // Span accessors only
        public ReadOnlySpan<StateDef> States => _states;
        public ReadOnlySpan<TransitionDef> Transitions => _transitions;
        public ReadOnlySpan<RegionDef> Regions => _regions;
        public ReadOnlySpan<GlobalTransitionDef> GlobalTransitions => _globalTransitions;
        public ReadOnlySpan<ushort> ActionIds => _actionIds;
        public ReadOnlySpan<ushort> GuardIds => _guardIds;

        // Indexed accessors with bounds checking
        public ref readonly StateDef GetState(int index)
        {
            if (index < 0 || index >= _states.Length)
                throw new IndexOutOfRangeException($"State index {index} out of range [0..{_states.Length-1}]");
            return ref _states[index];
        }

        public ref readonly TransitionDef GetTransition(int index)
        {
            if (index < 0 || index >= _transitions.Length)
                throw new IndexOutOfRangeException($"Transition index {index} out of range [0..{_transitions.Length-1}]");
            return ref _transitions[index];
        }

        public ref readonly RegionDef GetRegion(int index)
        {
            if (index < 0 || index >= _regions.Length)
                throw new IndexOutOfRangeException($"Region index {index} out of range [0..{_regions.Length-1}]");
            return ref _regions[index];
        }

        public ref readonly GlobalTransitionDef GetGlobalTransition(int index)
        {
            if (index < 0 || index >= _globalTransitions.Length)
                throw new IndexOutOfRangeException($"GlobalTransition index {index} out of range [0..{_globalTransitions.Length-1}]");
            return ref _globalTransitions[index];
        }
    }
}
