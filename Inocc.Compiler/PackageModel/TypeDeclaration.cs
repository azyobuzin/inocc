using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inocc.Compiler.GoLib.Ast;

namespace Inocc.Compiler.PackageModel
{
    public class TypeDeclaration : Symbol
    {
        public TypeDeclaration(TypeSpec spec)
        {
            this.Spec = spec;
        }
        
        public TypeSpec Spec { get; }
    }
}
