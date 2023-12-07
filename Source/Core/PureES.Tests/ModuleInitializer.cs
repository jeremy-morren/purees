using System;
using System.Runtime.CompilerServices;
using VerifyTests;

namespace PureES.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
    }
}