using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Renamer {
	/// <summary>
	///     Resolves generic arguments
	/// </summary>
	public struct GenericArgumentResolver {
		const int MaxRecursionDepth = 100;
		IList<TypeSig> typeArguments;
		int recursionDepth;

		/// <summary>
		///     Resolves the type signature with the specified generic arguments.
		/// </summary>
		/// <param name="typeSig">The type signature.</param>
		/// <param name="typeGenArgs">The type generic arguments.</param>
		/// <returns>Resolved type signature.</returns>
		/// <exception cref="System.ArgumentException">No generic arguments to resolve.</exception>
		public static TypeSig Resolve(TypeSig typeSig, IList<TypeSig> typeGenArgs) {
			if (typeGenArgs == null)
				throw new ArgumentException("No generic arguments to resolve.");

			var resolver = new GenericArgumentResolver {
				typeArguments = typeGenArgs
			};

			return resolver.ResolveGenericArgs(typeSig);
		}

		/// <summary>
		///     Resolves the method signature with the specified generic arguments.
		/// </summary>
		/// <param name="methodSig">The method signature.</param>
		/// <param name="typeGenArgs">The type generic arguments.</param>
		/// <returns>Resolved method signature.</returns>
		/// <exception cref="System.ArgumentException">No generic arguments to resolve.</exception>
		public static MethodSig Resolve(MethodSig methodSig, IList<TypeSig> typeGenArgs) {
			if (typeGenArgs == null)
				throw new ArgumentException("No generic arguments to resolve.");

			var resolver = new GenericArgumentResolver {
				typeArguments = typeGenArgs
			};

			return resolver.ResolveGenericArgs(methodSig);
		}

		bool ReplaceGenericArg(ref TypeSig typeSig) {
			var genericVar = typeSig as GenericVar;
			if (genericVar == null || genericVar.Number >= typeArguments.Count)
				return false;

			typeSig = typeArguments[(int)genericVar.Number];
			return true;
		}

		MethodSig ResolveGenericArgs(MethodSig sig) {
			if (sig == null)
				return null;
			if (recursionDepth >= MaxRecursionDepth)
				return sig;

			recursionDepth++;
			try {
				var result = ResolveGenericArgs(new MethodSig(sig.GetCallingConvention()), sig);
				result.ExtraData = sig.ExtraData;
				result.OriginalToken = sig.OriginalToken;
				return result;
			}
			finally {
				recursionDepth--;
			}
		}

		MethodSig ResolveGenericArgs(MethodSig sig, MethodSig old) {
			sig.RetType = ResolveGenericArgs(old.RetType);
			foreach (TypeSig p in old.Params)
				sig.Params.Add(ResolveGenericArgs(p));
			sig.GenParamCount = old.GenParamCount;
			if (old.ParamsAfterSentinel != null) {
				sig.ParamsAfterSentinel = new List<TypeSig>(old.ParamsAfterSentinel.Count);
				foreach (TypeSig p in old.ParamsAfterSentinel)
					sig.ParamsAfterSentinel.Add(ResolveGenericArgs(p));
			}
			return sig;
		}

		TypeSig ResolveGenericArgs(TypeSig typeSig) {
			if (typeSig == null)
				return null;
			if (recursionDepth >= MaxRecursionDepth)
				return typeSig;

			recursionDepth++;
			try {
				if (ReplaceGenericArg(ref typeSig))
					return typeSig;

				switch (typeSig.ElementType) {
					case ElementType.Ptr:
						return new PtrSig(ResolveGenericArgs(typeSig.Next));
					case ElementType.ByRef:
						return new ByRefSig(ResolveGenericArgs(typeSig.Next));
					case ElementType.Var:
					case ElementType.MVar:
						return typeSig;
					case ElementType.ValueArray:
						return new ValueArraySig(ResolveGenericArgs(typeSig.Next), ((ValueArraySig)typeSig).Size);
					case ElementType.SZArray:
						return new SZArraySig(ResolveGenericArgs(typeSig.Next));
					case ElementType.CModReqd:
						return new CModReqdSig(((ModifierSig)typeSig).Modifier, ResolveGenericArgs(typeSig.Next));
					case ElementType.CModOpt:
						return new CModOptSig(((ModifierSig)typeSig).Modifier, ResolveGenericArgs(typeSig.Next));
					case ElementType.Module:
						return new ModuleSig(((ModuleSig)typeSig).Index, ResolveGenericArgs(typeSig.Next));
					case ElementType.Pinned:
						return new PinnedSig(ResolveGenericArgs(typeSig.Next));
					case ElementType.FnPtr:
						var functionPointer = (FnPtrSig)typeSig;
						return functionPointer.MethodSig == null
							? typeSig
							: new FnPtrSig(ResolveGenericArgs(functionPointer.MethodSig));

					case ElementType.Array:
						var arraySig = (ArraySig)typeSig;
						var sizes = new List<uint>(arraySig.Sizes);
						var lowerBounds = new List<int>(arraySig.LowerBounds);
						return new ArraySig(ResolveGenericArgs(typeSig.Next), arraySig.Rank, sizes, lowerBounds);
					case ElementType.GenericInst:
						var genericInstance = (GenericInstSig)typeSig;
						var genericArguments = new List<TypeSig>(genericInstance.GenericArguments.Count);
						foreach (TypeSig argument in genericInstance.GenericArguments)
							genericArguments.Add(ResolveGenericArgs(argument));

						return new GenericInstSig(
							ResolveGenericArgs(genericInstance.GenericType) as ClassOrValueTypeSig,
							genericArguments);

					default:
						return typeSig;
				}
			}
			finally {
				recursionDepth--;
			}
		}
	}
}
