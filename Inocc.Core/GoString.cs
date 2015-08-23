using System;
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
                if (index < 0 || this.value.Length >= index)
                    throw new PanicException("runtime error: index out of range");

                return this.value[index];
            }
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
            var len = s.value.Length;
            var b = new byte[len];
            Buffer.BlockCopy(s.value, 0, b, 0, len);
            return new GoSlice<byte>(b, 0, len - 1);
        }
    }
}
