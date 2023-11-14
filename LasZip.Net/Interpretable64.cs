// mydefs.hpp
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace LasZip
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal struct Interpretable64
    {
        [FieldOffset(0)]
        public UInt64 UInt64;
        [FieldOffset(0)]
        public Int64 Int64;
        [FieldOffset(0)]
        public double Double;

        public Interpretable64()
        {
        }

        public Interpretable64(ReadOnlySpan<byte> data) 
        {
            this.UInt64 = BinaryPrimitives.ReadUInt64LittleEndian(data);
        }
    }
}
