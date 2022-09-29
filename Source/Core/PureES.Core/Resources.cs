using Microsoft.AspNetCore.Mvc;

namespace PureES.Core;

internal static class Resources
{
    public const string UndecoratedHandlerParameter = 
        $"Undecorated parameter '{{Parameter}}' on command handler '{{Aggregate}}+{{Method}}'. All parameters except CancellationTokens on handler method must be decorated with either {nameof(CommandAttribute)} or {nameof(FromServicesAttribute)}";

    public const string MultipleCommandParameters = $"Only one parameter may be decorated with the {nameof(CommandAttribute)}";

    public static readonly string InvalidEnvelopeType =
        $"Could not get EventEnvelope constructor. A public constructor which takes a single parameter of type {typeof(EventEnvelope)} is required.";
}