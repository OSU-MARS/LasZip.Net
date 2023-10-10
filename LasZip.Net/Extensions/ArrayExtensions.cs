using System;

namespace LasZip.Extensions
{
    internal static class ArrayExtensions
    {
        public static T[] Extend<T>(this T[] array, UInt32 capacity)
        {
            T[] extendedArray = new T[capacity];
            Array.Copy(array, extendedArray, array.Length);
            return extendedArray;
        }
    }
}
