using System;
using System.Collections.Generic;
using System.Text;

namespace Inocc.Compiler.GoLib
{
    internal static class Utf8
    {
        public const int RuneError = '\uFFFD';
        public const int RuneSelf = 0x80;
        public const int MaxRune = 0x0010FFFF;
        public const int UTFMax = 4;

        // Code points in the surrogate range are not valid for UTF-8.
        private const int surrogateMin = 0xD800;
        private const int surrogateMax = 0xDFFF;

        private const byte t1 = 0x00; // 0000 0000
        private const byte tx = 0x80; // 1000 0000
        private const byte t2 = 0xC0; // 1100 0000
        private const byte t3 = 0xE0; // 1110 0000
        private const byte t4 = 0xF0; // 1111 0000
        private const byte t5 = 0xF8; // 1111 1000

        private const byte maskx = 0x3F; // 0011 1111
        private const byte mask2 = 0x1F; // 0001 1111
        private const byte mask3 = 0x0F; // 0000 1111
        private const byte mask4 = 0x07; // 0000 0111

        private const int rune1Max = 1 << 7 - 1;
        private const int rune2Max = 1 << 11 - 1;
        private const int rune3Max = 1 << 16 - 1;

        private static Tuple<int, int, bool> DecodeRuneInternal(IReadOnlyList<byte> p)
        {
            int r;
            var n = p.Count;
            if (n < 1)
            {
                return Tuple.Create(RuneError, 0, true);
            }
            var c0 = p[0];

            // 1-byte, 7-bit sequence?
            if (c0 < tx)
            {
                return Tuple.Create((int)c0, 1, false);
            }

            // unexpected continuation byte?
            if (c0 < t2)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // need first continuation byte
            if (n < 2)
            {
                return Tuple.Create(RuneError, 1, true);
            }
            var c1 = p[1];
            if (c1 < tx || t2 <= c1)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // 2-byte, 11-bit sequence?
            if (c0 < t3)
            {
                r = (c0 & mask2) << 6 | (c1 & maskx);
                if (r <= rune1Max)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                return Tuple.Create(r, 2, false);
            }

            // need second continuation byte
            if (n < 3)
            {
                return Tuple.Create(RuneError, 1, true);
            }
            var c2 = p[2];
            if (c2 < tx || t2 <= c2)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // 3-byte, 16-bit sequence?
            if (c0 < t4)
            {
                r = (c0 & mask3) << 12 | (c1 & maskx) << 6 | (c2 & maskx);
                if (r <= rune2Max)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                if (surrogateMin <= r && r <= surrogateMax)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                return Tuple.Create(r, 3, false);
            }

            // need third continuation byte
            if (n < 4)
            {
                return Tuple.Create(RuneError, 1, true);
            }
            var c3 = p[3];
            if (c3 < tx || t2 <= c3)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // 4-byte, 21-bit sequence?
            if (c0 < t5)
            {
                r = (c0 & mask4) << 18 | (c1 & maskx) << 12 | (c2 & maskx) << 6 | (c3 & maskx);
                if (r <= rune3Max || MaxRune < r)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                return Tuple.Create(r, 4, false);
            }

            // error
            return Tuple.Create(RuneError, 1, false);
        }

        // DecodeRune unpacks the first UTF-8 encoding in p and returns the rune and its width in bytes.
        // If the encoding is invalid, it returns (RuneError, 1), an impossible result for correct UTF-8.
        // An encoding is invalid if it is incorrect UTF-8, encodes a rune that is
        // out of range, or is not the shortest possible UTF-8 encoding for the
        // value. No other validation is performed.
        public static Tuple<int, int> DecodeRune(IReadOnlyList<byte> p)
        {
            var t = DecodeRuneInternal(p);
            return Tuple.Create(t.Item1, t.Item2);
        }

