using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ast = Inocc.Compiler.GoLib.Ast;
using System.Reflection.Emit;
using System.Reflection;

namespace Inocc.Compiler.CodeTree
{
    public class Interface
    {
        public Interface(FileScope file, string name, Ast.InterfaceType node)
        {
            this.File = file;
            this.Name = name;
            this.Node = node;
            this.Type = this.Package.PackageType.DefineNestedType(
                name,
                TypeAttributes.Interface | TypeAttributes.Abstract
                    | (char.IsUpper(name, 0) ? TypeAttributes.Public : TypeAttributes.NotPublic)
            );
        }

        public FileScope File { get; private set; }
        public string Name { get; private set; }
        public Ast.InterfaceType Node { get; private set; }
        public TypeBuilder Type { get; private set; }

        public PackageScope Package
        {
            get
            {
                return this.File.Package;
            }
        }
    }
}
