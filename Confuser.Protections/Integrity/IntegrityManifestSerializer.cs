using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Confuser.Protections.Integrity
{
    internal static class IntegrityManifestSerializer
    {
        const int Magic = 0x49584543; // "CEXI"
        const int Version = 1;

        /// <summary>Serialize the signed portion only (without signature).</summary>
        public static byte[] SerializeUnsigned(
            string buildId,
            IList<IntegritySegmentDescriptor> segments,
            string signatureAlgorithm)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(Magic);
                bw.Write(Version);

                WriteLengthString(bw, buildId ?? "");
                WriteLengthString(bw, "SHA256");

                // Segments sorted by Id
                var sorted = new List<IntegritySegmentDescriptor>(segments);
                sorted.Sort((a, b) => a.Id.CompareTo(b.Id));

                bw.Write(sorted.Count);
                foreach (var s in sorted)
                {
                    bw.Write(s.Id);
                    WriteLengthString(bw, s.Kind);
                    WriteLengthString(bw, s.Name);
                    bw.Write(s.Length);
                    bw.Write(s.Digest.Length);
                    bw.Write(s.Digest);
                }

                WriteLengthString(bw, signatureAlgorithm ?? "RSA-PKCS1-SHA256");
                return ms.ToArray();
            }
        }

        /// <summary>Append signature to an unsigned payload.</summary>
        public static byte[] AppendSignature(byte[] unsignedPayload, byte[] signature)
        {
            var result = new byte[unsignedPayload.Length + 4 + signature.Length];
            Array.Copy(unsignedPayload, 0, result, 0, unsignedPayload.Length);
            int pos = unsignedPayload.Length;
            result[pos++] = (byte)(signature.Length & 0xff);
            result[pos++] = (byte)((signature.Length >> 8) & 0xff);
            result[pos++] = (byte)((signature.Length >> 16) & 0xff);
            result[pos++] = (byte)((signature.Length >> 24) & 0xff);
            Array.Copy(signature, 0, result, pos, signature.Length);
            return result;
        }

        static void WriteLengthString(BinaryWriter bw, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? "");
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }
    }
}
