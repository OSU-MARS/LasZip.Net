// lasreaditemraw.hpp
namespace LasZip
{
    internal class LasReadItemRawPoint10 : LasReadItemRaw
    {
        private readonly byte[] buffer;

        public LasReadItemRawPoint10() 
        {
            this.buffer = new byte[20];
        }

        public unsafe override bool TryRead(LasPoint item)
        {
            int bytesRead = inStream.Read(this.buffer, 0, 20);
            if (bytesRead != 20)
            {
                return false;
            }

            fixed (byte* pBuffer = this.buffer)
            {
                LasPoint10* p10 = (LasPoint10*)pBuffer;
                item.X = p10->X;
                item.Y = p10->Y;
                item.Z = p10->Z;
                item.Intensity = p10->Intensity;
                item.Flags = p10->Flags;
                item.Classification = p10->Classification;
                item.ScanAngleRank = p10->ScanAngleRank;
                item.UserData = p10->UserData;
                item.PointSourceID = p10->PointSourceID;
            }

            return true;
        }
    }
}
