using System;

namespace Inocc.Core
{
    // ReSharper disable once UnusedTypeParameter
    public interface IGoPointer<T>
    {
        IntPtr GetAddress();
    }

    public class FieldPointer<T> : IGoPointer<T>
    {
        public FieldPointer(IntPtr address)
        {
            this.address = address;
        }

        public FieldPointer(IntPtr address, object holder)
        {
            this.address = address;
            this.holder = holder;
        }

        private readonly IntPtr address;

        // ReSharper disable once NotAccessedField.Local
        // to block GC
        private readonly object holder;

        public IntPtr GetAddress()
        {
            return this.address;
        }
    }
}
