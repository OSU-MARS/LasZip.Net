// lasreaditemraw.hpp
namespace LasZip
{
    internal class LasReadItemRawPoint14 : LasReadItemRaw
    {
        private readonly byte[] buffer = new byte[30];

        public LasReadItemRawPoint14() 
        { 
        }

        public unsafe override bool TryRead(LasPoint item)
        {
            if (inStream.Read(buffer, 0, 30) != 30)
            {
                return false;
            }

            fixed (byte* pBuffer = buffer)
            {
                LasPoint14* p14 = (LasPoint14*)pBuffer;

                item.X = p14->X;
                item.Y = p14->Y;
                item.Z = p14->Z;
                item.Intensity = p14->Intensity;
                if (p14->NumberOfReturnsOfGivenPulse > 7)
                {
                    if (p14->ReturnNumber > 6)
                    {
                        if (p14->ReturnNumber >= p14->NumberOfReturnsOfGivenPulse)
                        {
                            item.NumberOfReturnsOfGivenPulse = 7;
                        }
                        else
                        {
                            item.NumberOfReturnsOfGivenPulse = 6;
                        }
                    }
                    else
                    {
                        item.ReturnNumber = p14->ReturnNumber;
                    }
                    item.NumberOfReturnsOfGivenPulse = 7;
                }
                else
                {
                    item.ReturnNumber = p14->ReturnNumber;
                    item.NumberOfReturnsOfGivenPulse = p14->NumberOfReturnsOfGivenPulse;
                }
                item.ScanDirectionFlag = p14->ScanDirectionFlag;
                item.EdgeOfFlightLine = p14->EdgeOfFlightLine;
                item.Classification = (byte)((p14->ClassificationFlags << 5) | (p14->Classification & 31));
                item.ScanAngleRank = MyDefs.ClampInt8(MyDefs.QuantizeInt16(p14->ScanAngle * 0.006));
                item.UserData = p14->UserData;
                item.PointSourceID = p14->PointSourceID;
                item.ExtendedScannerChannel = p14->ScannerChannel;
                item.ExtendedClassificationFlags = (byte)(p14->ClassificationFlags & 8); // TODO Häää?
                item.ExtendedClassification = p14->Classification;
                item.ExtendedReturnNumber = p14->ReturnNumber;
                item.ExtendedNumberOfReturnsOfGivenPulse = p14->NumberOfReturnsOfGivenPulse;
                item.ExtendedScanAngle = p14->ScanAngle;
                item.Gpstime = p14->Gpstime;
            }

            return true;
        }
    }
}
