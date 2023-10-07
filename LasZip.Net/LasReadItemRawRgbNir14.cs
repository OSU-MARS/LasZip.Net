// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawRgbNir14 : LasReadItemRaw
    {
        public LasReadItemRawRgbNir14()
        {
        }

        public override bool TryRead(LasPoint item)
        {
            byte[] buf = new byte[8];
            if (inStream.Read(buf, 0, 8) != 8)
            {
                return false;
            }

            item.Rgb[0] = BitConverter.ToUInt16(buf, 0);
            item.Rgb[1] = BitConverter.ToUInt16(buf, 2);
            item.Rgb[2] = BitConverter.ToUInt16(buf, 4);
            item.Rgb[3] = BitConverter.ToUInt16(buf, 6);
            return true;
        }
    }
}
