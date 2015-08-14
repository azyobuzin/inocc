using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Inocc.Compiler.GoLib
{
    internal static class Filepath
    {
        private static bool IsPathSeparator(char c)
        {
            return c == '/' || (Helper.IsWindows() && c == '\\');
        }

        private static bool isSlash(char c)
        {
            return c == '\\' || c == '/';
        }

        // volumeNameLen returns length of the leading volume name on Windows.
        // It returns 0 elsewhere.
        private static int volumeNameLen(string path)
        {
            if (!Helper.IsWindows()) return 0;

            if (path.Length < 2)
            {
                return 0;
            }
            // with drive letter
            var c = path[0];
            if (path[1] == ':' && ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z'))
            {
                return 2;
            }
            // is it UNC
            var l = path.Length;
            if (l >= 5 && isSlash(path[0]) && isSlash(path[1]) &&
                !isSlash(path[2]) && path[2] != '.')
            {
                // first, leading `\\` and next shouldn't be `\`. its server name.
                for (var n = 3; n < l - 1; n++)
                {
                    // second, next '\' shouldn't be repeated.
                    if (isSlash(path[n]))
                    {
                        n++;
                        // third, following something characters. its share name.
                        if (!isSlash(path[n]))
                        {
                            if (path[n] == '.')
                            {
                                break;
                            }
                            for (; n < l; n++)
                            {
                                if (isSlash(path[n]))
                                {
                                    break;
                                }
                            }
                            return n;
                        }
                        break;
                    }
                }
            }
            return 0;
        }

        // IsAbs reports whether the path is absolute.
        public static bool IsAbs(string path)
        {
            if (Helper.IsWindows())
            {
                var l = volumeNameLen(path);
                if (l == 0)
                {
                    return false;
                }
                path = path.Substring(l);
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }
                return isSlash(path[0]);
            }
            else
            {
                return path.StartsWith("/");
            }
        }

        // A lazybuf is a lazily constructed path buffer.
        // It supports append, reading previously appended bytes,
        // and retrieving the final string. It does not allocate a buffer
        // to hold the output until that output diverges from s.
        private class lazybuf
        {
            internal string path;
            internal byte[] buf;
            internal int w;
            internal string volAndPath;
            internal int volLen;

            internal byte index(int i)
            {
                if (this.buf != null)
                {
                    return this.buf[i];
                }
                return Encoding.UTF8.GetBytes(this.path)[i];
            }

            internal void append(byte c)
            {
                if (this.buf == null)
                {
                    var path = Encoding.UTF8.GetBytes(this.path);
                    if (this.w < path.Length && path[this.w] == c)
                    {
                        this.w++;
                        return;
                    }
                    this.buf = new byte[path.Length];
                    Array.Copy(path, this.buf, this.w - 1); //copy(b.buf, b.path[:b.w])
                }
                this.buf[this.w] = c;
                this.w++;
            }

            public override string ToString()
            {
                var volAndPath = Encoding.UTF8.GetBytes(this.volAndPath);
                if (this.buf == null)
                {
                    return Encoding.UTF8.GetString(volAndPath, 0, this.volLen + this.w);
                }
                return Encoding.UTF8.GetString(volAndPath.Take(this.volLen).Concat(this.buf.Take(this.w)).ToArray());
            }
        }

        // Clean returns the shortest path name equivalent to path
        // by purely lexical processing.  It applies the following rules
        // iteratively until no further processing can be done:
        //
        //	1. Replace multiple Separator elements with a single one.
        //	2. Eliminate each . path name element (the current directory).
        //	3. Eliminate each inner .. path name element (the parent directory)
        //	   along with the non-.. element that precedes it.
        //	4. Eliminate .. elements that begin a rooted path:
        //	   that is, replace "/.." by "/" at the beginning of a path,
        //	   assuming Separator is '/'.
        //
        // The returned path ends in a slash only if it represents a root directory,
        // such as "/" on Unix or `C:\` on Windows.
        //
        // If the result of this process is an empty string, Clean
        // returns the string ".".
        //
        // See also Rob Pike, ``Lexical File Names in Plan 9 or
        // Getting Dot-Dot Right,''
        // http://plan9.bell-labs.com/sys/doc/lexnames.html
        public static string Clean(string path)
        {
            var originalPath = path;
            var volLen = volumeNameLen(path);
            path = path.Substring(volLen);
            if (string.IsNullOrEmpty(path))
            {
                if (volLen > 1 && originalPath[1] != ':')
                {
                    // should be UNC
                    return FromSlash(originalPath);
                }
                return originalPath + ".";
            }
            var rooted = IsPathSeparator(path[0]);

            // Invariants:
            //	reading from path; r is index of next byte to process.
            //	writing to buf; w is index of next byte to write.
            //	dotdot is index in buf where .. must stop, either because
            //		it is the leading slash or it is a leading ../../.. prefix.
            var n = path.Length;
            var @out = new lazybuf() { path = path, volAndPath = originalPath, volLen = volLen };
            var r = 0;
            var dotdot = 0;
            if (rooted)
            {
                @out.append((byte)Path.DirectorySeparatorChar);
                r = 1;
                dotdot = 1;
            }

            while (r < n)
            {
                if (IsPathSeparator(path[r]))
                    // empty path element
                    r++;
                else if (path[r] == '.' && (r + 1 == n || IsPathSeparator(path[r + 1])))
                    // . element
                    r++;
                else if (path[r] == '.' && path[r + 1] == '.' && (r + 2 == n || IsPathSeparator(path[r + 2])))
                {
                    // .. element: remove to last separator
                    r += 2;
                    if (@out.w > dotdot)
                    {
                        // can backtrack
                        @out.w--;
                        while (@out.w > dotdot && !IsPathSeparator((char)@out.index(@out.w)))
                        {
                            @out.w--;
                        }
                    }
                    else if (!rooted)
                    {
                        // cannot backtrack, but not rooted, so append .. element.
                        if (@out.w > 0)
                        {
                            @out.append((byte)Path.DirectorySeparatorChar);
                        }
                        @out.append((byte)'.');
                        @out.append((byte)'.');
                        dotdot = @out.w;
                    }
                }
                else
                {
                    // real path element.
                    // add slash if needed
                    if (rooted && @out.w != 1 || !rooted && @out.w != 0)
                    {
                        @out.append((byte)Path.DirectorySeparatorChar);
                    }
                    // copy element
                    for (; r < n && !IsPathSeparator(path[r]); r++)
                    {
                        @out.append((byte)path[r]);
                    }
                }
            }

            // Turn empty string into "."
            if (@out.w == 0)
            {
                @out.append((byte)'.');
            }

            return FromSlash(@out.ToString());
        }

        // FromSlash returns the result of replacing each slash ('/') character
        // in path with a separator character. Multiple slashes are replaced
        // by multiple separators.
        public static string FromSlash(string path)
        {
            if (Path.DirectorySeparatorChar == '/')
            {
                return path;
            }
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
