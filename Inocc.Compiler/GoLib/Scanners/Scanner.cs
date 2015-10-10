using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inocc.Compiler.GoLib.Tokens;

#pragma warning disable RECS0093 // Convert 'if' to '&&' expression
#pragma warning disable RECS0033 // Convert 'if' to '||' expression

namespace Inocc.Compiler.GoLib.Scanners
{
    using Pos = Int32;

    // An ErrorHandler may be provided to Scanner.Init. If a syntax error is
    // encountered and a handler was installed, the handler is called with a
    // position and an error message. The position points to the beginning of
    // the offending token.
    //
    public delegate void ErrorHandler(Position pos, string msg);

    // A Scanner holds the scanner's internal state while processing
    // a given text.  It can be allocated as part of another data
    // structure but must be initialized via Init before use.
    //
    public class Scanner
    {
        // immutable state
        File file; // source file handle
        string dir; // directory portion of file.Name()
        byte[] src; // source
        ErrorHandler err; // error reporting; or nil
        Mode mode; // scanning mode

        // scanning state
        int ch; // current character
        int offset; // character offset
        int rdOffset; // reading offset (position after current character)
        int lineOffset; // current line offset
        bool insertSemi; // insert a semicolon before next newline

        // public state - ok to modify
        int ErrorCount; // number of errors encountered

        private const int bom = 0xFEFF; // byte order mark, only permitted as very first character

        // Read the next Unicode char into s.ch.
        // s.ch < 0 means end-of-file.
        //
        private void next()
        {
            if (this.rdOffset < this.src.Length)
            {
                this.offset = this.rdOffset;
                if (this.ch == '\n')
                {
                    this.lineOffset = this.offset;
                    this.file.AddLine(this.offset);
                }
                var r = (int)this.src[this.rdOffset];
                var w = 1;
                if (r == 0)
                    this.error(this.offset, "illegal character NUL");
                else if (r >= 0x80)
                {
                    // not ASCII
                    var t = Utf8.DecodeRune(this.src.Slice(this.rdOffset));
                    r = t.Item1;
                    w = t.Item2;
                    if (r == Utf8.RuneError && w == 1)
                    {
                        this.error(this.offset, "illegal UTF-8 encoding");
                    }
                    else if (r == bom && this.offset > 0)
                    {
                        this.error(this.offset, "illegal byte order mark");
                    }
                }
                this.rdOffset += w;
                this.ch = r;
            }
            else
            {
                this.offset = this.src.Length;
                if (this.ch == '\n')
                {
                    this.lineOffset = this.offset;
                    this.file.AddLine(this.offset);
                }
                this.ch = -1; // eof
            }
        }

        [Flags]
        public enum Mode : uint
        {
            ScanComments = 1, // return comments as COMMENT tokens
            dontInsertSemis // do not automatically insert semicolons - for testing only
        }

        // Init prepares the scanner s to tokenize the text src by setting the
        // scanner at the beginning of src. The scanner uses the file set file
        // for position information and it adds line information for each line.
        // It is ok to re-use the same file when re-scanning the same file as
        // line information which is already present is ignored. Init causes a
        // panic if the file size does not match the src size.
        //
        // Calls to Scan will invoke the error handler err if they encounter a
        // syntax error and err is not nil. Also, for each error encountered,
        // the Scanner field ErrorCount is incremented by one. The mode parameter
        // determines how comments are handled.
        //
        // Note that Init may call err if there is an error in the first character
        // of the file.
        //
        public void Init(File file, byte[] src, ErrorHandler err, Mode mode)
        {
            // Explicitly initialize all fields since a scanner may be reused.
            if (file.Size != src.Length)
            {
                throw new InvalidOperationException(string.Join("file size ({0}) does not match src len ({1})", file.Size, src.Length));
            }
            this.file = file;
            this.dir = System.IO.Path.GetDirectoryName(file.Name);
            this.src = src;
            this.err = err;
            this.mode = mode;

            this.ch = ' ';
            this.offset = 0;
            this.rdOffset = 0;
            this.lineOffset = 0;
            this.insertSemi = false;
            this.ErrorCount = 0;

            this.next();
            if (this.ch == bom)
            {
                this.next(); // ignore BOM at file beginning
            }
        }

