// mydefs.hpp
using System;
using System.Runtime.InteropServices;

namespace LasZip
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    internal struct Interpretable32
    {
        [FieldOffset(0)]
        public UInt32 UInt32;
        [FieldOffset(0)]
        public int Int32;
        [FieldOffset(0)]
        public float Float;
    }
}
