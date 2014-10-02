using System;
using System.Collections.Generic;
using System.Linq;
using Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.GoLib.Scanners;
using Inocc.Compiler.GoLib.Tokens;

namespace Inocc.Compiler.GoLib.Parsers
{
    using Pos = Int32;

    // The parser structure holds the parser's internal state.
    internal class parser
    {
        private File file;
        internal ErrorList errors = new ErrorList();
        private Scanner scanner = new Scanner();

        // Tracing/debugging
        private Mode mode; // parsing mode
        private bool trace; // == (mode & Trace != 0)
        private int indent; // indentation used for tracing output

        // Comments
        private List<CommentGroup> comments = new List<CommentGroup>();
        private CommentGroup leadComment; // last lead comment
        private CommentGroup lineComment; // last line comment

        // Next token
        internal Pos pos;   // token position
        internal Token tok; // one token look-ahead
        internal string lit; // token literal

        // Error recovery
        // (used to limit the number of calls to syncXXX functions
        // w/o making scanning progress - avoids potential endless
        // loops across multiple parser functions during error recovery)
        private Pos syncPos; // last synchronization position
        private int syncCnt; // number of calls to syncXXX without progress

        // Non-syntactic parser control
        private int exprLev; // < 0: in control clause, >= 0: in expression
        private bool inRhs; // if set, the parser is parsing a rhs expression

        // Ordinary identifier scopes
        internal Scope pkgScope; // pkgScope.Outer == nil
        internal Scope topScope; // top-most scope; may be pkgScope
        private List<Ident> unresolved = new List<Ident>(); // unresolved identifiers
        private List<ImportSpec> imports = new List<ImportSpec>(); // list of imports

        // Label scopes
        // (maintained by open/close LabelScope)
        private Scope labelScope; // label scope for current function
        private List<List<Ident>> targetStack = new List<List<Ident>>(); // stack of unresolved labels

        internal void init(FileSet fset, string filename, byte[] src, Mode mode)
        {
            this.file = fset.AddFile(filename, -1, src.Length);
            var m = default(Scanner.Mode);
            if ((mode & Mode.ParseComments) != 0)
            {
                m = Scanner.Mode.ScanComments;
            }
            ErrorHandler eh = (pos, msg) => this.errors.Add(pos, msg);
            this.scanner.Init(this.file, src, eh, m);

            this.mode = mode;
            this.trace = (mode & Mode.Trace) != 0; // for convenience (p.trace is used frequently)

            this.next();
        }

        // ----------------------------------------------------------------------------
        // Scoping support

        internal void openScope()
        {
            this.topScope = new Scope(this.topScope);
        }

        internal void closeScope()
        {
            this.topScope = this.topScope.Outer;
        }

        private void openLabelScope()
        {
            this.labelScope = new Scope(this.labelScope);
            this.targetStack.Add(new List<Ident>());
        }

        private void closeLabelScope()
        {
            // resolve labels
            var n = this.targetStack.Count - 1;
            var scope = this.labelScope;
            foreach (var ident in this.targetStack[n])
            {
                ident.Obj = scope.Lookup(ident.Name);
                if (ident.Obj == null && (this.mode & Mode.DeclarationErrors) != 0)
                {
                    this.error(ident.Pos, string.Format("label {0} undefined", ident.Name));
                }
            }
            // pop label scope
            this.targetStack.Cut(n);
            this.labelScope = this.labelScope.Outer;
        }

        private void declare(object decl, object data, Scope scope, ObjKind kind, params Ident[] idents)
        {
            foreach (var ident in idents)
            {
                assert(ident.Obj == null, "identifier already declared or resolved");
                var obj = new EntityObject(kind, ident.Name);
                // remember the corresponding declaration for redeclaration
                // errors and global variable resolution/typechecking phase
                obj.Decl = decl;
                obj.Data = data;
                ident.Obj = obj;
                if (ident.Name != "_")
                {
                    var alt = scope.Insert(obj);
                    if (alt != null && (this.mode & Mode.DeclarationErrors) != 0)
                    {
                        var prevDecl = "";
                        var pos = alt.Pos;
                        if (Helper.IsValidPos(pos))
                        {
                            prevDecl = string.Format("\n\tprevious declaration at {0}", this.file.Position(pos));
                        }
                        this.error(ident.Pos, string.Format("{0} redeclared in this block{1}", ident.Name, prevDecl));
                    }
                }
            }
        }

        private void shortVarDecl(AssignStmt decl, Expr[] list)
        {
            // Go spec: A short variable declaration may redeclare variables
            // provided they were originally declared in the same block with
            // the same type, and at least one of the non-blank variables is new.
            var n = 0; // number of new variables
            foreach (var x in list)
            {
                var ident = x as Ident;
                if (ident != null)
                {
                    assert(ident.Obj == null, "identifier already declared or resolved");
                    var obj = new EntityObject(ObjKind.Var, ident.Name);
                    // remember corresponding assignment for other tools
                    obj.Decl = decl;
                    ident.Obj = obj;
                    if (ident.Name != "_")
                    {
                        var alt = this.topScope.Insert(obj);
                        if (alt != null)
                        {
                            ident.Obj = alt; // redeclaration
                        }
                        else
                        {
                            n++; // new declaration
                        }
                    }
                }
                else
                {
                    this.errorExpected(x.Pos, "identifier on left side of :=");
                }
            }
            if (n == 0 && (this.mode & Mode.DeclarationErrors) != 0)
            {
                this.error(list[0].Pos, "no new variables on left side of :=");
            }
        }

        // The unresolved object is a sentinel to mark identifiers that have been added
        // to the list of unresolved identifiers. The sentinel is only used for verifying
        // internal consistency.
        private static readonly EntityObject Unresolved = new EntityObject();

        // If x is an identifier, tryResolve attempts to resolve x by looking up
        // the object it denotes. If no object is found and collectUnresolved is
        // set, x is marked as unresolved and collected in the list of unresolved
        // identifiers.
        //
        private void tryResolve(Expr x, bool collectUnresolved)
        {
            // nothing to do if x is not an identifier or the blank identifier
            var ident = x as Ident;
            if (ident == null)
            {
                return;
            }
            assert(ident.Obj == null, "identifier already declared or resolved");
            if (ident.Name == "_")
            {
                return;
            }
            // try to resolve the identifier
            for (var s = this.topScope; s != null; s = s.Outer)
            {
                var obj = s.Lookup(ident.Name);
                if (obj != null)
                {
                    ident.Obj = obj;
                    return;
                }
            }
            // all local scopes are known, so any unresolved identifier
            // must be found either in the file scope, package scope
            // (perhaps in another file), or universe scope --- collect
            // them so that they can be resolved later
            if (collectUnresolved)
            {
                ident.Obj = Unresolved;
                this.unresolved.Add(ident);
            }
        }

        private void resolve(Expr x)
        {
            this.tryResolve(x, true);
        }

        // ----------------------------------------------------------------------------
        // Parsing support

        private void printTrace(params object[] a)
        {
            const string dots = ". . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . ";
            const int n = 64; // dots.Length
            var pos = this.file.Position(this.pos);
            Console.Write("{0,5}:{1,3}: ", pos.Line, pos.Column);
            var i = 2 * this.indent;
            while (i > n)
            {
                Console.Write(dots);
                i -= n;
            }
            // i <= n
            Console.Write(dots.Substring(0, i));
            Console.WriteLine("[{0}]", string.Join(" ", a));
        }

        private static parser Trace(parser p, string msg)
        {
            p.printTrace(msg, "(");
            p.indent++;
            return p;
        }

        // Usage pattern: defer un(trace(p, "..."))
        private static void Un(parser p)
        {
            p.indent--;
            p.printTrace(")");
        }

        // Advance to the next token.
        private void next0()
        {
            // Because of one-token look-ahead, print the previous token
            // when tracing as it provides a more readable output. The
            // very first token (!p.pos.IsValid()) is not initialized
            // (it is token.ILLEGAL), so don't print it .
            if (this.trace && Helper.IsValidPos(this.pos))
            {
                var s = this.tok.String();
                if (this.tok.IsLiteral())
                    this.printTrace(s, this.lit);
                else if (this.tok.IsOperator() || this.tok.IsKeyword())
                    this.printTrace("\"" + s + "\"");
                else
                    this.printTrace(s);
            }

            var t = this.scanner.Scan();
            this.pos = t.Item1;
            this.tok = t.Item2;
            this.lit = t.Item3;
        }

        // Consume a comment and return it and the line on which it ends.
        private Tuple<Comment, int> consumeComment()
        {
            // /*-style comments may end on a different line than where they start.
            // Scan the comment for '\n' chars and adjust endline accordingly.
            var endline = this.file.Line(this.pos);
            if (this.lit[1] == '*')
            {
                // don't use range here - no need to decode Unicode code points
                for (var i = 0; i < this.lit.Length; i++)
                {
                    if (this.lit[i] == '\n')
                    {
                        endline++;
                    }
                }
            }

            var comment = new Comment { Slash = this.pos, Text = this.lit };
            this.next0();

            return Tuple.Create(comment, endline);
        }

        // Consume a group of adjacent comments, add it to the parser's
        // comments list, and return it together with the line at which
        // the last comment in the group ends. A non-comment token or n
        // empty lines terminate a comment group.
        //
        private Tuple<CommentGroup, int> consumeCommentGroup(int n)
        {
            var list = new List<Comment>();
            var endline = this.file.Line(this.pos);
            while (this.tok == Token.COMMENT && this.file.Line(this.pos) <= endline + n)
            {
                var t = this.consumeComment();
                var comment = t.Item1;
                endline = t.Item2;
                list.Add(comment);
            }

            // add comment group to the comments list
            var comments = new CommentGroup { List = list.ToArray() };
            this.comments.Add(comments);

            return Tuple.Create(comments, endline);
        }

