// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawPoint10BigEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[20];
            this.InStream.ReadExactly(buffer);
            MyDefs.EndianSwap32(buffer, item); // X
            MyDefs.EndianSwap32(buffer[4..], item[4..]); // Y
            MyDefs.EndianSwap32(buffer[8..], item[8..]); // Z
            MyDefs.EndianSwap16(buffer[12..], item[12..]); // intensity
            item[14] = buffer[14]; // bitfield
            item[15] = buffer[15]; // classification
            item[16] = buffer[16]; // scan_angle_rank
            item[17] = buffer[17]; //user_data
            MyDefs.EndianSwap16(buffer[18..], item[18..]); // point_source_ID
            return true;
        }
    }
}
