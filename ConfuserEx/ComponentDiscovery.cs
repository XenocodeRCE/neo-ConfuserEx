using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Confuser.Core;

namespace ConfuserEx {
	internal class ComponentDiscovery {
		public static void LoadComponents(
			IList<ConfuserComponent> protections,
			IList<ConfuserComponent> packers,
			string pluginPath) {
			string fullPluginPath = Path.GetFullPath(pluginPath);
			var context = new DiscoveryContext(protections, packers, fullPluginPath);
			var loadContext = new PluginLoadContext(fullPluginPath);
			try {
				Assembly assembly = loadContext.LoadFromAssemblyPath(fullPluginPath);
				foreach (Type type in assembly.GetTypes()) {
					if (type.IsAbstract || !PluginDiscovery.HasAccessibleDefConstructor(type))
						continue;

					if (typeof(Protection).IsAssignableFrom(type))
						context.AddProtection(Info.FromComponent(
							(Protection)Activator.CreateInstance(type),
							fullPluginPath));
					else if (typeof(Packer).IsAssignableFrom(type))
						context.AddPacker(Info.FromComponent(
							(Packer)Activator.CreateInstance(type),
							fullPluginPath));
				}
			}
			finally {
				loadContext.Unload();
			}
		}

		public static void RemoveComponents(
			IList<ConfuserComponent> protections,
			IList<ConfuserComponent> packers,
			string pluginPath) {
			string fullPluginPath = Path.GetFullPath(pluginPath);
			protections.RemoveWhere(comp => comp is InfoComponent &&
				((InfoComponent)comp).info.path == fullPluginPath);
			packers.RemoveWhere(comp => comp is InfoComponent &&
				((InfoComponent)comp).info.path == fullPluginPath);
		}

		class DiscoveryContext {
			readonly IList<ConfuserComponent> packers;
			readonly string pluginPath;
			readonly IList<ConfuserComponent> protections;

			public DiscoveryContext(
				IList<ConfuserComponent> protections,
				IList<ConfuserComponent> packers,
				string pluginPath) {
				this.protections = protections;
				this.packers = packers;
				this.pluginPath = pluginPath;
			}

			public void AddProtection(Info info) {
				if (protections.Any(component => component.Id == info.id))
					return;
				protections.Add(new InfoComponent(info));
			}

			public void AddPacker(Info info) {
				if (packers.Any(component => component.Id == info.id))
					return;
				packers.Add(new InfoComponent(info));
			}
		}

		sealed class PluginLoadContext : AssemblyLoadContext {
			readonly AssemblyDependencyResolver dependencyResolver;

			public PluginLoadContext(string pluginPath)
				: base("ConfuserEx.Plugin." + Path.GetFileNameWithoutExtension(pluginPath), true) {
				dependencyResolver = new AssemblyDependencyResolver(pluginPath);
			}

			protected override Assembly Load(AssemblyName assemblyName) {
				Assembly shared = AppDomain.CurrentDomain.GetAssemblies()
					.FirstOrDefault(assembly =>
						AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));
				if (shared != null)
					return shared;

				string path = dependencyResolver.ResolveAssemblyToPath(assemblyName);
				return path == null ? null : LoadFromAssemblyPath(path);
			}
		}

		class Info {
			public string desc;
			public string fullId;
			public string id;
			public string name;
			public string path;

			public static Info FromComponent(ConfuserComponent component, string pluginPath) {
				return new Info {
					name = component.Name,
					desc = component.Description,
					id = component.Id,
					fullId = component.FullId,
					path = pluginPath
				};
			}
		}

		class InfoComponent : ConfuserComponent {
			public readonly Info info;

			public InfoComponent(Info info) {
				this.info = info;
			}

			public override string Name { get { return info.name; } }
			public override string Description { get { return info.desc; } }
			public override string Id { get { return info.id; } }
			public override string FullId { get { return info.fullId; } }

			protected override void Initialize(ConfuserContext context) {
				throw new NotSupportedException();
			}

			protected override void PopulatePipeline(ProtectionPipeline pipeline) {
				throw new NotSupportedException();
			}
		}
	}
}
