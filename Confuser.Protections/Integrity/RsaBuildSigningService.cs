using System;
using System.IO;
using System.Security.Cryptography;

namespace Confuser.Protections.Integrity
{
    internal sealed class RsaBuildSigningService : IBuildSigningService, IDisposable
    {
        readonly RSACryptoServiceProvider _rsa;

        public RsaBuildSigningService()
        {
            var keyPath = Environment.GetEnvironmentVariable("CONFUSER_INTEGRITY_KEY_PATH");
            if (string.IsNullOrEmpty(keyPath))
                throw new InvalidOperationException(
                    "CONFUSER_INTEGRITY_KEY_PATH environment variable is not set. " +
                    "The integrity protection requires an RSA private key.");

            if (!File.Exists(keyPath))
                throw new FileNotFoundException(
                    "RSA private key not found at the path given by CONFUSER_INTEGRITY_KEY_PATH.",
                    keyPath);

            _rsa = LoadKey(keyPath);
        }

        public static RsaBuildSigningService CreateForTest()
        {
            return new RsaBuildSigningService(true);
        }

        RsaBuildSigningService(bool forTest)
        {
            _rsa = forTest
                ? new RSACryptoServiceProvider(2048)
                : throw new InvalidOperationException("Use the parameterless constructor.");
        }

        static RSACryptoServiceProvider LoadKey(string path)
        {
            var pem = File.ReadAllText(path);

            if (pem.TrimStart().StartsWith("<"))
            {
                var rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(pem);
                return rsa;
            }

            var base64 = ExtractPemBody(pem);
            if (base64 == null)
                throw new InvalidOperationException(
                    "Unrecognised key format. Expected XML or PEM (PKCS#1 / PKCS#8).");

            var der = Convert.FromBase64String(base64);
            var rsa2 = new RSACryptoServiceProvider();
            rsa2.ImportParameters(DecodePkcs1PrivateKey(der));
            return rsa2;
        }

        static string ExtractPemBody(string pem)
        {
            var lines = pem.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var body = new System.Text.StringBuilder();
            bool inBody = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("-----BEGIN ")) { inBody = true; continue; }
                if (line.StartsWith("-----END ")) { inBody = false; continue; }
                if (inBody) body.Append(line.Trim());
            }
            return body.Length > 0 ? body.ToString() : null;
        }

        static RSAParameters DecodePkcs1PrivateKey(byte[] der)
        {
            using (var ms = new MemoryStream(der))
            {
                ExpectTag(ms, 0x30); ReadLength(ms); // SEQUENCE
                ExpectTag(ms, 0x02); SkipBytes(ms, ReadLength(ms)); // version
                return new RSAParameters
                {
                    Modulus   = ReadInteger(ms),
                    Exponent  = ReadInteger(ms),
                    D         = ReadInteger(ms),
                    P         = ReadInteger(ms),
                    Q         = ReadInteger(ms),
                    DP        = ReadInteger(ms),
                    DQ        = ReadInteger(ms),
                    InverseQ  = ReadInteger(ms),
                };
            }
        }

        static void ExpectTag(MemoryStream ms, byte tag) { if (ms.ReadByte() != tag) throw new InvalidDataException("Bad ASN.1 tag"); }
        static int  ReadLength(MemoryStream ms) { int b = ms.ReadByte(); if (b < 0x80) return b; int n = b & 0x7F, len = 0; for (int i = 0; i < n; i++) len = (len << 8) | ms.ReadByte(); return len; }
        static void SkipBytes(MemoryStream ms, int count) { ms.Seek(count, SeekOrigin.Current); }
        static byte[] ReadInteger(MemoryStream ms) { ExpectTag(ms, 0x02); int len = ReadLength(ms); var buf = new byte[len]; ms.Read(buf, 0, len); if (buf.Length > 0 && buf[0] == 0) { var t = new byte[len - 1]; Array.Copy(buf, 1, t, 0, t.Length); return t; } return buf; }

        public byte[] Sign(byte[] data) => _rsa.SignData(data, typeof(SHA256));
        public byte[] GetPublicKey() => _rsa.ExportCspBlob(false);

        public string KeyFingerprint
        {
            get
            {
                var pub = GetPublicKey();
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(pub)).Replace("-", "").ToLowerInvariant();
            }
        }

        public void Dispose() { _rsa?.Clear(); }
    }
}
