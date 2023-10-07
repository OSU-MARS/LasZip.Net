// lasreaditemraw.hpp
using System;

namespace LasZip
{
    internal class LasReadItemRawByte : LasReadItemRaw
    {
        private readonly UInt32 number = 0;

        public LasReadItemRawByte(UInt32 number) 
        { 
            this.number = number; 
        }

        public override bool TryRead(LasPoint item)
        {
            if (this.inStream.Read(item.ExtraBytes, 0, (int)number) != (int)number)
            {
                return false;
            }

            return true;
        }
    }
}
