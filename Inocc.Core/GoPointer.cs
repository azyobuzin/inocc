using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inocc.Core
{
    public class GoPointer<T>
    {
        public T Value;
        public bool IsNil;

        public void ThrowIfIsNil()
        {
            if (this.IsNil)
            {
                // runtime.errorString
                throw new PanicException("runtime error: invalid memory address or nil pointer dereference");
            }
        }

        public static GoPointer<T> CreateNil()
        {
            return new GoPointer<T>() { IsNil = true };
        }
    }
}