        // Advance to the next non-comment token. In the process, collect
        // any comment groups encountered, and remember the last lead and
        // and line comments.
        //
        // A lead comment is a comment group that starts and ends in a
        // line without any other tokens and that is followed by a non-comment
        // token on the line immediately after the comment group.
        //
        // A line comment is a comment group that follows a non-comment
        // token on the same line, and that has no tokens after it on the line
        // where it ends.
        //
        // Lead and line comments may be considered documentation that is
        // stored in the AST.
        //
        internal void next()
        {
            this.leadComment = null;
            this.lineComment = null;
            var prev = this.pos;
            this.next0();

            if (this.tok == Token.COMMENT)
            {
                CommentGroup comment = null;
                int endline;

                if (this.file.Line(this.pos) == this.file.Line(prev))
                {
                    // The comment is on same line as the previous token; it
                    // cannot be a lead comment but may be a line comment.
                    var t = this.consumeCommentGroup(0);
                    comment = t.Item1;
                    endline = t.Item2;
                    if (this.file.Line(this.pos) != endline)
                    {
                        // The next token is on a different line, thus
                        // the last comment group is a line comment.
                        this.lineComment = comment;
                    }
                }

                // consume successor comments, if any
                endline = -1;
                while (this.tok == Token.COMMENT)
                {
                    var t = this.consumeCommentGroup(1);
                    comment = t.Item1;
                    endline = t.Item2;
                }

                if (endline + 1 == this.file.Line(this.pos))
                {
                    // The next token is following on the line immediately after the
                    // comment group, thus the last comment group is a lead comment.
                    this.leadComment = comment;
                }
            }
        }

        // A bailout panic is raised to indicate early termination.
        public class Bailout : Exception { }

        private void error(Pos pos, string msg)
        {
            var epos = this.file.Position(pos);

            // If AllErrors is not set, discard errors reported on the same line
            // as the last recorded error and stop parsing if there are more than
            // 10 errors.
            if ((this.mode & Mode.AllErrors) == 0)
            {
                var n = this.errors.Count;
                if (n > 0 && this.errors[n - 1].Pos.Line == epos.Line)
                {
                    return; // discard - likely a spurious error
                }
                if (n > 10)
                {
                    throw new Bailout();
                }
            }

            this.errors.Add(epos, msg);
        }

        private void errorExpected(Pos pos, string msg)
        {
            msg = "expected " + msg;
            if (pos == this.pos)
            {
                // the error happened at the current position;
                // make the error message more specific
                if (this.tok == Token.SEMICOLON && this.lit == "\n")
                {
                    msg += ", found newline";
                }
                else
                {
                    msg += ", found '" + this.tok.String() + "'";
                    if (this.tok.IsLiteral())
                    {
                        msg += " " + this.lit;
                    }
                }
            }
            this.error(pos, msg);
        }

        internal Pos expect(Token tok)
        {
            var pos = this.pos;
            if (this.tok != tok)
            {
                this.errorExpected(pos, "'" + tok.String() + "'");
            }
            this.next(); // make progress
            return pos;
        }

        // expectClosing is like expect but provides a better error message
        // for the common case of a missing comma before a newline.
        //
        private Pos expectClosing(Token tok, string context)
        {
            if (this.tok != tok && this.tok == Token.SEMICOLON && this.lit == "\n")
            {
                this.error(this.pos, "missing ',' before newline in " + context);
                this.next();
            }
            return this.expect(tok);
        }

        private void expectSemi()
        {
            // semicolon is optional before a closing ')' or '}'
            if (this.tok != Token.RPAREN && this.tok != Token.RBRACE)
            {
                if (this.tok == Token.SEMICOLON)
                {
                    this.next();
                }
                else
                {
                    this.errorExpected(this.pos, "';'");
                    syncStmt(this);
                }
            }
        }

        private bool atComma(string context)
        {
            if (this.tok == Token.COMMA)
            {
                return true;
            }
            if (this.tok == Token.SEMICOLON && this.lit == "\n")
            {
                this.error(this.pos, "missing ',' before newline in " + context);
                return true; // "insert" the comma and continue

            }
            return false;
        }

        public class ParserAssertionException : Exception
        {
            public ParserAssertionException(string message) : base(message) { }
        }

        internal static void assert(bool cond, string msg)
        {
            if (!cond)
            {
                throw new ParserAssertionException("go/parser internal error: " + msg);
            }
        }

        // syncStmt advances to the next statement.
        // Used for synchronization after an error.
        //
        private static void syncStmt(parser p)
        {
            while (true)
            {
                switch (p.tok)
                {
                    case Token.BREAK:
                    case Token.CONST:
                    case Token.CONTINUE:
                    case Token.DEFER:
                    case Token.FALLTHROUGH:
                    case Token.FOR:
                    case Token.GO:
                    case Token.GOTO:
                    case Token.IF:
                    case Token.RETURN:
                    case Token.SELECT:
                    case Token.SWITCH:
                    case Token.TYPE:
                    case Token.VAR:
                        // Return only if parser made some progress since last
                        // sync or if it has not reached 10 sync calls without
                        // progress. Otherwise consume at least one token to
                        // avoid an endless parser loop (it is possible that
                        // both parseOperand and parseStmt call syncStmt and
                        // correctly do not advance, thus the need for the
                        // invocation limit p.syncCnt).
                        if (p.pos == p.syncPos && p.syncCnt < 10)
                        {
                            p.syncCnt++;
                            return;
                        }
                        if (p.pos > p.syncPos)
                        {
                            p.syncPos = p.pos;
                            p.syncCnt = 0;
                            return;
                        }
                        // Reaching here indicates a parser bug, likely an
                        // incorrect token list in this function, but it only
                        // leads to skipping of possibly correct code if a
                        // previous error is present, and thus is preferred
                        // over a non-terminating parse.
                        break;
                    case Token.EOF:
                        return;
                }
                p.next();
            }
        }

        // syncDecl advances to the next declaration.
        // Used for synchronization after an error.
        //
        private static void syncDecl(parser p)
        {
            while (true)
            {
                switch (p.tok)
                {
                    case Token.CONST:
                    case Token.TYPE:
                    case Token.VAR:
                        // see comments in syncStmt
                        if (p.pos == p.syncPos && p.syncCnt < 10)
                        {
                            p.syncCnt++;
                            return;
                        }
                        if (p.pos > p.syncPos)
                        {
                            p.syncPos = p.pos;
                            p.syncCnt = 0;
                            return;
                        }
                        break;
                    case Token.EOF:
                        return;
                }
                p.next();
            }
        }

        // safePos returns a valid file position for a given position: If pos
        // is valid to begin with, safePos returns pos. If pos is out-of-range,
        // safePos returns the EOF position.
        //
        // This is hack to work around "artificial" end positions in the AST which
        // are computed by adding 1 to (presumably valid) token positions. If the
        // token positions are invalid due to parse errors, the resulting end position
        // may be past the file's EOF position, which would lead to panics if used
        // later on.
        //
        private Pos safePos(Pos pos)
        {
            try
            {
                this.file.Offset(pos); // trigger a panic if position is out-of-range
                return pos;
            }
            catch
            {
                return this.file.Base + this.file.Size; // EOF position
            }
        }

        // ----------------------------------------------------------------------------
        // Identifiers

        private Ident parseIdent()
        {
            var pos = this.pos;
            var name = "_";
            if (this.tok == Token.IDENT)
            {
                name = this.lit;
                this.next();
            }
            else
            {
                this.expect(Token.IDENT); // use expect() error handling
            }
            return new Ident { NamePos = pos, Name = name };
        }

