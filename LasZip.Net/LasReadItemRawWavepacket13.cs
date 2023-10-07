// lasreaditemraw.hpp
namespace LasZip
{
    internal class LasReadItemRawWavepacket13 : LasReadItemRaw
    {
        public LasReadItemRawWavepacket13()
        {
        }

        public override bool TryRead(LasPoint item)
        {
            if (this.inStream.Read(item.Wavepacket, 0, 29) != 29)
            {
                return false;
            }

            return true;
        }
    }
}
