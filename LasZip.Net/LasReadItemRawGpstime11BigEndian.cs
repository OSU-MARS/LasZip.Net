// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawGpstime11BigEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[8];
            this.InStream.ReadExactly(buffer);
            MyDefs.EndianSwap64(buffer, item);
            return true;
        }
    }
}
