// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawByte : LasWriteItemRaw
    {
        private readonly int number = 0;

        public LasWriteItemRawByte(UInt32 number)
        {
            this.number = (int)number;
        }

        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            this.OutStream.Write(item[..this.number]);
            return true;
        }
    }
}
