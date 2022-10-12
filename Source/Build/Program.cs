// See https://aka.ms/new-console-template for more information

using CommandLine;
using PureES.Build.Verbs;

await Parser.Default.ParseArguments<Build, GetVersion>(args)
    .MapResult((Build build) => build.Invoke(),
        (GetVersion _) => GetVersion.Invoke(),
        _ => Task.FromResult(2));

