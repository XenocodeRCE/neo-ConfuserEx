using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using dnlib.DotNet;

namespace Confuser.Protections.Integrity
{
    internal static class IntegrityManifestBuilder
    {
        public static IList<IntegritySegmentDescriptor> BuildResourceSegments(
            ModuleDef module,
            string resourcePattern)
        {
            var segments = new List<IntegritySegmentDescriptor>();
            var namesSeen = new HashSet<string>(StringComparer.Ordinal);
            var regex = new Regex(resourcePattern ?? @"^(?!integrity\.).*");

            var resources = new List<Resource>(module.Resources);
            resources.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

            int id = 0;
            using (var sha = SHA256.Create())
            {
                foreach (var resource in resources)
                {
                    if (resource == null) continue;

                    if (!regex.IsMatch(resource.Name))
                        continue;

                    if (!namesSeen.Add(resource.Name))
                        throw new InvalidOperationException(
                            "Duplicate resource name: " + resource.Name);

                    var embedded = resource as EmbeddedResource;
                    if (embedded == null)
                        throw new InvalidOperationException(
                            "Non-embedded resource matched pattern: " + resource.Name +
                            " (" + resource.GetType().Name + ")");

                    var raw = IntegrityCanonicalizer.CanonicalizeEmbedded(embedded);
                    var digest = sha.ComputeHash(raw);

                    segments.Add(new IntegritySegmentDescriptor
                    {
                        Id = id++,
                        Kind = "EmbeddedResource",
                        Name = resource.Name,
                        Length = raw.LongLength,
                        Digest = digest
                    });
                }
            }

            return segments;
        }
    }
}
