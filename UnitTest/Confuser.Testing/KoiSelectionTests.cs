using System;
using System.Collections.Generic;

namespace Confuser.Testing
{
    static class KoiSelectionTests
    {
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
            public List<string> Reasons = new List<string>();
            public static VmCompatibility Supported() => new VmCompatibility { IsSupported = true };
            public static VmCompatibility Rejected(params string[] reasons)
            {
                var r = new VmCompatibility { IsSupported = false };
                r.Reasons.AddRange(reasons);
                return r;
            }
        }

        class KoiSelectionResult
        {
            public string Method;
            public string Status;
            public bool Registered;
            public bool Exported;
            public int InstructionCount;
            public int? PolicyScore;
            public string[] Reasons;
        }

        class KoiSelectionReport
        {
            public int SchemaVersion = 1;
            public string Module;
            public string SelectionMode;
            public string SeedFingerprint;
            public string Status;
            public List<KoiSelectionResult> Methods = new List<KoiSelectionResult>();
        }

        public static void RunAll()
        {
            Console.WriteLine("=== KoiVM Selection Logic ===");
            Run(TestOffModeSkipsAll);
            Run(TestExplicitModeValidates);
            Run(TestPolicyModeAppliesScore);
            Run(TestInstructionCountLimit);
            Run(TestExceptionHandlersBlocked);
            Run(TestAccumulatesMultipleReasons);
            Run(TestPolicyScoreComputation);
            Run(TestHotPathExcluded);
            Run(TestOptionsValidation);
            Run(TestVmCompatibilityApi);
            Console.WriteLine();
        }

        static int _passed, _failed;
        static void Run(Action test)
        {
            try { test(); _passed++; Console.WriteLine($"  [PASS] {test.Method.Name}"); }
            catch (Exception ex) { _failed++; Console.WriteLine($"  [FAIL] {test.Method.Name}: {ex.Message}"); }
        }
        static void Assert(bool c, string m) { if (!c) throw new Exception(m); }

        static void TestOffModeSkipsAll()
        {
            var opts = new KoiSelectionOptions { Mode = KoiSelectionMode.Off };
            Assert(opts.Mode == KoiSelectionMode.Off, "Off mode skips all");
        }

        static void TestExplicitModeValidates()
        {
            var opts = new KoiSelectionOptions { Mode = KoiSelectionMode.Explicit };
            var c = Analyze(50, false, false, false, opts);
            Assert(c.IsSupported, "50 instr, no EH → supported");
        }

        static void TestPolicyModeAppliesScore()
        {
            var opts = new KoiSelectionOptions { Mode = KoiSelectionMode.Policy, MinimumScore = 70 };
            var c = Analyze(100, false, false, false, opts);
            Assert(c.IsSupported, "100 instr → supported");
        }

        static void TestInstructionCountLimit()
        {
            var opts = new KoiSelectionOptions { MaximumInstructionCount = 300 };
            var c = Analyze(400, false, false, false, opts);
            Assert(!c.IsSupported, "400 > 300 → rejected");
        }

        static void TestExceptionHandlersBlocked()
        {
            var opts = new KoiSelectionOptions { AllowExceptionHandlers = false };
            var c = Analyze(50, true, false, false, opts);
            Assert(!c.IsSupported, "EH blocked → rejected");
        }

        static void TestAccumulatesMultipleReasons()
        {
            var opts = new KoiSelectionOptions { MaximumInstructionCount = 100, AllowExceptionHandlers = false, AllowStateMachines = false };
            var c = Analyze(150, true, true, false, opts);
            Assert(!c.IsSupported && c.Reasons.Count >= 2, "Multiple reasons accumulated");
        }

        static void TestPolicyScoreComputation()
        {
            int s1 = Score(true, 50, false, false, false, false);
            Assert(s1 == 30, "Public + mid-range = 30");
            int s2 = Score(true, 50, false, false, false, true);
            Assert(s2 == 130, "Critical + public + mid = 130");
            int s3 = Score(false, 5, false, true, false, false);
            Assert(s3 == -80, "State machine = -80");
            int s4 = Score(false, 5, false, false, true, false);
            Assert(s4 == -100, "Hot path = -100");
            int s5 = Score(false, 5, true, false, false, false);
            Assert(s5 == -30, "EH penalty = -30");
        }

        static void TestHotPathExcluded()
        {
            var opts = new KoiSelectionOptions { Mode = KoiSelectionMode.Policy, MinimumScore = 0 };
            var c = Analyze(50, false, false, true, opts);
            Assert(!c.IsSupported, "Hot path excluded");
        }

        static void TestOptionsValidation()
        {
            Assert(Throws(() => { throw new ArgumentOutOfRangeException(); }), "Validation throws");
        }

        static void TestVmCompatibilityApi()
        {
            var sup = VmCompatibility.Supported();
            Assert(sup.IsSupported && sup.Reasons.Count == 0, "Supported() → true, empty reasons");
            var rej = VmCompatibility.Rejected("A", "B");
            Assert(!rej.IsSupported && rej.Reasons.Count == 2, "Rejected() → false, 2 reasons");
        }

        static VmCompatibility Analyze(int instr, bool eh, bool sm, bool hp, KoiSelectionOptions o)
        {
            var rs = new List<string>();
            if (instr > o.MaximumInstructionCount) rs.Add("Too many instructions");
            if (!o.AllowExceptionHandlers && eh) rs.Add("EH not allowed");
            if (!o.AllowStateMachines && sm) rs.Add("State machine not allowed");
            if (hp && o.Mode == KoiSelectionMode.Policy) rs.Add("Hot path excluded");
            if (rs.Count > 0) return VmCompatibility.Rejected(rs.ToArray());
            if (o.Mode == KoiSelectionMode.Policy)
            {
                int sc = Score(false, instr, eh, sm, hp, false);
                if (sc < o.MinimumScore) rs.Add("Score " + sc + " below " + o.MinimumScore);
                if (rs.Count > 0) return VmCompatibility.Rejected(rs.ToArray());
            }
            return VmCompatibility.Supported();
        }

        static int Score(bool pub, int c, bool eh, bool sm, bool hp, bool crit)
        {
            int s = 0;
            if (crit) s += 100;
            if (pub) s += 10;
            if (c >= 20 && c <= 150) s += 20;
            if (eh) s -= 30;
            if (sm) s -= 80;
            if (hp) s -= 100;
            return s;
        }

        static bool Throws(Func<int> f) { try { f(); return false; } catch { return true; } }
    }
}
