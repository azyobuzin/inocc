using System;

namespace Inocc.Core
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class GoArrayAttribute : Attribute
    {
        public GoArrayAttribute(Type type, int size)
        {
            this.Type = type;
            this.Size = size;
        }

        public Type Type { get; }
        public int Size { get; }
    }
}
