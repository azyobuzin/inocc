using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inocc.Compiler.CodeTree
{
    public class LocalScope
    {
        public LocalScope(LocalScope parent)
        {
            this.Symbols = new SymbolTable(parent.Symbols);
        }

        public SymbolTable Symbols { get; private set; }
    }
}
