// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawRgb12LittleEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            this.OutStream.Write(item[..6]);
            return true;
        }
    }
}
