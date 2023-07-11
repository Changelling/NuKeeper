using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Versioning;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.NuGet;
using NuKeeper.Abstractions.RepositoryInspection;

namespace NuKeeper.Update.Process
{
    public class UpdateNuspecCommand : IUpdateNuspecCommand
    {
        private readonly INuKeeperLogger _logger;

        public UpdateNuspecCommand(INuKeeperLogger logger)
        {
            _logger = logger;
        }

        public Task Invoke(PackageInProject currentPackage,
            NuGetVersion newVersion, PackageSource packageSource, NuGetSources allSources)
        {
            if (currentPackage == null)
            {
                throw new ArgumentNullException(nameof(currentPackage));
            }

            if (newVersion == null)
            {
                throw new ArgumentNullException(nameof(newVersion));
            }

            XDocument xml;
            using (var xmlInput = File.OpenRead(currentPackage.Path.FullName))
            {
                xml = XDocument.Load(xmlInput);
            }

            using (var xmlOutput = File.Open(currentPackage.Path.FullName, FileMode.Truncate))
            {
                UpdateNuspec(xmlOutput, newVersion, currentPackage, xml);
            }

            return Task.CompletedTask;
        }

        private void UpdateNuspec(Stream fileContents, NuGetVersion newVersion,
            PackageInProject currentPackage, XDocument xml)
        {
            var ns = xml.Root?.GetDefaultNamespace();
            var packagesNode = (ns == null) ? xml.Element("package")?.Element("metadata") : xml.Element(ns + "package")?.Element(ns + "metadata");
            if (packagesNode == null)
            {
                return;
            }

            //dependency update
            var dependencyNode = string.IsNullOrEmpty(ns.NamespaceName) ? packagesNode?.Element("dependencies"): packagesNode?.Element(ns + "dependencies");
            if (dependencyNode == null)
            {
                return;
            }

            var packageNodeList = string.IsNullOrEmpty(ns.NamespaceName) ? dependencyNode.Elements("dependency").Concat(dependencyNode.Elements("group").Elements("dependency")).Where(x=> x.Attributes("id").Any(a => a.Value == currentPackage.Id)) :
                dependencyNode.Elements(ns + "dependency").Concat(dependencyNode.Elements(ns + "group").Elements(ns + "dependency")).Where(x => x.Attributes("id").Any(a => a.Value == currentPackage.Id));               

            foreach (var dependencyToUpdate in packageNodeList)
            {
                _logger.Detailed($"Updating nuspec depenencies: {currentPackage.Id} in path {currentPackage.Path.FullName}");
                dependencyToUpdate.Attribute("version").Value = newVersion.ToString();
            }
            //release note update
            var releaseNotesNode = string.IsNullOrEmpty(ns.NamespaceName) ? packagesNode?.Element("releaseNotes"): packagesNode?.Element(ns + "releaseNotes");
            if (releaseNotesNode != null)
            {
                var currentReleaseNotes = releaseNotesNode.Value;
                var newReleaseNotes = $@"
      [{DateTime.Now:yyyy-MM-dd}][v{newVersion.ToString()}]
          Updated dependencies for {currentPackage.Id} to version {newVersion.ToString()}";
                releaseNotesNode.Value = $"{newReleaseNotes}{currentReleaseNotes}";
            }

            xml.Save(fileContents);
        }
    }
}
