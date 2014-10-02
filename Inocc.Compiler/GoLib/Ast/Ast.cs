using System;
using System.Collections.Generic;
using System.Linq;
using Inocc.Compiler.GoLib.Tokens;

namespace Inocc.Compiler.GoLib.Ast
{
    // ----------------------------------------------------------------------------
    // Interfaces
    //
    // There are 3 main classes of nodes: Expressions and type nodes,
    // statement nodes, and declaration nodes. The node names usually
    // match the corresponding Go spec production names to which they
    // correspond. The node fields correspond to the individual parts
    // of the respective productions.
    //
    // All nodes contain position information marking the beginning of
    // the corresponding source text segment; it is accessible via the
    // Pos accessor method. Nodes may contain additional position info
    // for language constructs where comments may be found between parts
    // of the construct (typically any larger, parenthesized subpart).
    // That position information is needed to properly position comments
    // when printing the construct.

    // All node types implement the Node interface.
    public abstract class Node
    {
        public abstract int Pos { get; } // position of first character belonging to the node
        public abstract int End { get; } // position of first character immediately after the node
    }

    // All expression nodes implement the Expr interface.
    public abstract class Expr : Node
    {
        protected internal virtual void exprNode() { }
    }

    // All statement nodes implement the Stmt interface.
    public abstract class Stmt : Node
    {
        protected internal virtual void stmtNode() { }
    }

    // All declaration nodes implement the Decl interface.
    public abstract class Decl : Node
    {
        protected internal virtual void declNode() { }
    }

    // ----------------------------------------------------------------------------
    // Comments

    // A Comment node represents a single //-style or /*-style comment.
    public class Comment : Node
    {
        public int Slash { get; set; } // position of "/" starting the comment
        public string Text { get; set; }  // comment text (excluding '\n' for //-style comments)

        public override int Pos
        {
            get { return this.Slash; }
        }

        public override int End
        {
            get { return this.Slash + this.Text.Length; }
        }
    }

    // A CommentGroup represents a sequence of comments
    // with no other tokens and no empty lines between.
    //
    public class CommentGroup : Node
    {
        public Comment[] List { get; set; }  // len(List) > 0

        public override int Pos
        {
            get { return this.List[0].Pos; }
        }

        public override int End
        {
            get { return this.List[this.List.Length - 1].End; }
        }

        // Text returns the text of the comment.
        // Comment markers (//, /*, and */), the first space of a line comment, and
        // leading and trailing empty lines are removed. Multiple empty lines are
        // reduced to one, and trailing space on lines is trimmed. Unless the result
        // is empty, it is newline-terminated.
        //
        public string Text
        {
            get
            {
                //if g == nil {
                //    return ""
                //}
                var comments = this.List.Select(c => c.Text);

                var lines = new List<string>(10); // most comments are less than 10 lines
                foreach (var _ in comments)
                {
                    var c = _;
                    // Remove comment markers.
                    // The parser has given us exactly the comment text.
                    switch (c[1])
                    {
                        case '/':
                            //-style comment (no newline at the end)
                            c = c.Substring(2);
                            // strip first space - required for Example tests
                            if (c.Length > 0 && c[0] == ' ')
                            {
                                c = c.Substring(1);
                            }
                            break;
                        case '*':
                            /*-style comment */
                            c = c.Substring(2, c.Length - 4);
                            break;
                    }

                    // Split on newlines.
                    var cl = c.Split('\n');

                    // Walk lines, stripping trailing white space and adding to list.
                    foreach (var l in cl)
                    {
                        lines.Add(Helper.StripTrailingWhitespace(l));
                    }
                }

                // Remove leading blank lines; convert runs of
                // interior blank lines to a single blank line.
                var n = 0;
                foreach (var line in lines)
                {
                    if (line != "" || n > 0 && lines[n - 1] != "")
                    {
                        lines[n] = line;
                        n++;
                    }
                }
                lines.Cut(n);

                // Add final "" entry to get trailing newline from Join.
                if (n > 0 && lines[n - 1] != "")
                {
                    lines.Add("");
                }

                return string.Join("\n", lines);
            }
        }
    }

