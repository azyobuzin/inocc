using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.GoLib.Scanners;
using Inocc.Compiler.GoLib.Tokens;

namespace Inocc.Compiler.GoLib.Parsers
{
    // A Mode value is a set of flags (or 0).
    // They control the amount of source code parsed and other optional
    // parser functionality.
    //
    [Flags]
    public enum Mode : uint
    {
        Zero,
        PackageClauseOnly, // stop parsing after package clause
        ImportsOnly, // stop parsing after import declarations
        ParseComments, // parse comments and add them to AST
        Trace, // print a trace of parsed productions
        DeclarationErrors, // report declaration errors
        SpuriousErrors, // same as AllErrors, for backward-compatibility
        AllErrors = SpuriousErrors // report all errors (not just the first 10 on different lines)
    }

    public static class Parser
    {
        // If src != nil, readSource converts src to a []byte if possible;
        // otherwise it returns an error. If src == nil, readSource returns
        // the result of reading the file specified by filename.
        //
        private static byte[] readSource(string filename, object src)
        {
            if (src != null)
            {
                if (src is string)
                    return Encoding.UTF8.GetBytes(((string)src));
                if (src is byte[])
                    return (byte[])src;
                if (src is Stream)
                {
                    using (var ms = new MemoryStream())
                    {
                        var s = (Stream)src;
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
                throw new ArgumentException("invalid source");
            }
            return System.IO.File.ReadAllBytes(filename);
        }

        // ParseFile parses the source code of a single Go source file and returns
        // the corresponding ast.File node. The source code may be provided via
        // the filename of the source file, or via the src parameter.
        //
        // If src != nil, ParseFile parses the source from src and the filename is
        // only used when recording position information. The type of the argument
        // for the src parameter must be string, []byte, or io.Reader.
        // If src == nil, ParseFile parses the file specified by filename.
        //
        // The mode parameter controls the amount of source text parsed and other
        // optional parser functionality. Position information is recorded in the
        // file set fset.
        //
        // If the source couldn't be read, the returned AST is nil and the error
        // indicates the specific failure. If the source was read but syntax
        // errors were found, the result is a partial AST (with ast.Bad* nodes
        // representing the fragments of erroneous source code). Multiple errors
        // are returned via a scanner.ErrorList which is sorted by file position.
        //
        public static Tuple<FileNode, ErrorList> ParseFile(FileSet fset, string filename, object src, Mode mode)
        {
            // get source
            var text = readSource(filename, src);

            var p = new parser();
            FileNode f = null;
            ErrorList err = null;

            try
            {
                // parse source
                p.init(fset, filename, text, mode);
                f = p.parseFile();
            }
            catch (parser.Bailout) { }

            // set result values
            if (f == null)
            {
                // source is not a valid Go source file - satisfy
                // ParseFile API and return a valid (but) empty
                // *ast.File
                f = new FileNode
                {
                    Name = new Ident(),
                    Scope = new Scope(null)
                };
            }

            p.errors.Sort();
            err = p.errors;

            return Tuple.Create(f, err);
        }

        // ParseDir calls ParseFile for all files with names ending in ".go" in the
        // directory specified by path and returns a map of package name -> package
        // AST with all the packages found.
        //
        // If filter != nil, only the files with os.FileInfo entries passing through
        // the filter (and ending in ".go") are considered. The mode bits are passed
        // to ParseFile unchanged. Position information is recorded in fset.
        //
        // If the directory couldn't be read, a nil map and the respective error are
        // returned. If a parse error occurred, a non-nil but incomplete map and the
        // first error encountered are returned.
        //
        public static Tuple<IReadOnlyDictionary<string, PackageNode>, ErrorList> ParseDir(FileSet fset, string path, Func<FileInfo, bool> filter, Mode mode)
        {
            ErrorList first = null;
            var list = new DirectoryInfo(path).EnumerateFiles();
            var pkgs = new Dictionary<string, PackageNode>();
            foreach (var d in list)
            {
                if (d.Name.EndsWith(".go") && (filter == null || filter(d)))
                {
                    var filename = d.FullName;
                    var t = ParseFile(fset, filename, null, mode);
                    var src = t.Item1;
                    var err = t.Item2;
                    if (err == null)
                    {
                        var name = src.Name.Name;
                        PackageNode pkg;
                        if (!pkgs.TryGetValue(name, out pkg))
                        {
                            pkg = new PackageNode
                            {
                                Name = name,
                                Files = new Dictionary<string, FileNode>()
                            };
                            pkgs[name] = pkg;
                        }
                        pkg.Files[filename] = src;
                    }
                    else if (first == null)
                    {
                        first = err;
                    }
                }
            }

            return new Tuple<IReadOnlyDictionary<string, PackageNode>, ErrorList>(pkgs, first);
        }

        // ParseExpr is a convenience function for obtaining the AST of an expression x.
        // The position information recorded in the AST is undefined. The filename used
        // in error messages is the empty string.
        //
        public static Tuple<Expr, ErrorList> ParseExpr(string x)
        {
            var p = new parser();
            p.init(new FileSet(), "", Encoding.UTF8.GetBytes(x), 0);

            // Set up pkg-level scopes to avoid nil-pointer errors.
            // This is not needed for a correct expression x as the
            // parser will be ok with a nil topScope, but be cautious
            // in case of an erroneous x.
            p.openScope();
            p.pkgScope = p.topScope;
            var e = p.parseRhsOrType();
            p.closeScope();
            parser.assert(p.topScope == null, "unbalanced scopes");

            // If a semicolon was inserted, consume it;
            // report an error if there's more tokens.
            if (p.tok == Token.SEMICOLON && p.lit == "\n")
            {
                p.next();
            }
            p.expect(Token.EOF);

            if (p.errors.Len() > 0)
            {
                p.errors.Sort();
                return new Tuple<Expr, ErrorList>(null, p.errors);
            }

            return new Tuple<Expr, ErrorList>(e, null);
        }
    }
}
