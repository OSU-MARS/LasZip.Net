using System;

namespace LasZip
{
    internal class LasReadItemRawPoint10LittleEndian : LasReadItemRaw
    {
        public override bool TryRead(Span<byte> item, uint context)
        {
            this.InStream.ReadExactly(item);
            return true;
        }
    }
}
