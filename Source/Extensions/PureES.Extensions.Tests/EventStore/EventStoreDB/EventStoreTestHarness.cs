using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using EventStore.Client;

// ReSharper disable StringLiteralTypo
// ReSharper disable MemberCanBePrivate.Global

namespace PureES.Extensions.Tests.EventStore.EventStoreDB;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class EventStoreTestHarness : IDisposable
{
    private readonly EventStoreClient _client;

    public readonly string Instance = Guid.NewGuid().ToString();
    public readonly int Port = Random.Shared.Next(2048, 60000);
    public readonly EventStoreClientSettings Settings;

    public EventStoreTestHarness()
    {
        Start();
        Settings = EventStoreClientSettings.Create($"esdb://localhost:{Port}?tls=false");
        _client = new EventStoreClient(Settings);
    }

    public EventStoreClient GetClient() => _client;

    public void Dispose()
    {
        RunCommand("stop", Instance);
        _client.Dispose();
    }

    private void Start()
    {
        const string image = "eventstore/eventstore:21.10.8-buster-slim";
        RunCommand("pull", "-q", image);
        
        var args = new List<string>()
        {
            "run", $"--name={Instance}", $"-p={Port}:2113", "--rm", "-d"
        };
        args.AddRange(Options.Select(p => $"-e=EVENTSTORE_{p.Key}={p.Value}"));
        args.Add(image);
        RunCommand(args.ToArray());
        
        //Wait for start
        Thread.Sleep(TimeSpan.FromSeconds(2.5));
    }

    private static void RunCommand(params string[] arguments)
    {
        var resp = CommandHelper.RunCommand("docker", arguments);
        Debug.WriteLine(resp);
    }

    private static readonly Dictionary<string, string> Options = new()
    {
        {"CLUSTER_SIZE", "1"},
        {"INSECURE", "true"},
        {"MEM_DB", "true"}
    };
}