namespace Confuser.Core
{
    /// <summary>
    ///     Annotation keys shared across protections for marking injected runtime helpers
    ///     that should be excluded from further transformation phases.
    /// </summary>
    public static class ProtectionAnnotations
    {
        /// <summary>
        ///     Marks a type or method as an injected runtime helper that must not be
        ///     renamed, virtualized, or otherwise transformed by downstream phases.
        /// </summary>
        public static readonly object InjectedHelper = new object();

        /// <summary>
        ///     Returns true if the object was marked as an injected helper.
        /// </summary>
        public static bool IsInjectedHelper(ConfuserContext context, object obj)
        {
            return context.Annotations.Get(obj, InjectedHelper, false);
        }
    }
}
