// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawGpstime11BigEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[8];
            MyDefs.EndianSwap64(item, buffer);
            this.OutStream.Write(buffer);
            return true;
        }
    }
}
