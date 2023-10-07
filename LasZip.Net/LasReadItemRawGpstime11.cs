// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawGpstime11 : LasReadItemRaw
    {
        private readonly byte[] buffer = new byte[8];

        public LasReadItemRawGpstime11() 
        { 
        }

        public override bool TryRead(LasPoint item)
        {
            if (inStream.Read(buffer, 0, 8) != 8)
            {
                return false;
            }

            item.Gpstime = BitConverter.ToDouble(buffer, 0);
            return true;
        }
    }
}
