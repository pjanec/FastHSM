using System;
using System.Collections.Generic;

namespace Fhsm.Compiler.Graph
{
    /// <summary>
    /// Root container for state machine graph before compilation.
    /// </summary>
    public class StateMachineGraph
    {
        public string Name { get; set; }
        public Guid MachineId { get; set; }
        
        public StateNode RootState { get; set; }
        public Dictionary<string, StateNode> States { get; } = new();
        public List<TransitionNode> GlobalTransitions { get; } = new();
        
        // Event definitions
        public Dictionary<string, ushort> EventNameToId { get; } = new();
        
        // Function registrations
        public HashSet<string> RegisteredActions { get; } = new();
        public HashSet<string> RegisteredGuards { get; } = new();
        
        public StateMachineGraph(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MachineId = Guid.NewGuid();
            
            // Create implicit root
            RootState = new StateNode("__Root");
            States["__Root"] = RootState;
        }
        
        public void AddState(StateNode state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (States.ContainsKey(state.Name))
                throw new InvalidOperationException($"State '{state.Name}' already exists");
            
            States[state.Name] = state;
        }
        
        public StateNode? FindState(string name)
        {
            return States.TryGetValue(name, out var state) ? state : null;
        }
    }
}