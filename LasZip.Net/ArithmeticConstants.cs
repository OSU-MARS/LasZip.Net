// arithmeticmodel.hpp
using System;

namespace LasZip
{
    internal static class EncodeDecode
    {
        public const int BufferSize = 4096; // AC_HEADER_BYTE is unused in C++ source

        public const UInt32 MinLength = 0x01000000u; // threshold for renormalization
        public const UInt32 MaxLength = 0xFFFFFFFFu; // maximum AC interval length
    }

    internal static class GeneralModels
    {
        // Maximum values for general models
        public const int LengthShift = 15; // length bits discarded before mult.
        public const UInt32 MaxCount = 1u << LengthShift; // for adaptive models
    }

    internal static class BinaryModels
    {
        // Maximum values for binary models
        public const int LengthShift = 13; // length bits discarded before mult.
        public const UInt32 MaxCount = 1u << LengthShift; // for adaptive models
    }
}
