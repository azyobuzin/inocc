using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.GoLib.Scanners;

namespace Inocc.Compiler
{
    public class ParseResult
    {
        public ParseResult(FileInfo file, Tuple<FileNode, ErrorList> t)
        {
            this.File = file;
            this.CompilationUnit = t.Item1;
            this.Errors = t.Item2 != null
                ? t.Item2.Select(e => new InoccError(InoccErrorType.Parsing, e)).ToArray()
                : new InoccError[] { };
        }

        public FileInfo File { get; private set; }
        public FileNode CompilationUnit { get; private set; }
        public IReadOnlyList<InoccError> Errors { get; private set; }

        public bool HasError
        {
            get
            {
                return this.Errors.Count != 0;
            }
        }
    }
}
