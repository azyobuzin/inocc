using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inocc.Core
{
    public unsafe struct GoString
    {
        public static readonly Encoding UTF8 = new UTF8Encoding(false);

        public GoString(byte* ptr, int len)
        {
            this.ptr = ptr;
            this.len = len;
        }

        private byte* ptr;
        private int len;

        public int Length { get { return this.len; } }

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= len)
                    throw new ArgumentOutOfRangeException();
                return this.ptr[index];
            }
        }

        public byte this[uint index]
        {
            get
            {
                if (index < 0 || index >= len)
                    throw new ArgumentOutOfRangeException();
                return this.ptr[index];
            }
        }

        public byte this[long index]
        {
            get
            {
                if (index < 0 || index >= len)
                    throw new ArgumentOutOfRangeException();
                return this.ptr[index];
            }
        }

        public byte this[ulong index]
        {
            get
            {
                var i = checked((int)index);
                if (i < 0 || i >= len)
                    throw new ArgumentOutOfRangeException();
                return this.ptr[i];
            }
        }

        public int GetRune(int index)
        {
            var c = stackalloc char[2];
            var i = UTF8.GetChars(this.ptr + index, len - index, c, 2);
            if (char.IsHighSurrogate(c[0]))
                return char.ConvertToUtf32(c[0], c[1]);
            return c[0];
        }
    }
}