        private static Tuple<int, int, bool> decodeRuneInStringInternal(string s_)
        {
            var s = Encoding.UTF8.GetBytes(s_);
            int r;

            var n = s.Length;
            if (n < 1)
            {
                return Tuple.Create(RuneError, 0, true);
            }
            var c0 = s[0];

            // 1-byte, 7-bit sequence?
            if (c0 < tx)
            {
                return Tuple.Create((int)c0, 1, false);
            }

            // unexpected continuation byte?
            if (c0 < t2)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // need first continuation byte
            if (n < 2)
            {
                return Tuple.Create(RuneError, 1, true);
            }
            var c1 = s[1];
            if (c1 < tx || t2 <= c1)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // 2-byte, 11-bit sequence?
            if (c0 < t3)
            {
                r = (c0 & mask2) << 6 | (c1 & maskx);
                if (r <= rune1Max)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                return Tuple.Create(r, 2, false);
            }

            // need second continuation byte
            if (n < 3)
            {
                return Tuple.Create(RuneError, 1, true);
            }
            var c2 = s[2];
            if (c2 < tx || t2 <= c2)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // 3-byte, 16-bit sequence?
            if (c0 < t4)
            {
                r = (c0 & mask3) << 12 | (c1 & maskx) << 6 | (c2 & maskx);
                if (r <= rune2Max)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                if (surrogateMin <= r && r <= surrogateMax)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                return Tuple.Create(r, 3, false);
            }

            // need third continuation byte
            if (n < 4)
            {
                return Tuple.Create(RuneError, 1, true);
            }
            var c3 = s[3];
            if (c3 < tx || t2 <= c3)
            {
                return Tuple.Create(RuneError, 1, false);
            }

            // 4-byte, 21-bit sequence?
            if (c0 < t5)
            {
                r = (c0 & mask4) << 18 | (c1 & maskx) << 12 | (c2 & maskx) << 6 | (c3 & maskx);
                if (r <= rune3Max || MaxRune < r)
                {
                    return Tuple.Create(RuneError, 1, false);
                }
                return Tuple.Create(r, 4, false);
            }

            // error
            return Tuple.Create(RuneError, 1, false);
        }

        // DecodeRuneInString is like DecodeRune but its input is a string.
        // If the encoding is invalid, it returns (RuneError, 1), an impossible result for correct UTF-8.
        // An encoding is invalid if it is incorrect UTF-8, encodes a rune that is
        // out of range, or is not the shortest possible UTF-8 encoding for the
        // value. No other validation is performed.
        public static Tuple<int, int> DecodeRuneInString(string s)
        {
            var t = decodeRuneInStringInternal(s);
            return Tuple.Create(t.Item1, t.Item2);
        }

        // EncodeRune writes into p (which must be large enough) the UTF-8 encoding of the rune.
        // It returns the number of bytes written.
        public static int EncodeRune(byte[] p, int r)
        {
            // Negative values are erroneous.  Making it unsigned addresses the problem.
            var i = (uint)r;
            if (i <= rune1Max)
            {
                p[0] = (byte)r;
                return 1;
            }
            if (i <= rune2Max)
            {
                p[0] = (byte)(t2 | (r >> 6));
                p[1] = (byte)(tx | (r & maskx));
                return 2;
            }
            if (i > MaxRune || (surrogateMin <= i && i <= surrogateMax))
            {
                r = RuneError;
                // fallthrough
            }
            if (i <= rune3Max)
            {
                p[0] = (byte)(t3 | (r >> 12));
                p[1] = (byte)(tx | ((r >> 6) & maskx));
                p[2] = (byte)(tx | (r & maskx));
                return 3;
            }

            p[0] = (byte)(t4 | (r >> 18));
            p[1] = (byte)(tx | ((r >> 12) & maskx));
            p[2] = (byte)(tx | ((r >> 6) & maskx));
            p[3] = (byte)(tx | (r & maskx));
            return 4;
        }
    }
}
