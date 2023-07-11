using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuKeeper.Abstractions;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.RepositoryInspection;

namespace NuKeeper.Inspection.RepositoryInspection
{
    public class NuspecFileReader : IPackageReferenceFinder
    {
        private readonly INuKeeperLogger _logger;
        private readonly PackageInProjectReader _packageInProjectReader;

        public NuspecFileReader(INuKeeperLogger logger)
        {
            _logger = logger;
            _packageInProjectReader = new PackageInProjectReader(logger);
        }

        public IReadOnlyCollection<PackageInProject> ReadFile(string baseDirectory, string relativePath)
        {
            var packagePath = new PackagePath(baseDirectory, relativePath, PackageReferenceType.Nuspec);
            try
            {
                using (var fileContents = File.OpenRead(packagePath.FullName))
                {
                    return Read(fileContents, packagePath);
                }
            }
            catch (IOException ex)
            {
                throw new NuKeeperException($"Unable to parse file {packagePath.FullName}", ex);
            }
        }

        public IReadOnlyCollection<string> GetFilePatterns()
        {
            return new[] { "*.nuspec" };
        }

        public IReadOnlyCollection<PackageInProject> Read(Stream fileContents, PackagePath path)
        {
            var xml = XDocument.Load(fileContents);
            var ns = xml.Root?.GetDefaultNamespace();
            var packagesNode = string.IsNullOrEmpty(ns.NamespaceName) ? xml.Element("package")?.Element("metadata")?.Element("dependencies") :
                xml.Element(ns + "package")?.Element(ns + "metadata")?.Element(ns + "dependencies");
            if (packagesNode == null)
            {
                return Array.Empty<PackageInProject>();
            }

            var packageNodeList = string.IsNullOrEmpty(ns.NamespaceName) ? packagesNode.Elements("dependency").Concat(packagesNode.Elements("group").Elements("dependency")) :
                packagesNode.Elements(ns + "dependency").Concat(packagesNode.Elements(ns + "group").Elements(ns + "dependency"));
            if (packageNodeList == null)
            {
                return Array.Empty<PackageInProject>();
            }

            return packageNodeList
                .Select(el => XmlToPackage(el, path))
                .Where(el => el != null)
                .ToList();
        }

        private PackageInProject XmlToPackage(XElement el, PackagePath path)
        {
            var id = el.Attribute("id")?.Value;
            var version = el.Attribute("version")?.Value;

            return _packageInProjectReader.Read(id, version, path, null);
        }
    }
}
