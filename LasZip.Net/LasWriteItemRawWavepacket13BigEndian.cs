// laswriteitemraw.hpp
using System;

namespace LasZip
{
	internal class LasWriteItemRawWavepacket13BigEndian : LasWriteItemRaw
	{
		public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
		{
            Span<byte> buffer = stackalloc byte[29];
            buffer[0] = item[0]; // wavepacket descriptor index
            MyDefs.EndianSwap64(item[1..], buffer[1..]); // byte offset to waveform data
            MyDefs.EndianSwap32(item[9..], buffer[9..]); // waveform packet size in bytes
            MyDefs.EndianSwap32(item[13..], buffer[13..]); // return point waveform location
            MyDefs.EndianSwap32(item[17..], buffer[17..]); // X(t)
            MyDefs.EndianSwap32(item[21..], buffer[21..]); // Y(t)
            MyDefs.EndianSwap32(item[25..], buffer[25..]); // Z(t)
            this.OutStream.Write(buffer);
			return true;
		}
	}
}
