using System;
using System.Diagnostics.Contracts;

namespace Inocc.Core
{
    public struct GoSlice<T>
    {
        public GoSlice(T[] array, int offset, int count)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (offset < 0 || count < 0 || array.Length - offset < count)
                throw new PanicException("runtime error: slice bounds out of range");

            this.Array = array;
            this.Offset = offset;
            this.Count = count;
        }

        public readonly T[] Array;
        public readonly int Offset;
        public readonly int Count;

        public T this[int index]
        {
            get
            {
                var i = this.Offset + index;
                if (this.Array == null || index < 0 || i >= this.Array.Length)
                    throw new PanicException("runtime error: index out of range");

                return this.Array[i];
            }
        }
    }

    public static class GoSlice
    {
        internal static string ArrayNotNullReason = "Array field is not null because Count field has been specified.";

        public static GoSlice<T> Append<T>(GoSlice<T> slice, params T[] elems)
        {
            // Different behavior from Go

            if (elems.Length == 0) return slice;

            var elemsLen = elems.Length;
            if (slice.Count == 0)
                return new GoSlice<T>(elems, 0, elemsLen);

            Contract.Assume(slice.Array != null, ArrayNotNullReason);

            var space = slice.Array.Length - slice.Offset - slice.Count;
            if (space <= elemsLen)
            {
                Array.Copy(elems, 0, slice.Array, slice.Offset + slice.Count, elemsLen);
                return new GoSlice<T>(slice.Array, slice.Offset, slice.Count + elemsLen);
            }

            var newLen = Math.Max(slice.Count * 2, slice.Count + elemsLen);
            var array = new T[newLen];
            Array.Copy(slice.Array, array, slice.Count);
            Array.Copy(elems, 0, array, slice.Count, elemsLen);
            return new GoSlice<T>(array, 0, newLen);
        }

        public static int Cap<T>(GoSlice<T> slice)
        {
            return slice.Array == null
                ? 0
                : slice.Array.Length - slice.Offset;
        }
    }
}
