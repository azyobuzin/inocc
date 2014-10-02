using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Pos = System.Int32;

namespace Inocc.Compiler.GoLib
{
    internal static class Helper
    {
        internal static bool IsWhitespace(byte ch)
        {
            return ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r';
        }

        internal static string StripTrailingWhitespace(string s)
        {
            return s.TrimEnd(' ', '\t', '\n', '\r');
        }

        internal static bool IsValidPos(Pos p)
        {
            return p != 0;
        }

        /// <summary>
        /// IsExported reports whether name is an exported Go symbol
        /// (that is, whether it begins with an upper-case letter).
        /// </summary>
        internal static bool IsExported(string name)
        {
            return char.IsUpper(name, 0);
        }

        internal static int SearchInts(IReadOnlyList<int> a, int x)
        {
            // This function body is a manually inlined version of:
            //
            //   return sort.Search(len(a), func(i int) bool { return a[i] > x }) - 1
            //
            // With better compiler optimizations, this may not be needed in the
            // future, but at the moment this change improves the go/printer
            // benchmark performance by ~30%. This has a direct impact on the
            // speed of gofmt and thus seems worthwhile (2011-04-29).
            // TODO(gri): Remove this when compilers have caught up.
            var i = 0;
            var j = a.Count;
            while (i < j)
            {
                var h = i + (j - i) / 2; // avoid overflow when computing h
                // i ≤ h < j
                if (a[h] <= x)
                {
                    i = h + 1;
                }
                else
                {
                    j = h;
                }
            }
            return i - 1;
        }

        // Search uses binary search to find and return the smallest index i
        // in [0, n) at which f(i) is true, assuming that on the range [0, n),
        // f(i) == true implies f(i+1) == true.  That is, Search requires that
        // f is false for some (possibly empty) prefix of the input range [0, n)
        // and then true for the (possibly empty) remainder; Search returns
        // the first true index.  If there is no such index, Search returns n.
        // (Note that the "not found" return value is not -1 as in, for instance,
        // strings.Index).
        // Search calls f(i) only for i in the range [0, n).
        //
        // A common use of Search is to find the index i for a value x in
        // a sorted, indexable data structure such as an array or slice.
        // In this case, the argument f, typically a closure, captures the value
        // to be searched for, and how the data structure is indexed and
        // ordered.
        //
        // For instance, given a slice data sorted in ascending order,
        // the call Search(len(data), func(i int) bool { return data[i] >= 23 })
        // returns the smallest index i such that data[i] >= 23.  If the caller
        // wants to find whether 23 is in the slice, it must test data[i] == 23
        // separately.
        //
        // Searching data sorted in descending order would use the <=
        // operator instead of the >= operator.
        //
        // To complete the example above, the following code tries to find the value
        // x in an integer slice data sorted in ascending order:
        //
        //	x := 23
        //	i := sort.Search(len(data), func(i int) bool { return data[i] >= x })
        //	if i < len(data) && data[i] == x {
        //		// x is present at data[i]
        //	} else {
        //		// x is not present in data,
        //		// but i is the index where it would be inserted.
        //	}
        //
        // As a more whimsical example, this program guesses your number:
        //
        //	func GuessingGame() {
        //		var s string
        //		fmt.Printf("Pick an integer from 0 to 100.\n")
        //		answer := sort.Search(100, func(i int) bool {
        //			fmt.Printf("Is your number <= %d? ", i)
        //			fmt.Scanf("%s", &s)
        //			return s != "" && s[0] == 'y'
        //		})
        //		fmt.Printf("Your number is %d.\n", answer)
        //	}
        //
        internal static int Search(int n, Func<int, bool> f)
        {
            // Define f(-1) == false and f(n) == true.
            // Invariant: f(i-1) == false, f(j) == true.
            var i = 0;
            var j = n;
            while (i < j)
            {
                var h = i + (j - i) / 2; // avoid overflow when computing h
                // i ≤ h < j
                if (!f(h))
                {
                    i = h + 1; // preserves f(i-1) == false
                }
                else
                {
                    j = h; // preserves f(j) == true
                }
            }
            // i == j, f(i-1) == false, and f(j) (= f(i)) == true  =>  answer is i.
            return i;
        }

        internal static ArraySegment<T> Slice<T>(this T[] array, int startIndex)
        {
            return new ArraySegment<T>(array, startIndex, array.Length - startIndex);
        }

