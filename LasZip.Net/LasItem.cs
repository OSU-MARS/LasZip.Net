// laszip.hpp
using System;

namespace LasZip
{
    internal class LasItem
    {
        public LasItemType Type { get; set; }

        public UInt16 Size { get; set; }
        public UInt16 Version { get; set; }

        public bool IsType(LasItemType type)
        {
            if (type != Type) return false;
            switch (type)
            {
                case LasItemType.Point10:
                    if (Size != 20) return false;
                    break;
                case LasItemType.Point14:
                    if (Size != 30) return false;
                    break;
                case LasItemType.Gpstime11:
                    if (Size != 8) return false;
                    break;
                case LasItemType.Rgb12:
                    if (Size != 6) return false;
                    break;
                case LasItemType.Wavepacket13:
                    if (Size != 29) return false;
                    break;
                case LasItemType.Byte:
                    if (Size < 1) return false;
                    break;
                default: return false;
            }
            return true;
        }

        public string? GetName()
        {
            switch (Type)
            {
                case LasItemType.Point10: return "POINT10";
                case LasItemType.Point14: return "POINT14";
                case LasItemType.Gpstime11: return "GPSTIME11";
                case LasItemType.Rgb12: return "RGB12";
                case LasItemType.Wavepacket13: return "WAVEPACKET13";
                case LasItemType.Byte: return "BYTE";
                default: break;
            }
            return null;
        }
    }
}
