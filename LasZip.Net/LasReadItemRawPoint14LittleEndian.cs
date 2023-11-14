// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawPoint14LittleEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            this.InStream.ReadExactly(item[..30]);
            return true;
        }
    }
}
