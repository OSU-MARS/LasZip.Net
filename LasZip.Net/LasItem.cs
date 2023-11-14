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
                    if (this.Size != 20) { return false; }
                    break;
                case LasItemType.Point14:
                    if (this.Size != 30) { return false; }
                    break;
                case LasItemType.Gpstime11:
                    if (this.Size != 8) { return false; }
                    break;
                case LasItemType.Rgb12:
                    if (this.Size != 6) { return false; }
                    break;
                case LasItemType.Byte:
                    if (this.Size != 1) { return false; }
                    break;
                case LasItemType.Rgb14:
                    if (this.Size != 6) { return false; }
                    break;
                case LasItemType.RgbNir14:
                    if (this.Size != 8) { return false; }
                    break;
                case LasItemType.Byte14:
                    if (this.Size < 1) { return false; }
                    break;
                case LasItemType.Wavepacket13:
                    if (this.Size != 29) { return false; }
                    break;
                case LasItemType.Wavepacket14:
                    if (this.Size != 29) { return false; }
                    break;
                default: return false;
            }
            return true;
        }

        public string? GetName()
        {
            switch (Type)
            {
                case LasItemType.Point10: 
                    return "POINT10";
                case LasItemType.Point14: 
                    return "POINT14";
                case LasItemType.Gpstime11: 
                    return "GPSTIME11";
                case LasItemType.Rgb12: 
                    return "RGB12";
                case LasItemType.Byte:
                    return "BYTE";
                case LasItemType.Rgb14:
                    return "RGB14";
                case LasItemType.RgbNir14:
                    return "RGBNIR14";
                case LasItemType.Byte14:
                    return "BYTE14";
                case LasItemType.Wavepacket13: 
                    return "WAVEPACKET13";
                case LasItemType.Wavepacket14:
                    return "WAVEPACKET14";
                default: break;
            }
            return null;
        }
    }
}
