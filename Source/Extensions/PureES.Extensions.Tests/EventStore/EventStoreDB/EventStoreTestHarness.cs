using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Docker.DotNet;
using Docker.DotNet.Models;
using EventStore.Client;

// ReSharper disable StringLiteralTypo
// ReSharper disable MemberCanBePrivate.Global

namespace PureES.Extensions.Tests.EventStore.EventStoreDB;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class EventStoreTestHarness : IDisposable
{
    public readonly int Port = Random.Shared.Next(2048, 60000);
    public readonly string ContainerId;

    public readonly EventStoreClient Client;

    public readonly EventStoreClientSettings Settings;

    public EventStoreTestHarness()
    {
        ContainerId = CreateContainer().GetAwaiter().GetResult();
        Settings = EventStoreClientSettings.Create($"esdb://localhost:{Port}?tls=false");
        Client = new EventStoreClient(Settings);
    }

    public void Dispose()
    {
        Client.Dispose();
        DisposeContainer().GetAwaiter().GetResult();
    }
    
    #region Docker

    private async Task<string> CreateContainer()
    {
        var ct = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
        var id = await Start(ct);
        await WaitReady(id, ct);
        return id;
    }
    
    private async Task DisposeContainer()
    {
        var ct = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
        await DockerClient.Containers.StopContainerAsync(ContainerId, new ContainerStopParameters(), ct);
    }

    private static readonly DockerClient DockerClient = new DockerClientConfiguration().CreateClient();

    private async Task<string> Start(CancellationToken ct)
    {
        var response = await DockerClient.Containers.CreateContainerAsync(new CreateContainerParameters()
        {
            Env = new List<string>()
            {
                "EVENTSTORE_CLUSTER_SIZE=1",
                "EVENTSTORE_INSECURE=true",
                "EVENTSTORE_MEM_DB=true",
            },
            Image = "eventstore/eventstore:21.10.8-buster-slim",
            Platform = "linux",
            HostConfig = new HostConfig()
            {
                AutoRemove = true,
                PortBindings = new Dictionary<string, IList<PortBinding>>()
                {
                    {
                        "2113/tcp", new List<PortBinding>()
                        {
                            {new () { HostPort = Port.ToString(), HostIP = "0.0.0.0"}}
                        }
                    }
                }
            },
        }, ct);
        if (response.Warnings.Count > 0)
            Debug.WriteLine($"Warnings creating container: {Environment.NewLine}{string.Join(Environment.NewLine, response.Warnings)}");
        if (!await DockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), ct))
            throw new Exception($"Error starting container {response.ID}");
        return response.ID;
    }

    private static async Task WaitReady(string id, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (await GetContainerHealthStatus(id, ct) == "healthy")
                break;
            await Task.Delay(millisecondsDelay: 25, ct);
        }
    }

    private static async Task<string> GetContainerHealthStatus(string id, CancellationToken ct)
    {
        var response = await DockerClient.Containers.InspectContainerAsync(id, ct);
        return response.State.Health.Status;
    }
    
    #endregion
}