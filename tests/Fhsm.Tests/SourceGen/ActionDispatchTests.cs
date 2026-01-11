using System;
using System.Runtime.CompilerServices;
using Xunit;
using Fhsm.Kernel;
using Fhsm.Kernel.Attributes;

// Define explicit alias for Kernel namespace usage if needed, 
// but using dynamic namespace separation avoids collision.

namespace Fhsm.Tests.SourceGen
{
    public unsafe class ActionDispatchTests
    {
        private static int _actionCallCount = 0;
        private static int _guardCallCount = 0;
        private static ushort _lastEventId = 0;

        [HsmAction(Name = "TestAction")]
        internal static void TestAction(void* instance, void* context, ushort eventId)
        {
            _actionCallCount++;
            _lastEventId = eventId;
        }

        [HsmGuard(Name = "TestGuard")]
        internal static bool TestGuard(void* instance, void* context, ushort eventId)
        {
            _guardCallCount++;
            return true;
        }

        [HsmAction]
        internal static void ImplicitNameAction(void* instance, void* context, ushort eventId)
        {
            _actionCallCount++;
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
        public void Dispatcher_Executes_Action()
        {
            Fhsm.Tests.Generated.HsmActionRegistrar.RegisterAll();
            _actionCallCount = 0;
            ushort id = ComputeHash("TestAction");
            
            // Use the KERNEL dispatcher
            Fhsm.Kernel.HsmActionDispatcher.ExecuteAction(id, null, null, 123);
            
            Assert.Equal(1, _actionCallCount);
            Assert.Equal(123, _lastEventId);
        }

        [Fact]
        public void Dispatcher_Evaluates_Guard()
        {
            Fhsm.Tests.Generated.HsmActionRegistrar.RegisterAll();
            _guardCallCount = 0;
            ushort id = ComputeHash("TestGuard");
            
            bool result = Fhsm.Kernel.HsmActionDispatcher.EvaluateGuard(id, null, null, 123);
            
            Assert.True(result);
            Assert.Equal(1, _guardCallCount);
        }

        [Fact]
        public void Dispatcher_Unknown_Action_Is_Safe()
        {
            // Should not throw or crash
            Fhsm.Kernel.HsmActionDispatcher.ExecuteAction(9999, null, null, 0);
        }

        [Fact]
        public void Dispatcher_Unknown_Guard_Returns_True()
        {
            bool result = Fhsm.Kernel.HsmActionDispatcher.EvaluateGuard(9999, null, null, 0);
            Assert.True(result);
        }

        [Fact]
        public void Implicit_Name_Works()
        {
            Fhsm.Tests.Generated.HsmActionRegistrar.RegisterAll();

            _actionCallCount = 0;
            // Name should be "ImplicitNameAction"
            ushort id = ComputeHash("ImplicitNameAction");
            
            Fhsm.Kernel.HsmActionDispatcher.ExecuteAction(id, null, null, 0);
            Assert.Equal(1, _actionCallCount);
        }
    }
}