        private void error(int offs, string msg)
        {
            if (this.err != null)
            {
                this.err(this.file.Position(this.file.Pos(offs)), msg);
            }
            this.ErrorCount++;
        }

        private const string prefix = "//line ";

        private void interpretLineComment(byte[] text)
        {
            if (text.Take(prefix.Length).SequenceEqual(prefix.Select(_ => (byte)_)))
            {
                // get filename and line number, if any
                var i = Array.LastIndexOf(text, (byte)':');
                if (i > 0)
                {
                    int line;
                    if (int.TryParse(Encoding.UTF8.GetString(text.Slice(i + 1).ToArray()), out line) && line > 0)
                    {
                        // valid //line filename:line comment
                        var filename = string.Concat(text.Slice(prefix.Length, i)).Trim();
                        if (!string.IsNullOrEmpty(filename))
                        {
                            filename = Filepath.Clean(filename);
                            if (!Filepath.IsAbs(filename))
                            {
                                // make filename relative to current directory
                                filename = System.IO.Path.Combine(this.dir, filename);
                            }
                        }
                        // update scanner position
                        this.file.AddLineInfo(this.lineOffset + text.Length + 1, filename, line); // +len(text)+1 since comment applies to next line
                    }
                }
            }
        }

        private string scanComment()
        {
            // initial '/' already consumed; s.ch == '/' || s.ch == '*'
            var offs = this.offset - 1; // position of initial '/'
            var hasCR = false;

            if (this.ch == '/')
            {
                //-style comment
                this.next();
                while (this.ch != '\n' && this.ch >= 0)
                {
                    if (this.ch == '\r')
                    {
                        hasCR = true;
                    }
                    this.next();
                }
                if (offs == this.lineOffset)
                {
                    // comment starts at the beginning of the current line
                    this.interpretLineComment(this.src.Slice(offs, this.offset).ToArray());
                }
                goto exit;
            }

            /*-style comment */
            this.next();
            while (this.ch >= 0)
            {
                var ch = this.ch;
                if (ch == '\r')
                {
                    hasCR = true;
                }
                this.next();
                if (ch == '*' && this.ch == '/')
                {
                    this.next();
                    goto exit;
                }
            }

            this.error(offs, "comment not terminated");

        exit:
            var lit = this.src.Slice(offs, this.offset).ToArray();
            if (hasCR)
            {
                lit = stripCR(lit);
            }

            return Encoding.UTF8.GetString(lit);
        }

        private bool findLineEnd()
        {
            // initial '/' already consumed

            try
            {

                // read ahead until a newline, EOF, or non-comment token is found
                while (this.ch == '/' || this.ch == '*')
                {
                    if (this.ch == '/')
                    {
                        //-style comment always contains a newline
                        return true;
                    }
                    /*-style comment: look for newline */
                    this.next();
                    while (this.ch >= 0)
                    {
                        var ch = this.ch;
                        if (ch == '\n')
                        {
                            return true;
                        }
                        this.next();
                        if (ch == '*' && this.ch == '/')
                        {
                            this.next();
                            break;
                        }
                    }
                    this.skipWhitespace(); // this.insertSemi is set
                    if (this.ch < 0 || this.ch == '\n')
                    {
                        return true;
                    }
                    if (this.ch != '/')
                    {
                        // non-comment token
                        return false;
                    }
                    this.next(); // consume '/'
                }

                return false;
            }
            finally
            {
                var offs = this.offset - 1;
                // reset scanner state to where it was upon calling findLineEnd
                this.ch = '/';
                this.offset = offs;
                this.rdOffset = offs + 1;
                this.next(); // consume initial '/' again
            }
        }

        private static bool isLetter(int ch)
        {
            if (ch == -1) return false;
            return ch == '_' || char.IsLetter(char.ConvertFromUtf32(ch), 0);
        }

