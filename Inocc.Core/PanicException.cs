using System;

namespace Inocc.Core
{
    public sealed class PanicException : Exception
    {
        public PanicException(object value)
            : base(value.ToString())
        {
            this.Value = value;
        }

        public object Value { get; private set; }
    }
}
