using System;
using System.Globalization;

namespace ConfuserEx.Corpus.Constants
{
    internal static class Program
    {
        private static int _failures;

        static int Main(string[] args)
        {
            // ── chaîne vide ────────────────────────────────────

            string empty = "";
            Check(empty.Length == 0, $"empty.Length expected 0, got {empty.Length}");
            Check(empty == string.Empty, "empty string not equal to string.Empty");

            // ── chaîne ASCII ────────────────────────────────────

            string ascii = "Hello, World!";
            Check(ascii == "Hello, World!", $"ASCII: expected 'Hello, World!', got '{ascii}'");
            Check(ascii.Length == 13, $"ASCII length expected 13, got {ascii.Length}");

            // ── chaîne Unicode ──────────────────────────────────

            string unicode = "été|λόγος|日本語";
            Check(unicode == "été|λόγος|日本語",
                $"Unicode: expected 'été|λόγος|日本語', got '{unicode}'");
            Check(unicode.Length == 13, $"Unicode length expected 13, got {unicode.Length}");

            // ── chaînes répétées (deux variables, même valeur) ──

            string repeated1 = "ConfuserEx corpus sample – repeated string literal";
            string repeated2 = "ConfuserEx corpus sample – repeated string literal";
            Check(repeated1 == repeated2, "repeated strings: not equal");
            Check(object.ReferenceEquals(repeated1, repeated2) || !object.ReferenceEquals(repeated1, repeated2),
                "repeated strings: unexpected reference behavior");

            // ── int.MinValue / int.MaxValue ─────────────────────

            int imin = int.MinValue;
            int imax = int.MaxValue;
            Check(imin == -2147483648, $"int.MinValue expected -2147483648, got {imin}");
            Check(imax == 2147483647, $"int.MaxValue expected 2147483647, got {imax}");
            Check(imax + imin == -1, $"int.MaxValue + int.MinValue expected -1, got {imax + imin}");

            // ── long.MinValue / long.MaxValue ───────────────────

            long lmin = long.MinValue;
            long lmax = long.MaxValue;
            Check(lmin == -9223372036854775808L, $"long.MinValue expected -9223372036854775808, got {lmin}");
            Check(lmax == 9223372036854775807L, $"long.MaxValue expected 9223372036854775807, got {lmax}");
            Check(lmax + lmin == -1L, $"long.MaxValue + long.MinValue expected -1, got {lmax + lmin}");

            // ── float.NaN ───────────────────────────────────────

            float fnan = float.NaN;
            Check(float.IsNaN(fnan), "float.NaN: IsNaN returned false");
            // IEEE 754: NaN is never equal to anything, including itself.
            // We verify this via IsNaN rather than == / != to avoid CS1718.
            Check(float.IsNaN(fnan + 1.0f), "float.NaN: NaN + 1.0 should still be NaN");
            Check(!float.IsNaN(0.0f), "float.NaN: 0.0f should not be NaN");

            // ── double.PositiveInfinity ─────────────────────────

            double dinf = double.PositiveInfinity;
            Check(double.IsPositiveInfinity(dinf), "double.PositiveInfinity: IsPositiveInfinity returned false");
            Check(dinf > double.MaxValue, "double.PositiveInfinity: should be > double.MaxValue");

            // ── double.NegativeInfinity ─────────────────────────

            double dinfneg = double.NegativeInfinity;
            Check(double.IsNegativeInfinity(dinfneg), "double.NegativeInfinity: IsNegativeInfinity returned false");
            Check(dinfneg < double.MinValue, "double.NegativeInfinity: should be < double.MinValue");

            // ── -0.0 vs +0.0 ────────────────────────────────────

            double negZero = -0.0;
            double posZero = +0.0;
            Check(negZero == posZero, "-0.0 == +0.0: should be true per IEEE 754");

            long negZeroBits = BitConverter.DoubleToInt64Bits(negZero);
            long posZeroBits = BitConverter.DoubleToInt64Bits(posZero);
            Check(negZeroBits != posZeroBits,
                $"-0.0 bits (0x{negZeroBits:X16}) should differ from +0.0 bits (0x{posZeroBits:X16})");

            Check(double.IsNegativeInfinity(1.0 / negZero),
                "-0.0: 1.0 / -0.0 should yield NegativeInfinity");
            Check(double.IsPositiveInfinity(1.0 / posZero),
                "+0.0: 1.0 / +0.0 should yield PositiveInfinity");

            // ── formatage avec CultureInfo.InvariantCulture ─────

            string formatted = (12345.6789).ToString("F2", CultureInfo.InvariantCulture);
            Check(formatted == "12345.68", $"formatting: expected '12345.68', got '{formatted}'");

            string intFormatted = int.MinValue.ToString(CultureInfo.InvariantCulture);
            Check(intFormatted == "-2147483648",
                $"int formatting: expected '-2147483648', got '{intFormatted}'");

            // ── résultat final ──────────────────────────────────

            if (_failures == 0)
                Console.WriteLine("RESULT:PASS");
            else
                Console.WriteLine($"RESULT:FAIL ({_failures} failures)");

            return _failures;
        }

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
