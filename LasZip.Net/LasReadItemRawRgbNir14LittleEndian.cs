// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawRgbNir14LittleEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            this.InStream.ReadExactly(item[..8]);
            return true;
        }
    }
}
