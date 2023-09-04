using System;
using System.Runtime.CompilerServices;
using VerifyTests;

namespace PureES.Core.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    [Obsolete("Obsolete")]
    public static void Init()
    {
        VerifySourceGenerators.Enable();
    }
}