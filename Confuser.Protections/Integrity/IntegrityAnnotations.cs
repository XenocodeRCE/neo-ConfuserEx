using Confuser.Core;

namespace Confuser.Protections.Integrity
{
    /// <summary>
    ///     Annotation keys used by the Integrity protection.
    ///     Delegates to ProtectionAnnotations.InjectedHelper for cross-protection compatibility.
    /// </summary>
    internal static class IntegrityAnnotations
    {
        public static readonly object InjectedHelper = ProtectionAnnotations.InjectedHelper;
    }
}
