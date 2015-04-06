using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inocc.Compiler.CodeTree
{
    /*public class SymbolTable
    {
        public SymbolTable()
        {

        }

        public SymbolTable(SymbolTable parent)
        {
            this.Parent = parent;
        }

        private readonly Dictionary<string, Expression> dict = new Dictionary<string, Expression>();

        public SymbolTable Parent { get; private set; }

        public void Add(string key, Expression expr)
        {
            this.dict.Add(key, expr);
        }

        public Expression this[string key]
        {
            get
            {
                Expression expr;
                if (this.dict.TryGetValue(key, out expr))
                    return expr;
                return this.Parent != null ? this.Parent[key] : null;
            }
        }

        public bool ContainsLocally(string key)
        {
            return this.dict.ContainsKey(key);
        }

        public static SymbolTable CreateBuiltin()
        {
            return new SymbolTable(); //TODO
        }
    }*/
}
