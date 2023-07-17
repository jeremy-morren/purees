using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Docker.DotNet;
using Docker.DotNet.Models;
using EventStore.Client;

// ReSharper disable StringLiteralTypo
// ReSharper disable MemberCanBePrivate.Global

namespace PureES.EventStores.Tests.EventStoreDB;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class EventStoreDBTestHarness : IAsyncDisposable
{
    public readonly EventStoreClient Client;
    public readonly string ContainerId;
    public readonly int Port;

    public readonly EventStoreClientSettings Settings;

    private EventStoreDBTestHarness(string containerId, int port)
    {
        ContainerId = containerId;
        Port = port;
        Settings = EventStoreClientSettings.Create($"esdb://localhost:{Port}?tls=false");
        Client = new EventStoreClient(Settings);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await DisposeContainer(ContainerId);
    }

    public static async Task<EventStoreDBTestHarness> Create(CancellationToken ct)
    {
        var port = Random.Shared.Next(2048, 60000);
        var containerId = await CreateContainer(port, ct);
        return new EventStoreDBTestHarness(containerId, port);
    }

    #region Docker

    private static async Task<string> CreateContainer(int port, CancellationToken ct)
    {
        var id = await Start(port, ct);
        await WaitReady(id, ct);
        return id;
    }

    private static async Task DisposeContainer(string containerId)
    {
        var ct = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
        await DockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters(), ct);
    }

    private static readonly DockerClient DockerClient = new DockerClientConfiguration().CreateClient();

    private static async Task<string> Start(int port, CancellationToken ct)
    {
        const string platform = "linux";
        const string image = "eventstore/eventstore";
        const string tag = "21.10.8-buster-slim";
        //Pull image
        await DockerClient.Images.CreateImageAsync(new ImagesCreateParameters()
            {
                FromImage = image,
                Tag = tag,
                Platform = platform
            },
            null,
            new Progress<JSONMessage>(),
            ct);
        
        //Start container
        var response = await DockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Env = new List<string>
            {
                "EVENTSTORE_CLUSTER_SIZE=1",
                "EVENTSTORE_INSECURE=true",
                "EVENTSTORE_MEM_DB=true"
            },
            Image = $"{image}:{tag}",
            Platform = platform,
            HostConfig = new HostConfig
            {
                AutoRemove = true,
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    {
                        "2113/tcp", new List<PortBinding>
                        {
                            new() {HostPort = port.ToString(), HostIP = "0.0.0.0"}
                        }
                    }
                }
            }
        }, ct);
        if (response.Warnings.Count > 0)
            Debug.WriteLine(
                $"Warnings creating container: {Environment.NewLine}{string.Join(Environment.NewLine, response.Warnings)}");
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
            await Task.Delay(25, ct);
        }
    }

    private static async Task<string> GetContainerHealthStatus(string id, CancellationToken ct)
    {
        var response = await DockerClient.Containers.InspectContainerAsync(id, ct);
        return response.State.Health.Status;
    }

    #endregion
}