// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawRgbNir14 : LasWriteItemRaw
    {
        public LasWriteItemRawRgbNir14()
        {
        }

        public override bool Write(LasPoint item)
        {
            OutStream.Write(BitConverter.GetBytes(item.Rgb[0]), 0, 2);
            OutStream.Write(BitConverter.GetBytes(item.Rgb[1]), 0, 2);
            OutStream.Write(BitConverter.GetBytes(item.Rgb[2]), 0, 2);
            OutStream.Write(BitConverter.GetBytes(item.Rgb[3]), 0, 2);
            return true;
        }
    }
}
