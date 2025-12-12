using System.Reflection;

namespace PureES;

internal static class ReflectionItems
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.Instance;

    public static readonly MethodInfo GetService =
        typeof(IServiceProvider).GetMethod("GetService", InstanceFlags)!;
}