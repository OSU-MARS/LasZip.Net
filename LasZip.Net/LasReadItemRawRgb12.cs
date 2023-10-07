// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawRgb12 : LasReadItemRaw
    {
        public LasReadItemRawRgb12()
        { 
        }

        public override bool TryRead(LasPoint item)
        {
            byte[] buf = new byte[6];
            if (inStream.Read(buf, 0, 6) != 6)
            {
                return false;
            }

            item.Rgb[0] = BitConverter.ToUInt16(buf, 0);
            item.Rgb[1] = BitConverter.ToUInt16(buf, 2);
            item.Rgb[2] = BitConverter.ToUInt16(buf, 4);
            return true;
        }
    }
}
