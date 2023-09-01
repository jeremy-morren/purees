namespace PureES.Core;

/// <summary>
/// Validates the command 
/// </summary>
/// <typeparam name="TCommand"></typeparam>
[PublicAPI]
public interface IAsyncCommandValidator<in TCommand>
{
    Task ValidateAsync(TCommand command, CancellationToken cancellationToken);
}