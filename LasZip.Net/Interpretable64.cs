// mydefs.hpp
using System;
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
    }
}
