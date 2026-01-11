using System;

namespace Fhsm.Compiler.Graph
{
    internal class TransitionNode
    {
        public StateNode Source { get; set; }
        public StateNode Target { get; set; }
        
        public ushort EventId { get; set; }
        public string? GuardFunction { get; set; }  // Optional guard
        public string? ActionFunction { get; set; }  // Optional action
        
        public byte Priority { get; set; } = 128;  // Default normal
        public bool IsInternal { get; set; }  // Internal vs External
        
        public TransitionNode(StateNode source, StateNode target, ushort eventId)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target; // Allow null initially, set by builder
            EventId = eventId;
        }
    }
}