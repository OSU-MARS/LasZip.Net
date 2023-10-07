// laswriteitemraw.hpp
using System;

namespace LasZip
{
	internal class LasWriteItemRawRgb12 : LasWriteItemRaw
	{
		public LasWriteItemRawRgb12()
		{
		}

		public override bool Write(LasPoint item)
		{
            if (this.OutStream == null)
            {
                throw new InvalidOperationException();
            }

            this.OutStream.Write(BitConverter.GetBytes(item.Rgb[0]), 0, 2);
			this.OutStream.Write(BitConverter.GetBytes(item.Rgb[1]), 0, 2);
			this.OutStream.Write(BitConverter.GetBytes(item.Rgb[2]), 0, 2);
			return true;
		}
	}
}
