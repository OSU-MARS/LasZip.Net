// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawRgb12LittleEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            this.InStream.ReadExactly(item[..6]);
            return true;
        }
    }
}
