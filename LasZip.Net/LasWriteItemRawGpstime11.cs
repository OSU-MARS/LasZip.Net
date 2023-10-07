// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawGpstime11 : LasWriteItemRaw
    {
        public LasWriteItemRawGpstime11()
        {
        }

        public override bool Write(LasPoint item)
        {
            OutStream.Write(BitConverter.GetBytes(item.Gpstime), 0, 8);
            return true;
        }
    }
}
