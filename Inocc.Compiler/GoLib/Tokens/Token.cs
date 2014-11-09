using System.Collections.Generic;

namespace Inocc.Compiler.GoLib.Tokens
{
    public enum Token
    {
        // Special tokens
        ILLEGAL,
        EOF,
        COMMENT,

        literal_beg,
        // Identifiers and basic type literals
        // (these tokens stand for classes of literals)
        IDENT,  // main
        INT,    // 12345
        FLOAT,  // 123.45
        IMAG,   // 123.45i
        CHAR,   // 'a'
        STRING, // "abc"
        literal_end,

        operator_beg,
        // Operators and delimiters
        ADD, // +
        SUB, // -
        MUL, // *
        QUO, // /
        REM, // %

        AND,     // &
        OR,      // |
        XOR,     // ^
        SHL,     // <<
        SHR,     // >>
        AND_NOT, // &^

        ADD_ASSIGN, // +=
        SUB_ASSIGN, // -=
        MUL_ASSIGN, // *=
        QUO_ASSIGN, // /=
        REM_ASSIGN, // %=

        AND_ASSIGN,     // &=
        OR_ASSIGN,      // |=
        XOR_ASSIGN,     // ^=
        SHL_ASSIGN,     // <<=
        SHR_ASSIGN,     // >>=
        AND_NOT_ASSIGN, // &^=

        LAND,  // &&
        LOR,   // ||
        ARROW, // <-
        INC,   // ++
        DEC,   // --

        EQL,    // ==
        LSS,    // <
        GTR,    // >
        ASSIGN, // =
        NOT,    // !

        NEQ,      // !=
        LEQ,      // <=
        GEQ,      // >=
        DEFINE,   // :=
        ELLIPSIS, // ...

        LPAREN, // (
        LBRACK, // [
        LBRACE, // {
        COMMA,  // ,
        PERIOD, // .

        RPAREN,    // )
        RBRACK,    // ]
        RBRACE,    // }
        SEMICOLON, // ;
        COLON,     // :
        operator_end,

        keyword_beg,
        // Keywords
        BREAK,
        CASE,
        CHAN,
        CONST,
        CONTINUE,

        DEFAULT,
        DEFER,
        ELSE,
        FALLTHROUGH,
        FOR,

        FUNC,
        GO,
        GOTO,
        IF,
        IMPORT,

        INTERFACE,
        MAP,
        PACKAGE,
        RANGE,
        RETURN,

        SELECT,
        STRUCT,
        SWITCH,
        TYPE,
        VAR,
        keyword_end
    }

    public static class TokenPackage
    {
        private static string[] tokens;
        private static Dictionary<string, Token> keywords;

        static TokenPackage()
        {
            tokens = new string[((int)Token.keyword_end) + 1];
            tokens[(int)Token.ILLEGAL] = "ILLEGAL";

            tokens[(int)Token.EOF] = "EOF";
            tokens[(int)Token.COMMENT] = "COMMENT";

            tokens[(int)Token.IDENT] = "IDENT";
            tokens[(int)Token.INT] = "INT";
            tokens[(int)Token.FLOAT] = "FLOAT";
            tokens[(int)Token.IMAG] = "IMAG";
            tokens[(int)Token.CHAR] = "CHAR";
            tokens[(int)Token.STRING] = "STRING";

            tokens[(int)Token.ADD] = "+";
            tokens[(int)Token.SUB] = "-";
            tokens[(int)Token.MUL] = "*";
            tokens[(int)Token.QUO] = "/";
            tokens[(int)Token.REM] = "%";

            tokens[(int)Token.AND] = "&";
            tokens[(int)Token.OR] = "|";
            tokens[(int)Token.XOR] = "^";
            tokens[(int)Token.SHL] = "<<";
            tokens[(int)Token.SHR] = ">>";
            tokens[(int)Token.AND_NOT] = "&^";

            tokens[(int)Token.ADD_ASSIGN] = "+=";
            tokens[(int)Token.SUB_ASSIGN] = "-=";
            tokens[(int)Token.MUL_ASSIGN] = "*=";
            tokens[(int)Token.QUO_ASSIGN] = "/=";
            tokens[(int)Token.REM_ASSIGN] = "%=";

            tokens[(int)Token.AND_ASSIGN] = "&=";
            tokens[(int)Token.OR_ASSIGN] = "|=";
            tokens[(int)Token.XOR_ASSIGN] = "^=";
            tokens[(int)Token.SHL_ASSIGN] = "<<=";
            tokens[(int)Token.SHR_ASSIGN] = ">>=";
            tokens[(int)Token.AND_NOT_ASSIGN] = "&^=";

            tokens[(int)Token.LAND] = "&&";
            tokens[(int)Token.LOR] = "||";
            tokens[(int)Token.ARROW] = "<-";
            tokens[(int)Token.INC] = "++";
            tokens[(int)Token.DEC] = "--";

            tokens[(int)Token.EQL] = "==";
            tokens[(int)Token.LSS] = "<";
            tokens[(int)Token.GTR] = ">";
            tokens[(int)Token.ASSIGN] = "=";
            tokens[(int)Token.NOT] = "!";

            tokens[(int)Token.NEQ] = "!=";
            tokens[(int)Token.LEQ] = "<=";
            tokens[(int)Token.GEQ] = ">=";
            tokens[(int)Token.DEFINE] = ":=";
            tokens[(int)Token.ELLIPSIS] = "...";

            tokens[(int)Token.LPAREN] = "(";
            tokens[(int)Token.LBRACK] = "[";
            tokens[(int)Token.LBRACE] = "{";
            tokens[(int)Token.COMMA] = ",";
            tokens[(int)Token.PERIOD] = ".";

            tokens[(int)Token.RPAREN] = ")";
            tokens[(int)Token.RBRACK] = "]";
            tokens[(int)Token.RBRACE] = "}";
            tokens[(int)Token.SEMICOLON] = ";";
            tokens[(int)Token.COLON] = ":";

            tokens[(int)Token.BREAK] = "break";
            tokens[(int)Token.CASE] = "case";
            tokens[(int)Token.CHAN] = "chan";
            tokens[(int)Token.CONST] = "const";
            tokens[(int)Token.CONTINUE] = "continue";

            tokens[(int)Token.DEFAULT] = "default";
            tokens[(int)Token.DEFER] = "defer";
            tokens[(int)Token.ELSE] = "else";
            tokens[(int)Token.FALLTHROUGH] = "fallthrough";
            tokens[(int)Token.FOR] = "for";

            tokens[(int)Token.FUNC] = "func";
            tokens[(int)Token.GO] = "go";
            tokens[(int)Token.GOTO] = "goto";
            tokens[(int)Token.IF] = "if";
            tokens[(int)Token.IMPORT] = "import";

            tokens[(int)Token.INTERFACE] = "interface";
            tokens[(int)Token.MAP] = "map";
            tokens[(int)Token.PACKAGE] = "package";
            tokens[(int)Token.RANGE] = "range";
            tokens[(int)Token.RETURN] = "return";

            tokens[(int)Token.SELECT] = "select";
            tokens[(int)Token.STRUCT] = "struct";
            tokens[(int)Token.SWITCH] = "switch";
            tokens[(int)Token.TYPE] = "type";
            tokens[(int)Token.VAR] = "var";

            keywords = new Dictionary<string, Token>();
            for (var i = Token.keyword_beg + 1; i < Token.keyword_end; i++)
            {
                keywords[tokens[(int)i]] = i;
            }
        }