        private static bool isDigit(int ch)
        {
            if (ch == -1) return false;
            return char.IsDigit(char.ConvertFromUtf32(ch), 0);
        }

        private string scanIdentifier()
        {
            var offs = this.offset;
            while (isLetter(this.ch) || isDigit(this.ch))
            {
                this.next();
            }
            return Encoding.UTF8.GetString(this.src.Slice(offs, this.offset).ToArray());
        }

        private static int digitVal(int ch)
        {
            if ('0' <= ch && ch <= '9')
                return ch - '0';
            if ('a' <= ch && ch <= 'f')
                return ch - 'a' + 10;
            if ('A' <= ch && ch <= 'F')
                return ch - 'A' + 10;
            return 16; // larger than any legal digit val
        }

        private void scanMantissa(int @base)
        {
            while (digitVal(this.ch) < @base)
            {
                this.next();
            }
        }

        private Tuple<Token, string> scanNumber(bool seenDecimalPoint)
        {
            // digitVal(this.ch) < 10
            var offs = this.offset;
            var tok = Token.INT;

            if (seenDecimalPoint)
            {
                offs--;
                tok = Token.FLOAT;
                this.scanMantissa(10);
                goto exponent;
            }

            if (this.ch == '0')
            {
                // int or float
                var offs2 = this.offset;
                this.next();
                if (this.ch == 'x' || this.ch == 'X')
                {
                    // hexadecimal int
                    this.next();
                    this.scanMantissa(16);
                    if (this.offset - offs2 <= 2)
                    {
                        // only scanned "0x" or "0X"
                        this.error(offs2, "illegal hexadecimal number");
                    }
                }
                else
                {
                    // octal int or float
                    var seenDecimalDigit = false;
                    this.scanMantissa(8);
                    if (this.ch == '8' || this.ch == '9')
                    {
                        // illegal octal int or float
                        seenDecimalDigit = true;
                        this.scanMantissa(10);
                    }
                    if (this.ch == '.' || this.ch == 'e' || this.ch == 'E' || this.ch == 'i')
                    {
                        goto fraction;
                    }
                    // octal int
                    if (seenDecimalDigit)
                    {
                        this.error(offs2, "illegal octal number");
                    }
                }
                goto exit;
            }

            // decimal int or float
            this.scanMantissa(10);

        fraction:
            if (this.ch == '.')
            {
                tok = Token.FLOAT;
                this.next();
                this.scanMantissa(10);
            }

        exponent:
            if (this.ch == 'e' || this.ch == 'E')
            {
                tok = Token.FLOAT;
                this.next();
                if (this.ch == '-' || this.ch == '+')
                {
                    this.next();
                }
                this.scanMantissa(10);
            }

            if (this.ch == 'i')
            {
                tok = Token.IMAG;
                this.next();
            }

        exit:
            return Tuple.Create(tok, Encoding.UTF8.GetString(this.src.Slice(offs, this.offset).ToArray()));
        }

        // scanEscape parses an escape sequence where rune is the accepted
        // escaped quote. In case of a syntax error, it stops at the offending
        // character (without consuming it) and returns false. Otherwise
        // it returns true.
        private bool scanEscape(char quote)
        {
            var offs = this.offset;

            int n;
            uint @base;
            uint max;
            if (new int[] { 'a', 'b', 'f', 'n', 'r', 't', 'v', '\\', quote }.Contains(this.ch))
            {
                this.next();
                return true;
            }
            switch (this.ch)
            {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                    n = 3;
                    @base = 8;
                    max = 255;
                    break;
                case 'x':
                    this.next();
                    n = 2;
                    @base = 16;
                    max = 255;
                    break;
                case 'u':
                    this.next();
                    n = 4;
                    @base = 16;
                    max = Utf8.MaxRune;
                    break;
                case 'U':
                    this.next();
                    n = 8;
                    @base = 16;
                    max = Utf8.MaxRune;
                    break;
                default:
                    var msg = "unknown escape sequence";
                    if (this.ch < 0)
                    {
                        msg = "escape sequence not terminated";
                    }
                    this.error(offs, msg);
                    return false;
            }

            uint x = 0;
            while (n > 0)
            {
                var d = (uint)digitVal(this.ch);
                if (d >= @base)
                {
                    var msg = string.Format("illegal character U+{0:X4} in escape sequence", this.ch);
                    if (this.ch < 0)
                    {
                        msg = "escape sequence not terminated";
                    }
                    this.error(this.offset, msg);
                    return false;
                }
                x = x * @base + d;
                this.next();
                n--;
            }

            if (x > max || 0xD800 <= x && x < 0xE000)
            {
                this.error(offs, "escape sequence is invalid Unicode code point");
                return false;
            }

            return true;
        }

