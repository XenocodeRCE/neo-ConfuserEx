using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	/// Resolves exact assembly identities before falling back to framework-appropriate
	/// fuzzy matching.
	/// </summary>
	internal sealed class ConfuserAssemblyResolver : IAssemblyResolver {
		readonly AssemblyResolver exactResolver = new AssemblyResolver { FindExactMatch = true };
		readonly AssemblyResolver frameworkResolver = new AssemblyResolver { FindExactMatch = false };
		readonly AssemblyResolver modernResolver = new AssemblyResolver {
			FindExactMatch = false,
			EnableFrameworkRedirect = false
		};

		public bool EnableTypeDefCache {
			get { return exactResolver.EnableTypeDefCache; }
			set {
				exactResolver.EnableTypeDefCache = value;
				frameworkResolver.EnableTypeDefCache = value;
				modernResolver.EnableTypeDefCache = value;
			}
		}

		public ModuleContext DefaultModuleContext {
			get { return exactResolver.DefaultModuleContext; }
			set {
				exactResolver.DefaultModuleContext = value;
				frameworkResolver.DefaultModuleContext = value;
				modernResolver.DefaultModuleContext = value;
			}
		}

		public IList<string> PreSearchPaths {
			get { return new TeeList(exactResolver.PreSearchPaths, frameworkResolver.PreSearchPaths, modernResolver.PreSearchPaths); }
		}

		public IList<string> PostSearchPaths {
			get { return new TeeList(exactResolver.PostSearchPaths, frameworkResolver.PostSearchPaths, modernResolver.PostSearchPaths); }
		}

		public AssemblyDef Resolve(IAssembly assembly, ModuleDef sourceModule) {
			var assemblyDef = assembly as AssemblyDef;
			if (assemblyDef != null)
				return assemblyDef;

			var resolved = ResolveFromSearchPaths(assembly, sourceModule, true);
			if (resolved != null)
				return resolved;

			resolved = exactResolver.Resolve(assembly, sourceModule);
			if (resolved != null)
				return resolved;

			resolved = TargetFrameworkResolver.IsModernDotNet(sourceModule)
				? modernResolver.Resolve(assembly, sourceModule)
				: frameworkResolver.Resolve(assembly, sourceModule);
			if (resolved != null)
				return resolved;

			return TargetFrameworkResolver.IsModernDotNet(sourceModule)
				? ResolveFromSearchPaths(assembly, sourceModule, false)
				: null;
		}

		AssemblyDef ResolveFromSearchPaths(IAssembly assembly, ModuleDef sourceModule, bool exactMatch) {
			var comparer = exactMatch
				? AssemblyNameComparer.CompareAll
				: AssemblyNameComparer.NameAndPublicKeyTokenOnly;
			var paths = PreSearchPaths.Concat(PostSearchPaths)
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Distinct(StringComparer.OrdinalIgnoreCase);

			foreach (string path in paths) {
				foreach (string extension in new[] { ".dll", ".exe" }) {
					string candidatePath = Path.Combine(path, assembly.Name + extension);
					if (!File.Exists(candidatePath))
						continue;

					ModuleDefMD candidate = null;
					try {
						candidate = ModuleDefMD.Load(candidatePath, DefaultModuleContext);
						if (candidate.Assembly == null || !comparer.Equals(assembly, candidate.Assembly))
							continue;

						AssemblyDef result = candidate.Assembly;
						candidate = null;
						AddToCache(result);
						return result;
					}
					catch (BadImageFormatException) {
						// Keep searching other framework/runtime locations.
					}
					catch (IOException) {
						// Keep searching other framework/runtime locations.
					}
					finally {
						if (candidate != null)
							candidate.Dispose();
					}
				}
			}
			return null;
		}

		public bool AddToCache(AssemblyDef assembly) {
			bool exact = exactResolver.AddToCache(assembly);
			bool framework = frameworkResolver.AddToCache(assembly);
			bool modern = modernResolver.AddToCache(assembly);
			return exact && framework && modern;
		}

		public bool AddToCache(ModuleDef module) {
			return module != null && module.Assembly != null && AddToCache(module.Assembly);
		}

		public bool Remove(AssemblyDef assembly) {
			bool removed = exactResolver.Remove(assembly);
			removed |= frameworkResolver.Remove(assembly);
			removed |= modernResolver.Remove(assembly);
			return removed;
		}

		public void Clear() {
			exactResolver.Clear();
			frameworkResolver.Clear();
			modernResolver.Clear();
		}

		public IEnumerable<AssemblyDef> GetCachedAssemblies() {
			return exactResolver.GetCachedAssemblies()
			                    .Concat(frameworkResolver.GetCachedAssemblies())
			                    .Concat(modernResolver.GetCachedAssemblies())
			                    .Where(assembly => assembly != null)
			                    .Distinct();
		}

		sealed class TeeList : IList<string> {
			readonly IList<IList<string>> lists;

			public TeeList(params IList<string>[] lists) {
				this.lists = lists;
			}

			public IEnumerator<string> GetEnumerator() {
				return lists[0].GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator() {
				return GetEnumerator();
			}

			public void Add(string item) {
				foreach (var list in lists)
					list.Add(item);
			}

			public void Clear() {
				foreach (var list in lists)
					list.Clear();
			}

			public bool Contains(string item) {
				return lists[0].Contains(item);
			}

			public void CopyTo(string[] array, int arrayIndex) {
				lists[0].CopyTo(array, arrayIndex);
			}

			public bool Remove(string item) {
				bool removed = false;
				foreach (var list in lists)
					removed |= list.Remove(item);
				return removed;
			}

			public int Count {
				get { return lists[0].Count; }
			}

			public bool IsReadOnly {
				get { return lists[0].IsReadOnly; }
			}

			public int IndexOf(string item) {
				return lists[0].IndexOf(item);
			}

			public void Insert(int index, string item) {
				foreach (var list in lists)
					list.Insert(index, item);
			}

			public void RemoveAt(int index) {
				foreach (var list in lists)
					list.RemoveAt(index);
			}

			public string this[int index] {
				get { return lists[0][index]; }
				set {
					foreach (var list in lists)
						list[index] = value;
				}
			}
		}
	}
}
