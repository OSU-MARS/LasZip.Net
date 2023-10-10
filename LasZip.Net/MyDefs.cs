// mydefs.{hpp, cpp}
using System;

namespace LasZip
{
    internal class MyDefs
    {
        private const int UInt8MaxPlusOne = 0x0100; // 256
        //private const int U16_MAX_PLUS_ONE = 0x00010000; // 65536
        //private const Int64 U32_MAX_PLUS_ONE = 0x0000000100000000; // 4294967296

        public static int FoldUint8(int n)
        {
            return n < byte.MinValue ? (n + UInt8MaxPlusOne) : (n > byte.MaxValue ? (n - UInt8MaxPlusOne) : n);
        }

        public static sbyte ClampInt8(int n)
        {
            return n <= sbyte.MinValue ? sbyte.MinValue : (n >= sbyte.MaxValue ? sbyte.MaxValue : (sbyte)n);
        }

        public static byte ClampUint8(int n)
        {
            return n <= byte.MinValue ? byte.MinValue : (n >= byte.MaxValue ? byte.MaxValue : (byte)n);
        }

        public static Int16 ClampInt16(int n)
        {
            return n <= Int16.MinValue ? Int16.MinValue : (n >= Int16.MaxValue ? Int16.MaxValue : (Int16)n);
        }

        public static UInt16 ClampUint16(int n)
        {
            return n <= UInt16.MinValue ? UInt16.MinValue : (n >= UInt16.MaxValue ? UInt16.MaxValue : (UInt16)n);
        }

        //#define I32_CLAMP(n)    (((n) <= I32_MIN) ? I32_MIN : (((n) >= I32_MAX) ? I32_MAX : ((I32)(n))))
        //#define U32_CLAMP(n)    (((n) <= U32_MIN) ? U32_MIN : (((n) >= U32_MAX) ? U32_MAX : ((U32)(n))))

        //#define I8_QUANTIZE(n) (((n) >= 0) ? (I8)((n)+0.5f) : (I8)((n)-0.5f))
        //#define U8_QUANTIZE(n) (((n) >= 0) ? (U8)((n)+0.5f) : (U8)(0))

        public static Int16 QuantizeInt16(double n)
        {
            return (Int16)(n >= 0 ? n + 0.5 : n - 0.5);
        }

        //#define U16_QUANTIZE(n) (((n) >= 0) ? (U16)((n)+0.5f) : (U16)(0))

        public static int QuantizeInt32(double n)
        {
            return (int)(n >= 0.0 ? n + 0.5 : n - 0.5);
        }

        public static UInt32 QuantizeUInt32(float n)
        {
            return (n >= 0.0F) ? (UInt32)(n + 0.5F) : 0U;
        }

        //#define I64_QUANTIZE(n) (((n) >= 0) ? (I64)((n)+0.5f) : (I64)((n)-0.5f))
        //#define U64_QUANTIZE(n) (((n) >= 0) ? (U64)((n)+0.5f) : (U64)(0))

        public static Int16 FloorInt16(double n)
        {
            return (Int16)n > n ? (Int16)((Int16)n - 1) : (Int16)n;
        }

        public static int FloorInt32(double n)
        {
            return (int)n > n ? (int)n - 1 : (int)n;
        }

        public static Int64 FloorInt64(double n)
        {
            return (Int64)n > n ? (Int64)n - 1 : (Int64)n;
        }
    }
}
