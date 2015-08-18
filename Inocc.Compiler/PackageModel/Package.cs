using System;
using System.Reflection;
using Inocc.Core;

namespace Inocc.Compiler.PackageModel
{
    public class Package : Symbol
    {
        public Package(Type packageType)
        {
            this.PackageType = packageType;
            var attr = packageType.GetCustomAttribute<GoPackageAttribute>();
            this.Name = attr.Name;
            this.Path = attr.Path;
        }

        public Type PackageType { get; }
        public string Name { get; }
        public string Path { get; }
    }
}
