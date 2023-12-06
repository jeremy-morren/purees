namespace PureES;

/// <summary>
/// Validates the command 
/// </summary>
/// <typeparam name="TCommand"></typeparam>
[PublicAPI]
public interface ICommandValidator<in TCommand>
{
    void Validate(TCommand command);
}