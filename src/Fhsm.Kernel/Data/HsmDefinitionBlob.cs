using System;

namespace Fhsm.Kernel.Data
{
    /// <summary>
    /// Container for the immutable ROM definition of a state machine.
    /// helds the defining structures (States, Transitions, Regions, etc.).
    /// </summary>
    public class HsmDefinitionBlob
    {
        /// <summary>
        /// The header containing counts and validation info.
        /// </summary>
        public HsmDefinitionHeader Header;

        // Data arrays
        private StateDef[] _states;
        private TransitionDef[] _transitions;
        private RegionDef[] _regions;
        private GlobalTransitionDef[] _globalTransitions;

        public HsmDefinitionBlob()
        {
            _states = Array.Empty<StateDef>();
            _transitions = Array.Empty<TransitionDef>();
            _regions = Array.Empty<RegionDef>();
            _globalTransitions = Array.Empty<GlobalTransitionDef>();
        }

        public StateDef[] States
        {
            get => _states;
            set => _states = value ?? Array.Empty<StateDef>();
        }

        public TransitionDef[] Transitions
        {
            get => _transitions;
            set => _transitions = value ?? Array.Empty<TransitionDef>();
        }

        public RegionDef[] Regions
        {
            get => _regions;
            set => _regions = value ?? Array.Empty<RegionDef>();
        }

        public GlobalTransitionDef[] GlobalTransitions
        {
            get => _globalTransitions;
            set => _globalTransitions = value ?? Array.Empty<GlobalTransitionDef>();
        }

        // Span accessors (Zero-allocation)
        public ReadOnlySpan<StateDef> StateSpan => _states;
        public ReadOnlySpan<TransitionDef> TransitionSpan => _transitions;
        public ReadOnlySpan<RegionDef> RegionSpan => _regions;
        public ReadOnlySpan<GlobalTransitionDef> GlobalTransitionSpan => _globalTransitions;

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
