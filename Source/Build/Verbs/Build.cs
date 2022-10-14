using CommandLine;
using SimpleExec;
using static Bullseye.Targets;
using static SimpleExec.Command;
// ReSharper disable StringLiteralTypo

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace PureES.Build.Verbs;

[Verb("build", HelpText = "Builds PureES for the specified targets")]
public class Build
{
    [Option('t', "targets", Required = true, HelpText = "Build targets")]
    public IEnumerable<string> Targets { get; }

    [Option('q', "quiet", Default = false, HelpText = "Suppress output")]
    public bool Quiet { get; }

    public Build(IEnumerable<string> targets, bool quiet)
    {
        Targets = targets;
        Quiet = quiet;
    }

    public const string Version = "0.9.7.6";

    private void WriteLine(string message)
    {
        if (!Quiet)
            Console.WriteLine(message);
    }
    
    #region Implementation

    public async Task Invoke()
    {
        const string core = "core";
        const string extensions = "extensions";

        AddTargets(core, 
            new [] { "PureES.Core.Abstractions", "PureES.Core" }, 
            new [] { "PureES.Core.Tests" });
        AddTargets(extensions, new[]
            {
                "AspNetCore.HealthChecks.EventStoreDB",
                "PureES.EventBus",
                "PureES.EventStore.InMemory",
                "PureES.EventStoreDB",
                "PureES.RavenDB"
            },
            new[] {"PureES.Extensions.Tests"});

        Target("pack", DependsOn(PackTarget(core), PackTarget(extensions)));

        Target("clean", DependsOn(CleanTarget(core), CleanTarget(extensions)));
        
        Target("default", DependsOn("clean", extensions, core));

        await RunTargetsAndExitAsync(Targets,
            ex => ex is ExitCodeException,
            outputWriter: Quiet ? TextWriter.Null : Console.Out);
    }
    
    private static void AddTargets(string name,
        string[] build,
        string[] test)
    {
        var basicParams = new[]
        {
            "-consoleLoggerParameters:DisableConsoleColor",
            "--nologo",
            "-v=q",
        };
        var buildParams = basicParams.Concat(new[]
        {
            "-c=Release",
            $"-p:Version={Version}",
        }).ToArray();
        
        Target(CleanTarget(name), () =>
        {
            foreach (var b in build)
            {
                Run("dotnet", new[] { "clean", GetProjectPath(b).FullName }.Concat(basicParams),
                    noEcho: true);
                try
                {
                    GetPackagePath(b).Delete();
                }
                catch (FileNotFoundException)
                {
                    //Ignore
                }
            }
        });
        
        Target(BuildTarget(name), () =>
        {
            foreach (var b in build)
                Run("dotnet", new[] { "build", GetProjectPath(b).FullName }.Concat(buildParams),
                noEcho: true);
        });
        
        Target(TestTarget(name), DependsOn(BuildTarget(name)), () =>
        {
            foreach (var t in test)
                Run("dotnet", new [] {"test", GetProjectPath(t).FullName }.Concat(buildParams),
                    noEcho: true);
        });

        Target(PackTarget(name), () =>
        {
            foreach (var b in build.Where(b => !GetPackagePath(b).Exists))
            {
                Run("dotnet", new[]
                    { 
                        "pack", 
                        GetProjectPath(b).FullName, 
                        $"-o={NuGet}",
                    }.Concat(buildParams),
                    noEcho: true);
            }
        });

        Target(name, DependsOn(BuildTarget(name), TestTarget(name), PackTarget(name)));
    }

    private static FileInfo GetProjectPath(string project)
    {
        var file = Directory.GetFiles(Root.Value, "*.csproj", SearchOption.AllDirectories)
            .Single(p => p.EndsWith($"{project}.csproj"));
        return new FileInfo(file);
    }

    private static string NuGet => Path.Combine(Root.Value, "NuGet");

    private static FileInfo GetPackagePath(string project) => new (Path.Combine(NuGet, $"{project}.{Version}.nupkg"));
    
    private static string BuildTarget(string name) => $"build-{name}";

    private static string PackTarget(string name) => $"pack-{name}";
    
    private static string TestTarget(string name) => $"test-{name}";
    
    private static string CleanTarget(string name) => $"clean-{name}";

    private static readonly Lazy<string> Root = new(() =>
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        while (true)
        {
            if (Directory.GetFiles(directory, "PureES.sln", SearchOption.TopDirectoryOnly).Any()) 
                return directory;
            var parent = Directory.GetParent(directory)
                         ?? throw new InvalidOperationException("Unable to get root folder");
            directory = parent.FullName;
        }
    }, true);
    
    #endregion
}