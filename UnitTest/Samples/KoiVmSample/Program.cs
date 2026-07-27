using System;

namespace KoiVmSample
{
    /// <summary>
    ///     Sample application for KoiVM virtualization testing.
    ///     Contains simple methods suitable for virtualization:
    ///     no exception handlers, no state machines, small instruction count.
    /// </summary>
    internal static class Program
    {
        static int Main(string[] args)
        {
            int result = 0;
            result += Add(1, 2);           // 3
            result += Multiply(3, 4);      // 12
            result += Fibonacci(5);        // 5
            result += Factorial(4);        // 24
            result += Compute(10);         // 55 (sum 1..10)

            int expected = 3 + 12 + 5 + 24 + 55; // 99
            if (result == expected)
            {
                Console.WriteLine("RESULT:PASS");
                return 0;
            }
            Console.WriteLine("RESULT:FAIL");
            return 1;
        }

        /// <summary>Simple addition — good VM candidate.</summary>
        public static int Add(int a, int b)
        {
            return a + b;
        }

        /// <summary>Simple multiplication — good VM candidate.</summary>
        public static int Multiply(int a, int b)
        {
            return a * b;
        }

        /// <summary>Recursive Fibonacci — tests VM call support.</summary>
        public static int Fibonacci(int n)
        {
            if (n <= 1) return n;
            return Fibonacci(n - 1) + Fibonacci(n - 2);
        }

        /// <summary>Iterative factorial — tests loops.</summary>
        public static int Factorial(int n)
        {
            int result = 1;
            for (int i = 2; i <= n; i++)
                result *= i;
            return result;
        }

        /// <summary>Summation loop — tests local variables and branches.</summary>
        public static int Compute(int max)
        {
            int sum = 0;
            for (int i = 1; i <= max; i++)
                sum += i;
            return sum;
        }
    }
}
