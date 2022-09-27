using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using EventStore.Client;

namespace PureES.Extensions.Tests.EventStore;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class EventStoreTestHarness : IDisposable
{
    private static readonly string File = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EventStore", "docker-compose.yaml");

    private readonly EventStoreClient _client;

    public EventStoreTestHarness()
    {
        Debug.WriteLine(CommandHelper.RunCommand("docker", 
            "compose", "-f", File, "up", "-d", "--quiet-pull"));
        _client = new EventStoreClient(EventStoreClientSettings.Create("esdb://localhost:2113?tls=false"));
        //Wait for start
        Thread.Sleep(TimeSpan.FromSeconds(1.5));
    }

    public void Dispose()
    {
        _client.Dispose();
        Debug.WriteLine(CommandHelper.RunCommand("docker", "compose", "-f", File, "down"));
    }

    public EventStoreClient GetClient() => _client;
}