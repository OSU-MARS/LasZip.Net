// laswriteitemraw.hpp
using System;

namespace LasZip
{
	internal class LasWriteItemRawRgb12BigEndian : LasWriteItemRaw
	{
		public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
		{
			Span<byte> buffer = stackalloc byte[6];
			MyDefs.EndianSwap16(item, buffer);
			MyDefs.EndianSwap16(item[2..], buffer[2..]);
            MyDefs.EndianSwap16(item[4..], buffer[4..]);
            this.OutStream.Write(buffer);
			return true;
		}
	}
}
