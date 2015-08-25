using System;

namespace Inocc.Core
{
    public interface IGoPointer<T>
    {
        IntPtr GetAddress();
    }
}
