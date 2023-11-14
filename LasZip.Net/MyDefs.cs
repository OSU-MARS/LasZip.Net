// mydefs.{hpp, cpp}
using System;

namespace LasZip
{
    internal class MyDefs
    {
        private const int UInt8MaxPlusOne = 0x0100; // 256
        //private const int U16_MAX_PLUS_ONE = 0x00010000; // 65536
        //private const Int64 U32_MAX_PLUS_ONE = 0x0000000100000000; // 4294967296

        public static int FoldUInt8(int n)
        {
            return n < byte.MinValue ? (n + UInt8MaxPlusOne) : (n > byte.MaxValue ? (n - UInt8MaxPlusOne) : n);
        }

        public static sbyte ClampInt8(int n)
        {
            return n <= sbyte.MinValue ? sbyte.MinValue : (n >= sbyte.MaxValue ? sbyte.MaxValue : (sbyte)n);
        }

        public static byte ClampUInt8(int n)
        {
            return n <= byte.MinValue ? byte.MinValue : (n >= byte.MaxValue ? byte.MaxValue : (byte)n);
        }

        public static Int16 ClampInt16(int n)
        {
            return n <= Int16.MinValue ? Int16.MinValue : (n >= Int16.MaxValue ? Int16.MaxValue : (Int16)n);
        }

        public static UInt16 ClampUInt16(int n)
        {
            return n <= UInt16.MinValue ? UInt16.MinValue : (n >= UInt16.MaxValue ? UInt16.MaxValue : (UInt16)n);
        }

        //#define I32_CLAMP(n)    (((n) <= I32_MIN) ? I32_MIN : (((n) >= I32_MAX) ? I32_MAX : ((I32)(n))))
        //#define U32_CLAMP(n)    (((n) <= U32_MIN) ? U32_MIN : (((n) >= U32_MAX) ? U32_MAX : ((U32)(n))))

        //#define I8_QUANTIZE(n) (((n) >= 0) ? (I8)((n)+0.5f) : (I8)((n)-0.5f))
        //#define U8_QUANTIZE(n) (((n) >= 0) ? (U8)((n)+0.5f) : (U8)(0))

        public static sbyte QuantizeInt8(double n)
        {
            return n >= 0.0 ? (sbyte)(n + 0.5) : (sbyte)(n - 0.5);
        }

        public static Int16 QuantizeInt16(double n)
        {
            return (Int16)(n >= 0.0 ? n + 0.5 : n - 0.5);
        }

        public static Int32 QuantizeInt32(double n)
        {
            return (Int32)(n >= 0.0 ? n + 0.5 : n - 0.5);
        }

        public static Int64 QuantizeInt64(double n)
        {
            return n >= 0 ? (Int64)(n + 0.5) : (Int64)(n - 0.5);
        }

        public static byte QuantizeUInt8(double n)
        {
            return n >= 0.0 ? (byte)(n + 0.5) : (byte)0;
        }

        public static UInt16 QuantizeUInt16(double n)
        {
            return n >= 0.0 ? (UInt16)(n + 0.5) : (UInt16)0;
        }

        public static UInt32 QuantizeUInt32(float n)
        {
            return n >= 0.0F ? (UInt32)(n + 0.5F) : 0U;
        }

        public static UInt32 QuantizeUInt32(double n)
        {
            return n >= 0.0 ? (UInt32)(n + 0.5) : 0U;
        }

        public static UInt64 QuantizeUInt64(double n)
        {
            return n >= 0 ? (UInt64)(n + 0.5) : 0UL;
        }

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

        public static void EndianSwap16(ReadOnlySpan<byte> reversedBuffer, Span<byte> item)
        {
            item[0] = reversedBuffer[1];
            item[1] = reversedBuffer[0];
        }

        public static void EndianSwap32(ReadOnlySpan<byte> reversedBuffer, Span<byte> item)
        {
            item[0] = reversedBuffer[3];
            item[1] = reversedBuffer[2];
            item[2] = reversedBuffer[1];
            item[3] = reversedBuffer[0];
        }

        public static void EndianSwap64(ReadOnlySpan<byte> reversedBuffer, Span<byte> item)
        {
            item[0] = reversedBuffer[7];
            item[1] = reversedBuffer[6];
            item[2] = reversedBuffer[5];
            item[3] = reversedBuffer[4];
            item[4] = reversedBuffer[3];
            item[5] = reversedBuffer[2];
            item[6] = reversedBuffer[1];
            item[7] = reversedBuffer[0];
        }
    }
}
