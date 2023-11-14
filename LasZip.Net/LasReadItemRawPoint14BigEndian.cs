// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawPoint14BigEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[30];
            this.InStream.ReadExactly(buffer);
            MyDefs.EndianSwap32(buffer, item); // x
            MyDefs.EndianSwap32(buffer[4..], item[4..]); // y
            MyDefs.EndianSwap32(buffer[8..], item[8..]); // z
            MyDefs.EndianSwap16(buffer[12..], item[12..]); // intensity
            item[14] = buffer[14]; // return number and number of returns
            item[15] = buffer[15]; // classification flags, scanner channel, scan direction and edge of flight line flags
            item[16] = buffer[16]; // classification
            item[17] = buffer[17]; // user data
            MyDefs.EndianSwap16(buffer[18..], item[18..]); // scan angle
            MyDefs.EndianSwap16(buffer[20..], item[20..]); // point source ID
            MyDefs.EndianSwap64(buffer[22..], item[22..]); // GPS time
            return true;
        }
    }
}
