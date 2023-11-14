// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawPoint10BigEndian : LasWriteItemRaw
    {
        public override bool Write(ReadOnlySpan<byte> item, UInt32 context)
        {
            Span<byte> buffer = stackalloc byte[20];
            MyDefs.EndianSwap32(item, buffer); // X
            MyDefs.EndianSwap32(item[4..], buffer[4..]); // Y
            MyDefs.EndianSwap32(item[8..], buffer[8..]); // Z
            MyDefs.EndianSwap16(item[12..], buffer[12..]); // intensity
            buffer[14] = item[14]; // bitfield
            buffer[15] = item[15]; // classification
            buffer[16] = item[16]; // scan_angle_rank
            buffer[17] = item[17]; //user_data
            MyDefs.EndianSwap16(item[18..], buffer[18..]); // point_source_ID

            this.OutStream.Write(buffer);
            return true;
        }        
    }
}
