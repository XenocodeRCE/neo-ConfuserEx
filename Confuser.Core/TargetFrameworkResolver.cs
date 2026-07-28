using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	/// Discovers installed shared-framework and targeting-pack directories for the
	/// target framework declared by an input assembly.
	/// </summary>
	public static class TargetFrameworkResolver {
		public static bool IsModernDotNet(ModuleDef module) {
			FrameworkName framework;
			return TryGetTargetFramework(module, out framework) &&
			       (framework.Identifier.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase) ||
			        framework.Identifier.Equals(".NETStandard", StringComparison.OrdinalIgnoreCase));
		}

		public static void AddRuntimeSearchPaths(ConfuserContext context, IEnumerable<ModuleDefMD> modules) {
			var knownPaths = new HashSet<string>(
				context.InternalResolver.PostSearchPaths.Select(Path.GetFullPath),
				StringComparer.OrdinalIgnoreCase);

			foreach (var module in modules) {
				FrameworkName framework;
				if (!TryGetTargetFramework(module, out framework) || !IsModernDotNet(module))
					continue;

				foreach (var path in FindRuntimeSearchPaths(framework)) {
					string fullPath = Path.GetFullPath(path);
					if (!knownPaths.Add(fullPath))
						continue;

					context.InternalResolver.PostSearchPaths.Add(fullPath);
					context.Logger.DebugFormat(
						"Added automatic {0} probe path '{1}'.",
						framework.FullName,
						fullPath);
				}
			}
		}

		static bool TryGetTargetFramework(ModuleDef module, out FrameworkName framework) {
			framework = null;
			if (module == null || module.CorLibTypes == null || module.CorLibTypes.AssemblyRef == null)
				return false;

			var corLib = module.CorLibTypes.AssemblyRef;
			string name = UTF8String.IsNullOrEmpty(corLib.Name) ? string.Empty : corLib.Name.ToString();
			string identifier;
			if (name.Equals("netstandard", StringComparison.OrdinalIgnoreCase))
				identifier = ".NETStandard";
			else if (name.Equals("System.Runtime", StringComparison.OrdinalIgnoreCase) ||
			         name.Equals("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase) ||
			         name.Equals("corefx", StringComparison.OrdinalIgnoreCase))
				identifier = ".NETCoreApp";
			else
				return false;

			framework = new FrameworkName(identifier, corLib.Version ?? new Version(0, 0));
			return true;
		}

		static IEnumerable<string> FindRuntimeSearchPaths(FrameworkName framework) {
			var results = new List<string>();
			foreach (var root in GetDotNetRoots()) {
				AddSharedFrameworkPaths(results, root, framework.Version);
				AddReferencePackPaths(results, Path.Combine(root, "packs"), framework);
			}

			foreach (var packageRoot in GetNuGetPackageRoots())
				AddReferencePackPaths(results, packageRoot, framework);

			return results.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		static IEnumerable<string> GetDotNetRoots() {
			var roots = new[] {
				Environment.GetEnvironmentVariable("DOTNET_ROOT"),
				Environment.GetEnvironmentVariable("DOTNET_ROOT_X64"),
				Environment.GetEnvironmentVariable("DOTNET_ROOT_X86"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet"),
				"/usr/share/dotnet",
				"/usr/lib/dotnet",
				"/usr/local/share/dotnet",
				"/opt/homebrew/share/dotnet"
			};

			return roots.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
			            .Distinct(StringComparer.OrdinalIgnoreCase);
		}

		static IEnumerable<string> GetNuGetPackageRoots() {
			var roots = new List<string>();
			string configuredRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
			if (!string.IsNullOrWhiteSpace(configuredRoot))
				roots.Add(configuredRoot);

			string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrWhiteSpace(profile))
				roots.Add(Path.Combine(profile, ".nuget", "packages"));

			return roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
		}

		static void AddSharedFrameworkPaths(ICollection<string> results, string dotNetRoot, Version requestedVersion) {
			string sharedRoot = Path.Combine(dotNetRoot, "shared");
			if (!Directory.Exists(sharedRoot))
				return;

			foreach (var frameworkDirectory in Directory.GetDirectories(sharedRoot)
			                                             .OrderBy(GetSharedFrameworkPriority)
			                                             .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)) {
				string versionDirectory = FindBestVersionDirectory(frameworkDirectory, requestedVersion);
				if (versionDirectory != null)
					results.Add(versionDirectory);
			}
		}

		static int GetSharedFrameworkPriority(string path) {
			string name = Path.GetFileName(path);
			if (name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase))
				return 0;
			if (name.Equals("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase))
				return 1;
			return 2;
		}

		static void AddReferencePackPaths(ICollection<string> results, string packsRoot, FrameworkName framework) {
			if (!Directory.Exists(packsRoot))
				return;

			string tfmPrefix = framework.Identifier.Equals(".NETStandard", StringComparison.OrdinalIgnoreCase)
				? "netstandard"
				: "net";
			string tfm = tfmPrefix + framework.Version.Major + "." + framework.Version.Minor;

			foreach (var packDirectory in Directory.GetDirectories(packsRoot)
			                                        .Where(path => Path.GetFileName(path).EndsWith(".Ref", StringComparison.OrdinalIgnoreCase))
			                                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) {
				string versionDirectory = FindBestVersionDirectory(packDirectory, framework.Version);
				if (versionDirectory == null)
					continue;

				string referenceDirectory = Path.Combine(versionDirectory, "ref", tfm);
				if (Directory.Exists(referenceDirectory))
					results.Add(referenceDirectory);
			}
		}

		static string FindBestVersionDirectory(string parent, Version requestedVersion) {
			return Directory.GetDirectories(parent)
			                .Select(path => new {
				                Path = path,
				                Version = ParseVersion(Path.GetFileName(path))
			                })
			                .Where(item => item.Version != null &&
			                               item.Version.Major == requestedVersion.Major &&
			                               item.Version.Minor == requestedVersion.Minor)
			                .OrderByDescending(item => item.Version)
			                .Select(item => item.Path)
			                .FirstOrDefault();
		}

		static Version ParseVersion(string value) {
			int suffix = value.IndexOf('-');
			if (suffix >= 0)
				value = value.Substring(0, suffix);

			Version version;
			return Version.TryParse(value, out version) ? version : null;
		}
	}
}
