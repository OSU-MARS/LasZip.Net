// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawByte : LasReadItemRaw
    {
        private readonly int number = 0;

        public LasReadItemRawByte(UInt32 number) 
        { 
            this.number = (int)number; 
        }

        public override bool TryRead(Span<byte> item, UInt32 context)
        {
            this.InStream.ReadExactly(item[..this.number]);
            return true;
        }
    }
}
