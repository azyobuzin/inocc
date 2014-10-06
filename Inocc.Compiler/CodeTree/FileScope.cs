using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ast = Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.GoLib.Tokens;

namespace Inocc.Compiler.CodeTree
{
    public class FileScope
    {
        public FileScope(PackageScope pkg, Ast.FileNode node)
        {
            this.Package = pkg;
            this.Node = node;
            this.Scan1();
        }

        public PackageScope Package { get; private set; }
        public Ast.FileNode Node { get; private set; }

        public IReadOnlyList<Struct> Structs { get; private set; }

        private void Scan1()
        {
            this.Structs = this.Node.Decls.OfType<Ast.GenDecl>()
                .Where(d => d.Tok == Token.TYPE)
                .SelectMany(d => d.Specs.Cast<Ast.TypeSpec>()
                    .Select(s => Tuple.Create(s.Name.Name, s.Type as Ast.StructType))
                    .Where(t => t.Item2 != null)
                )
                .Select(t => new Struct(this, t.Item1, t.Item2))
                .ToArray();
        }
    }
}