        /// <summary>
        /// String returns the string corresponding to the token tok.
        /// For operators, delimiters, and keywords the string is the actual
        /// token character sequence (e.g., for the token ADD, the string is
        /// "+"). For all other tokens the string corresponds to the token
        /// constant name (e.g. for the token IDENT, the string is "IDENT").
        /// </summary>
        public static string String(this Token tok)
        {
            return 0 <= tok && (int)tok < tokens.Length ? tokens[(int)tok]
                : string.Format("token({0})", (int)tok);
        }

        // A set of constants for precedence-based expression parsing.
        // Non-operators have lowest precedence, followed by operators
        // starting with precedence 1 up to unary operators. The highest
        // precedence serves as "catch-all" precedence for selector,
        // indexing, and other operator and delimiter tokens.
        public const int LowestPrec = 0; // non-operators
        public const int UnaryPrec = 6;
        public const int HighestPrec = 7;

        /// <summary>
        /// Precedence returns the operator precedence of the binary
        /// operator op. If op is not a binary operator, the result
        /// is LowestPrecedence.
        /// </summary>
        public static int Precedence(this Token op)
        {
            switch (op)
            {
                case Token.LOR:
                    return 1;
                case Token.LAND:
                    return 2;
                case Token.EQL:
                case Token.NEQ:
                case Token.LSS:
                case Token.LEQ:
                case Token.GTR:
                case Token.GEQ:
                    return 3;
                case Token.ADD:
                case Token.SUB:
                case Token.OR:
                case Token.XOR:
                    return 4;
                case Token.MUL:
                case Token.QUO:
                case Token.REM:
                case Token.SHL:
                case Token.SHR:
                case Token.AND:
                case Token.AND_NOT:
                    return 5;
            }
            return LowestPrec;
        }

        /// <summary>
        /// Lookup maps an identifier to its keyword token or IDENT (if not a keyword).
        /// </summary>
        public static Token Lookup(string ident)
        {
            Token tok;
            if (keywords.TryGetValue(ident, out tok))
                return tok;
            return Token.IDENT;
        }

        // Predicates

        /// <summary>
        /// IsLiteral returns true for tokens corresponding to identifiers
        /// and basic type literals; it returns false otherwise.
        /// </summary>
        public static bool IsLiteral(this Token tok)
        {
            return Token.literal_beg < tok && tok < Token.literal_end;
        }

        /// <summary>
        /// IsLiteral returns true for tokens corresponding to identifiers
        /// and basic type literals; it returns false otherwise.
        /// </summary>
        public static bool IsOperator(this Token tok)
        {
            return Token.operator_beg < tok && tok < Token.operator_end;
        }

        /// <summary>
        /// IsKeyword returns true for tokens corresponding to keywords;
        /// it returns false otherwise.
        /// </summary>
        public static bool IsKeyword(this Token tok)
        {
            return Token.keyword_beg < tok && tok < Token.keyword_end;
        }
    }
}
