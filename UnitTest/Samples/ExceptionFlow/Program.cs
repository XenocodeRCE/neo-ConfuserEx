using System;

namespace ConfuserEx.Corpus.ExceptionFlow
{
    // ── exception personnalisée locale au projet ──────────────

    internal class CustomValidationException : Exception
    {
        public CustomValidationException(string message) : base(message) { }
    }

    internal static class Program
    {
        private static int _failures;
        private static bool _finallyRan;

        static int Main(string[] args)
        {
            // ── try/catch : DivideByZeroException ──────────────

            TestDivideByZero();

            // ── try/finally ────────────────────────────────────

            TestFinally();

            // ── exception personnalisée ────────────────────────

            TestCustomException();

            // ── vérification que aucun test n'a crashé ─────────

            if (_failures == 0)
                Console.WriteLine("RESULT:PASS");
            else
                Console.WriteLine($"RESULT:FAIL ({_failures} failures)");

            return _failures;
        }

        // ── DivideByZero ───────────────────────────────────────

        static void TestDivideByZero()
        {
            bool caught = false;
            try
            {
                int zero = 0;
                int boom = 42 / zero;
                // Should never reach here
                Check(false, "DivideByZero: no exception thrown");
            }
            catch (DivideByZeroException)
            {
                caught = true;
            }

            Check(caught, "DivideByZero: DivideByZeroException was not caught");
        }

        // ── try/finally ────────────────────────────────────────

        static void TestFinally()
        {
            _finallyRan = false;
            bool caught = false;

            try
            {
                try
                {
                    throw new InvalidOperationException("test");
                }
                finally
                {
                    _finallyRan = true;
                }
            }
            catch (InvalidOperationException)
            {
                caught = true;
            }

            Check(caught, "try/finally: exception not caught by outer handler");
            Check(_finallyRan, "try/finally: finally block did not run");
        }

        // ── exception personnalisée ────────────────────────────

        static void TestCustomException()
        {
            bool caught = false;
            try
            {
                ThrowCustom("validation error");
            }
            catch (CustomValidationException ex)
            {
                caught = true;
                Check(ex.Message == "validation error",
                    $"CustomException: expected 'validation error', got '{ex.Message}'");
            }

            Check(caught, "CustomException: CustomValidationException was not caught");
        }

        static void ThrowCustom(string message)
        {
            throw new CustomValidationException(message);
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
