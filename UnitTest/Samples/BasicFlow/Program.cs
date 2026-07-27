using System;
using System.Globalization;

namespace ConfuserEx.Corpus.BasicFlow
{
    internal static class Program
    {
        private static int _failures;

        static int Main(string[] args)
        {
            TestIfElse(true);
            TestIfElse(false);

            TestSwitch(1);
            TestSwitch(2);
            TestSwitch(3);
            TestSwitch(99);

            TestForLoop();

            TestWhileLoop();

            int fib = Fibonacci(10);
            Check(fib == 55, $"Fibonacci(10) expected 55, got {fib}");

            if (_failures == 0)
                Console.WriteLine("RESULT:PASS");
            else
                Console.WriteLine($"RESULT:FAIL ({_failures} failures)");

            return _failures;
        }

        // ── if/else ──────────────────────────────────────────

        static void TestIfElse(bool branch)
        {
            int result;
            if (branch)
            {
                result = 42;
            }
            else
            {
                result = 0;
            }

            int expected = branch ? 42 : 0;
            Check(result == expected, $"if/else: expected {expected}, got {result}");
        }

        // ── switch ────────────────────────────────────────────

        static void TestSwitch(int value)
        {
            string result;
            switch (value)
            {
                case 1:
                    result = "one";
                    break;
                case 2:
                    result = "two";
                    break;
                case 3:
                    result = "three";
                    break;
                default:
                    result = "other";
                    break;
            }

            string expected;
            switch (value)
            {
                case 1: expected = "one"; break;
                case 2: expected = "two"; break;
                case 3: expected = "three"; break;
                default: expected = "other"; break;
            }

            Check(result == expected, $"switch: expected '{expected}', got '{result}'");
        }

        // ── for ────────────────────────────────────────────────

        static void TestForLoop()
        {
            int sum = 0;
            for (int i = 1; i <= 10; i++)
            {
                sum += i;
            }

            // 1+2+...+10 = 55
            Check(sum == 55, $"for loop: expected 55, got {sum}");
        }

        // ── while ──────────────────────────────────────────────

        static void TestWhileLoop()
        {
            int sum = 0;
            int i = 1;
            while (i <= 10)
            {
                sum += i;
                i++;
            }

            Check(sum == 55, $"while loop: expected 55, got {sum}");
        }

        // ── recursion ──────────────────────────────────────────

        static int Fibonacci(int n)
        {
            if (n <= 0) return 0;
            if (n == 1) return 1;
            return Fibonacci(n - 1) + Fibonacci(n - 2);
        }

        // ── helper ─────────────────────────────────────────────

        static void Check(bool condition, string message)
        {
            if (!condition)
            {
                Console.Error.WriteLine($"[FAIL] {message}");
                _failures++;
            }
        }
    }
}
