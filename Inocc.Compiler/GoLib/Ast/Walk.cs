using System;

namespace Inocc.Compiler.GoLib.Ast
{
    // A Visitor's Visit method is invoked for each node encountered by Walk.
    // If the result visitor w is not null, Walk visits each of the children
    // of node with the visitor w, followed by a call of w.Visit(null).
    public interface IVisitor
    {
        IVisitor Visit(Node node);
    }

    public delegate bool Inspector(Node node);

    public static class AstPackage
    {
        // Helper functions for common node lists. They may be empty.

        private static void walkIdentList(IVisitor v, Ident[] list)
        {
            foreach (var x in list)
            {
                Walk(v, x);
            }
        }

        private static void walkExprList(IVisitor v, Expr[] list)
        {
            foreach (var x in list)
            {
                Walk(v, x);
            }
        }

        private static void walkStmtList(IVisitor v, Stmt[] list)
        {
            foreach (var x in list)
            {
                Walk(v, x);
            }
        }

        private static void walkDeclList(IVisitor v, Decl[] list)
        {
            foreach (var x in list)
            {
                Walk(v, x);
            }
        }

        // TODO(gri): Investigate if providing a closure to Walk leads to
        //            simpler use (and may help eliminate Inspect in turn).

        // Walk traverses an AST in depth-first order: It starts by calling
        // v.Visit(node); node must not be null. If the visitor w returned by
        // v.Visit(node) is not null, Walk is invoked recursively with visitor
        // w for each of the non-null children of node, followed by a call of
        // w.Visit(null).
        //
        public static void Walk(IVisitor v, Node node)
        {
            v = v.Visit(node);
            if (v == null)
            {
                return;
            }

            // walk children
            // (the order of the cases matches the order
            // of the corresponding node types in ast.go)
            // Comments and fields
            if (node is Comment)
            {
                // nothing to do
            }
            else if (node is CommentGroup)
            {
                var n = node as CommentGroup;
                foreach (var c in n.List)
                {
                    Walk(v, c);
                }
            }
            else if (node is Field)
            {
                var n = node as Field;
                if (n.Doc != null)
                {
                    Walk(v, n.Doc);
                }
                walkIdentList(v, n.Names);
                Walk(v, n.Type);
                if (n.Tag != null)
                {
                    Walk(v, n.Tag);
                }
                if (n.Comment != null)
                {
                    Walk(v, n.Comment);
                }
            }
            else if (node is FieldList)
            {
                var n = node as FieldList;
                foreach (var f in n.List)
                {
                    Walk(v, f);
                }
            }
            // Expressions
            else if (node is BadExpr || node is Ident || node is BasicLit)
            {
                // nothing to do
            }
            else if (node is Ellipsis)
            {
                var n = node as Ellipsis;
                if (n.Elt != null)
                {
                    Walk(v, n.Elt);
                }
            }
            else if (node is FuncLit)
            {
                var n = node as FuncLit;
                Walk(v, n.Type);
                Walk(v, n.Body);
            }
            else if (node is CompositeLit)
            {
                var n = node as CompositeLit;
                if (n.Type != null)
                {
                    Walk(v, n.Type);
                }
                walkExprList(v, n.Elts);
            }
            else if (node is ParenExpr)
            {
                var n = node as ParenExpr;
                Walk(v, n.X);
            }
            else if (node is SelectorExpr)
            {
                var n = node as SelectorExpr;
                Walk(v, n.X);
                Walk(v, n.Sel);
            }
            else if (node is IndexExpr)
            {
                var n = node as IndexExpr;
                Walk(v, n.X);
                Walk(v, n.Index);
            }
            else if (node is SliceExpr)
            {
                var n = node as SliceExpr;
                Walk(v, n.X);
                if (n.Low != null)
                {
                    Walk(v, n.Low);
                }
                if (n.High != null)
                {
                    Walk(v, n.High);
                }
                if (n.Max != null)
                {
                    Walk(v, n.Max);
                }
            }
            else if (node is TypeAssertExpr)
            {
                var n = node as TypeAssertExpr;
                Walk(v, n.X);
                if (n.Type != null)
                {
                    Walk(v, n.Type);
                }
            }
            else if (node is CallExpr)
            {
                var n = node as CallExpr;
                Walk(v, n.Fun);
                walkExprList(v, n.Args);
            }
            else if (node is StarExpr)
            {
                var n = node as StarExpr;
                Walk(v, n.X);
            }
            else if (node is UnaryExpr)
            {
                var n = node as UnaryExpr;
                Walk(v, n.X);
            }
            else if (node is BinaryExpr)
            {
                var n = node as BinaryExpr;
                Walk(v, n.X);
                Walk(v, n.Y);
            }
            else if (node is KeyValueExpr)
            {
                var n = node as KeyValueExpr;
                Walk(v, n.Key);
                Walk(v, n.Value);
            }
            // Types
            else if (node is ArrayType)
            {
                var n = node as ArrayType;
                if (n.Len != null)
                {
                    Walk(v, n.Len);
                }
                Walk(v, n.Elt);
            }
            else if (node is StructType)
            {
                var n = node as StructType;
                Walk(v, n.Fields);
            }
            else if (node is FuncType)
            {
                var n = node as FuncType;
                if (n.Params != null)
                {
                    Walk(v, n.Params);
                }
                if (n.Results != null)
                {
                    Walk(v, n.Results);
                }
            }
            else if (node is InterfaceType)
            {
                var n = node as InterfaceType;
                Walk(v, n.Methods);
            }
            else if (node is MapType)
            {
                var n = node as MapType;
                Walk(v, n.Key);
                Walk(v, n.Value);
            }
            else if (node is ChanType)
            {
                var n = node as ChanType;
                Walk(v, n.Value);
            }
            // Statements
            else if (node is BadStmt)
            {
                // nothing to do
            }
            else if (node is DeclStmt)
            {
                var n = node as DeclStmt;
                Walk(v, n.Decl);
            }
            else if (node is EmptyStmt)
            {
                // nothing to do
            }
            else if (node is LabeledStmt)
            {
                var n = node as LabeledStmt;
                Walk(v, n.Label);
                Walk(v, n.Stmt);
            }
            else if (node is ExprStmt)
            {
                var n = node as ExprStmt;
                Walk(v, n.X);
            }
            else if (node is SendStmt)
            {
                var n = node as SendStmt;
                Walk(v, n.Chan);
                Walk(v, n.Value);
            }
            else if (node is IncDecStmt)
            {
                var n = node as IncDecStmt;
                Walk(v, n.X);
            }
            else if (node is AssignStmt)
            {
                var n = node as AssignStmt;
                walkExprList(v, n.Lhs);
                walkExprList(v, n.Rhs);
            }
            else if (node is GoStmt)
            {
                var n = node as GoStmt;
                Walk(v, n.Call);
            }
            else if (node is DeferStmt)
            {
                var n = node as DeferStmt;
                Walk(v, n.Call);
            }
            else if (node is ReturnStmt)
            {
                var n = node as ReturnStmt;
                walkExprList(v, n.Results);
            }
            else if (node is BranchStmt)
            {
                var n = node as BranchStmt;
                if (n.Label != null)
                {
                    Walk(v, n.Label);
                }
            }
            else if (node is BlockStmt)
            {
                var n = node as BlockStmt;
                walkStmtList(v, n.List);
            }
            else if (node is IfStmt)
            {
                var n = node as IfStmt;
                if (n.Init != null)
                {
                    Walk(v, n.Init);
                }
                Walk(v, n.Cond);
                Walk(v, n.Body);
                if (n.Else != null)
                {
                    Walk(v, n.Else);
                }
            }
            else if (node is CaseClause)
            {
                var n = node as CaseClause;
                walkExprList(v, n.List);
                walkStmtList(v, n.Body);
            }
            else if (node is SwitchStmt)
            {
                var n = node as SwitchStmt;
                if (n.Init != null)
                {
                    Walk(v, n.Init);
                }
                if (n.Tag != null)
                {
                    Walk(v, n.Tag);
                }
                Walk(v, n.Body);
            }
            else if (node is TypeSwitchStmt)
            {
                var n = node as TypeSwitchStmt;
                if (n.Init != null)
                {
                    Walk(v, n.Init);
                }
                Walk(v, n.Assign);
                Walk(v, n.Body);
            }
            else if (node is CommClause)
            {
                var n = node as CommClause;
                if (n.Comm != null)
                {
                    Walk(v, n.Comm);
                }
                walkStmtList(v, n.Body);
            }
            else if (node is SelectStmt)
            {
                var n = node as SelectStmt;
                Walk(v, n.Body);
            }
            else if (node is ForStmt)
            {
                var n = node as ForStmt;
                if (n.Init != null)
                {
                    Walk(v, n.Init);
                }
                if (n.Cond != null)
                {
                    Walk(v, n.Cond);
                }
                if (n.Post != null)
                {
                    Walk(v, n.Post);
                }
                Walk(v, n.Body);
            }
            else if (node is RangeStmt)
            {
                var n = node as RangeStmt;
                if (n.Key != null)
                {
                    Walk(v, n.Key);
                }
                if (n.Value != null)
                {
                    Walk(v, n.Value);
                }
                Walk(v, n.X);
                Walk(v, n.Body);
            }
            // Declarations
            else if (node is ImportSpec)
            {
                var n = node as ImportSpec;
                if (n.Doc != null)
                {
                    Walk(v, n.Doc);
                }
                if (n.Name != null)
                {
                    Walk(v, n.Name);
                }
                Walk(v, n.Path);
                if (n.Comment != null)
                {
                    Walk(v, n.Comment);
                }
            }
            else if (node is ValueSpec)
            {
                var n = node as ValueSpec;
                if (n.Doc != null)
                {
                    Walk(v, n.Doc);
                }
                walkIdentList(v, n.Names);
                if (n.Type != null)
                {
                    Walk(v, n.Type);
                }
                walkExprList(v, n.Values);
                if (n.Comment != null)
                {
                    Walk(v, n.Comment);
                }
            }
            else if (node is TypeSpec)
            {
                var n = node as TypeSpec;
                if (n.Doc != null)
                {
                    Walk(v, n.Doc);
                }
                Walk(v, n.Name);
                Walk(v, n.Type);
                if (n.Comment != null)
                {
                    Walk(v, n.Comment);
                }
            }
            else if (node is BadDecl)
            {
                // nothing to do
            }
            else if (node is GenDecl)
            {
                var n = node as GenDecl;
                if (n.Doc != null)
                {
                    Walk(v, n.Doc);
                }
                foreach (var s in n.Specs)
                {
                    Walk(v, s);
                }
            }
            else if (node is FuncDecl)
            {
                var n = node as FuncDecl;
                if (n.Doc != null)
                {
                    Walk(v, n.Doc);
                }
                if (n.Recv != null)
                {
                    Walk(v, n.Recv);
                }
                Walk(v, n.Name);
                Walk(v, n.Type);
                if (n.Body != null)
                {
                    Walk(v, n.Body);
                }
            }
            // Files and packages
            else if (node is FileNode)
            {
                var n = node as FileNode;
                if (n.Doc != null)
                {
                    Walk(v, n.Doc);
                }
                Walk(v, n.Name);
                walkDeclList(v, n.Decls);
                // don't walk n.Comments - they have been
                // visited already through the individual
                // nodes
            }
            else if (node is PackageNode)
            {
                var n = node as PackageNode;
                foreach (var f in n.Files.Values)
                {
                    Walk(v, f);
                }
            }
            else
            {
                throw new ArgumentException("ast.Walk: unexpected node type " + node.GetType().Name);
            }

            v.Visit(null);
        }

        private class InspectorVisitor : IVisitor
        {
            public InspectorVisitor(Inspector f)
            {
                this.f = f;
            }

            private readonly Inspector f;

            public IVisitor Visit(Node node)
            {
                if (f(node))
                {
                    return this;
                }
                return null;
            }
        }

        // Inspect traverses an AST in depth-first order: It starts by calling
        // f(node); node must not be nil. If f returns true, Inspect invokes f
        // recursively for each of the non-nil children of node, followed by a
        // call of f(nil).
        //
        public static void Inspect(Node node, Inspector f)
        {
            Walk(new InspectorVisitor(f), node);
        }
    }
}
