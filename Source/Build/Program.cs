// See https://aka.ms/new-console-template for more information

using SimpleExec;
using static Bullseye.Targets;
using static SimpleExec.Command;

namespace Build;

internal static class Program
{
    private const string Version = "0.9.0";
    
    public static async Task Main(string[] args)
    {
        const string core = "core";
        const string extensions = "extensions";
        
        Target("clean", () =>
        {
            try
            {
                Directory.Delete(Nuget, true);
            }
            catch (DirectoryNotFoundException)
            {
                //Ignore
            }
            Console.WriteLine($"Cleaned package output {Nuget}");
        });

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

        Target("pack", DependsOn(Pack(core), Pack(extensions)));
        
        Target("default", DependsOn("clean", extensions, core));

        await RunTargetsAndExitAsync(args, ex => ex is ExitCodeException);
    }

    private static void AddTargets(string name,
        string[] build,
        string[] test)
    {
        var dotnetParams = new[]
        {
            "-consoleLoggerParameters:DisableConsoleColor",
            "--nologo",
            "-c=Release"
        };
        
        Target(Build(name), () =>
        {
            foreach (var b in build)
                Run("dotnet", new[] { "build", GetProjectPath(b) }.Concat(dotnetParams),
                noEcho: true);
        });
        
        Target(Test(name), DependsOn(Build(name)), () =>
        {
            foreach (var t in test)
                Run("dotnet", new [] {"test", GetProjectPath(t) }.Concat(dotnetParams),
                    noEcho: true);
        });
        
        Target(Pack(name), () =>
        {
            foreach (var b in build)
            {
                if (Directory.Exists(Nuget))
                {
                    var package = Directory.GetFiles(Nuget, $"{b}.{Version}.nupkg").FirstOrDefault();
                    //Check if pack is required
                    if (package != null)
                    {
                        Console.WriteLine($"Nuget package {Path.GetFileName(package)} already exists");
                        continue;
                    }
                }
                Run("dotnet", new[]
                    { 
                        "pack", 
                        GetProjectPath(b), 
                        "-o", Nuget,
                        $"-p:Version={Version}",
                        "--include-symbols", "--include-source"
                    }.Concat(dotnetParams),
                    noEcho: true);
            }
        });

        Target(name, DependsOn(Build(name), Test(name), Pack(name)));
    }

    private static string GetProjectPath(string project)
    {
        return Directory.GetFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Single(p => p.EndsWith($"{project}.csproj"));
    }
    
    private static string Build(string name) => $"build-{name}";

    private static string Pack(string name) => $"pack-{name}";
    
    private static string Test(string name) => $"test-{name}";

    private static string Nuget => Path.Combine(Root, "Nuget");

    private static string Root => RootLazy.Value;

    private static readonly Lazy<string> RootLazy = new(() =>
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
}
