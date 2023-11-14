// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawGpstime11LittleEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            this.OutStream.Write(item[..8]);
            return true;
        }
    }
}
