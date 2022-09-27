using Microsoft.AspNetCore.Mvc;

namespace PureES.Core;

public static class Resources
{
    public const string UndecoratedCreateHandlerParameters = 
        $"Undecorated parameters on create handler method. All parameters except CancellationTokens on Create handler method must be decorated with either {nameof(CommandAttribute)} or {nameof(FromServicesAttribute)}";

    public const string MultipleCommandParameters = $"Only one parameter may be decorated with the {nameof(CommandAttribute)}";

    public static readonly string InvalidEnvelopeType =
        $"Could not get EventEnvelope constructor. A public constructor which takes a single parameter of type {typeof(EventEnvelope)} is required.";
}