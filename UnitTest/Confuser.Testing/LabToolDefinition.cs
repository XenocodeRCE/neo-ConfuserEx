using System;

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
}
