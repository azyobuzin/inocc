using Inocc.Compiler.GoLib.Ast;

namespace Inocc.Compiler
{
    public class AstVisitor<T>
    {
        public virtual T Visit(Node node) => node.Accept(this);

        public virtual T DefaultVisit(Node node) => default(T);

        public virtual T VisitExpr(Expr node) => this.DefaultVisit(node);

        public virtual T VisitStmt(Stmt node) => this.DefaultVisit(node);

        public virtual T VisitDecl(Decl node) => this.DefaultVisit(node);

        public virtual T VisitComment(Comment node) => this.DefaultVisit(node);

        public virtual T VisitCommentGroup(CommentGroup node) => this.DefaultVisit(node);

        public virtual T VisitField(Field node) => this.DefaultVisit(node);

        public virtual T VisitFieldList(FieldList node) => this.DefaultVisit(node);

        public virtual T VisitBadExpr(BadExpr node) => this.VisitExpr(node);

        public virtual T VisitIdent(Ident node) => this.VisitExpr(node);

        public virtual T VisitEllipsis(Ellipsis node) => this.VisitExpr(node);

        public virtual T VisitBasicLit(BasicLit node) => this.VisitExpr(node);

        public virtual T VisitFuncLit(FuncLit node) => this.VisitExpr(node);

        public virtual T VisitCompositeLit(CompositeLit node) => this.VisitExpr(node);

        public virtual T VisitParenExpr(ParenExpr node) => this.VisitExpr(node);

        public virtual T VisitSelectorExpr(SelectorExpr node) => this.VisitExpr(node);

        public virtual T VisitIndexExpr(IndexExpr node) => this.VisitExpr(node);

        public virtual T VisitSliceExpr(SliceExpr node) => this.VisitExpr(node);

        public virtual T VisitTypeAssertExpr(TypeAssertExpr node) => this.VisitExpr(node);

        public virtual T VisitCallExpr(CallExpr node) => this.VisitExpr(node);

        public virtual T VisitStarExpr(StarExpr node) => this.VisitExpr(node);

        public virtual T VisitUnaryExpr(UnaryExpr node) => this.VisitExpr(node);

        public virtual T VisitBinaryExpr(BinaryExpr node) => this.VisitExpr(node);

        public virtual T VisitKeyValueExpr(KeyValueExpr node) => this.VisitExpr(node);

        public virtual T VisitArrayType(ArrayType node) => this.VisitExpr(node);

        public virtual T VisitStructType(StructType node) => this.VisitExpr(node);

        public virtual T VisitFuncType(FuncType node) => this.VisitExpr(node);

        public virtual T VisitInterfaceType(InterfaceType node) => this.VisitExpr(node);

        public virtual T VisitMapType(MapType node) => this.VisitExpr(node);

        public virtual T VisitChanType(ChanType node) => this.VisitExpr(node);

        public virtual T VisitBadStmt(BadStmt node) => this.VisitStmt(node);

        public virtual T VisitDeclStmt(DeclStmt node) => this.VisitStmt(node);

        public virtual T VisitEmptyStmt(EmptyStmt node) => this.VisitStmt(node);

        public virtual T VisitLabeledStmt(LabeledStmt node) => this.VisitStmt(node);

        public virtual T VisitExprStmt(ExprStmt node) => this.VisitStmt(node);

        public virtual T VisitSendStmt(SendStmt node) => this.VisitStmt(node);

        public virtual T VisitIncDecStmt(IncDecStmt node) => this.VisitStmt(node);

        public virtual T VisitAssignStmt(AssignStmt node) => this.VisitStmt(node);

        public virtual T VisitGoStmt(GoStmt node) => this.VisitStmt(node);

        public virtual T VisitDeferStmt(DeferStmt node) => this.VisitStmt(node);

        public virtual T VisitReturnStmt(ReturnStmt node) => this.VisitStmt(node);

        public virtual T VisitBranchStmt(BranchStmt node) => this.VisitStmt(node);

        public virtual T VisitBlockStmt(BlockStmt node) => this.VisitStmt(node);

        public virtual T VisitIfStmt(IfStmt node) => this.VisitStmt(node);

        public virtual T VisitCaseClause(CaseClause node) => this.VisitStmt(node);

        public virtual T VisitSwitchStmt(SwitchStmt node) => this.VisitStmt(node);

        public virtual T VisitTypeSwitchStmt(TypeSwitchStmt node) => this.VisitStmt(node);

        public virtual T VisitCommClause(CommClause node) => this.VisitStmt(node);

        public virtual T VisitSelectStmt(SelectStmt node) => this.VisitStmt(node);

        public virtual T VisitForStmt(ForStmt node) => this.VisitStmt(node);

        public virtual T VisitRangeStmt(RangeStmt node) => this.VisitStmt(node);

        public virtual T VisitSpec(Spec node) => this.DefaultVisit(node);

        public virtual T VisitImportSpec(ImportSpec node) => this.VisitSpec(node);

        public virtual T VisitValueSpec(ValueSpec node) => this.VisitSpec(node);

        public virtual T VisitTypeSpec(TypeSpec node) => this.VisitSpec(node);

        public virtual T VisitBadDecl(BadDecl node) => this.VisitDecl(node);

        public virtual T VisitGenDecl(GenDecl node) => this.VisitDecl(node);

        public virtual T VisitFuncDecl(FuncDecl node) => this.VisitDecl(node);

        public virtual T VisitFile(FileNode node) => this.DefaultVisit(node);

        public virtual T VisitPackage(PackageNode node) => this.DefaultVisit(node);
    }
}
