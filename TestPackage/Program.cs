using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inocc.Core;
using System.Diagnostics;

namespace TestPackage
{
    static class Program
    {
        static void Main()
        {
            
        }
    }

    [GoPackage("testpkg", "<Inocc>/testpkg")]
    static class TestPackage
    {
        internal struct testWriter
        {
            internal GoString s;
        }

        internal static Tuple<int, IError> Write(this GoPointer<testWriter> self, GoSlice<byte> p)
        {
            throw new NotImplementedException();
        }
    }
}