        private Ident[] parseIdentList()
        {
            try
            {
                var list = new List<Ident>() { this.parseIdent() };
                while (this.tok == Token.COMMA)
                {
                    this.next();
                    list.Add(this.parseIdent());
                }

                return list.ToArray();
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "IdentList"));
            }
        }

        // ----------------------------------------------------------------------------
        // Common productio

        // If lhs is set, result list elements which are identifiers are not resolved.
        private Expr[] parseExprList(bool lhs)
        {
            try
            {
                var list = new List<Expr>() { this.checkExpr(this.parseExpr(lhs)) };
                while (this.tok == Token.COMMA)
                {
                    this.next();
                    list.Add(this.checkExpr(this.parseExpr(lhs)));
                }

                return list.ToArray();
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ExpressionList"));
            }
        }

        private Expr[] parseLhsList()
        {
            var old = this.inRhs;
            this.inRhs = false;
            var list = this.parseExprList(true);
            switch (this.tok)
            {
                case Token.DEFINE:
                    // lhs of a short variable declaration
                    // but doesn't enter scope until later:
                    // caller must call p.shortVarDecl(p.makeIdentList(list))
                    // at appropriate time.
                    break;
                case Token.COLON:
                    // lhs of a label declaration or a communication clause of a select
                    // statement (parseLhsList is not called when parsing the case clause
                    // of a switch statement):
                    // - labels are declared by the caller of parseLhsList
                    // - for communication clauses, if there is a stand-alone identifier
                    //   followed by a colon, we have a syntax error; there is no need
                    //   to resolve the identifier in that case
                    break;
                default:
                    // identifiers must be declared elsewhere
                    foreach (var x in list)
                    {
                        this.resolve(x);
                    }
                    break;
            }
            this.inRhs = old;
            return list;
        }

        private Expr[] parseRhsList()
        {
            var old = this.inRhs;
            this.inRhs = true;
            var list = this.parseExprList(false);
            this.inRhs = old;
            return list;
        }

        // ----------------------------------------------------------------------------
        // Types

        private Expr parseType()
        {
            try
            {
                var typ = this.tryType();

                if (typ == null)
                {
                    var pos = this.pos;
                    this.errorExpected(pos, "type");
                    this.next(); // make progress
                    return new BadExpr { From = pos, To = this.pos };
                }

                return typ;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Type"));
            }
        }

        // If the result is an identifier, it is not resolved.
        private Expr parseTypeName()
        {
            try
            {
                var ident = this.parseIdent();
                // don't resolve ident yet - it may be a parameter or field name

                if (this.tok == Token.PERIOD)
                {
                    // ident is a package name
                    this.next();
                    this.resolve(ident);
                    var sel = this.parseIdent();
                    return new SelectorExpr { X = ident, Sel = sel };
                }

                return ident;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "TypeName"));
            }
        }

        private Expr parseArrayType()
        {
            try
            {
                var lbrack = this.expect(Token.LBRACK);
                this.exprLev++;
                Expr len = null;
                // always permit ellipsis for more fault-tolerant parsing
                if (this.tok == Token.ELLIPSIS)
                {
                    len = new Ellipsis { EllipsisPos = this.pos };
                    this.next();
                }
                else if (this.tok != Token.RBRACK)
                {
                    len = this.parseRhs();
                }
                this.exprLev--;
                this.expect(Token.RBRACK);
                var elt = this.parseType();

                return new ArrayType { Lbrack = lbrack, Len = len, Elt = elt };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ArrayType"));
            }
        }

        private Ident[] makeIdentList(Expr[] list)
        {
            var idents = new Ident[list.Length];
            for (var i = 0; i < list.Length; i++)
            {
                var x = list[i];
                var ident = x as Ident;
                if (ident == null)
                {
                    if (!(x is BadExpr))
                    {
                        // only report error if it's a new one
                        this.errorExpected(x.Pos, "identifier");
                    }
                    ident = new Ident { NamePos = x.Pos, Name = "_" };
                }
                idents[i] = ident;
            }
            return idents;
        }

        private Field parseFieldDecl(Scope scope)
        {
            try
            {
                var doc = this.leadComment;

                // FieldDecl
                var t = this.parseVarList(false);
                var list = t.Item1;
                var typ = t.Item2;

                // Tag
                BasicLit tag = null;
                if (this.tok == Token.STRING)
                {
                    tag = new BasicLit { ValuePos = this.pos, Kind = this.tok, Value = this.lit };
                    this.next();
                }

                // analyze case
                var idents = new Ident[0];
                if (typ != null)
                {
                    // IdentifierList Type
                    idents = this.makeIdentList(list);
                }
                else
                {
                    // ["*"] TypeName (AnonymousField)
                    typ = list[0]; // we always have at least one element
                    var n = list.Length;
                    if (n > 1 || !isTypeName(deref(typ)))
                    {
                        var pos = typ.Pos;
                        this.errorExpected(pos, "anonymous field");
                        typ = new BadExpr { From = pos, To = this.safePos(list[n - 1].End) };
                    }
                }

                this.expectSemi(); // call before accessing p.linecomment

                var field = new Field { Doc = doc, Names = idents, Type = typ, Tag = tag, Comment = this.lineComment };
                this.declare(field, null, scope, ObjKind.Var, idents);
                this.resolve(typ);

                return field;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "FieldDecl"));
            }
        }

        private StructType parseStructType()
        {
            try
            {
                var pos = this.expect(Token.STRUCT);
                var lbrace = this.expect(Token.LBRACE);
                var scope = new Scope(null); // struct scope
                var list = new List<Field>();
                while (this.tok == Token.IDENT || this.tok == Token.MUL || this.tok == Token.LPAREN)
                {
                    // a field declaration cannot start with a '(' but we accept
                    // it here for more robust parsing and better error messages
                    // (parseFieldDecl will check and complain if necessary)
                    list.Add(this.parseFieldDecl(scope));
                }
                var rbrace = this.expect(Token.RBRACE);

                return new StructType
                {
                    Struct = pos,
                    Fields = new FieldList
                    {
                        Opening = lbrace,
                        List = list.ToArray(),
                        Closing = rbrace,
                    }
                };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "StructType"));
            }
        }

        private StarExpr parsePointerType()
        {
            try
            {
                var star = this.expect(Token.MUL);
                var @base = this.parseType();

                return new StarExpr { Star = star, X = @base };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "PointerType"));
            }
        }

        // If the result is an identifier, it is not resolved.
        private Expr tryVarType(bool isParam)
        {
            if (isParam && this.tok == Token.ELLIPSIS)
            {
                var pos = this.pos;
                this.next();
                var typ = this.tryIdentOrType(); // don't use parseType so we can provide better error message
                if (typ != null)
                {
                    this.resolve(typ);
                }
                else
                {
                    this.error(pos, "'...' parameter is missing type");
                    typ = new BadExpr { From = pos, To = this.pos };
                }
                return new Ellipsis { EllipsisPos = pos, Elt = typ };
            }
            return this.tryIdentOrType();
        }

        // If the result is an identifier, it is not resolved.
        private Expr parseVarType(bool isParam)
        {
            var typ = this.tryVarType(isParam);
            if (typ == null)
            {
                var pos = this.pos;
                this.errorExpected(pos, "type");
                this.next(); // make progress
                typ = new BadExpr { From = pos, To = this.pos };
            }
            return typ;
        }

        // If any of the results are identifiers, they are not resolved.
        private Tuple<Expr[], Expr> parseVarList(bool isParam)
        {
            try
            {
                // a list of identifiers looks like a list of type names
                //
                // parse/tryVarType accepts any type (including parenthesized
                // ones) even though the syntax does not permit them here: we
                // accept them all for more robust parsing and complain later
                var list = new List<Expr>();
                var typ = this.parseVarType(isParam);
                while (typ != null)
                {
                    list.Add(typ);
                    if (this.tok != Token.COMMA)
                    {
                        break;
                    }
                    this.next();
                    typ = this.tryVarType(isParam); // maybe null as in: func f(int,) {}
                }

                // if we had a list of identifiers, it must be followed by a type
                typ = this.tryVarType(isParam);

                return Tuple.Create(list.ToArray(), typ);
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "VarList"));
            }
        }

        private Field[] parseParameterList(Scope scope, bool ellipsisOk)
        {
            try
            {
                var @params = new List<Field>();
                // ParameterDecl
                var t = this.parseVarList(ellipsisOk);
                var list = t.Item1;
                var typ = t.Item2;

                // analyze case
                if (typ != null)
                {
                    // IdentifierList Type
                    var idents = this.makeIdentList(list);
                    var field = new Field { Names = idents, Type = typ };
                    @params.Add(field);
                    // Go spec: The scope of an identifier denoting a function
                    // parameter or result variable is the function body.
                    this.declare(field, null, scope, ObjKind.Var, idents);
                    this.resolve(typ);
                    if (!this.atComma("parameter list"))
                    {
                        return @params.ToArray();
                    }
                    this.next();
                    while (this.tok != Token.RPAREN && this.tok != Token.EOF)
                    {
                        var idents2 = this.parseIdentList();
                        var typ2 = this.parseVarType(ellipsisOk);
                        var field2 = new Field { Names = idents2, Type = typ2 };
                        @params.Add(field2);
                        // Go spec: The scope of an identifier denoting a function
                        // parameter or result variable is the function body.
                        this.declare(field2, null, scope, ObjKind.Var, idents2);
                        this.resolve(typ);
                        if (!this.atComma("parameter list"))
                        {
                            break;
                        }
                        this.next();
                    }
                    return @params.ToArray();
                }

                // Type { "," Type } (anonymous parameters)
                var params2 = new Field[list.Length];
                for (var i = 0; i < list.Length; i++)
                {
                    typ = list[i];
                    this.resolve(typ);
                    params2[i] = new Field { Type = typ };
                }
                return params2;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ParameterList"));
            }
        }

        private FieldList parseParameters(Scope scope, bool ellipsisOk)
        {
            try
            {
                var @params = new Field[0];
                var lparen = this.expect(Token.LPAREN);
                if (this.tok != Token.RPAREN)
                {
                    @params = this.parseParameterList(scope, ellipsisOk);
                }
                var rparen = this.expect(Token.RPAREN);

                return new FieldList { Opening = lparen, List = @params, Closing = rparen };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Parameters"));
            }
        }

        private FieldList parseResult(Scope scope)
        {
            try
            {
                if (this.tok == Token.LPAREN)
                {
                    return this.parseParameters(scope, false);
                }

                var typ = this.tryType();
                if (typ != null)
                {
                    var list = new Field[1];
                    list[0] = new Field { Type = typ };
                    return new FieldList { List = list };
                }

                return null;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Result"));
            }
        }

        private Tuple<FieldList, FieldList> parseSignature(Scope scope)
        {
            try
            {
                var @params = this.parseParameters(scope, true);
                var results = this.parseResult(scope);

                return Tuple.Create(@params, results);
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Signature"));
            }
        }

        private Tuple<FuncType, Scope> parseFuncType()
        {
            try
            {
                var pos = this.expect(Token.FUNC);
                var scope = new Scope(this.topScope); // function scope
                var t = this.parseSignature(scope);
                var @params = t.Item1;
                var results = t.Item2;

                return Tuple.Create(new FuncType { Func = pos, Params = @params, Results = results }, scope);
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "FuncType"));
            }
        }

        private Field parseMethodSpec(Scope scope)
        {
            try
            {
                var doc = this.leadComment;
                var idents = new Ident[0];
                Expr typ = null;
                var x = this.parseTypeName();
                var ident = x as Ident;
                if (ident != null && this.tok == Token.LPAREN)
                {
                    // method
                    idents = new[] { ident };
                    var scope2 = new Scope(null); // method scope
                    var t = this.parseSignature(scope2);
                    var @params = t.Item1;
                    var results = t.Item2;
                    typ = new FuncType { Func = 0, Params = @params, Results = results };
                }
                else
                {
                    // embedded interface
                    typ = x;
                    this.resolve(typ);
                }
                this.expectSemi(); // call before accessing this.linecomment

                var spec = new Field { Doc = doc, Names = idents, Type = typ, Comment = this.lineComment };
                this.declare(spec, null, scope, ObjKind.Fun, idents);

                return spec;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "MethodSpec"));
            }
        }

        private InterfaceType parseInterfaceType()
        {
            try
            {
                var pos = this.expect(Token.INTERFACE);
                var lbrace = this.expect(Token.LBRACE);
                var scope = new Scope(null); // interface scope
                var list = new List<Field>();
                while (this.tok == Token.IDENT)
                {
                    list.Add(this.parseMethodSpec(scope));
                }
                var rbrace = this.expect(Token.RBRACE);

                return new InterfaceType
                {
                    Interface = pos,
                    Methods = new FieldList
                    {
                        Opening = lbrace,
                        List = list.ToArray(),
                        Closing = rbrace,
                    }
                };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "InterfaceType"));
            }
        }

        private MapType parseMapType()
        {
            try
            {
                var pos = this.expect(Token.MAP);
                this.expect(Token.LBRACK);
                var key = this.parseType();
                this.expect(Token.RBRACK);
                var value = this.parseType();

                return new MapType { Map = pos, Key = key, Value = value };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "MapType"));
            }
        }

        private ChanType parseChanType()
        {
            try
            {
                var pos = this.pos;
                var dir = ChanDir.SEND | ChanDir.RECV;
                var arrow = 0;
                if (this.tok == Token.CHAN)
                {
                    this.next();
                    if (this.tok == Token.ARROW)
                    {
                        arrow = this.pos;
                        this.next();
                        dir = ChanDir.SEND;
                    }
                }
                else
                {
                    arrow = this.expect(Token.ARROW);
                    this.expect(Token.CHAN);
                    dir = ChanDir.RECV;
                }
                var value = this.parseType();

                return new ChanType { Begin = pos, Arrow = arrow, Dir = dir, Value = value };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ChanType"));
            }
        }

        // If the result is an identifier, it is not resolved.
        private Expr tryIdentOrType()
        {
            switch (this.tok)
            {
                case Token.IDENT:
                    return this.parseTypeName();
                case Token.LBRACK:
                    return this.parseArrayType();
                case Token.STRUCT:
                    return this.parseStructType();
                case Token.MUL:
                    return this.parsePointerType();
                case Token.FUNC:
                    return this.parseFuncType().Item1;
                case Token.INTERFACE:
                    return this.parseInterfaceType();
                case Token.MAP:
                    return this.parseMapType();
                case Token.CHAN:
                case Token.ARROW:
                    return this.parseChanType();
                case Token.LPAREN:
                    var lparen = this.pos;
                    this.next();
                    var typ = this.parseType();
                    var rparen = this.expect(Token.RPAREN);
                    return new ParenExpr { Lparen = lparen, X = typ, Rparen = rparen };
            }

            // no type found
            return null;
        }

        private Expr tryType()
        {
            var typ = this.tryIdentOrType();
            if (typ != null)
            {
                this.resolve(typ);
            }
            return typ;
        }

        // ----------------------------------------------------------------------------
        // Blocks

        private Stmt[] parseStmtList()
        {
            try
            {
                var list = new List<Stmt>();
                while (this.tok != Token.CASE && this.tok != Token.DEFAULT && this.tok != Token.RBRACE && this.tok != Token.EOF)
                {
                    list.Add(this.parseStmt());
                }

                return list.ToArray();
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "StatementList"));
            }
        }

        private BlockStmt parseBody(Scope scope)
        {
            try
            {
                var lbrace = this.expect(Token.LBRACE);
                this.topScope = scope; // open function scope
                this.openLabelScope();
                var list = this.parseStmtList();
                this.closeLabelScope();
                this.closeScope();
                var rbrace = this.expect(Token.RBRACE);

                return new BlockStmt { Lbrace = lbrace, List = list, Rbrace = rbrace };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Body"));
            }
        }

        private BlockStmt parseBlockStmt()
        {
            try
            {
                var lbrace = this.expect(Token.LBRACE);
                this.openScope();
                var list = this.parseStmtList();
                this.closeScope();
                var rbrace = this.expect(Token.RBRACE);

                return new BlockStmt { Lbrace = lbrace, List = list, Rbrace = rbrace };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "BlockStmt"));
            }
        }

        // ----------------------------------------------------------------------------
        // Expressions

        private Expr parseFuncTypeOrLit()
        {
            try
            {
                var t = this.parseFuncType();
                var typ = t.Item1;
                var scope = t.Item2;
                if (this.tok != Token.LBRACE)
                {
                    // function type only
                    return typ;
                }

                this.exprLev++;
                var body = this.parseBody(scope);
                this.exprLev--;

                return new FuncLit { Type = typ, Body = body };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "FuncTypeOrLit"));
            }
        }

        // parseOperand may return an expression or a raw type (incl. array
        // types of the form [...]T. Callers must verify the result.
        // If lhs is set and the result is an identifier, it is not resolved.
        //
        private Expr parseOperand(bool lhs)
        {
            try
            {
                switch (this.tok)
                {
                    case Token.IDENT:
                        {
                            var x = this.parseIdent();
                            if (!lhs)
                            {
                                this.resolve(x);
                            }
                            return x;
                        }
                    case Token.INT:
                    case Token.FLOAT:
                    case Token.IMAG:
                    case Token.CHAR:
                    case Token.STRING:
                        {
                            var x = new BasicLit { ValuePos = this.pos, Kind = this.tok, Value = this.lit };
                            this.next();
                            return x;
                        }
                    case Token.LPAREN:
                        {
                            var lparen = this.pos;
                            this.next();
                            this.exprLev++;
                            var x = this.parseRhsOrType(); // types may be parenthesized: (some type)
                            this.exprLev--;
                            var rparen = this.expect(Token.RPAREN);
                            return new ParenExpr { Lparen = lparen, X = x, Rparen = rparen };
                        }
                    case Token.FUNC:
                        return this.parseFuncTypeOrLit();
                }

                var typ = this.tryIdentOrType();
                if (typ != null)
                {
                    // could be type for composite literal or conversion
                    assert(!(typ is Ident), "type cannot be identifier");
                    return typ;
                }

                // we have an error
                var pos = this.pos;
                this.errorExpected(pos, "operand");
                syncStmt(this);
                return new BadExpr { From = pos, To = this.pos };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Operand"));
            }
        }

        private Expr parseSelector(Expr x)
        {
            try
            {
                var sel = this.parseIdent();

                return new SelectorExpr { X = x, Sel = sel };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Selector"));
            }
        }

        private Expr parseTypeAssertion(Expr x)
        {
            try
            {
                var lparen = this.expect(Token.LPAREN);
                Expr typ = null;
                if (this.tok == Token.TYPE)
                {
                    // type switch: typ == nil
                    this.next();
                }
                else
                {
                    typ = this.parseType();
                }
                var rparen = this.expect(Token.RPAREN);

                return new TypeAssertExpr { X = x, Type = typ, Lparen = lparen, Rparen = rparen };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "TypeAssertion"));
            }
        }

        private Expr parseIndexOrSlice(Expr x)
        {
            try
            {
                const int N = 3; // change the 3 to 2 to disable 3-index slices
                var lbrack = this.expect(Token.LBRACK);
                this.exprLev++;
                var index = new Expr[N];
                var colons = new Pos[N - 1];
                if (this.tok != Token.COLON)
                {
                    index[0] = this.parseRhs();
                }
                var ncolons = 0;
                while (this.tok == Token.COLON && ncolons < colons.Length)
                {
                    colons[ncolons] = this.pos;
                    ncolons++;
                    this.next();
                    if (this.tok != Token.COLON && this.tok != Token.RBRACK && this.tok != Token.EOF)
                    {
                        index[ncolons] = this.parseRhs();
                    }
                }
                this.exprLev--;
                var rbrack = this.expect(Token.RBRACK);

                if (ncolons > 0)
                {
                    // slice expression
                    var slice3 = false;
                    if (ncolons == 2)
                    {
                        slice3 = true;
                        // Check presence of 2nd and 3rd index here rather than during type-checking
                        // to prevent erroneous programs from passing through gofmt (was issue 7305).
                        if (index[1] == null)
                        {
                            this.error(colons[0], "2nd index required in 3-index slice");
                            index[1] = new BadExpr { From = colons[0] + 1, To = colons[1] };
                        }
                        if (index[2] == null)
                        {
                            this.error(colons[1], "3rd index required in 3-index slice");
                            index[2] = new BadExpr { From = colons[1] + 1, To = rbrack };
                        }
                    }
                    return new SliceExpr { X = x, Lbrack = lbrack, Low = index[0], High = index[1], Max = index[2], Slice3 = slice3, Rbrack = rbrack };
                }

                return new IndexExpr { X = x, Lbrack = lbrack, Index = index[0], Rbrack = rbrack };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "IndexOrSlice"));
            }
        }

        private CallExpr parseCallOrConversion(Expr fun)
        {
            try
            {
                var lparen = this.expect(Token.LPAREN);
                this.exprLev++;
                var list = new List<Expr>();
                var ellipsis = 0;
                while (this.tok != Token.RPAREN && this.tok != Token.EOF && !Helper.IsValidPos(ellipsis))
                {
                    list.Add(this.parseRhsOrType()); // builtins may expect a type: make(some type, ...)
                    if (this.tok == Token.ELLIPSIS)
                    {
                        ellipsis = this.pos;
                        this.next();
                    }
                    if (!this.atComma("argument list"))
                    {
                        break;
                    }
                    this.next();
                }
                this.exprLev--;
                var rparen = this.expectClosing(Token.RPAREN, "argument list");

                return new CallExpr { Fun = fun, Lparen = lparen, Args = list.ToArray(), Ellipsis = ellipsis, Rparen = rparen };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "CallOrConversion"));
            }
        }

        private Expr parseElement(bool keyOk)
        {
            try
            {
                if (this.tok == Token.LBRACE)
                {
                    return this.parseLiteralValue(null);
                }

                // Because the parser doesn't know the composite literal type, it cannot
                // know if a key that's an identifier is a struct field name or a name
                // denoting a value. The former is not resolved by the parser or the
                // resolver.
                //
                // Instead, _try_ to resolve such a key if possible. If it resolves,
                // it a) has correctly resolved, or b) incorrectly resolved because
                // the key is a struct field with a name matching another identifier.
                // In the former case we are done, and in the latter case we don't
                // care because the type checker will do a separate field lookup.
                //
                // If the key does not resolve, it a) must be defined at the top
                // level in another file of the same package, the universe scope, or be
                // undeclared; or b) it is a struct field. In the former case, the type
                // checker can do a top-level lookup, and in the latter case it will do
                // a separate field lookup.
                var x = this.checkExpr(this.parseExpr(keyOk));
                if (keyOk)
                {
                    if (this.tok == Token.COLON)
                    {
                        var colon = this.pos;
                        this.next();
                        // Try to resolve the key but don't collect it
                        // as unresolved identifier if it fails so that
                        // we don't get (possibly false) errors about
                        // undeclared names.
                        this.tryResolve(x, false);
                        return new KeyValueExpr { Key = x, Colon = colon, Value = this.parseElement(false) };
                    }
                    this.resolve(x); // not a key
                }

                return x;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Element"));
            }
        }

        private Expr[] parseElementList()
        {
            try
            {
                var list = new List<Expr>();
                while (this.tok != Token.RBRACE && this.tok != Token.EOF)
                {
                    list.Add(this.parseElement(true));
                    if (!this.atComma("composite literal"))
                    {
                        break;
                    }
                    this.next();
                }

                return list.ToArray();
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ElementList"));
            }
        }

        private Expr parseLiteralValue(Expr typ)
        {
            try
            {
                var lbrace = this.expect(Token.LBRACE);
                var elts = new Expr[0];
                this.exprLev++;
                if (this.tok != Token.RBRACE)
                {
                    elts = this.parseElementList();
                }
                this.exprLev--;
                var rbrace = this.expectClosing(Token.RBRACE, "composite literal");
                return new CompositeLit { Type = typ, Lbrace = lbrace, Elts = elts, Rbrace = rbrace };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "LiteralValue"));
            }
        }

        // checkExpr checks that x is an expression (and not a type).
        private Expr checkExpr(Expr x)
        {
            var _ = unparen(x);
            if (_ is ParenExpr)
                throw new Exception("unreachable");
            else if (!(_ is BadExpr || _ is Ident || _ is BasicLit || _ is FuncLit || _ is CompositeLit
                || _ is SelectorExpr || _ is IndexExpr || _ is SliceExpr || _ is TypeAssertExpr
                || _ is CallExpr || _ is StarExpr || _ is UnaryExpr || _ is BinaryExpr))
            {
                // all other nodes are not proper expressions
                this.errorExpected(x.Pos, "expression");
                x = new BadExpr { From = x.Pos, To = this.safePos(x.End) };
            }
            return x;
        }

        // isTypeName returns true iff x is a (qualified) TypeName.
        private static bool isTypeName(Expr x)
        {
            var t = x as SelectorExpr;
            if (t != null) return t.X is Ident;
            return x is BadExpr || x is Ident;
        }

        // isLiteralType returns true iff x is a legal composite literal type.
        private static bool isLiteralType(Expr x)
        {
            var t = x as SelectorExpr;
            if (t != null) return t.X is Ident;
            return x is BadExpr || x is Ident || x is ArrayType || x is StructType || x is MapType;
        }

        // If x is of the form *T, deref returns T, otherwise it returns x.
        private static Expr deref(Expr x)
        {
            var p = x as StarExpr;
            if (p != null)
            {
                x = p.X;
            }
            return x;
        }

        // If x is of the form (T), unparen returns unparen(T), otherwise it returns x.
        private static Expr unparen(Expr x)
        {
            var p = x as ParenExpr;
            if (p != null)
            {
                x = unparen(p.X);
            }
            return x;
        }

        // checkExprOrType checks that x is an expression or a type
        // (and not a raw type such as [...]T).
        //
        private Expr checkExprOrType(Expr x)
        {
            if (x is ParenExpr)
                throw new Exception("unreachable");
            var t = x as ArrayType;
            if (t != null)
            {
                var len = t.Len as Ellipsis;
                if (len != null)
                {
                    this.error(len.Pos, "expected array length, found '...'");
                    x = new BadExpr { From = x.Pos, To = this.safePos(x.End) };
                }
            }

            // all other nodes are expressions or types
            return x;
        }

        // If lhs is set and the result is an identifier, it is not resolved.
        private Expr parsePrimaryExpr(bool lhs)
        {
            try
            {
                var x = this.parseOperand(lhs);
                while (true)
                {
                    switch (this.tok)
                    {
                        case Token.PERIOD:
                            this.next();
                            if (lhs)
                            {
                                this.resolve(x);
                            }
                            switch (this.tok)
                            {
                                case Token.IDENT:
                                    x = this.parseSelector(this.checkExprOrType(x));
                                    break;
                                case Token.LPAREN:
                                    x = this.parseTypeAssertion(this.checkExpr(x));
                                    break;
                                default:
                                    var pos = this.pos;
                                    this.errorExpected(pos, "selector or type assertion");
                                    this.next(); // make progress
                                    x = new BadExpr { From = pos, To = this.pos };
                                    break;
                            }
                            break;
                        case Token.LBRACK:
                            if (lhs)
                            {
                                this.resolve(x);
                            }
                            x = this.parseIndexOrSlice(this.checkExpr(x));
                            break;
                        case Token.LPAREN:
                            if (lhs)
                            {
                                this.resolve(x);
                            }
                            x = this.parseCallOrConversion(this.checkExprOrType(x));
                            break;
                        case Token.LBRACE:
                            if (isLiteralType(x) && (this.exprLev >= 0 || !isTypeName(x)))
                            {
                                if (lhs)
                                {
                                    this.resolve(x);
                                }
                                x = this.parseLiteralValue(x);
                            }
                            else
                            {
                                goto L;
                            }
                            break;
                        default:
                            goto L;
                    }
                    lhs = false; // no need to try to resolve again
                }

            L: return x;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "PrimaryExpr"));
            }
        }

        // If lhs is set and the result is an identifier, it is not resolved.
        private Expr parseUnaryExpr(bool lhs)
        {
            try
            {
                switch (this.tok)
                {
                    case Token.ADD:
                    case Token.SUB:
                    case Token.NOT:
                    case Token.XOR:
                    case Token.AND:
                        {
                            var pos = this.pos;
                            var op = this.tok;
                            this.next();
                            var x = this.parseUnaryExpr(false);
                            return new UnaryExpr { OpPos = pos, Op = op, X = this.checkExpr(x) };
                        }
                    case Token.ARROW:
                        {
                            // channel type or receive expression
                            var arrow = this.pos;
                            this.next();

                            // If the next token is token.CHAN we still don't know if it
                            // is a channel type or a receive operation - we only know
                            // once we have found the end of the unary expression. There
                            // are two cases:
                            //
                            //   <- type  => (<-type) must be channel type
                            //   <- expr  => <-(expr) is a receive from an expression
                            //
                            // In the first case, the arrow must be re-associated with
                            // the channel type parsed already:
                            //
                            //   <- (chan type)    =>  (<-chan type)
                            //   <- (chan<- type)  =>  (<-chan (<-type))

                            var x = this.parseUnaryExpr(false);

                            // determine which case we have
                            var typ = x as ChanType;
                            var ok = typ != null;
                            if (ok)
                            {
                                // (<-type)

                                // re-associate position info and <-
                                var dir = ChanDir.SEND;
                                while (ok && dir == ChanDir.SEND)
                                {
                                    if (typ.Dir == ChanDir.RECV)
                                    {
                                        // error: (<-type) is (<-(<-chan T))
                                        this.errorExpected(typ.Arrow, "'chan'");
                                    }
                                    arrow = typ.Arrow;
                                    typ.Begin = arrow;
                                    typ.Arrow = arrow;
                                    dir = typ.Dir;
                                    typ.Dir = ChanDir.RECV;
                                    typ = typ.Value as ChanType;
                                    ok = typ != null;
                                }
                                if (dir == ChanDir.SEND)
                                {
                                    this.errorExpected(arrow, "channel type");
                                }

                                return x;
                            }

                            // <-(expr)
                            return new UnaryExpr { OpPos = arrow, Op = Token.ARROW, X = this.checkExpr(x) };
                        }
                    case Token.MUL:
                        {
                            // pointer type or unary "*" expression
                            var pos = this.pos;
                            this.next();
                            var x = this.parseUnaryExpr(false);
                            return new StarExpr { Star = pos, X = this.checkExprOrType(x) };
                        }
                }

                return this.parsePrimaryExpr(lhs);
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "UnaryExpr"));
            }
        }

        private Tuple<Token, int> tokPrec()
        {
            var tok = this.tok;
            if (this.inRhs && tok == Token.ASSIGN)
            {
                tok = Token.EQL;
            }
            return Tuple.Create(tok, tok.Precedence());
        }

        // If lhs is set and the result is an identifier, it is not resolved.
        private Expr parseBinaryExpr(bool lhs, int prec1)
        {
            try
            {
                var x = this.parseUnaryExpr(lhs);
                for (var prec = this.tokPrec().Item2; prec >= prec1; prec--)
                {
                    while (true)
                    {
                        var t = this.tokPrec();
                        var op = t.Item1;
                        var oprec = t.Item2;
                        if (oprec != prec)
                        {
                            break;
                        }
                        var pos = this.expect(op);
                        if (lhs)
                        {
                            this.resolve(x);
                            lhs = false;
                        }
                        var y = this.parseBinaryExpr(false, prec + 1);
                        x = new BinaryExpr { X = this.checkExpr(x), OpPos = pos, Op = op, Y = this.checkExpr(y) };
                    }
                }

                return x;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "BinaryExpr"));
            }
        }

        // If lhs is set and the result is an identifier, it is not resolved.
        // The result may be a type or even a raw type ([...]int). Callers must
        // check the result (using checkExpr or checkExprOrType), depending on
        // context.
        private Expr parseExpr(bool lhs)
        {
            try
            {
                return this.parseBinaryExpr(lhs, TokenPackage.LowestPrec + 1);
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Expression"));
            }
        }

        private Expr parseRhs()
        {
            var old = this.inRhs;
            this.inRhs = true;
            var x = this.checkExpr(this.parseExpr(false));
            this.inRhs = old;
            return x;
        }

        internal Expr parseRhsOrType()
        {
            var old = this.inRhs;
            this.inRhs = true;
            var x = this.checkExprOrType(this.parseExpr(false));
            this.inRhs = old;
            return x;
        }

        // ----------------------------------------------------------------------------
        // Statements

        // Parsing modes for parseSimpleStmt.
        private enum ParseSimpleStmtMode
        {
            basic,
            labelOk,
            rangeOk
        }

        // parseSimpleStmt returns true as 2nd result if it parsed the assignment
        // of a range clause (with mode == rangeOk). The returned statement is an
        // assignment with a right-hand side that is a single unary expression of
        // the form "range x". No guarantees are given for the left-hand side.
        private Tuple<Stmt, bool> parseSimpleStmt(ParseSimpleStmtMode mode)
        {
            try
            {
                var x = this.parseLhsList();

                switch (this.tok)
                {
                    case Token.DEFINE:
                    case Token.ASSIGN:
                    case Token.ADD_ASSIGN:
                    case Token.SUB_ASSIGN:
                    case Token.MUL_ASSIGN:
                    case Token.QUO_ASSIGN:
                    case Token.REM_ASSIGN:
                    case Token.AND_ASSIGN:
                    case Token.OR_ASSIGN:
                    case Token.XOR_ASSIGN:
                    case Token.SHL_ASSIGN:
                    case Token.SHR_ASSIGN:
                    case Token.AND_NOT_ASSIGN:
                        // assignment statement, possibly part of a range clause
                        var pos = this.pos;
                        var tok = this.tok;
                        this.next();
                        Expr[] y;
                        var isRange = false;
                        if (mode == ParseSimpleStmtMode.rangeOk && this.tok == Token.RANGE && (tok == Token.DEFINE || tok == Token.ASSIGN))
                        {
                            var pos2 = this.pos;
                            this.next();
                            y = new Expr[] { new UnaryExpr { OpPos = pos2, Op = Token.RANGE, X = this.parseRhs() } };
                            isRange = true;
                        }
                        else
                        {
                            y = this.parseRhsList();
                        }
                        var @as = new AssignStmt { Lhs = x, TokPos = pos, Tok = tok, Rhs = y };
                        if (tok == Token.DEFINE)
                        {
                            this.shortVarDecl(@as, x);
                        }
                        return new Tuple<Stmt, bool>(@as, isRange);
                }

                if (x.Length > 1)
                {
                    this.errorExpected(x[0].Pos, "1 expression");
                    // continue with first expression
                }

                switch (this.tok)
                {
                    case Token.COLON:
                        // labeled statement
                        var colon = this.pos;
                        this.next();
                        var label = x[0] as Ident;
                        if (mode == ParseSimpleStmtMode.labelOk && label != null)
                        {
                            // Go spec: The scope of a label is the body of the function
                            // in which it is declared and excludes the body of any nested
                            // function.
                            Stmt stmt = new LabeledStmt { Label = label, Colon = colon, Stmt = this.parseStmt() };
                            this.declare(stmt, null, this.labelScope, ObjKind.Lbl, label);
                            return Tuple.Create(stmt, false);
                        }
                        // The label declaration typically starts at x[0].Pos(), but the label
                        // declaration may be erroneous due to a token after that position (and
                        // before the ':'). If SpuriousErrors is not set, the (only) error re-
                        // ported for the line is the illegal label error instead of the token
                        // before the ':' that caused the problem. Thus, use the (latest) colon
                        // position for error reporting.
                        this.error(colon, "illegal label declaration");
                        return new Tuple<Stmt, bool>(new BadStmt { From = x[0].Pos, To = colon + 1 }, false);

                    case Token.ARROW:
                        // send statement
                        var arrow = this.pos;
                        this.next();
                        var y = this.parseRhs();
                        return new Tuple<Stmt, bool>(new SendStmt { Chan = x[0], Arrow = arrow, Value = y }, false);

                    case Token.INC:
                    case Token.DEC:
                        // increment or decrement
                        Stmt s = new IncDecStmt { X = x[0], TokPos = this.pos, Tok = this.tok };
                        this.next();
                        return Tuple.Create(s, false);
                }

                // expression
                return new Tuple<Stmt, bool>(new ExprStmt { X = x[0] }, false);
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "SimpleStmt"));
            }
        }

        private CallExpr parseCallExpr(string callType)
        {
            var x = this.parseRhsOrType(); // could be a conversion: (some type)(x)
            var call = x as CallExpr;
            if (call != null)
            {
                return call;
            }
            if (!(x is BadExpr))
            {
                // only report error if it's a new one
                this.error(this.safePos(x.End), string.Format("function must be invoked in {0} statement", callType));
            }
            return null;
        }

        private Stmt parseGoStmt()
        {
            try
            {
                var pos = this.expect(Token.GO);
                var call = this.parseCallExpr("go");
                this.expectSemi();
                if (call == null)
                {
                    return new BadStmt { From = pos, To = pos + 2 }; // len("go")
                }

                return new GoStmt { Go = pos, Call = call };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "GoStmt"));
            }
        }

        private Stmt parseDeferStmt()
        {
            try
            {
                var pos = this.expect(Token.DEFER);
                var call = this.parseCallExpr("defer");
                this.expectSemi();
                if (call == null)
                {
                    return new BadStmt { From = pos, To = pos + 5 }; // len("defer")
                }

                return new DeferStmt { Defer = pos, Call = call };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "DeferStmt"));
            }
        }

        private ReturnStmt parseReturnStmt()
        {
            try
            {
                var pos = this.pos;
                this.expect(Token.RETURN);
                var x = new Expr[0];
                if (this.tok != Token.SEMICOLON && this.tok != Token.RBRACE)
                {
                    x = this.parseRhsList();
                }
                this.expectSemi();

                return new ReturnStmt { Return = pos, Results = x };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ReturnStmt"));
            }
        }

        private BranchStmt parseBranchStmt(Token tok)
        {
            try
            {
                var pos = this.expect(tok);
                Ident label = null;
                if (tok != Token.FALLTHROUGH && this.tok == Token.IDENT)
                {
                    label = this.parseIdent();
                    // add to list of unresolved targets
                    var n = this.targetStack.Count - 1;
                    this.targetStack[n].Add(label);
                }
                this.expectSemi();

                return new BranchStmt { TokPos = pos, Tok = tok, Label = label };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "BranchStmt"));
            }
        }

        private Expr makeExpr(Stmt s, string kind)
        {
            if (s == null)
            {
                return null;
            }
            var es = s as ExprStmt;
            if (es != null)
            {
                return this.checkExpr(es.X);
            }
            this.error(s.Pos, string.Format("expected {0}, found simple statement (missing parentheses around composite literal?)", kind));
            return new BadExpr { From = s.Pos, To = this.safePos(s.End) };
        }

        private IfStmt parseIfStmt()
        {
            try
            {
                var pos = this.expect(Token.IF);
                this.openScope();
                try
                {
                    Stmt s = null;
                    Expr x = null;
                    {
                        var prevLev = this.exprLev;
                        this.exprLev = -1;
                        if (this.tok == Token.SEMICOLON)
                        {
                            this.next();
                            x = this.parseRhs();
                        }
                        else
                        {
                            s = this.parseSimpleStmt(ParseSimpleStmtMode.basic).Item1;
                            if (this.tok == Token.SEMICOLON)
                            {
                                this.next();
                                x = this.parseRhs();
                            }
                            else
                            {
                                x = this.makeExpr(s, "boolean expression");
                                s = null;
                            }
                        }
                        this.exprLev = prevLev;
                    }

                    var body = this.parseBlockStmt();
                    Stmt else_ = null;
                    if (this.tok == Token.ELSE)
                    {
                        this.next();
                        else_ = this.parseStmt();
                    }
                    else
                    {
                        this.expectSemi();
                    }

                    return new IfStmt { If = pos, Init = s, Cond = x, Body = body, Else = else_ };
                }
                finally
                {
                    this.closeScope();
                }
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "IfStmt"));
            }
        }

        private Expr[] parseTypeList()
        {
            try
            {
                var list = new List<Expr>() { this.parseType() };
                while (this.tok == Token.COMMA)
                {
                    this.next();
                    list.Add(this.parseType());
                }

                return list.ToArray();
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "TypeList"));
            }
        }

        private CaseClause parseCaseClause(bool typeSwitch)
        {
            try
            {
                var pos = this.pos;
                var list = new Expr[0];
                if (this.tok == Token.CASE)
                {
                    this.next();
                    if (typeSwitch)
                    {
                        list = this.parseTypeList();
                    }
                    else
                    {
                        list = this.parseRhsList();
                    }
                }
                else
                {
                    this.expect(Token.DEFAULT);
                }

                var colon = this.expect(Token.COLON);
                this.openScope();
                var body = this.parseStmtList();
                this.closeScope();

                return new CaseClause { Case = pos, List = list, Colon = colon, Body = body };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "CaseClause"));
            }
        }

        private static bool isTypeSwitchAssert(Expr x)
        {
            var a = x as TypeAssertExpr;
            return a != null && a.Type == null;
        }

        private static bool isTypeSwitchGuard(Stmt s)
        {
            if (s is ExprStmt)
                // x.(nil)
                return isTypeSwitchAssert((s as ExprStmt).X);
            var t = s as AssignStmt;
            if (t != null)
                // v := x.(nil)
                return t.Lhs.Length == 1 && t.Tok == Token.DEFINE && t.Rhs.Length == 1 && isTypeSwitchAssert(t.Rhs[0]);

            return false;
        }

        private Stmt parseSwitchStmt()
        {
            try
            {
                var pos = this.expect(Token.SWITCH);
                this.openScope();
                var scopeCount = 0;
                try
                {
                    Stmt s1 = null, s2 = null;
                    if (this.tok != Token.LBRACE)
                    {
                        var prevLev = this.exprLev;
                        this.exprLev = -1;
                        if (this.tok != Token.SEMICOLON)
                        {
                            s2 = this.parseSimpleStmt(ParseSimpleStmtMode.basic).Item1;
                        }
                        if (this.tok == Token.SEMICOLON)
                        {
                            this.next();
                            s1 = s2;
                            s2 = null;
                            if (this.tok != Token.LBRACE)
                            {
                                // A TypeSwitchGuard may declare a variable in addition
                                // to the variable declared in the initial SimpleStmt.
                                // Introduce extra scope to avoid redeclaration errors:
                                //
                                //	switch t := 0; t := x.(T) { ... }
                                //
                                // (this code is not valid Go because the first t
                                // cannot be accessed and thus is never used, the extra
                                // scope is needed for the correct error message).
                                //
                                // If we don't have a type switch, s2 must be an expression.
                                // Having the extra nested but empty scope won't affect it.
                                this.openScope();
                                scopeCount++;
                                s2 = this.parseSimpleStmt(ParseSimpleStmtMode.basic).Item1;
                            }
                        }
                        this.exprLev = prevLev;
                    }

                    var typeSwitch = isTypeSwitchGuard(s2);
                    var lbrace = this.expect(Token.LBRACE);
                    var list = new List<Stmt>();
                    while (this.tok == Token.CASE || this.tok == Token.DEFAULT)
                    {
                        list.Add(this.parseCaseClause(typeSwitch));
                    }
                    var rbrace = this.expect(Token.RBRACE);
                    this.expectSemi();
                    var body = new BlockStmt { Lbrace = lbrace, List = list.ToArray(), Rbrace = rbrace };

                    if (typeSwitch)
                    {
                        return new TypeSwitchStmt { Switch = pos, Init = s1, Assign = s2, Body = body };
                    }

                    return new SwitchStmt { Switch = pos, Init = s1, Tag = this.makeExpr(s2, "switch expression"), Body = body };
                }
                finally
                {
                    for (var i = 0; i < scopeCount; i++) this.closeScope();
                    this.closeScope();
                }
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "SwitchStmt"));
            }
        }

        private CommClause parseCommClause()
        {
            try
            {
                this.openScope();
                var pos = this.pos;
                Stmt comm = null;
                if (this.tok == Token.CASE)
                {
                    this.next();
                    var lhs = this.parseLhsList();
                    if (this.tok == Token.ARROW)
                    {
                        // SendStmt
                        if (lhs.Length > 1)
                        {
                            this.errorExpected(lhs[0].Pos, "1 expression");
                            // continue with first expression
                        }
                        var arrow = this.pos;
                        this.next();
                        var rhs = this.parseRhs();
                        comm = new SendStmt { Chan = lhs[0], Arrow = arrow, Value = rhs };
                    }
                    else
                    {
                        // RecvStmt
                        var tok = this.tok;
                        if (tok == Token.ASSIGN || tok == Token.DEFINE)
                        {
                            // RecvStmt with assignment
                            if (lhs.Length > 2)
                            {
                                this.errorExpected(lhs[0].Pos, "1 or 2 expressions");
                                // continue with first two expressions
                                lhs = new[] { lhs[0], lhs[1] };
                            }
                            var pos2 = this.pos;
                            this.next();
                            var rhs = this.parseRhs();
                            var @as = new AssignStmt { Lhs = lhs, TokPos = pos2, Tok = tok, Rhs = new[] { rhs } };
                            if (tok == Token.DEFINE)
                            {
                                this.shortVarDecl(@as, lhs);
                            }
                            comm = @as;
                        }
                        else
                        {
                            // lhs must be single receive operation
                            if (lhs.Length > 1)
                            {
                                this.errorExpected(lhs[0].Pos, "1 expression");
                                // continue with first expression
                            }
                            comm = new ExprStmt { X = lhs[0] };
                        }
                    }
                }
                else
                {
                    this.expect(Token.DEFAULT);
                }

                var colon = this.expect(Token.COLON);
                var body = this.parseStmtList();
                this.closeScope();

                return new CommClause { Case = pos, Comm = comm, Colon = colon, Body = body };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "CommClause"));
            }
        }

        private SelectStmt parseSelectStmt()
        {
            try
            {
                var pos = this.expect(Token.SELECT);
                var lbrace = this.expect(Token.LBRACE);
                var list = new List<Stmt>();
                while (this.tok == Token.CASE || this.tok == Token.DEFAULT)
                {
                    list.Add(this.parseCommClause());
                }
                var rbrace = this.expect(Token.RBRACE);
                this.expectSemi();
                var body = new BlockStmt { Lbrace = lbrace, List = list.ToArray(), Rbrace = rbrace };

                return new SelectStmt { Select = pos, Body = body };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "SelectStmt"));
            }
        }

        private Stmt parseForStmt()
        {
            try
            {
                var pos = this.expect(Token.FOR);
                this.openScope();
                try
                {
                    Stmt s1 = null, s2 = null, s3 = null;
                    var isRange = false;
                    if (this.tok != Token.LBRACE)
                    {
                        var prevLev = this.exprLev;
                        this.exprLev = -1;
                        if (this.tok != Token.SEMICOLON)
                        {
                            if (this.tok == Token.RANGE)
                            {
                                // "for range x" (nil lhs in assignment)
                                var pos2 = this.pos;
                                this.next();
                                var y = new Expr[] { new UnaryExpr { OpPos = pos2, Op = Token.RANGE, X = this.parseRhs() } };
                                s2 = new AssignStmt { Rhs = y };
                                isRange = true;
                            }
                            else
                            {
                                var t = this.parseSimpleStmt(ParseSimpleStmtMode.rangeOk);
                                s2 = t.Item1;
                                isRange = t.Item2;
                            }
                        }
                        if (!isRange && this.tok == Token.SEMICOLON)
                        {
                            this.next();
                            s1 = s2;
                            s2 = null;
                            if (this.tok != Token.SEMICOLON)
                            {
                                s2 = this.parseSimpleStmt(ParseSimpleStmtMode.basic).Item1;
                            }
                            this.expectSemi();
                            if (this.tok != Token.LBRACE)
                            {
                                s3 = this.parseSimpleStmt(ParseSimpleStmtMode.basic).Item1;
                            }
                        }
                        this.exprLev = prevLev;
                    }

                    var body = this.parseBlockStmt();
                    this.expectSemi();

                    if (isRange)
                    {
                        var @as = s2 as AssignStmt;
                        // check lhs
                        Expr key = null, value = null;
                        switch (@as.Lhs.Length)
                        {
                            case 0:
                                // nothing to do
                                break;
                            case 1:
                                key = @as.Lhs[0];
                                break;
                            case 2:
                                key = @as.Lhs[0];
                                value = @as.Lhs[1];
                                break;
                            default:
                                this.errorExpected(@as.Lhs[@as.Lhs.Length - 1].Pos, "at most 2 expressions");
                                return new BadStmt { From = pos, To = this.safePos(body.End) };
                        }
                        // parseSimpleStmt returned a right-hand side that
                        // is a single unary expression of the form "range x"
                        var x = (@as.Rhs[0] as UnaryExpr).X;
                        return new RangeStmt
                        {
                            For = pos,
                            Key = key,
                            Value = value,
                            TokPos = @as.TokPos,
                            Tok = @as.Tok,
                            X = x,
                            Body = body
                        };
                    }

                    // regular for statement
                    return new ForStmt
                    {
                        For = pos,
                        Init = s1,
                        Cond = this.makeExpr(s2, "boolean or range expression"),
                        Post = s3,
                        Body = body
                    };
                }
                finally
                {
                    this.closeScope();
                }
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ForStmt"));
            }
        }

        private Stmt parseStmt()
        {
            try
            {
                Stmt s;
                switch (this.tok)
                {
                    case Token.CONST:
                    case Token.TYPE:
                    case Token.VAR:
                        s = new DeclStmt { Decl = this.parseDecl(syncStmt) };
                        break;
                    case Token.IDENT:
                    case Token.INT:
                    case Token.FLOAT:
                    case Token.IMAG:
                    case Token.CHAR:
                    case Token.STRING:
                    case Token.FUNC:
                    case Token.LPAREN:
                    case Token.LBRACK:
                    case Token.STRUCT:
                    case Token.ADD:
                    case Token.SUB:
                    case Token.MUL:
                    case Token.AND:
                    case Token.XOR:
                    case Token.ARROW:
                    case Token.NOT: // unary operators
                        s = this.parseSimpleStmt(ParseSimpleStmtMode.labelOk).Item1;
                        // because of the required look-ahead, labeled statements are
                        // parsed by parseSimpleStmt - don't expect a semicolon after
                        // them
                        if (!(s is LabeledStmt))
                        {
                            this.expectSemi();
                        }
                        break;
                    case Token.GO:
                        s = this.parseGoStmt();
                        break;
                    case Token.DEFER:
                        s = this.parseDeferStmt();
                        break;
                    case Token.RETURN:
                        s = this.parseReturnStmt();
                        break;
                    case Token.BREAK:
                    case Token.CONTINUE:
                    case Token.GOTO:
                    case Token.FALLTHROUGH:
                        s = this.parseBranchStmt(this.tok);
                        break;
                    case Token.LBRACE:
                        s = this.parseBlockStmt();
                        this.expectSemi();
                        break;
                    case Token.IF:
                        s = this.parseIfStmt();
                        break;
                    case Token.SWITCH:
                        s = this.parseSwitchStmt();
                        break;
                    case Token.SELECT:
                        s = this.parseSelectStmt();
                        break;
                    case Token.FOR:
                        s = this.parseForStmt();
                        break;
                    case Token.SEMICOLON:
                        s = new EmptyStmt { Semicolon = this.pos };
                        this.next();
                        break;
                    case Token.RBRACE:
                        // a semicolon may be omitted before a closing "}"
                        s = new EmptyStmt { Semicolon = this.pos };
                        break;
                    default:
                        // no statement found
                        var pos = this.pos;
                        this.errorExpected(pos, "statement");
                        syncStmt(this);
                        s = new BadStmt { From = pos, To = this.pos };
                        break;
                }

                return s;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Statement"));
            }
        }

        // ----------------------------------------------------------------------------
        // Declarations

        private delegate Spec parseSpecFunction(CommentGroup doc, Token keyword, int iota);

        private static bool isValidImport(string lit)
        {
            const string illegalChars = @"!""#$%&'()*,:;<=>?[\]^{|}" + "`\uFFFD";
            var s = Helper.Unquote(lit); // go/scanner returns a legal string literal
            foreach (var r in s)
            {
                if (!Helper.IsGraphic(r) || Helper.IsSpace(r) || illegalChars.Contains(r))
                {
                    return false;
                }
            }
            return !string.IsNullOrEmpty(s);
        }

        private Spec parseImportSpec(CommentGroup doc, Token _, int __)
        {
            try
            {
                Ident ident = null;
                switch (this.tok)
                {
                    case Token.PERIOD:
                        ident = new Ident { NamePos = this.pos, Name = "." };
                        this.next();
                        break;
                    case Token.IDENT:
                        ident = this.parseIdent();
                        break;
                }

                var pos = this.pos;
                var path = "";
                if (this.tok == Token.STRING)
                {
                    path = this.lit;
                    if (!isValidImport(path))
                    {
                        this.error(pos, "invalid import path: " + path);
                    }
                    this.next();
                }
                else
                {
                    this.expect(Token.STRING); // use expect() error handling
                }
                this.expectSemi(); // call before accessing p.linecomment

                // collect imports
                var spec = new ImportSpec
                {
                    Doc = doc,
                    Name = ident,
                    Path = new BasicLit { ValuePos = pos, Kind = Token.STRING, Value = path },
                    Comment = this.lineComment
                };
                this.imports.Add(spec);

                return spec;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "ImportSpec"));
            }
        }

        private Spec parseValueSpec(CommentGroup doc, Token keyword, int iota)
        {
            try
            {
                var idents = this.parseIdentList();
                var typ = this.tryType();
                var values = new Expr[0];
                // always permit optional initialization for more tolerant parsing
                if (this.tok == Token.ASSIGN)
                {
                    this.next();
                    values = this.parseRhsList();
                }
                this.expectSemi(); // call before accessing p.linecomment

                // Go spec: The scope of a constant or variable identifier declared inside
                // a function begins at the end of the ConstSpec or VarSpec and ends at
                // the end of the innermost containing block.
                // (Global identifiers are resolved in a separate phase after parsing.)
                var spec = new ValueSpec
                {
                    Doc = doc,
                    Names = idents,
                    Type = typ,
                    Values = values,
                    Comment = this.lineComment
                };
                var kind = ObjKind.Con;
                if (keyword == Token.VAR)
                {
                    kind = ObjKind.Var;
                }
                this.declare(spec, iota, this.topScope, kind, idents);

                return spec;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, keyword.String() + "Spec"));
            }
        }

        private Spec parseTypeSpec(CommentGroup doc, Token _, int __)
        {
            try
            {
                var ident = this.parseIdent();

                // Go spec: The scope of a type identifier declared inside a function begins
                // at the identifier in the TypeSpec and ends at the end of the innermost
                // containing block.
                // (Global identifiers are resolved in a separate phase after parsing.)
                var spec = new TypeSpec { Doc = doc, Name = ident };
                this.declare(spec, null, this.topScope, ObjKind.Typ, ident);

                spec.Type = this.parseType();
                this.expectSemi(); // call before accessing p.linecomment
                spec.Comment = this.lineComment;

                return spec;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "TypeSpec"));
            }
        }

        private GenDecl parseGenDecl(Token keyword, parseSpecFunction f)
        {
            try
            {
                var doc = this.leadComment;
                var pos = this.expect(keyword);
                Pos lparen = 0, rparen = 0;
                var list = new List<Spec>();
                if (this.tok == Token.LPAREN)
                {
                    lparen = this.pos;
                    this.next();
                    for (var iota = 0; this.tok != Token.RPAREN && this.tok != Token.EOF; iota++)
                    {
                        list.Add(f(this.leadComment, keyword, iota));
                    }
                    rparen = this.expect(Token.RPAREN);
                    this.expectSemi();
                }
                else
                {
                    list.Add(f(null, keyword, 0));
                }

                return new GenDecl
                {
                    Doc = doc,
                    TokPos = pos,
                    Tok = keyword,
                    Lparen = lparen,
                    Specs = list.ToArray(),
                    Rparen = rparen
                };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "GenDecl(" + keyword.String() + ")"));
            }
        }

        private FuncDecl parseFuncDecl()
        {
            try
            {
                var doc = this.leadComment;
                var pos = this.expect(Token.FUNC);
                var scope = new Scope(this.topScope); // function scope

                FieldList recv = null;
                if (this.tok == Token.LPAREN)
                {
                    recv = this.parseParameters(scope, false);
                }

                var ident = this.parseIdent();

                var t = this.parseSignature(scope);
                var @params = t.Item1;
                var results = t.Item2;

                BlockStmt body = null;
                if (this.tok == Token.LBRACE)
                {
                    body = this.parseBody(scope);
                }
                this.expectSemi();

                var decl = new FuncDecl
                {
                    Doc = doc,
                    Recv = recv,
                    Name = ident,
                    Type = new FuncType
                    {
                        Func = pos,
                        Params = @params,
                        Results = results
                    },
                    Body = body
                };
                if (recv == null)
                {
                    // Go spec: The scope of an identifier denoting a constant, type,
                    // variable, or function (but not method) declared at top level
                    // (outside any function) is the package block.
                    //
                    // init() functions cannot be referred to and there may
                    // be more than one - don't put them in the pkgScope
                    if (ident.Name != "init")
                    {
                        this.declare(decl, null, this.pkgScope, ObjKind.Fun, ident);
                    }
                }

                return decl;
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "FunctionDecl"));
            }
        }

        private Decl parseDecl(Action<parser> sync)
        {
            try
            {
                parseSpecFunction f;
                switch (this.tok)
                {
                    case Token.CONST:
                    case Token.VAR:
                        f = this.parseValueSpec;
                        break;
                    case Token.TYPE:
                        f = this.parseTypeSpec;
                        break;
                    case Token.FUNC:
                        return this.parseFuncDecl();
                    default:
                        var pos = this.pos;
                        this.errorExpected(pos, "declaration");
                        sync(this);
                        return new BadDecl { From = pos, To = this.pos };
                }

                return this.parseGenDecl(this.tok, f);
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "Declaration"));
            }
        }

        // ----------------------------------------------------------------------------
        // Source files

        internal FileNode parseFile()
        {
            try
            {
                // Don't bother parsing the rest if we had errors scanning the first token.
                // Likely not a Go source file at all.
                if (this.errors.Len() != 0)
                {
                    return null;
                }

                // package clause
                var doc = this.leadComment;
                var pos = this.expect(Token.PACKAGE);
                // Go spec: The package clause is not a declaration;
                // the package name does not appear in any scope.
                var ident = this.parseIdent();
                if (ident.Name == "_" && (this.mode & Mode.DeclarationErrors) != 0)
                {
                    this.error(this.pos, "invalid package name _");
                }
                this.expectSemi();

                // Don't bother parsing the rest if we had errors parsing the package clause.
                // Likely not a Go source file at all.
                if (this.errors.Len() != 0)
                {
                    return null;
                }

                this.openScope();
                this.pkgScope = this.topScope;
                var decls = new List<Decl>();
                if ((this.mode & Mode.PackageClauseOnly) == 0)
                {
                    // import decls
                    while (this.tok == Token.IMPORT)
                    {
                        decls.Add(this.parseGenDecl(Token.IMPORT, this.parseImportSpec));
                    }

                    if ((this.mode & Mode.ImportsOnly) == 0)
                    {
                        // rest of package body
                        while (this.tok != Token.EOF)
                        {
                            decls.Add(this.parseDecl(syncDecl));
                        }
                    }
                }
                this.closeScope();
                assert(this.topScope == null, "unbalanced scopes");
                assert(this.labelScope == null, "unbalanced label scopes");

                // resolve global identifiers within the same file
                var i = 0;
                for (var x = 0; x < this.unresolved.Count; x++)
                {
                    var ident2 = this.unresolved[x];
                    // i <= index for current ident
                    assert(ident2.Obj == Unresolved, "object already resolved");
                    ident2.Obj = this.pkgScope.Lookup(ident2.Name); // also removes unresolved sentinel
                    if (ident2.Obj == null)
                    {
                        this.unresolved[i] = ident2;
                        i++;
                    }
                }

                return new FileNode
                {
                    Doc = doc,
                    Package = pos,
                    Name = ident,
                    Decls = decls.ToArray(),
                    Scope = this.pkgScope,
                    Imports = this.imports.ToArray(),
                    Unresolved = this.unresolved.Take(i).ToArray(),
                    Comments = this.comments.ToArray()
                };
            }
            finally
            {
                if (this.trace)
                    Un(Trace(this, "File"));
            }
        }
    }
}
