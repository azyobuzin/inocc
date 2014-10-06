using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.GoLib.Parsers;
using Inocc.Compiler.GoLib.Tokens;
using System.Reflection;
using System.Reflection.Emit;
using Inocc.Core;

namespace Inocc.Compiler
{
    public class Package
    {
        public Package(Type packageType)
        {
            this.PackageType = packageType;
            var attr = packageType.GetCustomAttribute<GoPackageAttribute>();
            this.Name = attr.Name;
            this.Path = attr.Path;
        }

        public Type PackageType { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }
    }
}
