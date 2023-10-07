// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawByte : LasWriteItemRaw
    {
        private readonly UInt32 number = 0;

        public LasWriteItemRawByte(UInt32 number)
        {
            this.number = number;
        }

        public override bool Write(LasPoint item)
        {
            OutStream.Write(item.ExtraBytes, 0, (int)number);
            return true;
        }
    }
}
