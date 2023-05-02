namespace PureES.Core.ExpBuilders;

internal class NewEventEnvelopeExpBuilder
{
    private readonly PureESBuilderOptions _options;

    public NewEventEnvelopeExpBuilder(PureESBuilderOptions options) => _options = options;

    public Expression New(Type envelopeType, Expression sourceEnvelope)
    {
        ValidateEnvelope(envelopeType);
        if (sourceEnvelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid source envelope expression");
        var constructor = envelopeType.GetConstructors()
                              .SingleOrDefault(c =>
                                  c.GetParameters().Length == 1
                                  && c.GetParameters()[0].ParameterType == typeof(EventEnvelope))
                          ?? throw new InvalidOperationException(Resources.InvalidEnvelopeType);
        return Expression.New(constructor, sourceEnvelope);
    }

    private void ValidateEnvelope(Type envelopeType)
    {
        var ex = new ArgumentException($"Invalid EventEnvelope type {envelopeType}");
        if (_options.IsStronglyTypedEventEnvelope != null
            && _options.IsStronglyTypedEventEnvelope(envelopeType))
            return;

        var args = envelopeType.GetGenericArguments();
        if (args.Length != 2) throw ex;
        if (typeof(EventEnvelope<,>).MakeGenericType(args) != envelopeType) throw ex;
    }
}