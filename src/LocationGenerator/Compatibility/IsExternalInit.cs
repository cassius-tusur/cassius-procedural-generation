#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Provides the compiler marker required for init-only properties on older target frameworks.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
