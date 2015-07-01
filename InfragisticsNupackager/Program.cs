using NuGet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace InfragisticsNupackager
{
    class Program
    {
        private static readonly HashSet<string> usefulGacReferences = new HashSet<string>
        {
            "PresentationCore",
            "PresentationFramework",
            "System.Xaml",
            "WindowsBase",
        };

        private const string _targetFramework = "net40";

        static void Main()
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            // We only care about packaging Infragistics dlls
            var searchPattern = "Infragistics*.dll";

            foreach (var path in Directory.EnumerateFiles(currentDirectory, searchPattern, SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var assembly = Assembly.ReflectionOnlyLoadFrom(path);
                    var packageName = GetPackageName(assembly.GetName());
                    Console.Write("{0}... ", packageName);

                    var references = GetReferences(assembly).ToList();
                    if (references.Any(reference => reference.Type == ReferenceKind.BrokenReference))
                    {
                        Console.WriteLine("FAILED");
                        Console.WriteLine("One or more of the assembly's references could not be loaded: {0}", string.Join(", ", from r in references where r.Type == ReferenceKind.BrokenReference select r.Assembly.Name));
                        continue;
                    }

                    var metadata = GetPackageMetadata(path, references);
                    var manifestFiles = GetManifestFiles(path);

                    var packageBuilder = new PackageBuilder();

                    packageBuilder.Populate(metadata);
                    packageBuilder.PopulateFiles(currentDirectory, manifestFiles);

                    using (var fileStream = File.Open(GetPackagePath(metadata), FileMode.Create, FileAccess.ReadWrite))
                    {
                        packageBuilder.Save(fileStream);
                    }
                    Console.WriteLine("DONE");
                }
                catch(Exception e)
                {
                    Console.WriteLine("FAILED");
                    Console.WriteLine(e);
                    Console.WriteLine();
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished, press any key to quit.");

            Console.ReadKey(true);
        }

        private static string GetPackageName(AssemblyName assemblyName)
        {
            var versionTag = string.Format(".v{0}.{1}", assemblyName.Version.Major, assemblyName.Version.Minor);

            return RemoveVersionTag(assemblyName.Name, versionTag);
        }

        private static string RemoveVersionTag(string assemblyname, string versionTag)
        {
            return assemblyname.EndsWith(versionTag) ? assemblyname.Remove(assemblyname.Length - versionTag.Length) : assemblyname;
        }

        private static IEnumerable<Reference> GetReferences(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().SelectMany(SelectReference);
        }

        private static IEnumerable<Reference> SelectReference(AssemblyName an)
        {
            try
            {
                var ass = Assembly.ReflectionOnlyLoad(an.FullName);
                if (!ass.GlobalAssemblyCache)
                {
                    // We only care about Infragistics references
                    if(an.Name.StartsWith("infragistics", StringComparison.InvariantCultureIgnoreCase))
                        return new Reference { Type = ReferenceKind.LocalReference, Assembly = an }.ToEnumerable();
                }
                else if(usefulGacReferences.Contains(an.Name))
                {
                    return new Reference { Type = ReferenceKind.GacReference, Assembly = an }.ToEnumerable();
                }
                return Enumerable.Empty<Reference>();
            }
            catch (Exception)
            {
                return new Reference { Type = ReferenceKind.GacReference, Assembly = an }.ToEnumerable();
            }
        }

        private static ManifestMetadata GetPackageMetadata(string filepath, IReadOnlyList<Reference> references)
        {
            var assembly = Assembly.ReflectionOnlyLoadFrom(filepath);
            var assemblyName = assembly.GetName();

            var packageName = GetPackageName(assemblyName);

            var packageMetadata = new ManifestMetadata
            {
                Id = packageName,
                Authors = "Infragistics",
                Description = string.Format("This package contains the infragistics assembly {0}.", assemblyName.Name),
                Version = assemblyName.Version.ToString(),
            };

            var frameworkAssemblies = (
                from r in references
                where r.Type == ReferenceKind.GacReference
                select new ManifestFrameworkAssembly{ AssemblyName = r.Assembly.Name, TargetFramework = _targetFramework }
                ).ToList();
            var infragisticsDependencies = (
                from r in references
                where r.Type == ReferenceKind.LocalReference
                select new ManifestDependency { Id = GetPackageName(r.Assembly), Version = r.Assembly.Version.ToString() }
                ).ToList();

            if (infragisticsDependencies.Any())
                packageMetadata.DependencySets = new List<ManifestDependencySet> { new ManifestDependencySet { Dependencies = infragisticsDependencies, TargetFramework = _targetFramework } };

            if (frameworkAssemblies.Any())
                packageMetadata.FrameworkAssemblies = frameworkAssemblies;

            return packageMetadata;
        }

        private static IEnumerable<ManifestFile> GetManifestFiles(string filepath)
        {
            yield return new ManifestFile { Source = Path.GetFileName(filepath), Target = string.Format(@"lib\{0}", _targetFramework) };
        }

        private static string GetPackagePath(ManifestMetadata metadata)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), string.Format("{0}.{1}.nupkg", metadata.Id, metadata.Version));
        }
    }

    internal struct Reference
    {
        public ReferenceKind Type { get; set; }
        public AssemblyName Assembly { get; set; }
    }

    enum ReferenceKind
    {
        GacReference,
        LocalReference,
        BrokenReference
    }

    internal static class EnumerableExtensions
    {
        public static IEnumerable<T> ToEnumerable<T>(this T source)
        {
            return new[] {source};
        }
    }
}