    // ----------------------------------------------------------------------------
    // Expressions and types

    // A Field represents a Field declaration list in a struct type,
    // a method list in an interface type, or a parameter/result declaration
    // in a signature.
    //
    public class Field : Node
    {
        public CommentGroup Doc { get; set; } // associated documentation; or nil
        public Ident[] Names { get; set; } // field/method/parameter names; or nil if anonymous field
        public Expr Type { get; set; } // field/method/parameter type
        public BasicLit Tag { get; set; } // field tag; or nil
        public CommentGroup Comment { get; set; } // line comments; or nil

        public override int Pos
        {
            get { return this.Names.Length > 0 ? this.Names[0].Pos : this.Type.Pos; }
        }

        public override int End
        {
            get { return this.Tag != null ? this.Tag.End : this.Type.End; }
        }
    }

    // A FieldList represents a list of Fields, enclosed by parentheses or braces.
    public class FieldList : Node
    {
        public int Opening { get; set; } // position of opening parenthesis/brace, if any
        public Field[] List { get; set; } // field list; or nil
        public int Closing { get; set; } // position of closing parenthesis/brace, if any

        public override int Pos
        {
            get
            {
                if (Helper.IsValidPos(this.Opening))
                    return this.Opening;
                // the list should not be empty in this case;
                // be conservative and guard against bad ASTs
                if (this.List.Length > 0)
                    return this.List[0].Pos;
                return 0;
            }
        }

        public override int End
        {
            get
            {
                if (Helper.IsValidPos(this.Closing))
                    return this.Closing + 1;
                // the list should not be empty in this case;
                // be conservative and guard against bad ASTs
                var n = this.List.Length;
                if (n > 0)
                    return this.List[n - 1].End;
                return 0;
            }
        }

        // NumFields returns the number of (named and anonymous fields) in a FieldList.
        public int NumFields()
        {
            var n = 0;
            //if f != nil {
            foreach (var g in this.List)
            {
                var m = g.Names.Length;
                if (m == 0)
                {
                    m = 1; // anonymous field
                }
                n += m;
            }
            //}
            return n;
        }
    }

    // An expression is represented by a tree consisting of one
    // or more of the following concrete expression nodes.
    //
    // A BadExpr node is a placeholder for expressions containing
    // syntax errors for which no correct expression nodes can be
    // created.
    //
    public class BadExpr : Expr
    {
        // position range of bad expression
        public int From { get; set; }
        public int To { get; set; }

        public override int Pos
        {
            get { return this.From; }
        }

        public override int End
        {
            get { return this.To; }
        }
    }

    // An Ident node represents an identifier.
    public class Ident : Expr
    {
        public Ident() { }

        // NewIdent creates a new Ident without position.
        // Useful for ASTs generated by code other than the Go parser.
        //
        public Ident(string name)
        {
            this.NamePos = 0;
            this.Name = name;
        }

        public int NamePos { get; set; } // identifier position
        public string Name { get; set; } // identifier name
        public EntityObject Obj { get; set; } // denoted object; or nil

        public override int Pos
        {
            get { return this.NamePos; }
        }

        public override int End
        {
            get { return this.NamePos + this.Name.Length; }
        }

