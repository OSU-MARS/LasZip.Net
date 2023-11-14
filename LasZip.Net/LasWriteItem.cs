// laswriteitem.hpp
using System;

namespace LasZip
{
    public abstract class LasWriteItem
    {
        public abstract bool Write(ReadOnlySpan<byte> item, UInt32 context);
    }
}
