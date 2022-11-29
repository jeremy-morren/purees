using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PureES.Core.ExpBuilders.Services;

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
            _logger.LogInformation("Handling command {@Command}", typeof(TCommand));
            var @delegate = _services.GetService<Func<TCommand, IServiceProvider, CancellationToken, Task<ulong>>>()
                            ?? throw new Exception($"No command handler found for command type {typeof(TCommand)}");
            return await @delegate(command, _serviceProvider, cancellationToken);
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
            return await @delegate(command, _serviceProvider, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogInformation(e, "Error handling command {@Command}", typeof(TCommand));
            throw;
        }
    }
}