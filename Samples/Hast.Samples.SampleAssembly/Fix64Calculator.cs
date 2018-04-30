﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hast.Algorithms;
using Hast.Transformer.Abstractions.SimpleMemory;

namespace Hast.Samples.SampleAssembly
{
    /// <summary>
    /// Sample using the <see cref="Fix64"/> 64 fixed-point number type. This is useful if you need more involved
    /// calculations with fractions where simply scaling the numbers up and down is not enough. Also see 
    /// <see cref="Fix64CalculatorSampleRunner"/> on what to configure to make this work.
    /// </summary>
    public class Fix64Calculator
    {
        public const int CalculateLargeIntegerSum_InputInt32Index = 0;
        public const int CalculateLargeIntegerSum_OutputInt32Index = 0;
        public const int ParallelizedCalculateLargeIntegerSum_Int32NumbersStartIndex = 0;
        public const int ParallelizedCalculateLargeIntegerSum_OutputInt32sStartIndex = 0;

        public const int MaxDegreeOfParallelism = 13;


        public virtual void CalculateIntegerSumUpToNumber(SimpleMemory memory)
        {
            var number = memory.ReadInt32(CalculateLargeIntegerSum_InputInt32Index);

            var a = new Fix64(1);
            var b = a;

            for (var i = 1; i < number; i++)
            {
                a += b;
            }

            var integers = a.ToIntegers();
            memory.WriteInt32(CalculateLargeIntegerSum_OutputInt32Index, integers[0]);
            memory.WriteInt32(CalculateLargeIntegerSum_OutputInt32Index + 1, integers[1]);
        }

        public virtual void ParallelizedCalculateIntegerSumUpToNumbers(SimpleMemory memory)
        {
            var numbers = new int[MaxDegreeOfParallelism];

            var tasks = new Task<TaskResult>[MaxDegreeOfParallelism];

            for (int i = 0; i < MaxDegreeOfParallelism; i++)
            {
                var upToNumber = memory.ReadInt32(ParallelizedCalculateLargeIntegerSum_Int32NumbersStartIndex + i);

                tasks[i] = Task.Factory.StartNew(
                    upToNumberObject =>
                    {
                        var a = new Fix64(1);
                        var b = a;

                        for (var j = 1; j < (int)upToNumberObject; j++)
                        {
                            a += b;
                        }

                        var integers = a.ToIntegers();

                        return new TaskResult
                        {
                            Fix64Low = integers[0],
                            Fix64High = integers[1]
                        };
                    }, upToNumber);
            }

            Task.WhenAll(tasks).Wait();

            for (int i = 0; i < MaxDegreeOfParallelism; i++)
            {
                var itemOutputStartIndex = ParallelizedCalculateLargeIntegerSum_OutputInt32sStartIndex + i * 2;

                memory.WriteInt32(itemOutputStartIndex, tasks[i].Result.Fix64Low);
                memory.WriteInt32(itemOutputStartIndex + 1, tasks[i].Result.Fix64High);
            }
        }


        private class TaskResult
        {
            public int Fix64Low { get; set; }
            public int Fix64High { get; set; }
        }
    }


    public static class Fix64CalculatorExtensions
    {
        public static Fix64 CalculateIntegerSumUpToNumber(this Fix64Calculator fix64Calculator, int input)
        {
            var memory = new SimpleMemory(2);

            memory.WriteInt32(Fix64Calculator.CalculateLargeIntegerSum_InputInt32Index, input);

            fix64Calculator.CalculateIntegerSumUpToNumber(memory);

            return Fix64.FromRawInts(new[]
            {
                memory.ReadInt32(Fix64Calculator.CalculateLargeIntegerSum_OutputInt32Index),
                memory.ReadInt32(Fix64Calculator.CalculateLargeIntegerSum_OutputInt32Index + 1)
            });
        }

        public static IEnumerable<Fix64> ParallelizedCalculateIntegerSumUpToNumbers(this Fix64Calculator fix64Calculator, int[] numbers)
        {
            if (numbers.Length != Fix64Calculator.MaxDegreeOfParallelism)
            {
                throw new ArgumentException(
                    "Provide as many numbers as the degree of parallelism of Fix64Calculator is (" +
                    Fix64Calculator.MaxDegreeOfParallelism + ")");
            }

            var memory = new SimpleMemory(2 * Fix64Calculator.MaxDegreeOfParallelism);

            for (int i = 0; i < numbers.Length; i++)
            {
                memory.WriteInt32(Fix64Calculator.ParallelizedCalculateLargeIntegerSum_Int32NumbersStartIndex + i, numbers[i]); 
            }

            fix64Calculator.ParallelizedCalculateIntegerSumUpToNumbers(memory);

            var results = new Fix64[Fix64Calculator.MaxDegreeOfParallelism];

            for (int i = 0; i < Fix64Calculator.MaxDegreeOfParallelism; i++)
            {
                var itemOutputStartIndex = Fix64Calculator.ParallelizedCalculateLargeIntegerSum_OutputInt32sStartIndex + i * 2;

                results[i] = Fix64.FromRawInts(new[]
                {
                    memory.ReadInt32(itemOutputStartIndex),
                    memory.ReadInt32(itemOutputStartIndex + 1)
                });
            }

            return results;
        }
    }
}
