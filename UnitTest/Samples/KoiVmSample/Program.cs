using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
class ProtectCriticalLogicAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
class HotPathAttribute : Attribute { }

namespace KoiVmSample
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            int result = 0;
            result += Add(1, 2);                    // 3
            result += UnmarkedHelper(5);            // 10
            result += TryCatchMethod(4);            // 2
            result += AsyncMethod().Result;         // 42
            int sum = 0;
            foreach (var v in IteratorMethod()) sum += v; // 6
            result += sum;
            result += LongMethod(10);               // 55
            result += new Calculator(3, 4).Compute(); // 7
            result += HotPathCompute(3);            // 9

            int expected = 3 + 10 + 2 + 42 + 6 + 55 + 7 + 9; // 134
            if (result == expected) { Console.WriteLine("RESULT:PASS"); return 0; }
            Console.WriteLine("RESULT:FAIL");
            return 1;
        }

        [ProtectCriticalLogic]
        public static int Add(int a, int b) => a + b;

        public static int UnmarkedHelper(int x) => x * 2;

        public static int TryCatchMethod(int divisor)
        {
            try { return 8 / divisor; }
            catch (DivideByZeroException) { return 0; }
        }

        public static async Task<int> AsyncMethod()
        {
            await Task.Delay(1);
            return 42;
        }

        public static IEnumerable<int> IteratorMethod()
        {
            yield return 1; yield return 2; yield return 3;
        }

        public static int LongMethod(int max)
        {
            int sum = 0;
            for (int i = 1; i <= max; i++) sum += i;
            return sum;
        }

        static Program() { }

        class Calculator
        {
            int a, b;
            public Calculator(int a, int b) { this.a = a; this.b = b; }
            public int Compute() => a + b;
        }

        [HotPath]
        public static int HotPathCompute(int x) => x * 3;
    }
}
