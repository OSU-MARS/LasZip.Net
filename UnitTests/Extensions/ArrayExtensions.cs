using System.Numerics;

namespace LasZip.UnitTests.Extensions
{
    public static class ArrayExtensions
    {
        public static bool Equals<T>(T[]? array, T[]? other) where T : INumber<T>
        {
            if (ReferenceEquals(array, other))
            {
                return true;
            }
            if (array == null ^ other == null)
            {
                return false;
            }
            if (array!.Length != other!.Length)
            {
                return false;
            }

            for (int index = 0; index < array.Length; ++index)
            {
                if (array[index] != other[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
