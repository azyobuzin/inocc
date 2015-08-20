using System;

namespace Inocc.Core
{
    public static class Builtin
    {
        public static GoSlice<T> Append<T>(GoSlice<T> slice, params T[] elems)
        {
            // Different behavior from Go

            if (elems.Length == 0) return slice;

            var elemsLen = elems.Length;
            if (slice.Array == null)
                return new GoSlice<T>(elems, 0, elemsLen - 1);

            var space = slice.Array.Length - slice.EndIndex - 1;
            if (space <= elemsLen)
            {
                Array.Copy(elems, 0, slice.Array, slice.EndIndex + 1, elemsLen);
                return new GoSlice<T>(slice.Array, slice.StartIndex, slice.EndIndex + elemsLen);
            }

            var len = slice.EndIndex - slice.StartIndex + 1;
            var newLen = Math.Max(len * 2, len + elemsLen);
            var array = new T[newLen];
            Array.Copy(slice.Array, array, len);
            Array.Copy(elems, 0, array, len, elemsLen);
            return new GoSlice<T>(array, 0, newLen - 1);
        }

        public static int Cap<T>(GoSlice<T> slice)
        {
            return slice.Array == null
                ? 0
                : slice.Array.Length - slice.StartIndex;
        }

        public static int Len<T>(GoSlice<T> slice)
        {
            return slice.Array == null
                ? 0
                : slice.EndIndex - slice.StartIndex + 1;
        }
    }
}
