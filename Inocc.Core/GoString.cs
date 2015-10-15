using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;

namespace Inocc.Core
{
    public struct GoString
    {
        public GoString(byte[] value)
        {
            this.value = value;
        }

        private readonly byte[] value;

        public byte this[int index]
        {
            get
            {
                if (this.value == null || index < 0 || this.value.Length >= index)
                    throw new PanicException("runtime error: index out of range");

                return this.value[index];
            }
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(this.value);
        }

        public static GoString FromBytes(byte[] source)
        {
            var len = source.Length;
            var b = new byte[len];
            Buffer.BlockCopy(source, 0, b, 0, len);
            return new GoString(b);
        }

        public static GoString FromString(string source)
        {
            return new GoString(Encoding.UTF8.GetBytes(source));
        }

        public static GoString FromField(int size, RuntimeFieldHandle field)
        {
            var b = new byte[size];
            RuntimeHelpers.InitializeArray(b, field);
            return new GoString(b);
        }

        public static GoSlice<byte> ToSlice(GoString s)
        {
            if (s.value == null || s.value.Length == 0) return new GoSlice<byte>();
            var len = s.value.Length;
            var b = new byte[len];
            Buffer.BlockCopy(s.value, 0, b, 0, len);
            return new GoSlice<byte>(b, 0, len);
        }

        public static GoString FromSlice(GoSlice<byte> s)
        {
            if (s.Count == 0) return new GoString();
            Contract.Assume(s.Array != null, GoSlice.ArrayNotNullReason);

            var b = new byte[s.Count];
            Buffer.BlockCopy(s.Array, s.Offset, b, 0, s.Count);
            return new GoString(b);
        }

        public static GoString Concat(GoString x, GoString y)
        {
            if (x.value == null || x.value.Length == 0) return y;
            if (y.value == null || y.value.Length == 0) return x;

            var xlen = x.value.Length;
            var ylen = y.value.Length;
            var b = new byte[xlen + ylen];
            Buffer.BlockCopy(x.value, 0, b, 0, xlen);
            Buffer.BlockCopy(y.value, 0, b, xlen, ylen);
            return new GoString(b);
        }

        public static GoString Concat(GoSlice<byte> x, GoString y)
        {
            if (x.Count == 0) return y;
            Contract.Assume(x.Array != null, GoSlice.ArrayNotNullReason);
            if (y.value == null || y.value.Length == 0) return FromSlice(x);

            var b = new byte[x.Count + y.value.Length];
            Buffer.BlockCopy(x.Array, x.Offset, b, 0, x.Count);
            Buffer.BlockCopy(y.value, 0, b, x.Count, y.value.Length);
            return new GoString(b);
        }

        public static GoString Concat(GoString x, GoSlice<byte> y)
        {
            if (x.value == null || x.value.Length == 0) return FromSlice(y);
            if (y.Count == 0) return x;
            Contract.Assume(y.Array != null, GoSlice.ArrayNotNullReason);

            var xlen = x.value.Length;
            var b = new byte[xlen + y.Count];
            Buffer.BlockCopy(x.value, 0, b, 0, xlen);
            Buffer.BlockCopy(y.Array, y.Offset, b, xlen, y.Count);
            return new GoString(b);
        }

        public static int Len(GoString s)
        {
            return s.value != null ? s.value.Length : 0;
        }
    }
}
