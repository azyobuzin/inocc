using System;

namespace Inocc.Core
{
    public class PanicException : Exception
    {
        public PanicException(object value)
        {
            this.Value = value;
        }

        public object Value { get; private set; }
    }
}
