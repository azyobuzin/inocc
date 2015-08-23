using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inocc.Core;

namespace TestPackage
{
    [GoPackage("io", "io")]
    public static class IoPackage
    {
        public static IError ErrShortWrite = ErrorsPackage.New(GoString.FromString("short write"));
        public static IError ErrShortBuffer = ErrorsPackage.New(GoString.FromString("short buffer"));
        public static IError EOF = ErrorsPackage.New(GoString.FromString("EOF"));
        public static IError ErrUnexpectedEOF = ErrorsPackage.New(GoString.FromString("unexpected EOF"));
        public static IError ErrNoProgress = ErrorsPackage.New(GoString.FromString("multiple Read calls return no data or error"));

        public interface Reader
        {
            Tuple<int, IError> Read(GoSlice<byte> p);
        }

        public interface Writer
        {
            Tuple<int, IError> Write(GoSlice<byte> p);
        }

        public interface Closer
        {
            IError Close();
        }

        public interface Seeker
        {
            Tuple<long, IError> Seek(long offset, int whence);
        }

        public interface ReadWriter : Reader, Writer { }

        public interface ReadCloser : Reader, Closer { }

        public interface WriteCloser : Writer, Closer { }

        public interface ReadWriteCloser : Reader, Writer, Closer { }

        public interface ReadSeeker : Reader, Seeker { }

        public interface WriteSeeker : Writer, Seeker { }

        public interface ReadWriteSeeker : Reader, Writer, Seeker { }

        public interface ReaderFrom
        {
            Tuple<long, IError> ReadFrom(Reader r);
        }

        public interface WriterTo
        {
            Tuple<long, IError> WriteTo(Writer w);
        }

        internal interface stringWriter
        {
            Tuple<int, IError> WriteString(GoString s);
        }

        public static Tuple<int, IError> WriteString(Writer w, GoString s)
        {
            var x = InterfaceCast.Cast<stringWriter>(w);
            var sw = x.Item1;
            var ok = x.Item2;
            if (ok)
            {
                return sw.WriteString(s);
            }
            return w.Write(GoString.ToSlice(s));
        }
    }
}
