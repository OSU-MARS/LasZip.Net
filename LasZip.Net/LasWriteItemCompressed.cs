// laswriteitem.hpp
using System;

namespace LasZip
{
    internal abstract class LasWriteItemCompressed : LasWriteItem
    {
        public virtual bool ChunkSizes()
        { 
            return false; 
        }
        
        public virtual bool ChunkBytes() 
        { 
            return false; 
        }

        public abstract bool Init(ReadOnlySpan<byte> item, UInt32 context);
    }
}
