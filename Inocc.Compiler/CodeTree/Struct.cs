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
    public class Struct
    {
        public Struct(FileScope file, string name, Ast.StructType node)
        {
            this.File = file;
            this.Name = name;
            this.Node = node;
            this.Type = this.Package.PackageType.DefineNestedType(
                name,
                TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable
                    | (char.IsUpper(name, 0) ? TypeAttributes.Public : TypeAttributes.NotPublic),
                typeof(ValueType)
            );
        }

        public FileScope File { get; private set; }
        public string Name { get; private set; }
        public Ast.StructType Node { get; private set; }
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
