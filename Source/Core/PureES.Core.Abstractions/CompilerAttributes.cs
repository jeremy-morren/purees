using JetBrains.Annotations;

// ReSharper disable CheckNamespace

namespace System.Runtime.CompilerServices
{
    [UsedImplicitly]
    internal class IsExternalInit : Attribute {}

    [UsedImplicitly]
    internal class RequiredMemberAttribute : Attribute {}

    [UsedImplicitly]
    internal class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string name) {}
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [UsedImplicitly]
    internal class SetsRequiredMembersAttribute : Attribute
    {
    }
}

