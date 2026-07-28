using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Confuser.Runtime
{
    internal enum IntegrityStatus
    {
        Valid = 0,
        ManifestMissing = 1,
        InvalidSignature = 2,
        Modified = 3
    }

    internal static class Integrity
    {
        internal static IntegrityStatus Verify()
        {
            var module = typeof(Integrity).Module;

            string manifestName = ReadKeyString(Mutation.KeyI0, Mutation.KeyI1,
                                                Mutation.KeyI2, Mutation.KeyI3);
            string publicKeyName = ReadKeyString(Mutation.KeyI4, Mutation.KeyI5,
                                                 Mutation.KeyI6, Mutation.KeyI7);

            if (string.IsNullOrEmpty(manifestName) || string.IsNullOrEmpty(publicKeyName))
                return IntegrityStatus.ManifestMissing;

            byte[] manifestData = LoadResource(module.Assembly, manifestName);
            byte[] publicKey = LoadResource(module.Assembly, publicKeyName);

            if (manifestData == null || manifestData.Length < 4)
                return IntegrityStatus.ManifestMissing;
            if (publicKey == null || publicKey.Length == 0)
                return IntegrityStatus.InvalidSignature;

            ParsedManifest parsed;
            if (!TryParse(manifestData, out parsed))
                return IntegrityStatus.InvalidSignature;

            if (!VerifySignature(parsed.SignedPayload, parsed.Signature, publicKey))
                return IntegrityStatus.InvalidSignature;

            foreach (var seg in parsed.Segments)
            {
                byte[] current = LoadResource(module.Assembly, seg.Name);
                if (current == null || current.LongLength != seg.Length)
                    return IntegrityStatus.Modified;

                byte[] digest = Sha256.ComputeHash(current);
                if (!FixedTimeEquals(digest, seg.Digest))
                    return IntegrityStatus.Modified;
            }

            return IntegrityStatus.Valid;
        }

        static void Initialize()
        {
            var status = Verify();
            if (status != IntegrityStatus.Valid)
                throw new InvalidOperationException(
                    "[ConfuserEx Integrity] Verification failed: " + status);
        }

        // ── Parsing ──────────────────────────────────────────────

        struct ParsedManifest
        {
            public byte[] SignedPayload;
            public byte[] Signature;
            public SegmentInfo[] Segments;
        }

        struct SegmentInfo
        {
            public string Name;
            public long Length;
            public byte[] Digest;
        }

        static bool TryParse(byte[] data, out ParsedManifest result)
        {
            result = default(ParsedManifest);
            try
            {
                int pos = 0;
                if (pos + 4 > data.Length) return false;
                int magic = ReadInt32LE(data, ref pos);
                if (magic != 0x49584543) return false;

                if (pos + 4 > data.Length) return false;
                int version = ReadInt32LE(data, ref pos);
                if (version != 1) return false;

                if (!SkipLengthString(data, ref pos)) return false;
                if (!SkipLengthString(data, ref pos)) return false;

                if (pos + 4 > data.Length) return false;
                int segCount = ReadInt32LE(data, ref pos);
                if (segCount < 0 || segCount > 65536) return false;

                var segments = new SegmentInfo[segCount];
                for (int i = 0; i < segCount; i++)
                {
                    if (pos + 4 > data.Length) return false;
                    int id = ReadInt32LE(data, ref pos);
                    string kind; if (!ReadLengthString(data, ref pos, out kind)) return false;
                    string name; if (!ReadLengthString(data, ref pos, out name)) return false;
                    if (pos + 8 > data.Length) return false;
                    long length = ReadInt64LE(data, ref pos);
                    if (length < 0 || length > int.MaxValue) return false;
                    if (pos + 4 > data.Length) return false;
                    int digestLen = ReadInt32LE(data, ref pos);
                    if (digestLen < 0 || digestLen > 512) return false;
                    if (pos + digestLen > data.Length) return false;
                    var digest = new byte[digestLen];
                    Array.Copy(data, pos, digest, 0, digestLen);
                    pos += digestLen;
                    segments[i] = new SegmentInfo { Name = name, Length = length, Digest = digest };
                }

                string sigAlgo;
                if (!ReadLengthString(data, ref pos, out sigAlgo)) return false;

                result.SignedPayload = new byte[pos];
                Array.Copy(data, 0, result.SignedPayload, 0, pos);

                if (pos + 4 > data.Length) return false;
                int sigLen = ReadInt32LE(data, ref pos);
                if (sigLen < 0 || sigLen > 8192) return false;
                if (pos + sigLen > data.Length) return false;
                result.Signature = new byte[sigLen];
                Array.Copy(data, pos, result.Signature, 0, sigLen);

                result.Segments = segments;
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool VerifySignature(byte[] signedPayload, byte[] signature, byte[] publicKey)
        {
            try
            {
                byte[] hash = Sha256.ComputeHash(signedPayload);
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.ImportCspBlob(publicKey);
                    return rsa.VerifyHash(hash, "SHA256", signature);
                }
            }
            catch
            {
                return false;
            }
        }

        static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // ── Binary I/O ────────────────────────────────────────────

        static byte[] LoadResource(Assembly asm, string name)
        {
            try
            {
                using (var stream = asm.GetManifestResourceStream(name))
                {
                    if (stream == null) return null;
                    var ms = new MemoryStream();
                    var buf = new byte[4096];
                    int read;
                    while ((read = stream.Read(buf, 0, buf.Length)) > 0)
                        ms.Write(buf, 0, read);
                    return ms.ToArray();
                }
            }
            catch { return null; }
        }

        static int ReadInt32LE(byte[] data, ref int pos)
        {
            int v = data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24);
            pos += 4;
            return v;
        }

        static long ReadInt64LE(byte[] data, ref int pos)
        {
            long v = (long)data[pos] | ((long)data[pos + 1] << 8) |
                     ((long)data[pos + 2] << 16) | ((long)data[pos + 3] << 24) |
                     ((long)data[pos + 4] << 32) | ((long)data[pos + 5] << 40) |
                     ((long)data[pos + 6] << 48) | ((long)data[pos + 7] << 56);
            pos += 8;
            return v;
        }

        static bool ReadLengthString(byte[] data, ref int pos, out string result)
        {
            result = null;
            if (pos + 4 > data.Length) return false;
            int len = ReadInt32LE(data, ref pos);
            if (len < 0 || len > 65536) return false;
            if (pos + len > data.Length) return false;
            var bytes = new byte[len];
            Array.Copy(data, pos, bytes, 0, len);
            pos += len;
            result = System.Text.Encoding.UTF8.GetString(bytes);
            return true;
        }

        static bool SkipLengthString(byte[] data, ref int pos)
        {
            if (pos + 4 > data.Length) return false;
            int len = ReadInt32LE(data, ref pos);
            if (len < 0 || len > 65536) return false;
            if (pos + len > data.Length) return false;
            pos += len;
            return true;
        }

        static string ReadKeyString(int a, int b, int c, int d)
        {
            var buf = new byte[16];
            WriteInt(buf, 0, a); WriteInt(buf, 4, b);
            WriteInt(buf, 8, c); WriteInt(buf, 12, d);
            int len = 0;
            while (len < 16 && buf[len] != 0) len++;
            return System.Text.Encoding.UTF8.GetString(buf, 0, len);
        }

        static void WriteInt(byte[] buf, int off, int v)
        {
            buf[off] = (byte)(v & 0xff);
            buf[off + 1] = (byte)((v >> 8) & 0xff);
            buf[off + 2] = (byte)((v >> 16) & 0xff);
            buf[off + 3] = (byte)((v >> 24) & 0xff);
        }

        // ── SHA-256 ─────────────────────────────────────────────────

        static class Sha256
        {
            static readonly uint[] K;

            static Sha256()
            {
                K = new uint[] {
                0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
                0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
                0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
                0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
                0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
                0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
                0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
                0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
                0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
                0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
                0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
                0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
                0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
                0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
                0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
                0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
                };
            }

            public static byte[] ComputeHash(byte[] data)
            {
                var h = new uint[8] {
                    0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
                    0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
                };
                long bitLen = (long)data.Length * 8;
                int padLen = 64 - ((data.Length + 8 + 1) % 64);
                if (padLen == 64) padLen = 0;
                int totalLen = data.Length + 1 + padLen + 8;
                var padded = new byte[totalLen];
                Array.Copy(data, padded, data.Length);
                padded[data.Length] = 0x80;
                for (int i = 0; i < 8; i++)
                    padded[totalLen - 1 - i] = (byte)((bitLen >> (i * 8)) & 0xff);

                for (int chunk = 0; chunk < totalLen; chunk += 64)
                {
                    var w = new uint[64];
                    for (int i = 0; i < 16; i++)
                    {
                        int off = chunk + i * 4;
                        w[i] = ((uint)padded[off] << 24) | ((uint)padded[off + 1] << 16) | ((uint)padded[off + 2] << 8) | padded[off + 3];
                    }
                    for (int i = 16; i < 64; i++)
                    {
                        uint s0 = RR(w[i - 15], 7) ^ RR(w[i - 15], 18) ^ (w[i - 15] >> 3);
                        uint s1 = RR(w[i - 2], 17) ^ RR(w[i - 2], 19) ^ (w[i - 2] >> 10);
                        w[i] = w[i - 16] + s0 + w[i - 7] + s1;
                    }
                    uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], hh = h[7];
                    for (int i = 0; i < 64; i++)
                    {
                        uint S1 = RR(e, 6) ^ RR(e, 11) ^ RR(e, 25);
                        uint ch = (e & f) ^ ((~e) & g);
                        uint t1 = hh + S1 + ch + K[i] + w[i];
                        uint S0 = RR(a, 2) ^ RR(a, 13) ^ RR(a, 22);
                        uint maj = (a & b) ^ (a & c) ^ (b & c);
                        uint t2 = S0 + maj;
                        hh = g; g = f; f = e; e = d + t1;
                        d = c; c = b; b = a; a = t1 + t2;
                    }
                    h[0] += a; h[1] += b; h[2] += c; h[3] += d;
                    h[4] += e; h[5] += f; h[6] += g; h[7] += hh;
                }
                var result = new byte[32];
                for (int i = 0; i < 8; i++)
                {
                    result[i * 4] = (byte)((h[i] >> 24) & 0xff);
                    result[i * 4 + 1] = (byte)((h[i] >> 16) & 0xff);
                    result[i * 4 + 2] = (byte)((h[i] >> 8) & 0xff);
                    result[i * 4 + 3] = (byte)(h[i] & 0xff);
                }
                return result;
            }

            static uint RR(uint v, int n) { return (v >> n) | (v << (32 - n)); }
        }
    }
}