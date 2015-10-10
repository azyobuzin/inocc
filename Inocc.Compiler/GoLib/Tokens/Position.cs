using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Inocc.Compiler.GoLib.Tokens
{
    // Pos is a compact encoding of a source position within a file set.
    // It can be converted into a Position for a more convenient, but much
    // larger, representation.
    //
    // The Pos value for a given file is a number in the range [base, base+size],
    // where base and size are specified when adding the file to the file set via
    // AddFile.
    //
    // To create the Pos value for a specific source offset (measured in bytes),
    // first add the respective file to the current file set using FileSet.AddFile
    // and then call File.Pos(offset) for that file. Given a Pos value p
    // for a specific file set fset, the corresponding Position value is
    // obtained by calling fset.Position(p).
    //
    // Pos values can be compared directly with the usual comparison operators:
    // If two Pos values p and q are in the same file, comparing p and q is
    // equivalent to comparing the respective source file offsets. If p and q
    // are in different files, p < q is true if the file implied by p was added
    // to the respective file set before the file implied by q.
    //
    using Pos = Int32;

    // -----------------------------------------------------------------------------
    // Positions

    // Position describes an arbitrary source position
    // including the file, line, and column location.
    // A Position is valid if the line number is > 0.
    //
    public struct Position
    {
        public string Filename { get; set; } // filename, if any
        public int Offset { get; set; } // offset, starting at 0
        public int Line { get; set; } // line number, starting at 1
        public int Column { get; set; } // column number, starting at 1 (byte count)

        // IsValid reports whether the position is valid.
        public bool IsValid()
        {
            return this.Line > 0;
        }

        // String returns a string in one of several forms:
        //
        //	file:line:column    valid position with file name
        //	line:column         valid position without file name
        //	file                invalid position with file name
        //	-                   invalid position without file name
        //
        public override string ToString()
        {
            var sb = new StringBuilder(this.Filename ?? "");
            if (this.IsValid())
            {
                if (!string.IsNullOrEmpty(this.Filename))
                    sb.Append(":");
                sb.AppendFormat("{0}:{1}", this.Line, this.Column);
            }
            if (sb.Length == 0)
                sb.Append("-");
            return sb.ToString();
        }
    }

    // -----------------------------------------------------------------------------
    // File

    // A File is a handle for a file belonging to a FileSet.
    // A File has a name, size, and line offset table.
    //
    public class File
    {
        internal File(FileSet set, string name, int @base, int size)
        {
            this.set = set;
            this.Name = name;
            this.Base = @base;
            this.Size = size;
        }

        private FileSet set;

        public string Name { get; private set; } // file name as provided to AddFile
        public int Base { get; private set; } // Pos value range for this file is [base...base+size]
        public int Size { get; private set; } // file size as provided to AddFile

        // lines and infos are protected by set.mutex
        private List<int> lines = new List<int>(); // lines contains the offset of the first character for each line (the first entry is always 0)
        private List<lineInfo> infos = new List<lineInfo>();

        // LineCount returns the number of lines in file f.
        public int LineCount
        {
            get
            {
                try
                {
                    this.set.mutex.EnterReadLock();
                    return this.lines.Count;
                }
                finally
                {
                    this.set.mutex.ExitReadLock();
                }
            }
        }

        // AddLine adds the line offset for a new line.
        // The line offset must be larger than the offset for the previous line
        // and smaller than the file size; otherwise the line offset is ignored.
        //
        public void AddLine(int offset)
        {
            this.set.mutex.EnterWriteLock();
            try
            {
                var i = this.lines.Count;
                if ((i == 0 || this.lines[i - 1] < offset) && offset < this.Size)
                    this.lines.Add(offset);
            }
            finally
            {
                this.set.mutex.ExitWriteLock();
            }
        }

        // MergeLine merges a line with the following line. It is akin to replacing
        // the newline character at the end of the line with a space (to not change the
        // remaining offsets). To obtain the line number, consult e.g. Position.Line.
        // MergeLine will panic if given an invalid line number.
        //
        public void MergeLine(int line)
        {
            if (line <= 0)
                throw new ArgumentException("illegal line number (line numbering starts at 1)");

            this.set.mutex.EnterWriteLock();
            try
            {
                if (line >= this.lines.Count)
                    throw new ArgumentException("illegal line number");

                // To merge the line numbered <line> with the line numbered <line+1>,
                // we need to remove the entry in lines corresponding to the line
                // numbered <line+1>. The entry in lines corresponding to the line
                // numbered <line+1> is located at index <line>, since indices in lines
                // are 0-based and line numbers are 1-based.
                this.lines.RemoveAt(line);
            }
            finally
            {
                this.set.mutex.ExitWriteLock();
            }
        }

        // SetLines sets the line offsets for a file and reports whether it succeeded.
        // The line offsets are the offsets of the first character of each line;
        // for instance for the content "ab\nc\n" the line offsets are {0, 3}.
        // An empty file has an empty line offset table.
        // Each line offset must be larger than the offset for the previous line
        // and smaller than the file size; otherwise SetLines fails and returns
        // false.
        //
        public bool SetLines(IReadOnlyList<int> lines)
        {
            // verify validity of lines table
            var size = this.Size;
            for (var i = 0; i < lines.Count; i++)
            {
                var offset = lines[i];
                if (i > 0 && offset <= lines[i - 1] || size <= offset)
                    return false;
            }

            // set lines table
            this.set.mutex.EnterWriteLock();
            try
            {
                this.lines = lines.ToList();
            }
            finally
            {
                this.set.mutex.ExitWriteLock();
            }
            return true;
        }

        // SetLinesForContent sets the line offsets for the given file content.
        // It ignores position-altering //line comments.
        public void SetLinesForContent(byte[] content)
        {
            var lines = new List<int>();
            var line = 0;
            for (var offset = 0; offset < content.Length; offset++)
            {
                var b = content[offset];
                if (line >= 0)
                    lines.Add(line);
                line = -1;
                if (b == '\n')
                    line = offset + 1;
            }

            // set lines table
            this.set.mutex.EnterWriteLock();
            try
            {
                this.lines = lines;
            }
            finally
            {
                this.set.mutex.ExitWriteLock();
            }
        }

        // A lineInfo object describes alternative file and line number
        // information (such as provided via a //line comment in a .go
        // file) for a given file offset.
        internal struct lineInfo
        {
            // fields are exported to make them accessible to gob
            public int Offset { get; set; }
            public string Filename { get; set; }
            public int Line { get; set; }
        }

        // AddLineInfo adds alternative file and line number information for
        // a given file offset. The offset must be larger than the offset for
        // the previously added alternative line info and smaller than the
        // file size; otherwise the information is ignored.
        //
        // AddLineInfo is typically used to register alternative position
        // information for //line filename:line comments in source files.
        //
        public void AddLineInfo(int offset, string filename, int line)
        {
            this.set.mutex.EnterWriteLock();
            try
            {
                var i = this.infos.Count;
                if (i == 0 || this.infos[i - 1].Offset < offset && offset < this.Size)
                    this.infos.Add(new lineInfo { Offset = offset, Filename = filename, Line = line });
            }
            finally
            {
                this.set.mutex.ExitWriteLock();
            }
        }

        // Pos returns the Pos value for the given file offset;
        // the offset must be <= f.Size().
        // f.Pos(f.Offset(p)) == p.
        //
        public Pos Pos(int offset)
        {
            if (offset > this.Size)
                throw new ArgumentException("illegal file offset");
            return this.Base + offset;
        }

        // Offset returns the offset for the given file position p;
        // p must be a valid Pos value in that file.
        // f.Offset(f.Pos(offset)) == offset.
        //
        public int Offset(Pos p)
        {
            if (p < this.Base || p > this.Base + this.Size)
                throw new ArgumentException("illegal Pos value");
            return p - this.Base;
        }

        // Line returns the line number for the given file position p;
        // p must be a Pos value in that file or NoPos.
        //
        public int Line(Pos p)
        {
            return this.Position(p).Line;
        }

        private static int searchLineInfos(List<lineInfo> a, int x)
        {
            return Helper.Search(a.Count, i => a[i].Offset > x) - 1;
        }

        // unpack returns the filename and line and column number for a file offset.
        // If adjusted is set, unpack will return the filename and line information
        // possibly adjusted by //line comments; otherwise those comments are ignored.
        //
        private Tuple<string, int, int> unpack(int offset, bool adjusted)
        {
            var filename = this.Name;
            var line = 0;
            var column = 0;
            var i = Helper.SearchInts(this.lines, offset);
            if (i >= 0)
            {
                line = i + 1;
                column = offset - this.lines[i] + 1;
            }
            if (adjusted && this.infos.Count > 0)
            {
                // almost no files have extra line infos
                i = searchLineInfos(this.infos, offset);
                if (i >= 0)
                {
                    var alt = this.infos[i];
                    filename = alt.Filename;
                    i = Helper.SearchInts(this.lines, alt.Offset);
                    if (i >= 0)
                    {
                        line += alt.Line - i - 1;
                    }
                }
            }
            return Tuple.Create(filename, line, column);
        }

        internal Position position(Pos p, bool adjusted)
        {
            var offset = p - this.Base;
            var t = this.unpack(offset, adjusted);
            return new Position
            {
                Offset = offset,
                Filename = t.Item1,
                Line = t.Item2,
                Column = t.Item3
            };
        }

        // PositionFor returns the Position value for the given file position p.
        // If adjusted is set, the position may be adjusted by position-altering
        // //line comments; otherwise those comments are ignored.
        // p must be a Pos value in f or NoPos.
        //
        public Position PositionFor(Pos p, bool adjusted)
        {
            if (p != 0)
            {
                if (p < this.Base || p > this.Base + this.Size)
                {
                    throw new ArgumentException("illegal Pos value");
                }
                return this.position(p, adjusted);
            }
            return new Position();
        }

        // Position returns the Position value for the given file position p.
        // Calling f.Position(p) is equivalent to calling f.PositionFor(p, true).
        //
        public Position Position(Pos p)
        {
            return this.PositionFor(p, true);
        }
    }

    // -----------------------------------------------------------------------------
    // FileSet

    // A FileSet represents a set of source files.
    // Methods of file sets are synchronized; multiple goroutines
    // may invoke them concurrently.
    //
    public class FileSet
    {
        internal ReaderWriterLockSlim mutex = new ReaderWriterLockSlim(); // protects the file set
        internal int @base; // base offset for the next file
        internal List<File> files = new List<File>(); // list of files in the order added to the set
        internal File last; // cache of last file looked up

        public FileSet()
        {
            this.@base = 0;
        }

        // Base returns the minimum base offset that must be provided to
        // AddFile when adding the next file.
        //
        public int Base
        {
            get
            {
                this.mutex.EnterReadLock();
                try
                {
                    return this.@base;
                }
                finally
                {
                    this.mutex.ExitReadLock();
                }
            }
        }

        // AddFile adds a new file with a given filename, base offset, and file size
        // to the file set s and returns the file. Multiple files may have the same
        // name. The base offset must not be smaller than the FileSet's Base(), and
        // size must not be negative. As a special case, if a negative base is provided,
        // the current value of the FileSet's Base() is used instead.
        //
        // Adding the file will set the file set's Base() value to base + size + 1
        // as the minimum base value for the next file. The following relationship
        // exists between a Pos value p for a given file offset offs:
        //
        //	int(p) = base + offs
        //
        // with offs in the range [0, size] and thus p in the range [base, base+size].
        // For convenience, File.Pos may be used to create file-specific position
        // values from a file offset.
        //
        public File AddFile(string filename, int @base, int size)
        {
            this.mutex.EnterWriteLock();
            try
            {
                if (@base < 0)
                {
                    @base = this.@base;
                }
                if (@base < this.@base || size < 0)
                {
                    throw new ArgumentException("illegal base or size");
                }
                // base >= s.base && size >= 0
                var f = new File(this, filename, @base, size);
                @base += size + 1; // +1 because EOF also has a position
                if (@base < 0)
                {
                    throw new OverflowException("token.Pos offset overflow (> 2G of source code in file set)");
                }
                // add the file to the file set
                this.@base = @base;
                this.files.Add(f);
                this.last = f;
                return f;
            }
            finally
            {
                this.mutex.ExitWriteLock();
            }
        }

        // Iterate calls f for the files in the file set in the order they were added
        // until f returns false.
        //
        public void Iterate(Func<File, bool> f)
        {
            for (var i = 0; ; i++)
            {
                File file = null;
                this.mutex.EnterReadLock();
                try
                {
                    if (i < this.files.Count)
                    {
                        file = this.files[i];
                    }
                }
                finally
                {
                    this.mutex.ExitReadLock();
                }
                if (file == null || !f(file))
                {
                    break;
                }
            }
        }

        private static int searchFiles(List<File> a, int x)
        {
            return Helper.Search(a.Count, i => a[i].Base > x) - 1;
        }

        private File file(Pos p)
        {
            this.mutex.EnterReadLock();
            // common case: p is in last file
            var f = this.last;
            if (f != null && f.Base <= p && p <= f.Base + f.Size)
            {
                this.mutex.ExitReadLock();
                return f;
            }
            // p is not in last file - search all files
            var i = searchFiles(this.files, p);
            if (i >= 0)
            {
                f = this.files[i];
                // f.base <= int(p) by definition of searchFiles
                if (p <= f.Base + f.Size)
                {
                    this.mutex.ExitReadLock();
                    this.mutex.EnterWriteLock();
                    this.last = f; // race is ok - s.last is only a cache
                    this.mutex.ExitWriteLock();
                    return f;
                }
            }
            this.mutex.ExitReadLock();
            return null;
        }

        // File returns the file that contains the position p.
        // If no such file is found (for instance for p == NoPos),
        // the result is nil.
        //
        public File File(Pos p)
        {
            return p != 0 ? this.file(p) : null;
        }

        // PositionFor converts a Pos p in the fileset into a Position value.
        // If adjusted is set, the position may be adjusted by position-altering
        // //line comments; otherwise those comments are ignored.
        // p must be a Pos value in s or NoPos.
        //
        public Position PositionFor(Pos p, bool adjusted)
        {
            if (p != 0)
            {
                var f = this.file(p);
                if (f != null)
                {
                    return f.position(p, adjusted);
                }
            }
            return new Position();
        }

        // Position converts a Pos p in the fileset into a Position value.
        // Calling s.Position(p) is equivalent to calling s.PositionFor(p, true).
        //
        public Position Position(Pos p)
        {
            return this.PositionFor(p, true);
        }
    }
}
