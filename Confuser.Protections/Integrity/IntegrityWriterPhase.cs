using System;
using System.Security.Cryptography;
using Confuser.Core;
using Confuser.Core.Helpers;
using dnlib.DotNet;

namespace Confuser.Protections.Integrity
{
    internal class IntegrityWriterPhase : ProtectionPhase
    {
        public IntegrityWriterPhase(IntegrityProtection parent)
            : base(parent) { }

        public override ProtectionTargets Targets => ProtectionTargets.Modules;

        public override string Name => "Integrity manifest writing";

        protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
        {
            var module = context.CurrentModule;

            var ctx = context.Annotations.Get<IntegrityContext>(
                module, IntegrityProtection.ContextKey);
            if (ctx == null) return;

            var signer = context.Annotations.Get<IBuildSigningService>(
                module, IntegrityContext.SignerKey);
            if (signer == null) return;

            string resourcePattern = parameters.GetParameter(
                context, module, "resourcePattern", @"^(?!integrity\.).*");

            // Build segments
            ctx.Segments = new System.Collections.Generic.List<IntegritySegmentDescriptor>(
                IntegrityManifestBuilder.BuildResourceSegments(module, resourcePattern));

            // Compute BuildId from unsigned payload (with empty BuildId first)
            var tempPayload = IntegrityManifestSerializer.SerializeUnsigned(
                "", ctx.Segments, "RSA-PKCS1-SHA256");
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(tempPayload);
                ctx.BuildId = BitConverter.ToString(hash, 0, 16)
                    .Replace("-", "").ToLowerInvariant();
            }

            // Serialize unsigned payload with final BuildId
            var unsignedPayload = IntegrityManifestSerializer.SerializeUnsigned(
                ctx.BuildId, ctx.Segments, "RSA-PKCS1-SHA256");

            // Sign
            var signature = signer.Sign(unsignedPayload);

            // Append signature
            var manifest = IntegrityManifestSerializer.AppendSignature(
                unsignedPayload, signature);

            // Resource name: integrity.<buildId>
            var resourceName = "integrity." + ctx.BuildId;

            // Collision check
            foreach (var res in module.Resources)
                if (res != null && res.Name == resourceName)
                    throw new InvalidOperationException(
                        "Integrity manifest resource name collision: " + resourceName);

            module.Resources.Add(new EmbeddedResource(
                resourceName, manifest,
                ManifestResourceAttributes.Private));

            // Patch runtime with the actual resource name
            PatchResourceName(ctx.VerifyMethod, resourceName);

            context.Logger.InfoFormat(
                "Integrity: {0} segments signed, build {1}, key {2}",
                ctx.Segments.Count, ctx.BuildId, signer.KeyFingerprint);
        }

        static void PatchResourceName(MethodDef verifyMethod, string resourceName)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(resourceName ?? "");
            var packed = new byte[16];
            Array.Copy(bytes, packed, Math.Min(bytes.Length, 16));

            var keys = new int[4];
            var keyIds = new int[4];
            for (int i = 0; i < 4; i++)
            {
                keyIds[i] = i;
                keys[i] = BitConverter.ToInt32(packed, i * 4);
            }

            MutationHelper.InjectKeys(verifyMethod, keyIds, keys);
        }
    }
}
