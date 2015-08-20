using System;

namespace Inocc.Core
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class GoAliasAttribute : Attribute
    {
        public GoAliasAttribute(Type baseType)
        {
            this.BaseType = baseType;
        }

        public Type BaseType { get; }
    }
}
