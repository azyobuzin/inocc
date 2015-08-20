using System;

namespace Inocc.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class GoPackageAttribute : Attribute
    {
        public GoPackageAttribute(string name, string path)
        {
            this.Name = name;
            this.Path = path;
        }

        public string Name { get; }
        public string Path { get; }
    }
}
