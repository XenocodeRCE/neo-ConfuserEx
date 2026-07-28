using System;
using System.Collections.Generic;
using System.Linq;

namespace GenericResolutionSample;

internal interface IProject<in TInput, out TResult> {
	TResult Project<TState>(TInput input, TState state);
}

internal abstract class ProjectBase<TInput, TResult> : IProject<TInput, TResult> {
	public abstract TResult Project<TState>(TInput input, TState state);
}

internal sealed class DictionaryProject<TValue>
	: ProjectBase<IReadOnlyDictionary<string, TValue>, IReadOnlyList<TValue>> {
	public override IReadOnlyList<TValue> Project<TState>(
		IReadOnlyDictionary<string, TValue> input,
		TState state) {
		if (state == null)
			throw new ArgumentNullException(nameof(state));

		return input.OrderBy(pair => pair.Key, StringComparer.Ordinal)
		            .Select(pair => pair.Value)
		            .ToArray();
	}
}

internal abstract unsafe class FunctionPointerBase<T> {
	public abstract T Apply(delegate* managed<T, T> callback, T value);
}

internal sealed unsafe class IntFunctionPointer : FunctionPointerBase<int> {
	public override int Apply(delegate* managed<int, int> callback, int value) => callback(value);
}

internal static class Program {
	private static int Double(int value) => checked(value * 2);

	private static unsafe int Main() {
		IProject<IReadOnlyDictionary<string, int>, IReadOnlyList<int>> project =
			new DictionaryProject<int>();
		var values = project.Project(
			new Dictionary<string, int> {
				["second"] = 2,
				["first"] = 1
			},
			"state");

		var functionPointerResult = new IntFunctionPointer().Apply(&Double, 21);
		var passed = values.SequenceEqual(new[] { 1, 2 }) && functionPointerResult == 42;
		Console.WriteLine(passed ? "RESULT:PASS" : "RESULT:FAIL");
		return passed ? 0 : 1;
	}
}
