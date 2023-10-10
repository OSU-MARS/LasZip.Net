// laswriteitemraw.hpp
using System;

namespace LasZip
{
    internal class LasWriteItemRawPoint14 : LasWriteItemRaw
    {
        private readonly byte[] buffer = new byte[30];

        public LasWriteItemRawPoint14()
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
                LasPoint14* p14 = (LasPoint14*)pBuffer;

                p14->X = item.X;
                p14->Y = item.Y;
                p14->Z = item.Z;
                p14->Intensity = item.Intensity;
                p14->ScanDirectionFlag = item.ScanDirectionFlag;
                p14->EdgeOfFlightLine = item.EdgeOfFlightLine;
                p14->Classification = (byte)(item.ClassificationAndFlags & 31);
                p14->UserData = item.UserData;
                p14->PointSourceID = item.PointSourceID;

                if (item.ExtendedPointType != 0)
                {
                    p14->ClassificationFlags = (byte)((item.ExtendedClassificationFlags & 8) | (item.ClassificationAndFlags >> 5));
                    if (item.ExtendedClassification > 31) p14->Classification = item.ExtendedClassification;
                    p14->ScannerChannel = item.ExtendedScannerChannel;
                    p14->ReturnNumber = item.ExtendedReturnNumber;
                    p14->NumberOfReturnsOfGivenPulse = item.ExtendedNumberOfReturnsOfGivenPulse;
                    p14->ScanAngle = item.ExtendedScanAngle;
                }
                else
                {
                    p14->ClassificationFlags = (byte)(item.ClassificationAndFlags >> 5);
                    p14->ScannerChannel = 0;
                    p14->ReturnNumber = item.ReturnNumber;
                    p14->NumberOfReturnsOfGivenPulse = item.NumberOfReturnsOfGivenPulse;
                    p14->ScanAngle = MyDefs.QuantizeInt16(item.ScanAngleRank / 0.006f);
                }

                p14->Gpstime = item.Gpstime;
            }

            this.OutStream.Write(buffer, 0, 30);
            return true;
        }
    }
}
