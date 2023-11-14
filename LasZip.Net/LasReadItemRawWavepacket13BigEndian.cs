// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawWavepacket13BigEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[29];
            this.InStream.ReadExactly(buffer);
            item[0] = buffer[0]; // wavepacket descriptor index
            MyDefs.EndianSwap64(buffer[1..], item[1..]); // byte offset to waveform data
            MyDefs.EndianSwap32(buffer[9..], item[9..]); // waveform packet size in bytes
            MyDefs.EndianSwap32(buffer[13..], item[13..]); // return point waveform location
            MyDefs.EndianSwap32(buffer[17..], item[17..]); // X(t)
            MyDefs.EndianSwap32(buffer[21..], item[21..]); // Y(t)
            MyDefs.EndianSwap32(buffer[25..], item[25..]); // Z(t)
            return true;
        }
    }
}
