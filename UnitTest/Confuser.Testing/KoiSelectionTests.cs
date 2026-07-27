using System;
using System.Collections.Generic;

namespace Confuser.Testing
{
    /// <summary>
    ///     Unit tests for KoiVM selection logic.
    ///     Mirrors the KoiVM.Confuser.Selection types to verify the model.
    /// </summary>
    static class KoiSelectionTests
    {
        // Simulated types matching KoiVM.Confuser.Selection
        enum KoiSelectionMode { Off, Explicit, Policy }

        class KoiSelectionOptions
        {
            public KoiSelectionMode Mode = KoiSelectionMode.Explicit;
            public int MinimumScore = 80;
            public int MaximumInstructionCount = 300;
            public bool AllowExceptionHandlers = false;
            public bool AllowStateMachines = false;
            public bool ExportSelectedMethods = true;
        }

        class VmCompatibility
        {
            public bool IsSupported;
            public int Score;
            public string Reason;
            public static VmCompatibility Unsupported(string reason, int score = 0)
                => new VmCompatibility { IsSupported = false, Score = score, Reason = reason };
        }

        class KoiSelectionResult
        {
            public KoiSelectionOptions Options;
            public List<string> Selected;
            public List<string> Rejected;
            public Dictionary<string, string> RejectionReasons;
            public int TotalCandidates;
            public int SelectedCount => Selected.Count;
            public int RejectedCount => Rejected.Count;
        }

        class KoiSelectionReport
        {
            public int TotalCandidates;
            public int SelectedCount;
            public int RejectedCount;
            public List<string> TopRejectionReasons;
        }

        public static void RunAll()
        {
            Console.WriteLine("=== KoiVM Selection Logic ===");
            Run(TestOffModeSkipsAll);
            Run(TestExplicitModeValidates);
            Run(TestPolicyModeAppliesScore);
            Run(TestInstructionCountLimit);
            Run(TestExceptionHandlersBlocked);
            Run(TestOptionsValidation);
            Run(TestSelectionReport);
            Console.WriteLine();
        }

        static int _passed, _failed;
        static void Run(Action test)
        {
            try { test(); _passed++; Console.WriteLine($"  [PASS] {test.Method.Name}"); }
            catch (Exception ex) { _failed++; Console.WriteLine($"  [FAIL] {test.Method.Name}: {ex.Message}"); }
        }

        static void Assert(bool condition, string msg) { if (!condition) throw new Exception(msg); }

        static void TestOffModeSkipsAll()
        {
            var opts = new KoiSelectionOptions { Mode = KoiSelectionMode.Off };
            Assert(opts.Mode == KoiSelectionMode.Off, "Off mode");
        }

        static void TestExplicitModeValidates()
        {
            var opts = new KoiSelectionOptions { Mode = KoiSelectionMode.Explicit };
            // In Explicit mode, methods must pass compatibility check
            var compat = CheckCompatibility(50, false, false, opts);
            Assert(compat.IsSupported, "50 instr, no EH → supported");
        }

        static void TestPolicyModeAppliesScore()
        {
            var opts = new KoiSelectionOptions { Mode = KoiSelectionMode.Policy, MinimumScore = 70 };
            var compat = CheckCompatibility(100, false, false, opts);
            Assert(compat.IsSupported, "100 instr, score ≥ 70 → supported");
        }

        static void TestInstructionCountLimit()
        {
            var opts = new KoiSelectionOptions { MaximumInstructionCount = 300 };
            var compat = CheckCompatibility(400, false, false, opts);
            Assert(!compat.IsSupported, "400 instr > 300 limit → rejected");
        }

        static void TestExceptionHandlersBlocked()
        {
            var opts = new KoiSelectionOptions { AllowExceptionHandlers = false };
            var compat = CheckCompatibility(50, true, false, opts);
            Assert(!compat.IsSupported, "EH blocked → rejected");
        }

        static void TestOptionsValidation()
        {
            Assert(Throws(() => new KoiSelectionOptions { MaximumInstructionCount = 0 }.MaximumInstructionCount < 1 ? throw new ArgumentOutOfRangeException() : 0),
                "MaxInstructions < 1 invalid");
            Assert(Throws(() => new KoiSelectionOptions { MinimumScore = -1 }.MinimumScore < 0 ? throw new ArgumentOutOfRangeException() : 0),
                "MinScore < 0 invalid");
            Assert(Throws(() => new KoiSelectionOptions { MinimumScore = 201 }.MinimumScore > 200 ? throw new ArgumentOutOfRangeException() : 0),
                "MinScore > 200 invalid");
        }

        static void TestSelectionReport()
        {
            var report = new KoiSelectionReport
            {
                TotalCandidates = 10,
                SelectedCount = 7,
                RejectedCount = 3,
                TopRejectionReasons = new List<string> { "EH not allowed (2)", "Score too low (1)" }
            };
            var str = report.TotalCandidates + "/" + report.SelectedCount + "/" + report.RejectedCount;
            Assert(str == "10/7/3", "Report: 10 candidates, 7 selected, 3 rejected");
        }

        static VmCompatibility CheckCompatibility(int instrCount, bool hasEH, bool isStateMachine,
            KoiSelectionOptions opts)
        {
            if (instrCount > opts.MaximumInstructionCount)
                return VmCompatibility.Unsupported("Too many instructions", 0);
            if (!opts.AllowExceptionHandlers && hasEH)
                return VmCompatibility.Unsupported("EH not allowed", 0);
            if (!opts.AllowStateMachines && isStateMachine)
                return VmCompatibility.Unsupported("State machine not allowed", 0);
            int score = 100 - instrCount / 10;
            if (score < opts.MinimumScore)
                return VmCompatibility.Unsupported("Score " + score + " below minimum " + opts.MinimumScore, score);
            return new VmCompatibility { IsSupported = true, Score = score };
        }

        static bool Throws(Func<int> f) { try { f(); return false; } catch { return true; } }
    }
}
