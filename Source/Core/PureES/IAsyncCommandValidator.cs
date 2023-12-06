namespace PureES;

/// <summary>
/// Validates the command 
/// </summary>
/// <typeparam name="TCommand"></typeparam>
[PublicAPI]
public interface IAsyncCommandValidator<in TCommand>
{
    Task Validate(TCommand command, CancellationToken cancellationToken);
}