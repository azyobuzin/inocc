using System;
using System.Runtime.CompilerServices;

namespace Inocc.Core
{
    public struct GoString
    {
        public GoString(byte[] value)
        {
            this.value = value;
        }

        public GoString(int size, RuntimeFieldHandle field)
        {
            this.value = new byte[size];
            RuntimeHelpers.InitializeArray(this.value, field);
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
    }
}
