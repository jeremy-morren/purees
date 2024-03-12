// ReSharper disable All
#pragma warning disable CA1018

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit : Attribute {}

    internal class RequiredMemberAttribute : Attribute {}

    internal class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string name) {}
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    internal class SetsRequiredMembersAttribute : Attribute
    {
    }
}