        // IsExported reports whether id is an exported Go symbol
        // (that is, whether it begins with an uppercase letter).
        public bool IsExported()
        {
            return Helper.IsExported(this.Name);
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    // An Ellipsis node stands for the "..." type in a
    // parameter list or the "..." length in an array type.
    //
    public class Ellipsis : Expr
    {
        public int EllipsisPos { get; set; } // position of "..."
        public Expr Elt { get; set; } // ellipsis element type (parameter lists only); or nil

        public override int Pos
        {
            get { return this.EllipsisPos; }
        }

        public override int End
        {
            get
            {
                if (this.Elt != null)
                {
                    return this.Elt.End;
                }
                return this.EllipsisPos + 3; // len("...")}
            }
        }
    }

    // A BasicLit node represents a literal of basic type.
    public class BasicLit : Expr
    {
        public int ValuePos { get; set; } // literal position
        public Token Kind { get; set; } // token.INT, token.FLOAT, token.IMAG, token.CHAR, or token.STRING
        public string Value { get; set; } // literal string; e.g. 42, 0x7f, 3.14, 1e-9, 2.4i, 'a', '\x7f', "foo" or `\m\n\o`

        public override int Pos
        {
            get { return this.ValuePos; }
        }

        public override int End
        {
            get { return this.ValuePos + this.Value.Length; }
        }
    }

    // A FuncLit node represents a function literal.
    public class FuncLit : Expr
    {
        public FuncType Type { get; set; } // function type
        public BlockStmt Body { get; set; } // function body

        public override int Pos
        {
            get { return this.Type.Pos; }
        }

        public override int End
        {
            get { return this.Body.End; }
        }
    }

    // A CompositeLit node represents a composite literal.
    public class CompositeLit : Expr
    {
        public Expr Type { get; set; } // literal type; or nil
        public int Lbrace { get; set; } // position of "{"
        public Expr[] Elts { get; set; } // list of composite elements; or nil
        public int Rbrace { get; set; } // position of "}"

        public override int Pos
        {
            get { return this.Type != null ? this.Type.Pos : this.Lbrace; }
        }

        public override int End
        {
            get { return this.Rbrace + 1; }
        }
    }

    // A ParenExpr node represents a parenthesized expression.
    public class ParenExpr : Expr
    {
        public int Lparen { get; set; } // position of "("
        public Expr X { get; set; } // parenthesized expression
        public int Rparen { get; set; } // position of ")"

        public override int Pos
        {
            get { return this.Lparen; }
        }

        public override int End
        {
            get { return this.Rparen + 1; }
        }
    }

    // A SelectorExpr node represents an expression followed by a selector.
    public class SelectorExpr : Expr
    {
        public Expr X { get; set; } // expression
        public Ident Sel { get; set; } // field selector

        public override int Pos
        {
            get { return this.X.Pos; }
        }

        public override int End
        {
            get { return this.Sel.End; }
        }
    }

    // An IndexExpr node represents an expression followed by an index.
    public class IndexExpr : Expr
    {
        public Expr X { get; set; } // expression
        public int Lbrack { get; set; } // position of "["
        public Expr Index { get; set; } // index expression
        public int Rbrack { get; set; } // position of "]"

        public override int Pos
        {
            get { return this.X.Pos; }
        }

        public override int End
        {
            get { return this.Rbrack + 1; }
        }
    }

    // An SliceExpr node represents an expression followed by slice indices.
    public class SliceExpr : Expr
    {
        public Expr X { get; set; } // expression
        public int Lbrack { get; set; } // position of "["
        public Expr Low { get; set; } // begin of slice range; or nil
        public Expr High { get; set; } // end of slice range; or nil
        public Expr Max { get; set; } // maximum capacity of slice; or nil
        public bool Slice3 { get; set; } // true if 3-index slice (2 colons present)
        public int Rbrack { get; set; } // position of "]"

        public override int Pos
        {
            get { return this.X.Pos; }
        }

        public override int End
        {
            get { return this.Rbrack + 1; }
        }
    }

    // A TypeAssertExpr node represents an expression followed by a
    // type assertion.
    //
    public class TypeAssertExpr : Expr
    {
        public Expr X { get; set; } // expression
        public int Lparen { get; set; } // position of "("
        public Expr Type { get; set; } // asserted type; nil means type switch X.(type)
        public int Rparen { get; set; } // position of ")"

        public override int Pos
        {
            get { return this.X.Pos; }
        }

        public override int End
        {
            get { return this.Rparen + 1; }
        }
    }

    // A CallExpr node represents an expression followed by an argument list.
    public class CallExpr : Expr
    {
        public Expr Fun { get; set; } // function expression
        public int Lparen { get; set; } // position of "("
        public Expr[] Args { get; set; } // function arguments; or nil
        public int Ellipsis { get; set; } // position of "...", if any
        public int Rparen { get; set; } // position of ")"

        public override int Pos
        {
            get { return this.Fun.Pos; }
        }

        public override int End
        {
            get { return this.Rparen + 1; }
        }
    }

    // A StarExpr node represents an expression of the form "*" Expression.
    // Semantically it could be a unary "*" expression, or a pointer type.
    //
    public class StarExpr : Expr
    {
        public int Star { get; set; } // position of "*"
        public Expr X { get; set; } // operand

        public override int Pos
        {
            get { return this.Star; }
        }

        public override int End
        {
            get { return this.X.End; }
        }
    }

    // A UnaryExpr node represents a unary expression.
    // Unary "*" expressions are represented via StarExpr nodes.
    //
    public class UnaryExpr : Expr
    {
        public int OpPos { get; set; } // position of Op
        public Token Op { get; set; } // operator
        public Expr X { get; set; } // operand

        public override int Pos
        {
            get { return this.OpPos; }
        }

        public override int End
        {
            get { return this.X.End; }
        }
    }

    // A BinaryExpr node represents a binary expression.
    public class BinaryExpr : Expr
    {
        public Expr X { get; set; } // left operand
        public int OpPos { get; set; } // position of Op
        public Token Op { get; set; } // operator
        public Expr Y { get; set; } // right operand

        public override int Pos
        {
            get { return this.X.Pos; }
        }

        public override int End
        {
            get { return this.Y.End; }
        }
    }

    // A KeyValueExpr node represents (key : value) pairs
    // in composite literals.
    //
    public class KeyValueExpr : Expr
    {
        public Expr Key { get; set; }
        public int Colon { get; set; } // position of ":"
        public Expr Value { get; set; }

        public override int Pos
        {
            get { return this.Key.Pos; }
        }

        public override int End
        {
            get { return this.Value.End; }
        }
    }

    // The direction of a channel type is indicated by one
    // of the following constants.
    //
    [Flags]
    public enum ChanDir
    {
        SEND = 1,
        RECV
    }

    // A type is represented by a tree consisting of one
    // or more of the following type-specific expression
    // nodes.
    //
    // An ArrayType node represents an array or slice type.
    public class ArrayType : Expr
    {
        public int Lbrack { get; set; } // position of "["
        public Expr Len { get; set; } // Ellipsis node for [...]T array types, nil for slice types
        public Expr Elt { get; set; } // element type

        public override int Pos
        {
            get { return this.Lbrack; }
        }

        public override int End
        {
            get { return this.Elt.End; }
        }
    }

    // public class A : ExprType node represents public class a : Expr type.
    public class StructType : Expr
    {
        public int Struct { get; set; } // position of "struct" keyword
        public FieldList Fields { get; set; } // list of field declarations
        public bool Incomplete { get; set; } // true if (source) fields are missing in the Fields list

        public override int Pos
        {
            get { return this.Struct; }
        }

        public override int End
        {
            get { return this.Fields.End; }
        }
    }

    // Pointer types are represented via StarExpr nodes.

    // A FuncType node represents a function type.
    public class FuncType : Expr
    {
        public int Func { get; set; } // position of "func" keyword (token.NoPos if there is no "func")
        public FieldList Params { get; set; } // (incoming) parameters; non-nil
        public FieldList Results { get; set; }  // (outgoing) results; or nil

        public override int Pos
        {
            get
            {
                if (Helper.IsValidPos(this.Func) || this.Params == null)
                { // see issue 3870
                    return this.Func;
                }
                return this.Params.Pos; // interface method declarations have no "func" keyword}
            }
        }

        public override int End
        {
            get { return this.Results != null ? this.Results.End : this.Params.End; }
        }
    }

    // An InterfaceType node represents an interface type.
    public class InterfaceType : Expr
    {
        public int Interface { get; set; } // position of "interface" keyword
        public FieldList Methods { get; set; } // list of methods
        public bool Incomplete { get; set; } // true if (source) methods are missing in the Methods list

        public override int Pos
        {
            get { return this.Interface; }
        }

        public override int End
        {
            get { return this.Methods.End; }
        }
    }

    // A MapType node represents a map type.
    public class MapType : Expr
    {
        public int Map { get; set; } // position of "map" keyword
        public Expr Key { get; set; }
        public Expr Value { get; set; }

        public override int Pos
        {
            get { return this.Map; }
        }

        public override int End
        {
            get { return this.Value.End; }
        }
    }

    // A ChanType node represents a channel type.
    public class ChanType : Expr
    {
        public int Begin { get; set; } // position of "chan" keyword or "<-" (whichever comes first)
        public int Arrow { get; set; } // position of "<-" (token.NoPos if there is no "<-")
        public ChanDir Dir { get; set; } // channel direction
        public Expr Value { get; set; } // value type

        public override int Pos
        {
            get { return this.Begin; }
        }

        public override int End
        {
            get { return this.Value.End; }
        }
    }

    // ----------------------------------------------------------------------------
    // Statements

    // A statement is represented by a tree consisting of one
    // or more of the following concrete statement nodes.
    //
    // A BadStmt node is a placeholder for statements containing
    // syntax errors for which no correct statement nodes can be
    // created.
    //
    public class BadStmt : Stmt
    {
        // position range of bad statement
        public int From { get; set; }
        public int To { get; set; }

        public override int Pos
        {
            get { return this.From; }
        }

        public override int End
        {
            get { return this.To; }
        }
    }

    // A DeclStmt node represents a declaration in a statement list.
    public class DeclStmt : Stmt
    {
        public Decl Decl { get; set; } // *GenDecl with CONST, TYPE, or VAR token

        public override int Pos
        {
            get { return this.Decl.Pos; }
        }

        public override int End
        {
            get { return this.Decl.End; }
        }
    }

    // An EmptyStmt node represents an empty statement.
    // The "position" of the empty statement is the position
    // of the immediately preceding semicolon.
    //
    public class EmptyStmt : Stmt
    {
        public int Semicolon { get; set; } // position of preceding ";"

        public override int Pos
        {
            get { return this.Semicolon; }
        }

        public override int End
        {
            get { return this.Semicolon + 1; /* len(";") */ }
        }
    }

    // A LabeledStmt node represents a labeled statement.
    public class LabeledStmt : Stmt
    {
        public Ident Label { get; set; }
        public int Colon { get; set; } // position of ":"
        public Stmt Stmt { get; set; }

        public override int Pos
        {
            get { return this.Label.Pos; }
        }

        public override int End
        {
            get { return this.Stmt.End; }
        }
    }

    // An ExprStmt node represents a (stand-alone) expression
    // in a statement list.
    //
    public class ExprStmt : Stmt
    {
        public Expr X { get; set; } // expression

        public override int Pos
        {
            get { return this.X.Pos; }
        }

        public override int End
        {
            get { return this.X.End; }
        }
    }

    // A SendStmt node represents a send statement.
    public class SendStmt : Stmt
    {
        public Expr Chan { get; set; }
        public int Arrow { get; set; } // position of "<-"
        public Expr Value { get; set; }

        public override int Pos
        {
            get { return this.Chan.Pos; }
        }

        public override int End
        {
            get { return this.Value.End; }
        }
    }

    // An IncDecStmt node represents an increment or decrement statement.
    public class IncDecStmt : Stmt
    {
        public Expr X { get; set; }
        public int TokPos { get; set; }   // position of Tok
        public Token Tok { get; set; } // INC or DEC

        public override int Pos
        {
            get { return this.X.Pos; }
        }

        public override int End
        {
            get { return this.TokPos + 2; /* len("++") */ }
        }
    }

    // An AssignStmt node represents an assignment or
    // a short variable declaration.
    //
    public class AssignStmt : Stmt
    {
        public Expr[] Lhs { get; set; }
        public int TokPos { get; set; }   // position of Tok
        public Token Tok { get; set; } // assignment token, DEFINE
        public Expr[] Rhs { get; set; }

        public override int Pos
        {
            get { return this.Lhs[0].Pos; }
        }

        public override int End
        {
            get { return this.Rhs[this.Rhs.Length - 1].End; }
        }
    }

    // A GoStmt node represents a go statement.
    public class GoStmt : Stmt
    {
        public int Go { get; set; } // position of "go" keyword
        public CallExpr Call { get; set; }

        public override int Pos
        {
            get { return this.Go; }
        }

        public override int End
        {
            get { return this.Call.End; }
        }
    }

    // A DeferStmt node represents a defer statement.
    public class DeferStmt : Stmt
    {
        public int Defer { get; set; } // position of "defer" keyword
        public CallExpr Call { get; set; }

        public override int Pos
        {
            get { return this.Defer; }
        }

        public override int End
        {
            get { return this.Call.End; }
        }
    }

    // A ReturnStmt node represents a return statement.
    public class ReturnStmt : Stmt
    {
        public int Return { get; set; } // position of "return" keyword
        public Expr[] Results { get; set; } // result expressions; or nil

        public override int Pos
        {
            get { return this.Return; }
        }

        public override int End
        {
            get
            {
                var n = this.Results.Length;
                if (n > 0)
                    return this.Results[n - 1].End;
                return this.Return + 6; // len("return")
            }
        }
    }

    // A BranchStmt node represents a break, continue, goto,
    // or fallthrough statement.
    //
    public class BranchStmt : Stmt
    {
        public int TokPos { get; set; }   // position of Tok
        public Token Tok { get; set; } // keyword token (BREAK, CONTINUE, GOTO, FALLTHROUGH)
        public Ident Label { get; set; } // label name; or nil

        public override int Pos
        {
            get { return this.TokPos; }
        }

        public override int End
        {
            get { return this.Label != null ? this.Label.End : this.TokPos + this.Tok.String().Length; }
        }
    }

    // A BlockStmt node represents a braced statement list.
    public class BlockStmt : Stmt
    {
        public int Lbrace { get; set; } // position of "{"
        public Stmt[] List { get; set; }
        public int Rbrace { get; set; } // position of "}"

        public override int Pos
        {
            get { return this.Lbrace; }
        }

        public override int End
        {
            get { return this.Rbrace + 1; }
        }
    }

    // An IfStmt node represents an if statement.
    public class IfStmt : Stmt
    {
        public int If { get; set; } // position of "if" keyword
        public Stmt Init { get; set; } // initialization statement; or nil
        public Expr Cond { get; set; } // condition
        public BlockStmt Body { get; set; }
        public Stmt Else { get; set; } // else branch; or nil

        public override int Pos
        {
            get { return this.If; }
        }

        public override int End
        {
            get { return this.Else != null ? this.Else.End : this.Body.End; }
        }
    }

    // A CaseClause represents a case of an expression or type switch statement.
    public class CaseClause : Stmt
    {
        public int Case { get; set; } // position of "case" or "default" keyword
        public Expr[] List { get; set; } // list of expressions or types; nil means default case
        public int Colon { get; set; } // position of ":"
        public Stmt[] Body { get; set; } // statement list; or nil

        public override int Pos
        {
            get { return this.Case; }
        }

        public override int End
        {
            get { return this.Body.Length > 0 ? this.Body[this.Body.Length - 1].End : this.Colon + 1; }
        }
    }

    // A SwitchStmt node represents an expression switch statement.
    public class SwitchStmt : Stmt
    {
        public int Switch { get; set; } // position of "switch" keyword
        public Stmt Init { get; set; } // initialization statement; or nil
        public Expr Tag { get; set; } // tag expression; or nil
        public BlockStmt Body { get; set; } // CaseClauses only

        public override int Pos
        {
            get { return this.Switch; }
        }

        public override int End
        {
            get { return this.Body.End; }
        }
    }

    // An TypeSwitchStmt node represents a type switch statement.
    public class TypeSwitchStmt : Stmt
    {
        public int Switch { get; set; } // position of "switch" keyword
        public Stmt Init { get; set; } // initialization statement; or nil
        public Stmt Assign { get; set; } // x := y.(type) or y.(type)
        public BlockStmt Body { get; set; } // CaseClauses only

        public override int Pos
        {
            get { return this.Switch; }
        }

        public override int End
        {
            get { return this.Body.End; }
        }
    }

    // A CommClause node represents a case of a select statement.
    public class CommClause : Stmt
    {
        public int Case { get; set; } // position of "case" or "default" keyword
        public Stmt Comm { get; set; } // send or receive statement; nil means default case
        public int Colon { get; set; } // position of ":"
        public Stmt[] Body { get; set; } // statement list; or nil

        public override int Pos
        {
            get { return this.Case; }
        }

        public override int End
        {
            get { return this.Body.Length > 0 ? this.Body[this.Body.Length - 1].End : this.Colon + 1; }
        }
    }

    // An SelectStmt node represents a select statement.
    public class SelectStmt : Stmt
    {
        public int Select { get; set; }  // position of "select" keyword
        public BlockStmt Body { get; set; } // CommClauses only

        public override int Pos
        {
            get { return this.Select; }
        }

        public override int End
        {
            get { return this.Body.End; }
        }
    }

    // A ForStmt represents a for statement.
    public class ForStmt : Stmt
    {
        public int For { get; set; } // position of "for" keyword
        public Stmt Init { get; set; } // initialization statement; or nil
        public Expr Cond { get; set; } // condition; or nil
        public Stmt Post { get; set; } // post iteration statement; or nil
        public BlockStmt Body { get; set; }

        public override int Pos
        {
            get { return this.For; }
        }

        public override int End
        {
            get { return this.Body.End; }
        }
    }

    // A RangeStmt represents a for statement with a range clause.
    public class RangeStmt : Stmt
    {
        public int For { get; set; }   // position of "for" keyword
        public Expr Key { get; set; }
        public Expr Value { get; set; } // Key, Value may be nil
        public int TokPos { get; set; }   // position of Tok; invalid if Key == nil
        public Token Tok { get; set; } // ILLEGAL if Key == nil, ASSIGN, DEFINE
        public Expr X { get; set; } // value to range over
        public BlockStmt Body { get; set; }

        public override int Pos
        {
            get { return this.For; }
        }

        public override int End
        {
            get { return this.Body.End; }
        }
    }

    // ----------------------------------------------------------------------------
    // Declarations

    // A Spec node represents a single (non-parenthesized) import,
    // constant, type, or variable declaration.
    //
    // The Spec type stands for any of *ImportSpec, *ValueSpec, and *TypeSpec.
    public abstract class Spec : Node
    {
        public virtual void specNode() { }
    }

    // An ImportSpec node represents a single package import.
    public class ImportSpec : Spec
    {
        public CommentGroup Doc { get; set; } // associated documentation; or nil
        public Ident Name { get; set; } // local package name (including "."); or nil
        public BasicLit Path { get; set; } // import path
        public CommentGroup Comment { get; set; } // line comments; or nil
        public int EndPos { get; set; } // end of spec (overrides Path.Pos if nonzero)

        public override int Pos
        {
            get { return this.Name != null ? this.Name.Pos : this.Path.Pos; }
        }

        public override int End
        {
            get { return this.EndPos != 0 ? this.EndPos : this.Path.End; }
        }
    }

    // A ValueSpec node represents a constant or variable declaration
    // (ConstSpec or VarSpec production).
    //
    public class ValueSpec : Spec
    {
        public CommentGroup Doc { get; set; } // associated documentation; or nil
        public Ident[] Names { get; set; } // value names (len(Names) > 0)
        public Expr Type { get; set; } // value type; or nil
        public Expr[] Values { get; set; } // initial values; or nil
        public CommentGroup Comment { get; set; } // line comments; or nil

        public override int Pos
        {
            get { return this.Names[0].Pos; }
        }

        public override int End
        {
            get
            {
                return this.Values.Length > 0 ? this.Values[this.Values.Length - 1].End
                    : this.Type != null ? this.Type.End
                    : this.Names[this.Names.Length - 1].End;
            }
        }
    }

    // A TypeSpec node represents a type declaration (TypeSpec production).
    public class TypeSpec : Spec
    {
        public CommentGroup Doc { get; set; } // associated documentation; or nil
        public Ident Name { get; set; } // type name
        public Expr Type { get; set; } // *Ident, *ParenExpr, *SelectorExpr, *StarExpr, or any of the *XxxTypes
        public CommentGroup Comment { get; set; } // line comments; or nil

        public override int Pos
        {
            get { return this.Name.Pos; }
        }

        public override int End
        {
            get { return this.Type.End; }
        }
    }


    // A declaration is represented by one of the following declaration nodes.
    //
    // A BadDecl node is a placeholder for declarations containing
    // syntax errors for which no correct declaration nodes can be
    // created.
    //
    public class BadDecl : Decl
    {
        // position range of bad declaration
        public int From { get; set; }
        public int To { get; set; }

        public override int Pos
        {
            get { return this.From; }
        }

        public override int End
        {
            get { return this.To; }
        }
    }

    // A GenDecl node (generic declaration node) represents an import,
    // constant, type or variable declaration. A valid Lparen position
    // (Lparen.Line > 0) indicates a parenthesized declaration.
    //
    // Relationship between Tok value and Specs element type:
    //
    //	token.public ImportSpec IMPORT {get;set;} //	token.public ValueSpec CONST {get;set;} //	token.public TypeSpec TYPE {get;set;} //	token.public ValueSpec VAR {get;set;} //
    public class GenDecl : Decl
    {
        public CommentGroup Doc { get; set; } // associated documentation; or nil
        public int TokPos { get; set; } // position of Tok
        public Token Tok { get; set; } // IMPORT, CONST, TYPE, VAR
        public int Lparen { get; set; } // position of '(', if any
        public Spec[] Specs { get; set; }
        public int Rparen { get; set; } // position of ')', if any

        public override int Pos
        {
            get { return this.TokPos; }
        }

        public override int End
        {
            get { return Helper.IsValidPos(this.Rparen) ? this.Rparen + 1 : this.Specs[0].End; }
        }
    }

    // A FuncDecl node represents a function declaration.
    public class FuncDecl : Decl
    {
        public CommentGroup Doc { get; set; } // associated documentation; or nil
        public FieldList Recv { get; set; } // receiver (methods); or nil (functions)
        public Ident Name { get; set; } // function/method name
        public FuncType Type { get; set; } // function signature: parameters, results, and position of "func" keyword
        public BlockStmt Body { get; set; } // function body; or nil (forward declaration)

        public override int Pos
        {
            get { return this.Type.Pos; }
        }

        public override int End
        {
            get { return this.Body != null ? this.Body.End : this.Type.End; }
        }
    }

    // ----------------------------------------------------------------------------
    // Files and packages

    // A File node represents a Go source file.
    //
    // The Comments list contains all comments in the source file in order of
    // appearance, including the comments that are pointed to from other nodes
    // via Doc and Comment fields.
    //
    public class FileNode : Node
    {
        public CommentGroup Doc { get; set; } // associated documentation; or nil
        public int Package { get; set; } // position of "package" keyword
        public Ident Name { get; set; } // package name
        public Decl[] Decls { get; set; } // top-level declarations; or nil
        public Scope Scope { get; set; } // package scope (this file only)
        public ImportSpec[] Imports { get; set; } // imports in this file
        public Ident[] Unresolved { get; set; } // unresolved identifiers in this file
        public CommentGroup[] Comments { get; set; } // list of all comments in the source file

        public override int Pos
        {
            get { return this.Package; }
        }

        public override int End
        {
            get { return this.Decls.Length > 0 ? this.Decls[this.Decls.Length - 1].End : this.Name.End; }
        }
    }

    // A Package node represents a set of source files
    // collectively building a Go package.
    //
    public class PackageNode : Node
    {
        public string Name { get; set; } // package name
        public Scope Scope { get; set; } // package scope across all files
        public Dictionary<string, EntityObject> Imports { get; set; } // map of package id -> package object
        public Dictionary<string, FileNode> Files { get; set; } // Go source files by filename

        public override int Pos
        {
            get { return 0; }
        }

        public override int End
        {
            get { return 0; }
        }
    }
}
