using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inocc.Compiler.GoLib.Ast;
using Inocc.Compiler.PackageModel;

namespace Inocc.Compiler
{
    public class CompilationContext
    {
        public CompilationContext(GoEnvironment env, DirectoryInfo workingDir)
        {
            this.Environment = env;
            this.WorkingDirectory = workingDir;
        }

        public GoEnvironment Environment { get; }
        public DirectoryInfo WorkingDirectory { get; private set; }

        private readonly List<InoccError> errors = new List<InoccError>();
        public IReadOnlyList<InoccError> Errors => this.errors;

        private readonly List<Package> packages = new List<Package>();

        public void AddPackage(Package pkg)
        {
            this.packages.Add(pkg);
        }

        public Package GetPackage(string path)
        {
            return this.packages.FirstOrDefault(p => p.Path == path);
        }

        private readonly HashSet<string> compilingPackages = new HashSet<string>();

        public Package Require(DirectoryInfo currentPackageDir, string path)
        {
            var pkg = this.GetPackage(path);
            if (pkg != null) return pkg;

            pkg = this.CompilePackage(currentPackageDir, path);
            this.AddPackage(pkg);
            return pkg;
        }

        private DirectoryInfo ResolvePath(DirectoryInfo currentPackageDir, string path)
        {
            path = path.Replace('/', Path.DirectorySeparatorChar);

            // "./hoge"
            if (path.StartsWith("." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return new DirectoryInfo(currentPackageDir.FullName + path.Substring(1));

            // "../hoge"
            if (path.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                do
                {
                    currentPackageDir = currentPackageDir.Parent;
                    path = path.Substring(3);
                } while (path.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));

                return new DirectoryInfo(Path.Combine(currentPackageDir.FullName, path));
            }

            // GOPATH
            var d = new DirectoryInfo(Path.Combine(this.Environment.Path.FullName, "src", path));
            if (d.Exists) return d;

            // GOROOT
            d = new DirectoryInfo(Path.Combine(this.Environment.Root.FullName, "src", path));
            if (d.Exists) return d;
            d = new DirectoryInfo(Path.Combine(this.Environment.Root.FullName, "src", "pkg", path));
            if (d.Exists) return d;

            return null;
        }

        public Package CompilePackage(DirectoryInfo currentPackageDir, string path)
        {
            if (!this.compilingPackages.Add(path))
                throw new InvalidOperationException("import cycle not allowed");

            try
            {
                return this.CompileFilesAsPackage(path,
                    this.ResolvePath(currentPackageDir, path).EnumerateFiles("*.go"));
            }
            finally
            {
                this.compilingPackages.Remove(path);
            }
        }

        public Package CompileFilesAsPackage(string path, IEnumerable<FileInfo> files)
        {
            var parsed = files.Select(GoLanguageServices.Parse).ToArray();

            var errors = parsed.SelectMany(pr => pr.Errors).ToArray();
            if (errors.Length > 0)
            {
                this.errors.AddRange(errors);
                return null;
            }

            foreach (var pr in parsed)
                foreach (var import in pr.CompilationUnit.Imports)
                    this.Require(pr.File.Directory, import.Path.Value);

            var types = parsed
                .SelectMany(pr =>
                    pr.CompilationUnit.Decls.OfType<GenDecl>()
                        .SelectMany(decl =>
                            decl.Specs.OfType<TypeSpec>()
                                .Select(x => new TypeDeclaration(x)))
                )
                .ToArray();

            IReadOnlyDictionary<TypeSpec, TypeDeclaration> typesDic =
                types.ToDictionary(x => x.Spec);

            foreach (var pr in parsed)
            {
                IReadOnlyDictionary<string, Symbol> env =
                    pr.CompilationUnit.Imports
                    .Select(import =>
                    {
                        var pkg = this.Require(pr.File.Directory, import.Path.Value);
                        string key;
                        if (import.Name == null)
                            key = pkg.Name;
                        else if (import.Name.Name == ".")
                            throw new NotImplementedException();
                        else
                            key = import.Name.Name;
                        return new Tuple<string, Symbol>(key, pkg);
                    })
                    .Concat(
                        types.Select(x => new Tuple<string, Symbol>(x.Spec.Name.Name, x))
                    )
                    .ToDictionary(x => x.Item1, x => x.Item2);
            }

            throw new NotImplementedException();
        }
    }
}
