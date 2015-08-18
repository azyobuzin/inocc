using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.PackageModel;

namespace Inocc.Compiler
{
    internal class DeclVisitor : AstVisitor<IEnumerable<Symbol>>
    {
        internal DeclVisitor(IReadOnlyDictionary<TypeSpec, TypeDeclaration> typeDic)
        {
            this.typeDic = typeDic;
        }

        private readonly IReadOnlyDictionary<TypeSpec, TypeDeclaration> typeDic;

        public override IEnumerable<Symbol> DefaultVisit(Node node)
        {
            throw new InvalidOperationException();
        }

        public override IEnumerable<Symbol> VisitGenDecl(GenDecl node)
        {
            return node.Specs.SelectMany(x => x.Accept(this));
        }

        public override IEnumerable<Symbol> VisitFuncDecl(FuncDecl node)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Symbol> VisitImportSpec(ImportSpec node)
        {
            return Enumerable.Empty<Symbol>();
        }

        public override IEnumerable<Symbol> VisitValueSpec(ValueSpec node)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Symbol> VisitTypeSpec(TypeSpec node)
        {
            var decl = this.typeDic[node];
            //TODO: 中身を解決
            return new[] { decl };
        }
    }
}
