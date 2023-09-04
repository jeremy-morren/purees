using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PureES.Core.Generators;
using Shouldly;
using VerifyXunit;
using Xunit;

namespace PureES.Core.Tests.Generators;

[UsesVerify]
public class IncrementalGeneratorTests
{
    [Fact]
    public Task Generate()
    {
        var syntaxTrees = Directory.GetFiles(Source)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)));
        
        var compilation = CSharpCompilation.Create(
            assemblyName: "PureES.GeneratorTests",
            references: GetReferences(typeof(EventAttribute),
                typeof(List<int>),
                typeof(Shouldly.Should),
                typeof(FluentAssertions.AssertionExtensions),
                typeof(UsedImplicitlyAttribute),
                typeof(IOptions<>),
                typeof(IServiceProvider), 
                typeof(ILoggerFactory), 
                typeof(AsyncEnumerable),
                typeof(IServiceCollection)),
            syntaxTrees: syntaxTrees,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        compilation.GetDiagnostics().ShouldBeEmpty();

        var generator = new PureESIncrementalGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        diagnostics.ShouldBeEmpty();

        return Verifier.Verify(driver);
    }
    
    private static readonly string Source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");

    private static IEnumerable<MetadataReference> GetReferences(params Type[] types)
    {
        var standard = new[]
        {
            Assembly.Load("netstandard, Version=2.0.0.0"),
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.ObjectModel")
        };
        var custom = types.Select(t => t.Assembly);
            
        return standard.Concat(custom)
            .Distinct()
            .Select(a => MetadataReference.CreateFromFile(a.Location));
    }
}