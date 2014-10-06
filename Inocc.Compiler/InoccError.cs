using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inocc.Compiler.GoLib.Scanners;

namespace Inocc.Compiler
{
    public enum InoccErrorType
    {
        Parsing,
        Resolving,
        Emitting
    }

    public class InoccError : Exception
    {
        public InoccError(InoccErrorType type, Error goError)
            : base(goError.Msg)
        {
            this.Type = type;
            this.File = new FileInfo(goError.Pos.Filename);
            this.Offset = goError.Pos.Offset;
            this.Line = goError.Pos.Line;
            this.Column = goError.Pos.Column;
        }

        public InoccErrorType Type { get; private set; }
        public FileInfo File { get; private set; }
        public int Offset { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
    }

    public class InoccErrorList : Exception, IReadOnlyList<InoccError>
    {
        public InoccErrorList(InoccErrorType type, ErrorList goErrors)
            : base(goErrors.Count + " errors")
        {
            this.items = goErrors.Select(e => new InoccError(type, e)).ToArray();
        }

        private readonly InoccError[] items;

        public InoccError this[int index]
        {
            get { return this.items[index]; }
        }

        public int Count
        {
            get { return this.items.Length; }
        }

        public IEnumerator<InoccError> GetEnumerator()
        {
            return this.items.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.items.GetEnumerator();
        }
    }
}