        private string scanRune()
        {
            // '\'' opening already consumed
            var offs = this.offset - 1;

            var valid = true;
            var n = 0;
            while (true)
            {
                var ch = this.ch;
                if (ch == '\n' || ch < 0)
                {
                    // only report error if we don't have one already
                    if (valid)
                    {
                        this.error(offs, "rune literal not terminated");
                        valid = false;
                    }
                    break;
                }
                this.next();
                if (ch == '\'')
                {
                    break;
                }
                n++;
                if (ch == '\\')
                {
                    if (!this.scanEscape('\''))
                    {
                        valid = false;
                    }
                    // continue to read to closing quote
                }
            }

            if (valid && n != 1)
            {
                this.error(offs, "illegal rune literal");
            }

            return Encoding.UTF8.GetString(this.src.Slice(offs, this.offset).ToArray());
        }

        private string scanString()
        {
            // '"' opening already consumed
            var offs = this.offset - 1;

            while (true)
            {
                var ch = this.ch;
                if (ch == '\n' || ch < 0)
                {
                    this.error(offs, "string literal not terminated");
                    break;
                }
                this.next();
                if (ch == '"')
                {
                    break;
                }
                if (ch == '\\')
                {
                    this.scanEscape('"');
                }
            }

            return Encoding.UTF8.GetString(this.src.Slice(offs, this.offset).ToArray());
        }

        private static byte[] stripCR(byte[] b)
        {
            var c = new List<byte>(b.Length);
            foreach (var ch in b)
            {
                if (ch != '\r')
                {
                    c.Add(ch);
                }
            }
            return c.ToArray();
        }

        private string scanRawString()
        {
            // '`' opening already consumed
            var offs = this.offset - 1;

            var hasCR = false;
            while (true)
            {
                var ch = this.ch;
                if (ch < 0)
                {
                    this.error(offs, "raw string literal not terminated");
                    break;
                }
                this.next();
                if (ch == '`')
                {
                    break;
                }
                if (ch == '\r')
                {
                    hasCR = true;
                }
            }

            var lit = this.src.Slice(offs, this.offset).ToArray();
            if (hasCR)
            {
                lit = stripCR(lit);
            }

            return Encoding.UTF8.GetString(lit);
        }

        private void skipWhitespace()
        {
            while (this.ch == ' ' || this.ch == '\t' || this.ch == '\n' && !this.insertSemi || this.ch == '\r')
            {
                this.next();
            }
        }

        // Helper functions for scanning multi-byte tokens such as >> += >>= .
        // Different routines recognize different length tok_i based on matches
        // of ch_i. If a token ends in '=', the result is tok1 or tok3
        // respectively. Otherwise, the result is tok0 if there was no other
        // matching character, or tok2 if the matching character was ch2.

        private Token switch2(Token tok0, Token tok1)
        {
            if (this.ch == '=')
            {
                this.next();
                return tok1;
            }
            return tok0;
        }

        private Token switch3(Token tok0, Token tok1, char ch2, Token tok2)
        {
            if (this.ch == '=')
            {
                this.next();
                return tok1;
            }
            if (this.ch == ch2)
            {
                this.next();
                return tok2;
            }
            return tok0;
        }

