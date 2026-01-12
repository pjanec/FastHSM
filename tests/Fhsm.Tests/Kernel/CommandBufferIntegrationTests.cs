using System;
using System.Runtime.CompilerServices;
using Fhsm.Kernel;
using Fhsm.Kernel.Attributes;
using Fhsm.Kernel.Data;
using Xunit;

namespace Fhsm.Tests.Kernel
{
    public unsafe class CommandBufferIntegrationTests
    {
        // Must match delegate* <void*, void*, HsmCommandWriter*, void>
        [HsmAction(Name = "WriteTestCommand")]
        public static void WriteTestCommand(void* instance, void* context, HsmCommandWriter* writer)
        {
            // Write a simple command payload
            // Command ID: 0x01
            // Payload: 0xAA, 0xBB
            byte[] cmd = new byte[] { 0x01, 0xAA, 0xBB };
            writer->TryWriteCommand(cmd);
        }

        private static ushort ComputeHash(string name)
        {
            uint hash = 2166136261;
            foreach (char c in name)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (ushort)(hash & 0xFFFF);
        }

        [Fact]
        public void Dispatcher_CanExecuteAction_WithCommandWriter()
        {
            // This relies on the Source Generator emitting code for THIS assembly
            // and the HsmActionRegistrar being available in Fhsm.Tests.Generated
            Fhsm.Tests.Generated.HsmActionRegistrar.RegisterAll();
            
            ushort actionId = ComputeHash("WriteTestCommand");
            
            // Create CommandPage
            CommandPage page = new CommandPage();
            HsmCommandWriter writer = new HsmCommandWriter(&page);
            
            // Execute
            HsmActionDispatcher.ExecuteAction(actionId, null, null, &writer);
            
            // Verify
            Assert.Equal(3, writer.BytesWritten);
            Assert.Equal(0x01, page.Data[0]);
            Assert.Equal(0xAA, page.Data[1]);
            Assert.Equal(0xBB, page.Data[2]);
        }
    }
}
