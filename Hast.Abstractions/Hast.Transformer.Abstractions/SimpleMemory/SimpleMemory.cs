﻿using System;
using System.Runtime.InteropServices;

namespace Hast.Transformer.Abstractions.SimpleMemory
{
    /// <summary>
    /// Represents a simplified memory model available on the FPGA for transformed algorithms. WARNING: SimpleMemory is
    /// NOT thread-safe, there shouldn't be any concurrent access to it.
    /// </summary>
    /// <remarks>
    /// All read/write methods' name MUST follow the convention to begin with "Read" or "Write" respectively.
    /// 
    /// WARNING: changes here should be in sync with the VHDL library. The signatures of the methods of this class 
    /// mustn't be changed (i.e. no renames, new or re-ordered arguments) without making adequate changes to the VHDL
    /// library too.
    /// </remarks>
    public class SimpleMemory : ISimpleMemory
    {
        public const int MemoryCellSizeBytes = sizeof(int);

        /// <summary>
        /// The number of extra cells used for header information like memberId or data length.
        /// </summary>
        public int PrefixCellCount { get; internal set; } = 3;

        /// <summary>
        /// This is the full memory including the <see cref="PrefixCellCount"/> cells of extra memory that is to be used
        /// for passing in extra input parameters (like memberId) without having to copy the operative memory contents
        /// into an auxiliary array.
        /// </summary>
        internal Memory<byte> PrefixedMemory { get; set; }

        /// <summary>
        /// Gets or sets the contents of the memory representation.
        /// </summary>
        /// <remarks>
        /// This is internal so the property can be read when handling communication with the FPGA but not by user code.
        /// </remarks>
        internal Memory<byte> Memory { get; set; }

        /// <summary>
        /// Gets the number of cells of this memory allocation, indicating memory cells of size <see cref="MemoryCellSizeBytes"/>.
        /// </summary>
        public int CellCount { get => Memory.Length / MemoryCellSizeBytes; }

        /// <summary>
        /// Gets the span of memory at the cellIndex, the length is <see cref="MemoryCellSizeBytes"/>.
        /// </summary>
        /// <param name="cellIndex">The cell index where the memory span starts.</param>
        /// <returns>A span starting at cellIndex * MemoryCellSizeBytes.</returns>
        internal Span<byte> this[int cellIndex]
        {
            get => Memory.Slice(cellIndex * MemoryCellSizeBytes, MemoryCellSizeBytes).Span;
        }

        /// <summary>
        /// Constructs a new <see cref="SimpleMemory"/> object that represents a simplified memory model available on 
        /// the FPGA for transformed algorithms.
        /// </summary>
        /// <param name="cellCount">
        /// The number of memory "cells". The memory is divided into independently accessible "cells"; the size of the
        /// allocated memory space is calculated from the cell count and the cell size indicated in 
        /// <see cref="MemoryCellSizeBytes"/>.
        /// </param>
        public SimpleMemory(int cellCount)
        {
            PrefixedMemory = new byte[(cellCount + PrefixCellCount) * MemoryCellSizeBytes];
            Memory = PrefixedMemory.Slice(PrefixCellCount * MemoryCellSizeBytes);
        }

        public void Write4Bytes(int cellIndex, byte[] bytes)
        {
            var target = this[cellIndex];

            for (int i = 0; i < bytes.Length; i++) target[i] = bytes[i];
            for (int i = bytes.Length; i < MemoryCellSizeBytes; i++) target[i] = 0;
        }

        public void Write4Bytes(int startCellIndex, byte[][] bytesMatrix)
        {
            for (int i = 0; i < bytesMatrix.Length; i++)
                Write4Bytes(startCellIndex + i, bytesMatrix[i]);
        }

        public byte[] Read4Bytes(int cellIndex)
        {
            var output = new byte[MemoryCellSizeBytes];

            this[cellIndex].CopyTo(output);

            return output;
        }

        public byte[][] Read4Bytes(int startCellIndex, int count)
        {
            var bytesMatrix = new byte[count][];

            for (int i = 0; i < count; i++)
            {
                bytesMatrix[i] = Read4Bytes(startCellIndex + i);
            }

            return bytesMatrix;
        }