        private Token switch4(Token tok0, Token tok1, char ch2, Token tok2, Token tok3)
        {
            if (this.ch == '=')
            {
                this.next();
                return tok1;
            }
            if (this.ch == ch2)
            {
                this.next();
                if (this.ch == '=')
                {
                    this.next();
                    return tok3;
                }
                return tok2;
            }
            return tok0;
        }

        // Scan scans the next token and returns the token position, the token,
        // and its literal string if applicable. The source end is indicated by
        // token.EOF.
        //
        // If the returned token is a literal (token.IDENT, token.INT, token.FLOAT,
        // token.IMAG, token.CHAR, token.STRING) or token.COMMENT, the literal string
        // has the corresponding value.
        //
        // If the returned token is a keyword, the literal string is the keyword.
        //
        // If the returned token is token.SEMICOLON, the corresponding
        // literal string is ";" if the semicolon was present in the source,
        // and "\n" if the semicolon was inserted because of a newline or
        // at EOF.
        //
        // If the returned token is token.ILLEGAL, the literal string is the
        // offending character.
        //
        // In all other cases, Scan returns an empty literal string.
        //
        // For more tolerant parsing, Scan will return a valid token if
        // possible even if a syntax error was encountered. Thus, even
        // if the resulting token sequence contains no illegal tokens,
        // a client may not assume that no error occurred. Instead it
        // must check the scanner's ErrorCount or the number of calls
        // of the error handler, if there was one installed.
        //
        // Scan adds line information to the file added to the file
        // set with Init. Token positions are relative to that file
        // and thus relative to the file set.
        //
        public Tuple<Pos, Token, string> Scan()
        {
            Pos pos;
            var tok = default(Token);
            string lit = null;

        scanAgain:
            this.skipWhitespace();

            // current token start
            pos = this.file.Pos(this.offset);

            // determine token value
            var insertSemi = false;
            var ch = this.ch;
            if (isLetter(ch))
            {
                lit = this.scanIdentifier();
                if (lit.Length > 1)
                {
                    // keywords are longer than one letter - avoid lookup otherwise
                    tok = TokenPackage.Lookup(lit);
                    switch (tok)
                    {
                        case Token.IDENT:
                        case Token.BREAK:
                        case Token.CONTINUE:
                        case Token.FALLTHROUGH:
                        case Token.RETURN:
                            insertSemi = true;
                            break;
                    }
                }
                else
                {
                    insertSemi = true;
                    tok = Token.IDENT;
                }
            }
            else if ('0' <= ch && ch <= '9')
            {
                insertSemi = true;
                var t = this.scanNumber(false);
                tok = t.Item1;
                lit = t.Item2;
            }
            else
            {
                this.next(); // always make progress
                switch (ch)
                {
                    case -1:
                        if (this.insertSemi)
                        {
                            this.insertSemi = false; // EOF consumed
                            return Tuple.Create(pos, Token.SEMICOLON, "\n");
                        }
                        tok = Token.EOF;
                        break;
                    case '\n':
                        // we only reach here if this.insertSemi was
                        // set in the first place and exited early
                        // from this.skipWhitespace()
                        this.insertSemi = false; // newline consumed
                        return Tuple.Create(pos, Token.SEMICOLON, "\n");
                    case '"':
                        insertSemi = true;
                        tok = Token.STRING;
                        lit = this.scanString();
                        break;
                    case '\'':
                        insertSemi = true;
                        tok = Token.CHAR;
                        lit = this.scanRune();
                        break;
                    case '`':
                        insertSemi = true;
                        tok = Token.STRING;
                        lit = this.scanRawString();
                        break;
                    case ':':
                        tok = this.switch2(Token.COLON, Token.DEFINE);
                        break;
                    case '.':
                        if ('0' <= this.ch && this.ch <= '9')
                        {
                            insertSemi = true;
                            var t = this.scanNumber(true);
                            tok = t.Item1;
                            lit = t.Item2;
                        }
                        else if (this.ch == '.')
                        {
                            this.next();
                            if (this.ch == '.')
                            {
                                this.next();
                                tok = Token.ELLIPSIS;
                            }
                        }
                        else
                        {
                            tok = Token.PERIOD;
                        }
                        break;
                    case ',':
                        tok = Token.COMMA;
                        break;
                    case ';':
                        tok = Token.SEMICOLON;
                        lit = ";";
                        break;
                    case '(':
                        tok = Token.LPAREN;
                        break;
                    case ')':
                        insertSemi = true;
                        tok = Token.RPAREN;
                        break;
                    case '[':
                        tok = Token.LBRACK;
                        break;
                    case ']':
                        insertSemi = true;
                        tok = Token.RBRACK;
                        break;
                    case '{':
                        tok = Token.LBRACE;
                        break;
                    case '}':
                        insertSemi = true;
                        tok = Token.RBRACE;
                        break;
                    case '+':
                        tok = this.switch3(Token.ADD, Token.ADD_ASSIGN, '+', Token.INC);
                        if (tok == Token.INC)
                        {
                            insertSemi = true;
                        }
                        break;
                    case '-':
                        tok = this.switch3(Token.SUB, Token.SUB_ASSIGN, '-', Token.DEC);
                        if (tok == Token.DEC)
                        {
                            insertSemi = true;
                        }
                        break;
                    case '*':
                        tok = this.switch2(Token.MUL, Token.MUL_ASSIGN);
                        break;
                    case '/':
                        if (this.ch == '/' || this.ch == '*')
                        {
                            // comment
                            if (this.insertSemi && this.findLineEnd())
                            {
                                // reset position to the beginning of the comment
                                this.ch = '/';
                                this.offset = this.file.Offset(pos);
                                this.rdOffset = this.offset + 1;
                                this.insertSemi = false; // newline consumed
                                return Tuple.Create(pos, Token.SEMICOLON, "\n");
                            }
                            var comment = this.scanComment();
                            if ((this.mode & Mode.ScanComments) == 0)
                            {
                                // skip comment
                                this.insertSemi = false; // newline consumed
                                goto scanAgain;
                            }
                            tok = Token.COMMENT;
                            lit = comment;
                        }
                        else
                        {
                            tok = this.switch2(Token.QUO, Token.QUO_ASSIGN);
                        }
                        break;
                    case '%':
                        tok = this.switch2(Token.REM, Token.REM_ASSIGN);
                        break;
                    case '^':
                        tok = this.switch2(Token.XOR, Token.XOR_ASSIGN);
                        break;
                    case '<':
                        if (this.ch == '-')
                        {
                            this.next();
                            tok = Token.ARROW;
                        }
                        else
                        {
                            tok = this.switch4(Token.LSS, Token.LEQ, '<', Token.SHL, Token.SHL_ASSIGN);
                        }
                        break;
                    case '>':
                        tok = this.switch4(Token.GTR, Token.GEQ, '>', Token.SHR, Token.SHR_ASSIGN);
                        break;
                    case '=':
                        tok = this.switch2(Token.ASSIGN, Token.EQL);
                        break;
                    case '!':
                        tok = this.switch2(Token.NOT, Token.NEQ);
                        break;
                    case '&':
                        if (this.ch == '^')
                        {
                            this.next();
                            tok = this.switch2(Token.AND_NOT, Token.AND_NOT_ASSIGN);
                        }
                        else
                        {
                            tok = this.switch3(Token.AND, Token.AND_ASSIGN, '&', Token.LAND);
                        }
                        break;
                    case '|':
                        tok = this.switch3(Token.OR, Token.OR_ASSIGN, '|', Token.LOR);
                        break;
                    default:
                        // next reports unexpected BOMs - don't repeat
                        if (ch != bom)
                        {
                            this.error(this.file.Offset(pos), string.Format("illegal character U+{0:X4}", ch));
                        }
                        insertSemi = this.insertSemi; // preserve insertSemi info
                        tok = Token.ILLEGAL;
                        lit = char.ConvertFromUtf32(ch);
                        break;
                }
            }
            if ((this.mode & Mode.dontInsertSemis) == 0)
            {
                this.insertSemi = insertSemi;
            }

            return Tuple.Create(pos, tok, lit);
        }
    }
}
