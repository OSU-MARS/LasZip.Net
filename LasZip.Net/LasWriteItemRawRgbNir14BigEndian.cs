// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawRgbNir14BigEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[8];
            MyDefs.EndianSwap16(item, buffer);
            MyDefs.EndianSwap16(item[2..], buffer[2..]);
            MyDefs.EndianSwap16(item[4..], buffer[4..]);
            MyDefs.EndianSwap16(item[6..], buffer[6..]);
            this.OutStream.Write(buffer);
            return true;
        }
    }
}
