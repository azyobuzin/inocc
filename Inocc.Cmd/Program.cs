using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inocc.Compiler.GoLib.Parsers;
using Inocc.Compiler.GoLib.Tokens;

namespace Inocc.Cmd
{
    class Program
    {
        static void Main(string[] args)
        {
            var fset = new FileSet();
            var t = Parser.ParseFile(
                fset,
                "test.go",
                null,
                Mode.Zero
            );
            Debugger.Break();
        }
    }
}
