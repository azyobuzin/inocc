using System;

namespace Inocc.Core
{
    public interface IGoPointer<T>
    {
        T GetValue();
        void SetValue(T value);
        IntPtr GetAddress();
    }
}