        public void WriteUInt32(int cellIndex, uint number) => MemoryMarshal.Write(this[cellIndex], ref number);

        public void WriteUInt32(int startCellIndex, params uint[] numbers) =>
            MemoryMarshal.Cast<uint, byte>(numbers)
                .CopyTo(Memory.Slice(startCellIndex * MemoryCellSizeBytes, numbers.Length * sizeof(uint)).Span);

        public uint ReadUInt32(int cellIndex) => MemoryMarshal.Read<uint>(this[cellIndex]);

        public uint[] ReadUInt32(int startCellIndex, int count) => 
            MemoryMarshal.Cast<byte, uint>(Memory.Slice(startCellIndex * MemoryCellSizeBytes, count * sizeof(uint)).Span).ToArray();


        public void WriteInt32(int cellIndex, int number) => Write4Bytes(cellIndex, BitConverter.GetBytes(number));

        public void WriteInt32(int startCellIndex, params int[] numbers) =>
            MemoryMarshal.Cast<int, byte>(numbers)
                .CopyTo(Memory.Slice(startCellIndex * MemoryCellSizeBytes, numbers.Length * sizeof(int)).Span);

        public int ReadInt32(int cellIndex) => MemoryMarshal.Read<int>(this[cellIndex]);

        /// <summary>
        /// Takes count integers at cellIndex.
        /// </summary>
        /// <param name="startCellIndex">The cell of the first integer</param>
        /// <param name="count">The amount of integers</param>
        /// <returns>The memory pointer as Span.</returns>
        public Span<int> ReadInt32Span(int startCellIndex, int count) =>
            MemoryMarshal.Cast<byte, int>(Memory.Slice(startCellIndex * MemoryCellSizeBytes, count * sizeof(int)).Span);

        /// <summary>
        /// Takes count integers at cellIndex. This version makes a copy of the data as array so only use it when Span
        /// isn't applicable!
        /// </summary>
        /// <param name="startCellIndex">The cell of the first integer</param>
        /// <param name="count">The amount of integers</param>
        /// <returns>The numbers in an array.</returns>
        public int[] ReadInt32(int startCellIndex, int count) => ReadInt32Span(startCellIndex, count).ToArray();

        public void WriteBoolean(int cellIndex, bool boolean) =>
            // Since the implementation of a boolean can depend on the system rather hard-coding the expected values here
            // so on the FPGA-side we can depend on it.
            // Would call MemoryMarshal.Write directly if not for the "ref".
            WriteUInt32(cellIndex, boolean ? uint.MaxValue : uint.MinValue);

        public void WriteBoolean(int startCellIndex, params bool[] booleans)
        {
            for (int i = 0; i < booleans.Length; i++)
                WriteBoolean(startCellIndex + i, booleans[i]);
        }

        public bool ReadBoolean(int cellIndex) => MemoryMarshal.Read<uint>(this[cellIndex]) != uint.MinValue;

        public bool[] ReadBoolean(int startCellIndex, int count)
        {
            var source = ReadUInt32(startCellIndex, count);
            var booleans = new bool[count];

            for (int i = 0; i < count; i++)
                booleans[i] = source[i] == uint.MaxValue;

            return booleans;
        }
    }


    /// <summary>
    /// Extensions for older Framework features which don't support Memory or Span yet.
    /// </summary>
    public static class MemoryExtensions
    {
        /// <summary>
        /// Gets the internal array to be used for <see cref="System.IO.Stream.Write(byte[], int, int)"/>.
        /// </summary>
        /// <param name="bytes">The source.</param>
        /// <returns>The underlying array.</returns>
        /// <remarks>
        /// Once Stream.Read(Span) based overload is available, use that instead!
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.read?view=netcore-2.2#System_IO_Stream_Read_System_Span_System_Byte__
        /// </remarks>
        public static ArraySegment<byte> GetUnderlyingArray(this Memory<byte> bytes) => GetUnderlyingArray((ReadOnlyMemory<byte>)bytes);

        public static ArraySegment<byte> GetUnderlyingArray(this ReadOnlyMemory<byte> bytes)
        {
            if (!MemoryMarshal.TryGetArray(bytes, out var arraySegment)) throw new NotSupportedException("This Memory does not support exposing the underlying array.");
            return arraySegment;
        }
    }
}
