// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawPoint14BigEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[30];
            MyDefs.EndianSwap32(item, buffer); // x
            MyDefs.EndianSwap32(item[4..], buffer[4..]); // y
            MyDefs.EndianSwap32(item[8..], buffer[8..]); // z
            MyDefs.EndianSwap16(item[12..], buffer[12..]); // intensity
            buffer[14] = item[14]; // return number and number of returns
            buffer[15] = item[15]; // classification flags, scanner channel, scan direction and edge of flight line flags
            buffer[16] = item[16]; // classification
            buffer[17] = item[17]; // user data
            MyDefs.EndianSwap16(item[18..], buffer[18..]); // scan angle
            MyDefs.EndianSwap16(item[20..], buffer[20..]); // point source ID
            MyDefs.EndianSwap64(item[22..], buffer[22..]); // GPS time
            this.OutStream.Write(buffer);
            return true;
        }
    }
}
