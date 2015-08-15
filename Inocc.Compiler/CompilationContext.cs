using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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
            if (path.StartsWith("." + Path.DirectorySeparatorChar))
                return new DirectoryInfo(currentPackageDir.FullName + path.Substring(1));

            // "../hoge"
            if (path.StartsWith(".." + Path.DirectorySeparatorChar))
            {
                do
                {
                    currentPackageDir = currentPackageDir.Parent;
                    path = path.Substring(3);
                } while (path.StartsWith(".." + Path.DirectorySeparatorChar));

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

            throw new NotImplementedException();
        }
    }
}
