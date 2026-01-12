using System;
using Xunit;
using Fhsm.Kernel;
using Fhsm.Kernel.Data;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Fhsm.Tests.Kernel
{
    public unsafe class FailSafeTests
    {
        private HsmDefinitionBlob CreateBlob()
        {
            // State 0 -> State 1 on Event 10
            // State 1 -> State 0 on Event 10
            
            var states = new StateDef[2];
            states[0] = new StateDef { ParentIndex = 0xFFFF, FirstTransitionIndex = 0, TransitionCount = 1 };
            states[1] = new StateDef { ParentIndex = 0xFFFF, FirstTransitionIndex = 1, TransitionCount = 1 };

            var transitions = new TransitionDef[2];
            // EventId 10
            transitions[0] = new TransitionDef { SourceStateIndex = 0, TargetStateIndex = 1, EventId = 10, Flags = 0 }; // Flags>>12 = Priority 0
            transitions[1] = new TransitionDef { SourceStateIndex = 1, TargetStateIndex = 0, EventId = 10, Flags = 0 };

            var header = new HsmDefinitionHeader();
            header.StructureHash = 0x12345678;
            header.StateCount = 2;
            header.TransitionCount = 2;

            return new HsmDefinitionBlob(
                header,
                states,
                transitions,
                Array.Empty<RegionDef>(),
                Array.Empty<GlobalTransitionDef>(),
                Array.Empty<ushort>(),
                Array.Empty<ushort>()
            );
        }

        [Fact]
        public void InfiniteLoop_Detected_And_Stops()
        {
            var blob = CreateBlob();
            var instances = new HsmInstance64[1];
            instances[0].Header.MachineId = 0x12345678;
            instances[0].Header.Phase = InstancePhase.RTC;
            instances[0].Header.Flags = InstanceFlags.DebugTrace; // Enable tracing
            instances[0].Header.RngState = 123;
            instances[0].ActiveLeafIds[0] = 0; // Start at State 0
            
            int context = 0;
            
            // Set current event to 10 manually for RTC phase
            fixed (HsmInstance64* ptr = instances)
            {
               byte* bPtr = (byte*)ptr;
               *(ushort*)(bPtr + 20) = 10; // CurrentEventId (Offset 20)
            }
            
            // 65KB buffer should be enough for 100 iterations * 16 bytes = 1600 bytes
            var traceBuffer = new HsmTraceBuffer(65536);
            traceBuffer.FilterLevel = TraceLevel.All;
            HsmKernelCore.SetTraceBuffer(traceBuffer);
            
            try 
            {
                HsmKernel.UpdateBatch(blob, instances.AsSpan(), context, 0.016f);
            }
            finally
            {
                HsmKernelCore.SetTraceBuffer(null);
            }
            
            // 1. Check Safe State (0xFFFF)
            Assert.Equal(0xFFFF, instances[0].ActiveLeafIds[0]);
            
            // 2. Check Phase Reset to Idle
            Assert.Equal(InstancePhase.Idle, instances[0].Header.Phase);
            
            // 3. Check Trace Error
            var data = traceBuffer.GetTraceData();
            Assert.True(data.Length > 0, "Trace buffer is empty");
            
            bool foundError = false;
            int offset = 0;
            int count = 0;
            
            while(offset < data.Length)
            {
                TraceOpCode op = (TraceOpCode)data[offset];
                int size = 0;
                
                switch (op)
                {
                    case TraceOpCode.Transition: size = 16; break;
                    case TraceOpCode.StateEnter: size = 12; break;
                    case TraceOpCode.StateExit: size = 12; break;
                    case TraceOpCode.Error: 
                        size = 12; 
                        foundError = true;
                        ushort errCode = BitConverter.ToUInt16(data.Slice(offset + 8, 2));
                        Assert.Equal(1, errCode);
                        break;
                    default: size = 12; break; // Assumed default
                }
                
                if (foundError) break;
                
                offset += size;
                count++;
            }
            
            Assert.True(foundError, "TraceError (0xFF) not found in buffer");
            Assert.True(count >= 100, "Should have run at least 100 transitions (or trace records) before failing");
        }
    }
}
