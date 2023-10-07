// laswriteitemraw.hpp
using System;

namespace LasZip
{
	internal class LasWriteItemRawWavepacket13 : LasWriteItemRaw
	{
		public LasWriteItemRawWavepacket13()
		{
		}

		public override bool Write(LasPoint item)
		{
			if (this.OutStream == null)
			{
				throw new InvalidOperationException();
			}

			this.OutStream.Write(item.Wavepacket, 0, 29);
			return true;
		}
	}
}
