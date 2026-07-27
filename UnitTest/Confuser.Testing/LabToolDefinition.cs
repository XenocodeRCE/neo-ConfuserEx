using System;
using System.Collections.Generic;

namespace Confuser.Testing.Lab
{
    /// <summary>
    ///     Definition of a deobfuscation tool used in lab campaigns.
    ///     Maps to entries in config/tool-lock.json.
    /// </summary>
    internal sealed class LabToolDefinition
    {
        public string Id { get; set; }
        public string Executable { get; set; }
        public string ArgumentTemplate { get; set; }
        public string RequiredInputStage { get; set; }
        public bool MayExecuteTargetCode { get; set; }
        public int TimeoutSeconds { get; set; }

        /// <summary>Human-readable purpose description.</summary>
        public string Purpose { get; set; }

        /// <summary>Expected input type description.</summary>
        public string ExpectedInput { get; set; }

        /// <summary>Prerequisites for this tool to be applicable.</summary>
        public ToolPrerequisites Prerequisites { get; set; }
    }

    /// <summary>
    ///     Prerequisites that must be met for a tool to be applicable to an input.
    ///     If any prerequisite fails, the tool returns NotApplicable (not Failed).
    /// </summary>
    internal sealed class ToolPrerequisites
    {
        public bool MustBeManaged { get; set; }
        public bool MustBeUnpacked { get; set; }
        public bool MustNotBeClean { get; set; }
        public string[] ObfuscatorMarkers { get; set; }
        public string[] ExcludeMarkers { get; set; }
        public string[] RequireResourcesNamed { get; set; }
        public string[] RequireTypeReferences { get; set; }
        public bool EntryPointRequired { get; set; }
        public int MaxCompressorDepth { get; set; }
        public string MinModuleVersion { get; set; }
    }

    /// <summary>
    ///     Result of a single tool execution against one protected input.
    ///     Normalized across all tools for comparison.
    /// </summary>
    internal sealed class LabRunResult
    {
        public string ToolId { get; set; }
        public string ToolCommit { get; set; }
        public string InputSha256 { get; set; }
        public string CommandFingerprint { get; set; }
        public string Status { get; set; }
        public int? ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public long DurationMilliseconds { get; set; }
        public string OutputSha256 { get; set; }
        public bool OutputExists { get; set; }
        public bool OutputIsManaged { get; set; }
        public bool OutputLoadsWithDnlib { get; set; }
        public bool BehaviorMatches { get; set; }
        public string FailureStage { get; set; }
        public string[] Diagnostics { get; set; }
        public string RunDir { get; set; }
        public string StdoutFile { get; set; }
        public string StderrFile { get; set; }

        /// <summary>Inspection metrics collected by Inspect-LabOutput.</summary>
        public LabInspectionResult Inspection { get; set; }

        /// <summary>
        ///     Allowed status values.
        /// </summary>
        public static class Statuses
        {
            public const string Succeeded = "Succeeded";
            public const string Partial = "Partial";
            public const string NoChange = "NoChange";
            public const string Failed = "Failed";
            public const string TimedOut = "TimedOut";
            public const string Crashed = "Crashed";
            public const string InvalidOutput = "InvalidOutput";
            public const string NotApplicable = "NotApplicable";
            public const string BuildFailed = "BuildFailed";
        }
    }

    /// <summary>
    ///     Detailed inspection results from Inspect-LabOutput.ps1.
    ///     Contains heuristic metrics for before/after comparison.
    /// </summary>
    internal sealed class LabInspectionResult
    {
        public string ToolId { get; set; }
        public string Status { get; set; }
        public string OutputSha256 { get; set; }
        public long OutputSize { get; set; }
        public bool IsManaged { get; set; }
        public bool LoadsWithDnlib { get; set; }
        public bool BehaviorMatches { get; set; }
        public string[] Diagnostics { get; set; }
        public LabOutputMetrics Metrics { get; set; }
        public int? CorpusExitCode { get; set; }
        public string CorpusStdout { get; set; }
        public string CorpusStderr { get; set; }
        public bool CorpusTimedOut { get; set; }
    }

    /// <summary>
    ///     Heuristic metrics collected during output inspection.
    ///     Values are raw; the calculation method is recorded in the report.
    ///     Metrics prefixed with "before_" are from the original input.
    /// </summary>
    internal sealed class LabOutputMetrics
    {
        public long OutputSize { get; set; }
        public string OutputHash { get; set; }
        public string StructuralFingerprintBefore { get; set; }
        public string StructuralFingerprintAfter { get; set; }
        public bool HasAssembly { get; set; }
        public string EntryPoint { get; set; }
        public int TypeCount { get; set; }
        public int MethodCount { get; set; }
        public int FieldCount { get; set; }
        public int ResourceCount { get; set; }
        public string RuntimeVersion { get; set; }

        /// <summary>Methods with CIL body containing call instructions.</summary>
        public int ModifiedMethods { get; set; }

        /// <summary>Non-empty strings in #US heap (requires before-after diff for accuracy).</summary>
        public int RestoredStrings { get; set; }

        /// <summary>Proxy call sites replaced (requires semantic comparison).</summary>
        public int ReplacedProxies { get; set; }

        /// <summary>Methods containing switch opcodes (trampoline dispatchers).</summary>
        public int RemainingDispatchers { get; set; }

        /// <summary>Types with short/special names in global namespace.</summary>
        public int RemainingHelpers { get; set; }

        /// <summary>Total embedded resources in module.</summary>
        public int RecoveredResources { get; set; }

        /// <summary>Methods referencing KoiVM.Runtime types.</summary>
        public int KoiMethods { get; set; }

        /// <summary>Before values from input for diff comparison.</summary>
        public int BeforeModifiedMethods { get; set; }
        public int BeforeRemainingDispatchers { get; set; }
        public int BeforeRemainingHelpers { get; set; }
        public int BeforeKoiMethods { get; set; }
    }

    /// <summary>
    ///     Campaign execution report (isolated or chained).
    /// </summary>
    internal sealed class CampaignReport
    {
        public string CampaignMode { get; set; }
        public string[] Chain { get; set; }
        public string InputSha256 { get; set; }
        public string InputFile { get; set; }
        public DateTime Timestamp { get; set; }
        public List<LabRunResult> Results { get; set; }
        public Dictionary<string, string> RecordedHashes { get; set; }
    }
}
