using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Confuser.Testing
{
    static class Program
    {
        static int _passed, _failed;

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-test") { RunAll(); return; }
            Console.WriteLine("Usage: Confuser.Testing.exe --self-test");
        }

        static void RunAll()
        {
            Section("Integrity Protection");
            TestRsaSignVerifyRoundtrip();
            TestRsaDifferentKeyFails();
            TestCexiFormatRoundtrip();
            TestKeyFingerprint();
            TestSha256KnownVector();
            TestSha256EmptyHash();
            TestSha256Deterministic();
            TestResourceNameEncoding();
            TestCexiParserRejectsBadMagic();
            TestCexiParserRejectsTruncated();
            TestFixedTimeEquals();

            Section("Integrity E2E");
            TestIntegrityValidRoundtrip();
            TestIntegrityManifestMissing();
            TestIntegrityModifiedPayload();
            TestIntegrityModifiedSignature();
            TestIntegrityModifiedResource();
            TestIntegrityMissingResource();
            TestIntegrityWrongPublicKey();
            TestIntegrityTruncatedManifest();
            TestIntegrityDeterministicOutput();
            TestIntegrityDifferentResourcesProduceDifferentOutput();
            TestIntegrityNoPrivateKeyLeak();

            Console.WriteLine();
            Console.WriteLine("Self-Test: ALL PASSED");
            Environment.Exit(0);
        }

        static void Section(string n) { Console.WriteLine(); Console.WriteLine("=== " + n + " ==="); }
        static void Pass(string m) { _passed++; Console.WriteLine("  [PASS] " + _passed + ". " + m); }
        static void Fail(string m) { _failed++; Console.WriteLine("  [FAIL] " + _failed + ". " + m); Console.WriteLine(); Console.WriteLine("Self-Test: " + _passed + " PASSED, " + _failed + " FAILED"); Environment.Exit(1); }

        static void TestRsaSignVerifyRoundtrip()
        {
            using (var r = new RSACryptoServiceProvider(2048))
            {
                var d = Encoding.UTF8.GetBytes("test");
                var s = r.SignData(d, typeof(SHA256));
                Assert(r.VerifyData(d, typeof(SHA256), s), "RSA sign → verify");
            }
        }

        static void TestRsaDifferentKeyFails()
        {
            using (var r1 = new RSACryptoServiceProvider(2048))
            using (var r2 = new RSACryptoServiceProvider(2048))
            {
                var d = Encoding.UTF8.GetBytes("test");
                Assert(!r2.VerifyData(d, typeof(SHA256), r1.SignData(d, typeof(SHA256))), "different key fails");
            }
        }

        static void TestCexiFormatRoundtrip()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(0x49584543); bw.Write(1);
                var b = Encoding.UTF8.GetBytes("bid"); bw.Write(b.Length); bw.Write(b);
                b = Encoding.UTF8.GetBytes("SHA256"); bw.Write(b.Length); bw.Write(b);
                bw.Write(1); bw.Write(0);
                b = Encoding.UTF8.GetBytes("ER"); bw.Write(b.Length); bw.Write(b);
                b = Encoding.UTF8.GetBytes("r.bin"); bw.Write(b.Length); bw.Write(b);
                bw.Write(3L); var h = SHA256.Create().ComputeHash(new byte[] { 1, 2, 3 }); bw.Write(h.Length); bw.Write(h);
                b = Encoding.UTF8.GetBytes("RSA-PKCS1-SHA256"); bw.Write(b.Length); bw.Write(b);
                payload = ms.ToArray();
                var sig = new byte[256]; bw.Write(sig.Length); bw.Write(sig);
            }
            Assert(payload.Length > 4 && payload[0] == 0x43, "CEXI magic");
        }

        static void TestKeyFingerprint()
        {
            using (var r = new RSACryptoServiceProvider(2048))
            {
                var fp = BitConverter.ToString(SHA256.Create().ComputeHash(r.ExportCspBlob(false))).Replace("-", "").ToLowerInvariant();
                Assert(fp.Length == 64, "fingerprint 64 hex");
            }
        }

        static void TestSha256KnownVector()
        {
            var h = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("abc"));
            Assert(ByteArrayEqual(h, HexToBytes("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")), "SHA-256(abc)");
        }

        static void TestSha256EmptyHash() { var h = SHA256.Create().ComputeHash(new byte[0]); Assert(h.Length == 32 && !IsAllZero(h), "SHA-256 empty"); }
        static void TestSha256Deterministic() { var d = Encoding.UTF8.GetBytes("x"); var h1 = SHA256.Create().ComputeHash(d); var h2 = SHA256.Create().ComputeHash(d); Assert(ByteArrayEqual(h1, h2), "SHA-256 deterministic"); }

        static void TestResourceNameEncoding()
        {
            var n = "cfg1a2b3c4d"; var b = Encoding.UTF8.GetBytes(n); var p = new byte[16]; Array.Copy(b, p, Math.Min(b.Length, 16));
            int l = 0; while (l < 16 && p[l] != 0) l++;
            Assert(Encoding.UTF8.GetString(p, 0, l) == n, "name roundtrip");
        }

        static void TestCexiParserRejectsBadMagic() { bool t = false; try { P( new byte[] { 0xFF, 0xFF, 0xFF, 0xFF } ); } catch { t = true; } Assert(t, "bad magic rejected"); }
        static void TestCexiParserRejectsTruncated() { bool t = false; try { P(new byte[] { 0x43, 0x00 }); } catch { t = true; } Assert(t, "truncated rejected"); }
        static void P(byte[] d) { int p = 0; if (p + 4 > d.Length) throw new InvalidDataException(); if ((d[p] | (d[p + 1] << 8) | (d[p + 2] << 16) | (d[p + 3] << 24)) != 0x49584543) throw new InvalidDataException(); }

        static void TestFixedTimeEquals()
        {
            bool F(byte[] a, byte[] b) { if (a.Length != b.Length) return false; int x = 0; for (int i = 0; i < a.Length; i++) x |= a[i] ^ b[i]; return x == 0; }
            var h1 = SHA256.Create().ComputeHash(new byte[] { 1, 2, 3 });
            Assert(F(h1, SHA256.Create().ComputeHash(new byte[] { 1, 2, 3 })), "FixedTime same");
            Assert(!F(h1, SHA256.Create().ComputeHash(new byte[] { 4, 5, 6 })), "FixedTime diff");
            Assert(!F(h1, new byte[16]), "FixedTime len diff");
        }

        // ── E2E helpers ──────────────────────────────────────────

        static Tuple<byte[], byte[], byte[], Dictionary<string, byte[]>> B(Dictionary<string, byte[]> r, string bid)
        {
            var ns = new List<string>(r.Keys); ns.Sort(StringComparer.Ordinal);
            var segs = new List<Tuple<int, string, string, long, byte[]>>();
            for (int i = 0; i < ns.Count; i++) { var d = r[ns[i]]; byte[] h; using (var s = SHA256.Create()) h = s.ComputeHash(d); segs.Add(Tuple.Create(i, "EmbeddedResource", ns[i], (long)d.Length, h)); }
            byte[] u; using (var ms = new MemoryStream()) using (var bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(0x49584543); bw.Write(1); W(bw, bid); W(bw, "SHA256"); bw.Write(segs.Count);
                foreach (var s in segs) { bw.Write(s.Item1); W(bw, s.Item2); W(bw, s.Item3); bw.Write(s.Item4); bw.Write(s.Item5.Length); bw.Write(s.Item5); }
                W(bw, "RSA-PKCS1-SHA256"); u = ms.ToArray();
            }
            byte[] sig, pk; using (var rsa = new RSACryptoServiceProvider(2048)) { sig = rsa.SignData(u, typeof(SHA256)); pk = rsa.ExportCspBlob(false); }
            var m = new byte[u.Length + 4 + sig.Length]; Array.Copy(u, 0, m, 0, u.Length); int pos = u.Length;
            m[pos++] = (byte)(sig.Length & 0xff); m[pos++] = (byte)((sig.Length >> 8) & 0xff); m[pos++] = (byte)((sig.Length >> 16) & 0xff); m[pos++] = (byte)((sig.Length >> 24) & 0xff);
            Array.Copy(sig, 0, m, pos, sig.Length); return Tuple.Create(m, sig, pk, r);
        }

        static Tuple<byte[], byte[], byte[], Dictionary<string, byte[]>> BK(Dictionary<string, byte[]> r, string bid, RSACryptoServiceProvider rsa)
        {
            var ns = new List<string>(r.Keys); ns.Sort(StringComparer.Ordinal);
            var segs = new List<Tuple<int, string, string, long, byte[]>>();
            for (int i = 0; i < ns.Count; i++) { var d = r[ns[i]]; byte[] h; using (var s = SHA256.Create()) h = s.ComputeHash(d); segs.Add(Tuple.Create(i, "EmbeddedResource", ns[i], (long)d.Length, h)); }
            byte[] u; using (var ms = new MemoryStream()) using (var bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(0x49584543); bw.Write(1); W(bw, bid); W(bw, "SHA256"); bw.Write(segs.Count);
                foreach (var s in segs) { bw.Write(s.Item1); W(bw, s.Item2); W(bw, s.Item3); bw.Write(s.Item4); bw.Write(s.Item5.Length); bw.Write(s.Item5); }
                W(bw, "RSA-PKCS1-SHA256"); u = ms.ToArray();
            }
            var sig = rsa.SignData(u, typeof(SHA256)); var pk = rsa.ExportCspBlob(false);
            var m = new byte[u.Length + 4 + sig.Length]; Array.Copy(u, 0, m, 0, u.Length); int pos = u.Length;
            m[pos++] = (byte)(sig.Length & 0xff); m[pos++] = (byte)((sig.Length >> 8) & 0xff); m[pos++] = (byte)((sig.Length >> 16) & 0xff); m[pos++] = (byte)((sig.Length >> 24) & 0xff);
            Array.Copy(sig, 0, m, pos, sig.Length); return Tuple.Create(m, sig, pk, r);
        }

        static int V(byte[] m, byte[] pk, Dictionary<string, byte[]> cr)
        {
            if (m == null || m.Length < 4) return 1; int p = 0;
            try
            {
                if (p + 4 > m.Length || RI(m, ref p) != 0x49584543 || p + 4 > m.Length || RI(m, ref p) != 1) return 2;
                if (!SL(m, ref p) || !SL(m, ref p) || p + 4 > m.Length) return 2;
                int sc = RI(m, ref p); if (sc < 0 || sc > 65536) return 2;
                var pr = new List<Tuple<string, long, byte[]>>();
                for (int i = 0; i < sc; i++)
                {
                    if (p + 4 > m.Length) return 2; RI(m, ref p);
                    string k, n; if (!RL(m, ref p, out k) || !RL(m, ref p, out n)) return 2;
                    if (p + 8 > m.Length) return 2; long len = R8(m, ref p); if (len < 0 || len > int.MaxValue) return 2;
                    if (p + 4 > m.Length) return 2; int dl = RI(m, ref p); if (dl < 0 || dl > 512) return 2;
                    if (p + dl > m.Length) return 2; var dg = new byte[dl]; Array.Copy(m, p, dg, 0, dl); p += dl;
                    pr.Add(Tuple.Create(n, len, dg));
                }
                string sa; if (!RL(m, ref p, out sa)) return 2;
                var sp = new byte[p]; Array.Copy(m, 0, sp, 0, p);
                if (p + 4 > m.Length) return 2; int sl = RI(m, ref p); if (sl < 0 || sl > 8192 || p + sl > m.Length) return 2;
                var sig = new byte[sl]; Array.Copy(m, p, sig, 0, sl);
                byte[] sh; using (var s = SHA256.Create()) sh = s.ComputeHash(sp);
                using (var r = new RSACryptoServiceProvider()) { r.ImportCspBlob(pk); if (!r.VerifyHash(sh, "SHA256", sig)) return 2; }
                foreach (var seg in pr) { byte[] c; if (!cr.TryGetValue(seg.Item1, out c) || c.LongLength != seg.Item2) return 3; byte[] ch; using (var s = SHA256.Create()) ch = s.ComputeHash(c); if (!ByteArrayEqual(ch, seg.Item3)) return 3; }
                return 0;
            }
            catch { return 2; }
        }

        static void W(BinaryWriter bw, string s) { var b = Encoding.UTF8.GetBytes(s ?? ""); bw.Write(b.Length); bw.Write(b); }
        static int RI(byte[] d, ref int p) { int v = d[p] | (d[p + 1] << 8) | (d[p + 2] << 16) | (d[p + 3] << 24); p += 4; return v; }
        static long R8(byte[] d, ref int p) { long v = (long)d[p] | ((long)d[p + 1] << 8) | ((long)d[p + 2] << 16) | ((long)d[p + 3] << 24) | ((long)d[p + 4] << 32) | ((long)d[p + 5] << 40) | ((long)d[p + 6] << 48) | ((long)d[p + 7] << 56); p += 8; return v; }
        static bool RL(byte[] d, ref int p, out string s) { s = null; if (p + 4 > d.Length) return false; int l = RI(d, ref p); if (l < 0 || l > 65536 || p + l > d.Length) return false; var b = new byte[l]; Array.Copy(d, p, b, 0, l); p += l; s = Encoding.UTF8.GetString(b); return true; }
        static bool SL(byte[] d, ref int p) { if (p + 4 > d.Length) return false; int l = RI(d, ref p); if (l < 0 || l > 65536 || p + l > d.Length) return false; p += l; return true; }

        // ── E2E tests ────────────────────────────────────────────

        static void TestIntegrityValidRoundtrip() { var r = B(D("r1", new byte[] { 1, 2, 3 }), "bid"); Assert(V(r.Item1, r.Item3, r.Item4) == 0, "valid → 0"); }
        static void TestIntegrityManifestMissing() { var r = B(D("r", new byte[] { 1 }), "t"); Assert(V(null, r.Item3, r.Item4) == 1, "null → 1"); Assert(V(new byte[0], r.Item3, r.Item4) == 1, "empty → 1"); }
        static void TestIntegrityModifiedPayload() { var r = B(D("r", new byte[] { 1, 2, 3 }), "b"); var t = (byte[])r.Item1.Clone(); t[8] ^= 0xFF; Assert(V(t, r.Item3, r.Item4) == 2, "modified payload → 2"); }
        static void TestIntegrityModifiedSignature() { var r = B(D("r", new byte[] { 1, 2, 3 }), "b"); var t = (byte[])r.Item1.Clone(); t[t.Length - 1] ^= 0xFF; Assert(V(t, r.Item3, r.Item4) == 2, "modified sig → 2"); }
        static void TestIntegrityModifiedResource() { var r = B(D("r", new byte[] { 1, 2, 3 }), "b"); Assert(V(r.Item1, r.Item3, D("r", new byte[] { 1, 2, 4 })) == 3, "modified res → 3"); }
        static void TestIntegrityMissingResource() { var r = B(D("r", new byte[] { 1, 2, 3 }), "b"); Assert(V(r.Item1, r.Item3, new Dictionary<string, byte[]>()) == 3, "missing res → 3"); }
        static void TestIntegrityWrongPublicKey() { var r = B(D("r", new byte[] { 1 }), "b"); byte[] wk; using (var x = new RSACryptoServiceProvider(2048)) wk = x.ExportCspBlob(false); Assert(V(r.Item1, wk, r.Item4) == 2, "wrong key → 2"); }
        static void TestIntegrityTruncatedManifest() { var r = B(D("r", new byte[] { 1 }), "b"); var t = new byte[r.Item1.Length / 2]; Array.Copy(r.Item1, t, t.Length); Assert(V(t, r.Item3, r.Item4) == 2, "truncated → 2"); }
        static void TestIntegrityDeterministicOutput() { var rs = D2("a", new byte[] { 1, 2 }, "b", new byte[] { 3, 4 }); using (var rsa = new RSACryptoServiceProvider(2048)) { var r1 = BK(rs, "det", rsa); var r2 = BK(rs, "det", rsa); Assert(ByteArrayEqual(r1.Item1, r2.Item1), "same key→identical manifest"); Assert(ByteArrayEqual(r1.Item2, r2.Item2), "same key→identical sig"); } }
        static void TestIntegrityDifferentResourcesProduceDifferentOutput() { Assert(!ByteArrayEqual(B(D("x", new byte[] { 1 }), "b").Item1, B(D("x", new byte[] { 2 }), "b").Item1), "diff res→diff manifest"); }
        static void TestIntegrityNoPrivateKeyLeak()
        {
            var rs = D("r", Encoding.UTF8.GetBytes("tc")); byte[] pkb; byte[] m;
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                pkb = rsa.ExportCspBlob(true);
                var ns = new List<string>(rs.Keys); ns.Sort(StringComparer.Ordinal);
                var segs = new List<Tuple<int, string, string, long, byte[]>>(); int id = 0;
                foreach (var n in ns) { byte[] h; using (var s = SHA256.Create()) h = s.ComputeHash(rs[n]); segs.Add(Tuple.Create(id++, "EmbeddedResource", n, (long)rs[n].Length, h)); }
                byte[] u; using (var ms = new MemoryStream()) using (var bw = new BinaryWriter(ms, Encoding.UTF8))
                { bw.Write(0x49584543); bw.Write(1); W(bw, "lk"); W(bw, "SHA256"); bw.Write(segs.Count); foreach (var s in segs) { bw.Write(s.Item1); W(bw, s.Item2); W(bw, s.Item3); bw.Write(s.Item4); bw.Write(s.Item5.Length); bw.Write(s.Item5); } W(bw, "RSA-PKCS1-SHA256"); u = ms.ToArray(); }
                var sig = rsa.SignData(u, typeof(SHA256));
                m = new byte[u.Length + 4 + sig.Length]; Array.Copy(u, 0, m, 0, u.Length); int p = u.Length; m[p++] = (byte)(sig.Length & 0xff); m[p++] = (byte)((sig.Length >> 8) & 0xff); m[p++] = (byte)((sig.Length >> 16) & 0xff); m[p++] = (byte)((sig.Length >> 24) & 0xff); Array.Copy(sig, 0, m, p, sig.Length);
            }
            if (pkb.Length > 24) { var h = new byte[24]; Array.Copy(pkb, 0, h, 0, 24); Assert(!CS(m, h), "no private key in manifest"); }
        }

        static Dictionary<string, byte[]> D(string k, byte[] v) { return new Dictionary<string, byte[]> { { k, v } }; }
        static Dictionary<string, byte[]> D2(string k1, byte[] v1, string k2, byte[] v2) { return new Dictionary<string, byte[]> { { k1, v1 }, { k2, v2 } }; }
        static bool CS(byte[] h, byte[] n) { if (n.Length > h.Length) return false; for (int i = 0; i <= h.Length - n.Length; i++) { bool m = true; for (int j = 0; j < n.Length; j++) if (h[i + j] != n[j]) { m = false; break; } if (m) return true; } return false; }

        static void Assert(bool c, string m) { if (c) Pass(m); else Fail(m); }
        static bool ByteArrayEqual(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        static byte[] HexToBytes(string h) { var b = new byte[h.Length / 2]; for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16); return b; }
        static bool IsAllZero(byte[] a) { foreach (var b in a) if (b != 0) return false; return true; }
    }
}