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

        internal static GoString Error(this IGoPointer<errorString> e)
        {
            return e.GetValue().s;
        }

        private class ErrorStringWrapper : InterfaceWrapper<IGoPointer<errorString>>, IError
        {
            internal ErrorStringWrapper(IGoPointer<errorString> value) : base(value) { }
            
            public GoString Error()
            {
                return this.Value.Error();
            }
        }

        public static IError New(GoString text)
        {
            return new ErrorStringWrapper(
                new GoPointer<errorString>(new errorString { s = text }));
        }
    }
}
