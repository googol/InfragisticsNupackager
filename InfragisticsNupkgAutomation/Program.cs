using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace InfragisticsNupkgAutomation
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

        static void Main()
        {
            if (!CanRun("NuGet.exe"))
            {
                Console.WriteLine("Cannot find NuGet.exe, won't continue");
                Console.ReadKey(true);
                return;
            }

            foreach (var path in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.v14.2.dll"))
            {
                var assembly = Assembly.ReflectionOnlyLoadFrom(path);
                var packageName = RemoveInfragisticsVersionTag(assembly.GetName().Name);
                Console.Write(packageName);

                var references = assembly.GetReferencedAssemblies().SelectMany(SelectReferenceKind).ToList();
                if (references.Any(reference => reference.Item1 == ReferenceKind.BrokenReference))
                {
                    Console.WriteLine(" One or more of the assembly's references could not be loaded: {0}", string.Join(", ", from r in references where r.Item1 == ReferenceKind.BrokenReference select r.Item2));
                    continue;
                }

                Console.Write(" Description: ");
                var description = Console.ReadLine();

                SavePackage(packageName, description, references, path);
            }

            long completedCount = 0;
            var failedSpecs = new List<string>();

            foreach (var nuspec in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.nuspec").Select(Path.GetFileName))
            {
                Console.Write("Packing {0}... ", nuspec);
                try
                {
                    var processStart = new ProcessStartInfo("NuGet.exe", string.Format("pack {0}", nuspec)) {CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden};
                    var process = Process.Start(processStart);
                    process.WaitForExit();
                    Console.WriteLine("Completed");
                    completedCount++;
                }
                catch (Exception)
                {
                    Console.WriteLine("Could not complete");
                }
            }

            Console.WriteLine();
            Console.WriteLine("{0} nuspecs packaged successfully.", completedCount);
            if (failedSpecs.Any())
                Console.WriteLine("Following nuspecs could not be packaged: {0}", string.Join(", ", failedSpecs));
            Console.ReadKey(true);
        }

        private static void SavePackage(string packageName, string description, List<Tuple<ReferenceKind, string>> references, string path)
        {
            var n = XNamespace.Get("http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd");
            var package = new XElement(n + "package",
                new XElement(n + "metadata",
                    new XElement(n + "id", packageName),
                    new XElement(n + "version", "14.2"),
                    new XElement(n + "authors", "Infragistics"),
                    new XElement(n + "description", description),
                    references.Any(r => r.Item1 == ReferenceKind.GacReference)
                        ? new XElement(n + "frameworkAssemblies",
                            from r in references
                            where r.Item1 == ReferenceKind.GacReference
                            select new XElement(n + "frameworkAssembly", new XAttribute("assemblyName", r.Item2), new XAttribute("targetFramework", "net40")))
                        : null,
                    references.Any(r => r.Item1 == ReferenceKind.LocalReference)
                        ? new XElement(n + "dependencies",
                            new XElement(n + "group",
                                new XAttribute("targetFramework", "net40"),
                                from r in references
                                where r.Item1 == ReferenceKind.LocalReference
                                select new XElement(n + "dependency", new XAttribute("id", r.Item2), new XAttribute("version", "14.2"))))
                        : null),
                new XElement(n + "files",
                    new XElement(n + "file", new XAttribute("src", Path.GetFileName(path)), new XAttribute("target", @"lib\net40"))));

            package.Save(packageName + ".14.2.nuspec");
        }

        private static IEnumerable<Tuple<ReferenceKind, string>> SelectReferenceKind(AssemblyName an)
        {
            try
            {
                var ass = Assembly.ReflectionOnlyLoad(an.FullName);
                if (!ass.GlobalAssemblyCache)
                    return Tuple.Create(ReferenceKind.LocalReference, RemoveInfragisticsVersionTag(an.Name)).ToEnumerable();

                return usefulGacReferences.Contains(an.Name)
                    ? Tuple.Create(ReferenceKind.GacReference, an.Name).ToEnumerable()
                    : Enumerable.Empty<Tuple<ReferenceKind, string>>();
            }
            catch (Exception)
            {
                return Tuple.Create(ReferenceKind.BrokenReference, an.Name).ToEnumerable();
            }
        }

        private static string RemoveInfragisticsVersionTag(string assemblyname)
        {
            return assemblyname.EndsWith(".v14.2") ? assemblyname.Remove(assemblyname.Length - 6) : assemblyname;
        }

        private static bool CanRun(string exeName)
        {
            try
            {
                var p = new Process
                {
                    StartInfo = {UseShellExecute = false, FileName = "where", Arguments = exeName, RedirectStandardOutput = true, RedirectStandardError = true}
                };
                p.Start();
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
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