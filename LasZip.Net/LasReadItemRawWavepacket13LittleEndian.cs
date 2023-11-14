// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawWavepacket13LittleEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            this.InStream.ReadExactly(item[..29]);
            return true;
        }
    }
}
