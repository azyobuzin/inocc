using System.Collections.Generic;
using System.Text;

namespace Inocc.Compiler.GoLib.Ast
{
    // A Scope maintains the set of named language entities declared
    // in the scope and a link to the immediately surrounding (outer)
    // scope.
    //
    public class Scope
    {
        public Scope() { }

        // NewScope creates a new scope nested in the outer scope.
        public Scope(Scope outer)
        {
            this.Outer = outer;
            this.Objects = new Dictionary<string, EntityObject>();
        }

        public Scope Outer { get; set; }
        public Dictionary<string, EntityObject> Objects { get; set; }

        // Lookup returns the object with the given name if it is
        // found in scope s, otherwise it returns nil. Outer scopes
        // are ignored.
        //
        public EntityObject Lookup(string name)
        {
            EntityObject value;
            return this.Objects.TryGetValue(name, out value) ? value : null;
        }


        // Insert attempts to insert a named object obj into the scope s.
        // If the scope already contains an object alt with the same name,
        // Insert leaves the scope unchanged and returns alt. Otherwise
        // it inserts obj and returns nil.
        //
        public EntityObject Insert(EntityObject obj)
        {
            EntityObject alt;
            if (!this.Objects.TryGetValue(obj.Name, out alt) || alt == null)
            {
                this.Objects[obj.Name] = obj;
            }
            return alt;
        }

        // Debugging support
        public override string ToString()
        {
            var buf = new StringBuilder();
            //buf.AppendFormat("scope {0} {{", this);
            buf.Append("scope {");
            if (/*s != nil &&*/ this.Objects.Count > 0)
            {
                buf.AppendLine();
                foreach (var obj in this.Objects.Values)
                {
                    buf.AppendFormat("\t{0} {1}\n", obj.Kind, obj.Name);
                }
            }
            buf.AppendLine("}");
            return buf.ToString();
        }
    }

    // ----------------------------------------------------------------------------
    // Objects

    // An Object describes a named language entity such as a package,
    // constant, type, variable, function (incl. methods), or label.
    //
    // The Data fields contains object-specific data:
    //
    //	Kind    Data type         Data value
    //	Pkg	*types.Package    package scope
    //	Con     int               iota for the respective declaration
    //	Con     != nil            constant value
    //	Typ     *Scope            (used as method scope during type checking - transient)
    //
    // type Object
    public class EntityObject
    {
        public EntityObject() { }

        // NewObj creates a new object of a given kind and name.
        public EntityObject(ObjKind kind, string name)
        {
            this.Kind = kind;
            this.Name = name;
        }

        public ObjKind Kind { get; set; }
        public string Name { get; set; }
        public object Decl { get; set; }
        public object Data { get; set; }
        public object Type { get; set; }

        // Pos computes the source position of the declaration of an object name.
        // The result may be an invalid position if it cannot be computed
        // (obj.Decl may be nil or not correct).
        public int Pos
        {
            get
            {
                if (this.Decl == null) return 0;
                if (this.Decl is Field)
                {
                    foreach (var n in (this.Decl as Field).Names)
                    {
                        if (n.Name == this.Name) return n.Pos;
                    }
                }
                else if (this.Decl is ImportSpec)
                {
                    var d = this.Decl as ImportSpec;
                    if (d.Name != null && d.Name.Name == this.Name)
                        return d.Name.Pos;
                    return d.Path.Pos;
                }
                else if (this.Decl is ValueSpec)
                {
                    foreach (var n in (this.Decl as ValueSpec).Names)
                    {
                        if (n.Name == this.Name) return n.Pos;
                    }
                }
                else if (this.Decl is TypeSpec)
                {
                    var d = this.Decl as TypeSpec;
                    if (d.Name.Name == this.Name)
                        return d.Name.Pos;
                }
                else if (this.Decl is FuncDecl)
                {
                    var d = this.Decl as FuncDecl;
                    if (d.Name.Name == this.Name)
                        return d.Name.Pos;
                }
                else if (this.Decl is LabeledStmt)
                {
                    var d = this.Decl as LabeledStmt;
                    if (d.Label.Name == this.Name)
                        return d.Label.Pos;
                }
                else if (this.Decl is AssignStmt)
                {
                    foreach (var x in (this.Decl as AssignStmt).Lhs)
                    {
                        var ident = x as Ident;
                        if (ident != null && ident.Name == this.Name)
                            return ident.Pos;
                    }
                }
                else if (this.Decl is Scope)
                {
                    // predeclared object - nothing to do for now
                }
                return 0;
            }
        }
    }

    public enum ObjKind
    {
        Bad, // for error handling
        Pkg, // package
        Con, // constant
        Typ, // type
        Var, // variable
        Fun, // function or method
        Lbl  // label
    }
}
