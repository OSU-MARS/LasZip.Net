// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawWavepacket13LittleEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            this.OutStream.Write(item[..29]);
            return true;
        }
    }
}
