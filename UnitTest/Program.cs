using System;
using System.Linq;
using Confuser.Testing;

namespace UnitTest
{
    class Program
    {
        static int Main(string[] args)
        {
            var genericTest = new GenericTest<string>();
            string reversed = new string(genericTest.GetReverse("Confuser").ToArray());
            if (reversed != "resufnoC")
                throw new InvalidOperationException("Generic method regression test failed.");

            KoiSelectionTests.RunAll();
            Console.WriteLine("All unit tests passed.");
            return 0;
        }
    }
}
