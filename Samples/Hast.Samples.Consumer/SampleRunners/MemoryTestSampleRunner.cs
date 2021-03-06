﻿using Hast.Layer;
using Hast.Samples.SampleAssembly;
using System.Threading.Tasks;

namespace Hast.Samples.Consumer.SampleRunners
{
    internal static class MemoryTestSampleRunner
    {
        public static void Configure(HardwareGenerationConfiguration configuration)
        {
            configuration.AddHardwareEntryPointType<MemoryTest>();
        }

        public static async Task Run(IHastlayer hastlayer, IHardwareRepresentation hardwareRepresentation)
        {
            var memoryTest = await hastlayer.GenerateProxy(hardwareRepresentation, new MemoryTest());

            var output1 = memoryTest.Run(0, 1);
            var output2 = memoryTest.Run(0, 3);
            var output3 = memoryTest.Run(0, 7);
            var output4 = memoryTest.Run(0, 50);
            var output5 = memoryTest.Run(1, 1);
            var output6 = memoryTest.Run(3, 7);
            var output7 = memoryTest.Run(47, 100);
        }
    }
}
