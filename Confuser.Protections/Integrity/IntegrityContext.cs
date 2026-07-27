using System.Collections.Generic;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections.Integrity
{
    internal sealed class IntegrityContext
    {
        /// <summary>Annotation key for IBuildSigningService.</summary>
        internal static readonly object SignerKey = new object();

        public ConfuserContext Context;
        public ModuleDef Module;
        public IntegrityProtection Protection;
        public MethodDef VerifyMethod;
        public string ManifestResourceName;
        public byte[] PublicKey;
        public string BuildId;
        public List<IntegritySegmentDescriptor> Segments;
    }
}
