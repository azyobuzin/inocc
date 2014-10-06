using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using Ast = Inocc.Compiler.GoLib.Ast;

namespace Inocc.Compiler.CodeTree
{
    public class PackageScope
    {
        public PackageScope(CompilationContext ctx, ModuleBuilder mb, string path, IEnumerable<ParseResult> files)
        {
            this.CompilationContext = ctx;
            this.moduleBuilder = mb;
            this.Path = path;
            this.Scan1();
        }

        private readonly IEnumerable<ParseResult> files;
        private readonly ModuleBuilder moduleBuilder;

        public CompilationContext CompilationContext { get; private set; }
        public IReadOnlyList<FileScope> Files { get; private set; }
        public string Path { get; private set; }
        public string Name { get; private set; }
        public TypeBuilder PackageType { get; private set; }

        private void Scan1()
        {
            var f = this.Files[0];
            this.Name = f.Node.Name.Name;

            this.PackageType = this.moduleBuilder.DefineType(
                this.Path.Replace('.', '_').Replace('/', '.') + "." + this.Name.Replace('.', '_'),
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed
            );

            //TODO: GoPackageAttribute

            this.Files = this.files.Select(pr => new FileScope(this, pr.CompilationUnit)).ToArray();
        }
    }
}
