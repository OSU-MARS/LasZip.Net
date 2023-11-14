// lasreaditem.hpp
using System;

namespace LasZip
{
    abstract class LasReadItemCompressed : LasReadItem
    {
        public virtual bool ChunkSizes()
        {
            return false;
        }

        public abstract bool Init(ReadOnlySpan<byte> item, UInt32 context);
    }
}
