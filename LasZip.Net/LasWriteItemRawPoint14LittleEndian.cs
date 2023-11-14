// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawPoint14LittleEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            this.OutStream.Write(item[..30]);
            return true;
        }
    }
}
