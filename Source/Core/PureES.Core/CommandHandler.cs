using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PureES.Core.ExpBuilders;

namespace PureES.Core;

internal class CommandHandler<TCommand> : ICommandHandler<TCommand>
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly PureESServices _services;

    public CommandHandler(IServiceProvider serviceProvider,
        PureESServices services)
    {
        _serviceProvider = serviceProvider;
        _logger = CommandServicesBuilder.GetLogger(serviceProvider);
        _services = services;
    }

    public async Task<ulong> Handle(TCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Handling command {@Command}", typeof(TCommand));
            var @delegate = _services.GetService<Func<TCommand, IServiceProvider, CancellationToken, Task<ulong>>>()
                            ?? throw new Exception($"No command handler found for command type {typeof(TCommand)}");
            var response = await @delegate(command, _serviceProvider, cancellationToken);
            _logger.LogInformation("Successfully handled command {@Command}", typeof(TCommand));
            return response;
        }
        catch (Exception e)
        {
            _logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }

    public async Task<TResult> Handle<TResult>(TCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var @delegate = _services.GetService<Func<TCommand, IServiceProvider, CancellationToken, Task<TResult>>>()
                            ?? throw new Exception(
                                $"No command handler found for command type {typeof(TCommand)} returning result {typeof(TResult)}");
            var response = await @delegate(command, _serviceProvider, cancellationToken);
            _logger.LogInformation("Successfully handled command {@Command}", typeof(TCommand));
            return response;
        }
        catch (Exception e)
        {
            _logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
}