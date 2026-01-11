using System;
using Xunit;
using Fhsm.Kernel;
using Fhsm.Kernel.Data;

namespace Fhsm.Tests.Kernel
{
    public class KernelEntryTests
    {
        // Dummy context for tests
        private struct TestContext
        {
            public int Value;
        }

        private HsmDefinitionBlob CreateEmptyBlob()
        {
            var header = new HsmDefinitionHeader();
            header.StructureHash = 0x12345678;
            
            return new HsmDefinitionBlob(
                header,
                Array.Empty<StateDef>(),
                Array.Empty<TransitionDef>(),
                Array.Empty<RegionDef>(),
                Array.Empty<GlobalTransitionDef>(),
                Array.Empty<ushort>(),
                Array.Empty<ushort>()
            );
        }

        [Fact]
        public void UpdateBatch_Processes_Multiple_Instances()
        {
            var blob = CreateEmptyBlob();
            var instances = new HsmInstance64[3];
            var context = new TestContext();
            
            // Setup instances
            for (int i = 0; i < 3; i++)
            {
                instances[i].Header.MachineId = blob.Header.StructureHash;
                instances[i].Header.Phase = InstancePhase.Entry; 
            }
            
            HsmKernel.UpdateBatch(blob, instances.AsSpan(), context, 0.16f);
            
            // Entry should advance to RTC
            Assert.Equal(InstancePhase.RTC, instances[0].Header.Phase);
            Assert.Equal(InstancePhase.RTC, instances[1].Header.Phase);
            Assert.Equal(InstancePhase.RTC, instances[2].Header.Phase);
        }

        [Fact]
        public void Update_Processes_Single_Instance()
        {
            var blob = CreateEmptyBlob();
            var instance = new HsmInstance64();
            instance.Header.MachineId = blob.Header.StructureHash;
            instance.Header.Phase = InstancePhase.Entry;
            var context = new TestContext();
            
            HsmKernel.Update(blob, ref instance, context, 0.16f);
            
            Assert.Equal(InstancePhase.RTC, instance.Header.Phase);
        }

        [Fact]
        public void Trigger_Sets_Entry_If_Idle()
        {
            var instance = new HsmInstance64();
            instance.Header.Phase = InstancePhase.Idle;
            
            HsmKernel.Trigger(ref instance);
            
            Assert.Equal(InstancePhase.Entry, instance.Header.Phase);
        }

        [Fact]
        public void Trigger_Ignores_Non_Idle()
        {
            var instance = new HsmInstance64();
            instance.Header.Phase = InstancePhase.RTC;
            
            HsmKernel.Trigger(ref instance);
            
            Assert.Equal(InstancePhase.RTC, instance.Header.Phase);
        }

        [Fact]
        public void UpdateBatch_Handles_Empty()
        {
            var blob = CreateEmptyBlob();
            var context = new TestContext();
            
            // Should not throw
            HsmKernel.UpdateBatch<HsmInstance64, TestContext>(blob, Span<HsmInstance64>.Empty, context, 0.16f);
        }

        [Fact]
        public void Phase_Idle_Stays_Idle()
        {
            var blob = CreateEmptyBlob();
            var instance = new HsmInstance64();
            instance.Header.MachineId = blob.Header.StructureHash;
            instance.Header.Phase = InstancePhase.Idle;
            var context = new TestContext();
            
            HsmKernel.Update(blob, ref instance, context, 0.16f);
            
            Assert.Equal(InstancePhase.Idle, instance.Header.Phase);
        }

        [Fact]
        public void Phase_Entry_Advances_To_RTC()
        {
            var blob = CreateEmptyBlob();
            var instance = new HsmInstance64();
            instance.Header.MachineId = blob.Header.StructureHash;
            instance.Header.Phase = InstancePhase.Entry;
            var context = new TestContext();
            
            HsmKernel.Update(blob, ref instance, context, 0.16f);
            
            Assert.Equal(InstancePhase.RTC, instance.Header.Phase);
        }

        [Fact]
        public void Phase_RTC_Advances_To_Activity()
        {
            var blob = CreateEmptyBlob();
            var instance = new HsmInstance64();
            instance.Header.MachineId = blob.Header.StructureHash;
            instance.Header.Phase = InstancePhase.RTC;
            var context = new TestContext();
            
            HsmKernel.Update(blob, ref instance, context, 0.16f);
            
            Assert.Equal(InstancePhase.Activity, instance.Header.Phase);
        }

        [Fact]
        public void Phase_Activity_Returns_To_Idle()
        {
            var blob = CreateEmptyBlob();
            var instance = new HsmInstance64();
            instance.Header.MachineId = blob.Header.StructureHash;
            instance.Header.Phase = InstancePhase.Activity;
            var context = new TestContext();
            
            HsmKernel.Update(blob, ref instance, context, 0.16f);
            
            Assert.Equal(InstancePhase.Idle, instance.Header.Phase);
        }

        [Fact]
        public void Invalid_Phase_Skipped()
        {
            var blob = CreateEmptyBlob();
            var instance = new HsmInstance64();
            instance.Header.MachineId = blob.Header.StructureHash;
            instance.Header.Phase = (InstancePhase)99; // Invalid
            var context = new TestContext();
            
            HsmKernel.Update(blob, ref instance, context, 0.16f);
            
            Assert.Equal((InstancePhase)99, instance.Header.Phase); // Should not change
        }

        [Fact]
        public void Wrong_MachineId_Skipped()
        {
            var blob = CreateEmptyBlob();
            var instance = new HsmInstance64();
            instance.Header.MachineId = 0xDEADBEEF; // Wrong ID
            instance.Header.Phase = InstancePhase.Entry;
            var context = new TestContext();
            
            HsmKernel.Update(blob, ref instance, context, 0.16f);
            
            Assert.Equal(InstancePhase.Entry, instance.Header.Phase); // Should not advance
        }

        [Fact]
        public void Null_Definition_Throws()
        {
            var instance = new HsmInstance64();
            var context = new TestContext();
            
            Assert.Throws<ArgumentNullException>(() => 
                HsmKernel.Update(null!, ref instance, context, 0.16f));
        }

        [Fact]
        public void Works_With_Different_Instance_Sizes()
        {
            var blob = CreateEmptyBlob();
            var context = new TestContext();
            
            // 64
            var i64 = new HsmInstance64();
            i64.Header.MachineId = blob.Header.StructureHash;
            i64.Header.Phase = InstancePhase.Entry;
            HsmKernel.Update(blob, ref i64, context, 0.16f);
            Assert.Equal(InstancePhase.RTC, i64.Header.Phase);
            
            // 128
            var i128 = new HsmInstance128();
            i128.Header.MachineId = blob.Header.StructureHash;
            i128.Header.Phase = InstancePhase.Entry;
            HsmKernel.Update(blob, ref i128, context, 0.16f);
            Assert.Equal(InstancePhase.RTC, i128.Header.Phase);
        }
    }
}
