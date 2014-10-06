using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.GoLib.Parsers;
using Inocc.Compiler.GoLib.Scanners;
using Inocc.Compiler.GoLib.Tokens;

namespace Inocc.Compiler
{
    public static class GoLanguageServices
    {
        public static ParseResult Parse(FileInfo file)
        {
            var fset = new FileSet();
            return new ParseResult(file, Parser.ParseFile(fset, file.FullName, null, Mode.AllErrors));
        }
    }
}
