// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawPoint10 : LasWriteItemRaw
    {
        private readonly byte[] buffer = new byte[20];

        public LasWriteItemRawPoint10()
        {
        }

        public unsafe override bool Write(LasPoint item)
        {
            if (this.OutStream == null)
            {
                throw new InvalidOperationException();
            }

            fixed (byte* pBuffer = buffer)
            {
                LasPoint10* p10 = (LasPoint10*)pBuffer;
                p10->X = item.X;
                p10->Y = item.Y;
                p10->Z = item.Z;
                p10->Intensity = item.Intensity;
                p10->Flags = item.Flags;
                p10->Classification = item.Classification;
                p10->ScanAngleRank = item.ScanAngleRank;
                p10->UserData = item.UserData;
                p10->PointSourceID = item.PointSourceID;
            }

            this.OutStream.Write(buffer, 0, 20);
            return true;
        }        
    }
}