        internal static ArraySegment<T> Slice<T>(this T[] array, int startIndex, int endIndex)
        {
            return new ArraySegment<T>(array, startIndex, endIndex - startIndex);
        }

        internal static bool IsWindows()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.WinCE:
                    return true;
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    return false;
            }
            throw new PlatformNotSupportedException();
        }

        internal static void Cut<T>(this IList<T> list, int count)
        {
            for (var i = list.Count; i > count; i--)
                list.RemoveAt(list.Count - 1);
        }

        private static Tuple<int, bool> unhex(byte b)
        {
            int c = b;
            if ('0' <= c && c <= '9')
                return Tuple.Create(c - '0', true);
            if ('a' <= c && c <= 'f')
                return Tuple.Create(c - 'a' + 10, true);
            if ('A' <= c && c <= 'F')
                return Tuple.Create(c - 'A' + 10, true);
            return Tuple.Create(0, false);
        }

        // UnquoteChar decodes the first character or byte in the escaped string
        // or character literal represented by the string s.
        // It returns four values:
        //
        //	1) value, the decoded Unicode code point or byte value;
        //	2) multibyte, a boolean indicating whether the decoded character requires a multibyte UTF-8 representation;
        //	3) tail, the remainder of the string after the character; and
        //	4) an error that will be nil if the character is syntactically valid.
        //
        // The second argument, quote, specifies the type of literal being parsed
        // and therefore which escaped quote character is permitted.
        // If set to a single quote, it permits the sequence \' and disallows unescaped '.
        // If set to a double quote, it permits \" and disallows unescaped ".
        // If set to zero, it does not permit either escape and allows both quote characters to appear unescaped.
        internal static Tuple<int, bool, string> UnquoteChar(string s_, byte quote)
        {
            var s = Encoding.UTF8.GetBytes(s_);
            var c = s[0];
            // easy cases
            if (c == quote && (quote == '\'' || quote == '"'))
                throw new ErrSyntax();
            if (c >= Utf8.RuneSelf)
            {
                var t = Utf8.DecodeRuneInString(s_);
                var r = t.Item1;
                var size = t.Item2;
                return Tuple.Create(r, true, Encoding.UTF8.GetString(s.Slice(size).ToArray()));
            }
            if (c != '\\')
                return Tuple.Create((int)s[0], false, Encoding.UTF8.GetString(s.Slice(1).ToArray()));

            // hard case: c is backslash
            if (s.Length <= 1)
            {
                throw new ErrSyntax();
            }
            c = s[1];
            s = s.Slice(2).ToArray();

            int value;
            var multibyte = false;
            switch ((char)c)
            {
                case 'a':
                    value = '\a';
                    break;
                case 'b':
                    value = '\b';
                    break;
                case 'f':
                    value = '\f';
                    break;
                case 'n':
                    value = '\n';
                    break;
                case 'r':
                    value = '\r';
                    break;
                case 't':
                    value = '\t';
                    break;
                case 'v':
                    value = '\v';
                    break;
                case 'x':
                case 'u':
                case 'U':
                    {
                        var n = 0;
                        switch ((char)c)
                        {
                            case 'x':
                                n = 2;
                                break;
                            case 'u':
                                n = 4;
                                break;
                            case 'U':
                                n = 8;
                                break;
                        }
                        var v = 0;
                        if (s.Length < n)
                        {
                            throw new ErrSyntax();
                        }
                        for (var j = 0; j < n; j++)
                        {
                            var t = unhex(s[j]);
                            var x = t.Item1;
                            var ok = t.Item2;
                            if (!ok)
                            {
                                throw new ErrSyntax();
                            }
                            v = v << 4 | x;
                        }
                        s = s.Slice(n).ToArray();
                        if (c == 'x')
                        {
                            // single-byte string, possibly not UTF-8
                            value = v;
                            break;
                        }
                        if (v > Utf8.MaxRune)
                        {
                            throw new ErrSyntax();
                        }
                        value = v;
                        multibyte = true;
                        break;
                    }
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                    {
                        var v = c - '0';
                        if (s.Length < 2)
                        {
                            throw new ErrSyntax();
                        }
                        for (var j = 0; j < 2; j++)
                        { // one digit already; two more
                            var x = s[j] - '0';
                            if (x < 0 || x > 7)
                            {
                                throw new ErrSyntax();
                            }
                            v = (v << 3) | x;
                        }
                        s = s.Slice(2).ToArray();
                        if (v > 255)
                        {
                            throw new ErrSyntax();
                        }
                        value = v;
                        break;
                    }
                case '\\':
                    value = '\\';
                    break;
                case '\'':
                case '"':
                    if (c != quote)
                    {
                        throw new ErrSyntax();
                    }
                    value = c;
                    break;
                default:
                    throw new ErrSyntax();
            }
            return Tuple.Create(value, multibyte, Encoding.UTF8.GetString(s));
        }

        // Unquote interprets s as a single-quoted, double-quoted,
        // or backquoted Go string literal, returning the string value
        // that s quotes.  (If s is single-quoted, it would be a Go
        // character literal; Unquote returns the corresponding
        // one-character string.)
        internal static string Unquote(string s_)
        {
            var s = Encoding.UTF8.GetBytes(s_);
            var n = s.Length;
            if (n < 2)
            {
                throw new ErrSyntax();
            }
            var quote = s[0];
            if (quote != s[n - 1])
            {
                throw new ErrSyntax();
            }
            s = s.Slice(1, n - 1).ToArray();

            if (quote == '`')
            {
                if (s.Contains((byte)'`'))
                {
                    throw new ErrSyntax();
                }
                return Encoding.UTF8.GetString(s);
            }
            if (quote != '"' && quote != '\'')
            {
                throw new ErrSyntax();
            }
            if (s.Contains((byte)'\n'))
            {
                throw new ErrSyntax();
            }

            // Is it trivial?  Avoid allocation.
            if (!s.Contains((byte)'\\') && !s.Contains(quote))
            {
                switch ((char)quote)
                {
                    case '"':
                        return Encoding.UTF8.GetString(s);
                    case '\'':
                        s_ = Encoding.UTF8.GetString(s);
                        var t = Utf8.DecodeRuneInString(s_);
                        var r = t.Item1;
                        var size = t.Item2;
                        if (size == s.Length && (r != Utf8.RuneError || size != 1))
                        {
                            return s_;
                        }
                        break;
                }
            }

            var runeTmp = new byte[Utf8.UTFMax];
            var buf = new List<byte>(3 * s.Length / 2); // Try to avoid more allocations.
            while (s.Length > 0)
            {
                var t = UnquoteChar(Encoding.UTF8.GetString(s), quote);
                var c = t.Item1;
                var multibyte = t.Item2;
                var ss = t.Item3;
                s = Encoding.UTF8.GetBytes(ss);
                if (c < Utf8.RuneSelf || !multibyte)
                {
                    buf.Add((byte)c);
                }
                else
                {
                    var n2 = Utf8.EncodeRune(runeTmp, c);
                    buf.AddRange(runeTmp.Slice(0, n2));
                }
                if (quote == '\'' && s.Length != 0)
                {
                    // single-quoted must be single character
                    throw new ErrSyntax();
                }
            }
            return Encoding.UTF8.GetString(buf.ToArray());
        }

        internal static bool IsGraphic(char c)
        {
            // これでいいの？
            // http://en.wikipedia.org/wiki/Graphic_character#Unicode
            switch (char.GetUnicodeCategory(c))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherPunctuation:
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.CurrencySymbol:
                case UnicodeCategory.ModifierSymbol:
                case UnicodeCategory.OtherSymbol:
                case UnicodeCategory.SpaceSeparator:
                    return true;
            }
            return false;
        }

        internal static bool IsSpace(char c)
        {
            // http://en.wikipedia.org/wiki/Whitespace_character
            switch (c)
            {
                case '\u0009':
                case '\u000A':
                case '\u000B':
                case '\u000C':
                case '\u000D':
                case '\u0020':
                case '\u0085':
                case '\u00A0':
                case '\u1680':
                case '\u2000':
                case '\u2001':
                case '\u2002':
                case '\u2003':
                case '\u2004':
                case '\u2005':
                case '\u2006':
                case '\u2007':
                case '\u2008':
                case '\u2009':
                case '\u200A':
                case '\u2028':
                case '\u2029':
                case '\u202F':
                case '\u205F':
                case '\u3000':
                    return true;
            }
            return false;
        }
    }

    internal class ErrSyntax : Exception
    {
        public ErrSyntax() : base("invalid syntax") { }
    }
}
