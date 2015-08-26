using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inocc.Core;

namespace TestPackage
{
    static class Program
    {
        static void Main()
        {
            var w = new GoPointer<TestPackage.testWriter>();
            IoPackage.WriteString(new TestPackage.testWriter_Writer(w), GoString.FromString("test"));
        }
    }

    [GoPackage("testpkg", "<Inocc>/testpkg")]
    static class TestPackage
    {
        internal struct testWriter
        {
        }

        internal static Tuple<int, IError> Write(this IGoPointer<testWriter> self, GoSlice<byte> p)
        {
            Console.WriteLine("Write");
            return new Tuple<int, IError>(p.Count, null);
        }

        internal static Tuple<int, IError> WriteString(this IGoPointer<testWriter> self, GoString s)
        {
            Console.WriteLine("WriteString");
            return new Tuple<int, IError>(GoString.Len(s), null);
        }

        internal class testWriter_Writer : InterfaceWrapper<IGoPointer<testWriter>>, IoPackage.Writer
        {
            public testWriter_Writer(IGoPointer<testWriter> value) : base(value) { }

            public Tuple<int, IError> Write(GoSlice<byte> p)
            {
                return TestPackage.Write(this.Value, p);
            }
        }
    }
}
