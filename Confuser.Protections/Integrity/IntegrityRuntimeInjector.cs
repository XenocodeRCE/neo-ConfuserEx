using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Renamer;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Integrity
{
    internal static class IntegrityRuntimeInjector
    {
        public static void Inject(
            ConfuserContext context,
            IntegrityProtection parent,
            IntegrityContext ctx,
            IRuntimeService rt,
            INameService name,
            IMarkerService marker)
        {
            var module = context.CurrentModule;

            var runtimeType = rt.GetRuntimeType("Confuser.Runtime.Integrity");
            var members = InjectHelper.Inject(runtimeType, module.GlobalType, module);

            // Mark all injected types and methods as integrity helpers so downstream
            // phases (KoiVM, rename, etc.) can exclude them.
            foreach (var member in members)
            {
                context.Annotations.Set(member, ProtectionAnnotations.InjectedHelper, true);
                if (member is MethodDef method)
                {
                    if (method.DeclaringType != null)
                        context.Annotations.Set(method.DeclaringType, ProtectionAnnotations.InjectedHelper, true);
                }
            }

            foreach (var member in members)
            {
                if (member.Name == "Verify")
                    ctx.VerifyMethod = (MethodDef)member;
                if (member.Name == "Initialize")
                    continue; // startup method, we'll call Verify from it

                // Mark helpers via INameService for rename exclusion
                name.MarkHelper(member, marker, parent);
                // Mark via IMarkerService so ConfuserEx knows these definitions belong to integrity
                marker.Mark(member, parent);
                if (member is MethodDef method)
                    ExcludeFromAll(context, method, parent);
            }

            if (ctx.VerifyMethod == null)
                throw new InvalidOperationException(
                    "Confuser.Runtime.Integrity.Verify not found after injection.");

            // Also mark/exclude the Verify method
            name.MarkHelper(ctx.VerifyMethod, marker, parent);
            ExcludeFromAll(context, ctx.VerifyMethod, parent);

            // Embed public key resource with fixed name
            var publicKeyResourceName = "integrity.pk";
            foreach (var res in module.Resources)
                if (res != null && res.Name == publicKeyResourceName)
                    throw new InvalidOperationException(
                        "Integrity public key resource name collision: " + publicKeyResourceName);

            module.Resources.Add(new EmbeddedResource(
                publicKeyResourceName, ctx.PublicKey,
                ManifestResourceAttributes.Private));

            // Inject placeholder resource name (will be patched in WriterPhase)
            // KeyI0-3: manifest resource name (zeroed, patched later)
            // KeyI4-7: public key resource name
            var pkBytes = EncodeName(publicKeyResourceName);
            var allBytes = new byte[32];
            // manifest name bytes left as zeroes (placeholder)
            Array.Copy(pkBytes, 0, allBytes, 16, 16);

            var keys = new int[8];
            var keyIds = new int[8];
            for (int i = 0; i < 8; i++)
            {
                keyIds[i] = i;
                keys[i] = BitConverter.ToInt32(allBytes, i * 4);
            }

            MutationHelper.InjectKeys(ctx.VerifyMethod, keyIds, keys);
        }

        static void ExcludeFromAll(ConfuserContext context, MethodDef method, IntegrityProtection parent)
        {
            // Exclude from rename, constants, ctrl flow, and integrity itself
            var pp = ProtectionParameters.GetParameters(context, method);
            pp.Remove(parent); // integrity
        }

        static byte[] EncodeName(string name)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(name ?? "");
            var result = new byte[16];
            Array.Copy(bytes, result, Math.Min(bytes.Length, 16));
            return result;
        }
    }
}
