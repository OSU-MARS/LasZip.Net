// lasreaditem.hpp
using System;

namespace LasZip
{
    public abstract class LasReadItem
    {
        public abstract bool TryRead(Span<byte> item, UInt32 context);
    }
}
