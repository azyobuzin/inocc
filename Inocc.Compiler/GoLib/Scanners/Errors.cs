using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Inocc.Compiler.GoLib.Tokens;

namespace Inocc.Compiler.GoLib.Scanners
{
    // In an ErrorList, an error is represented by an *Error.
    // The position Pos, if valid, points to the beginning of
    // the offending token, and the error condition is described
    // by Msg.
    //
    public class Error : IError
    {
        public Position Pos { get; set; }
        public string Msg { get; set; }

        // Error implements the error interface.
        public string ErrorString()
        {
            if (this.Pos.Filename != "" || this.Pos.IsValid())
            {
                // don't print "<unknown position>"
                // TODO(gri) reconsider the semantics of Position.IsValid
                return this.Pos.ToString() + ": " + this.Msg;
            }
            return this.Msg;
        }
    }

    // ErrorList is a list of *Errors.
    // The zero value for an ErrorList is an empty ErrorList ready to use.
    //
    public class ErrorList : IError, IReadOnlyList<Error>
    {
        internal readonly List<Error> p = new List<Error>();

        // Add adds an Error with given position and error message to an ErrorList.
        public void Add(Position pos, string msg)
        {
            p.Add(new Error { Pos = pos, Msg = msg });
        }

        // Reset resets an ErrorList to no errors.
        public void Reset() { p.Clear(); }

        // ErrorList implements the sort Interface.
        public int Len() { return p.Count; }
        public void Swap(int i, int j)
        {
            var a = p[j];
            var b = p[i];
            p[i] = a;
            p[j] = b;
        }

        public bool Less(int i, int j)
        {
            var e = p[i].Pos;
            var f = p[j].Pos;
            // Note that it is not sufficient to simply compare file offsets because
            // the offsets do not reflect modified line information (through //line
            // comments).
            if (e.Filename != f.Filename)
            {
                return string.CompareOrdinal(e.Filename, f.Filename) < 0;
            }
            if (e.Line != f.Line)
            {
                return e.Line < f.Line;
            }
            if (e.Column != f.Column)
            {
                return e.Column < f.Column;
            }
            return string.CompareOrdinal(p[i].Msg, p[j].Msg) < 0;
        }

        // Sort sorts an ErrorList. *Error entries are sorted by position,
        // other errors are sorted by error message, and before any *Error
        // entry.
        //
        public void Sort()
        {
            p.Sort((i, j) =>
            {
                var e = i.Pos;
                var f = j.Pos;

                var x = string.Compare(e.Filename, f.Filename, StringComparison.Ordinal);
                if (x != 0) return x;
                x = e.Line.CompareTo(f.Line);
                if (x != 0) return x;
                return e.Column.CompareTo(e.Column);
            });
        }

        // RemoveMultiples sorts an ErrorList and removes all but the first error per line.
        public void RemoveMultiples()
        {
            this.Sort();
            var last = default(Position); // initial last.Line is != any legal error line
            var i = 0;
            foreach (var e in p)
            {
                if (e.Pos.Filename != last.Filename || e.Pos.Line != last.Line)
                {
                    last = e.Pos;
                    p[i] = e;
                    i++;
                }
            }
            p.Cut(i);
        }

        // An ErrorList implements the error interface.
        public string ErrorString()
        {
            switch (p.Count)
            {
                case 0:
                    return "no errors";
                case 1:
                    return p[0].ErrorString();
            }
            return string.Format("{0} (and {1} more errors)", p[0].ErrorString(), p.Count - 1);
        }

        // Err returns an error equivalent to this error list.
        // If the list is empty, Err returns nil.
        public IError Err()
        {
            if (p.Count == 0)
            {
                return null;
            }
            return this;
        }

        public Error this[int index]
        {
            get { return this.p[index]; }
        }

        public int Count
        {
            get { return this.Len(); }
        }

        public List<Error>.Enumerator GetEnumerator()
        {
            return this.p.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator<Error> IEnumerable<Error>.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    public static class Errors
    {
        // PrintError is a utility function that prints a list of errors to w,
        // one error per line, if the err parameter is an ErrorList. Otherwise
        // it prints the err string.
        //
        public static void PrintError(TextWriter w, IError err)
        {
            var list = err as ErrorList;
            if (list != null)
            {
                foreach (var e in list)
                {
                    w.WriteLine(e.ErrorString());
                }
            }
            else if (err != null)
            {
                w.WriteLine(err.ErrorString());
            }
        }
    }
}
