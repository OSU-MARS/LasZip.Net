// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawRgbNir14BigEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[8];
            this.InStream.ReadExactly(buffer);
            MyDefs.EndianSwap16(buffer, item);
            MyDefs.EndianSwap16(buffer[2..], item[2..]);
            MyDefs.EndianSwap16(buffer[4..], item[4..]);
            MyDefs.EndianSwap16(buffer[6..], item[6..]);
            return true;
        }
    }
}
