using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Fhsm.Kernel.Data;

namespace Fhsm.Compiler
{
    public class HsmEmitter
    {
        /// <summary>
        /// Emit HsmDefinitionBlob from flattened data.
        /// </summary>
        public static HsmDefinitionBlob Emit(HsmFlattener.FlattenedData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            
            var header = new HsmDefinitionHeader();
            
            // Magic & version
            header.Magic = HsmDefinitionHeader.MagicNumber;
            header.FormatVersion = 1;
            
            // Counts
            header.StateCount = (ushort)data.States.Length;
            header.TransitionCount = (ushort)data.Transitions.Length;
            header.RegionCount = (ushort)data.Regions.Length;
            header.GlobalTransitionCount = (ushort)data.GlobalTransitions.Length;
            header.EventDefinitionCount = 0;  // Not tracked yet
            header.ActionCount = (ushort)data.ActionIds.Length;
            
            // Hashes
            header.StructureHash = ComputeStructureHash(data);
            header.ParameterHash = ComputeParameterHash(data);
            
            // Create blob
            return new HsmDefinitionBlob(
                header,
                data.States,
                data.Transitions,
                data.Regions,
                data.GlobalTransitions,
                data.ActionIds,
                data.GuardIds
            );
        }
        
        private static uint ComputeStructureHash(HsmFlattener.FlattenedData data)
        {
            // Hash topology: state count, parent indices, depths
            // This should be stable across renames (uses indices, not names)
            
            using var sha = SHA256.Create();
            // Use simple string concat of values for stability? 
            // Better: byte buffer. But StringBuilder is simpler for now.
            var builder = new StringBuilder();
            
            builder.Append(data.States.Length);
            
            foreach (var state in data.States)
            {
                builder.Append(state.ParentIndex);
                builder.Append(state.Depth);
                builder.Append(state.FirstChildIndex);
                builder.Append(state.NextSiblingIndex);
                // Also flags (Composite, Parallel, History) affect structure
                builder.Append(state.Flags); 
            }
            
            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            var hash = sha.ComputeHash(bytes);
            
            // Take first 4 bytes as uint
            return BitConverter.ToUInt32(hash, 0);
        }
        
        private static uint ComputeParameterHash(HsmFlattener.FlattenedData data)
        {
            // Hash logic: action IDs, guard IDs, event IDs
            // This changes when logic changes
            
            using var sha = SHA256.Create();
            var builder = new StringBuilder();
            
            foreach (var state in data.States)
            {
                builder.Append(state.OnEntryActionId);
                builder.Append(state.OnExitActionId);
                builder.Append(state.ActivityActionId);
                builder.Append(state.TimerActionId);
            }
            
            foreach (var trans in data.Transitions)
            {
                builder.Append(trans.EventId);
                builder.Append(trans.GuardId);
                builder.Append(trans.ActionId);
            }
            
            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            var hash = sha.ComputeHash(bytes);
            
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
