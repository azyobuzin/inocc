using Inocc.Core;

namespace TestPackage
{
    [GoPackage("errors", "errors")]
    public static class ErrorsPackage
    {
        internal struct errorString
        {
            internal GoString s;
        }

        internal static GoString Error(this GoPointer<errorString> e)
        {
            e.ThrowIfIsNil();
            return e.Value.s;
        }

        private class ErrorStringWrapper : InterfaceWrapper<GoPointer<errorString>>, IError
        {
            internal ErrorStringWrapper(GoPointer<errorString> value) : base(value) { }
            
            public GoString Error()
            {
                return this.Value.Error();
            }
        }

        public static IError New(GoString text)
        {
            return new ErrorStringWrapper(
                new GoPointer<errorString>(new errorString() { s = text }));
        }
    }
}
