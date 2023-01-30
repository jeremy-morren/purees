using Microsoft.AspNetCore.Mvc;

namespace PureES.Core.ExpBuilders.WhenHandlers;

/// <summary>
/// Builds CreateWhen Handler (i.e. for first event in list)
/// </summary>
internal class CreatedWhenExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public CreatedWhenExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression BuildCreateExpression(Type aggregateType,
        Expression envelope,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        if (serviceProvider.Type != typeof(IServiceProvider))
            throw new ArgumentException("Invalid serviceProvider expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new ArgumentException("Invalid cancellationToken expression");
        var createMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => IsCreatedWhen(aggregateType, m))
            .ToList();
        //TODO: validate multiple methods with identical parameters

        //We need an expression along the lines of
        //if (envelope.Event is EventType) return When(NewGenericEnvelope(envelope))
        //When method builder returns ValueTask<TAggregate>

        var eventProperty =
            envelope.Type.GetProperty(nameof(EventEnvelope.Event), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Unable to get EventEnvelope.Event property");
        var @event = Expression.Variable(typeof(object));
        var envelopeVar = Expression.Variable(typeof(EventEnvelope));
        var expressions = new List<Expression>
        {
            //Assign to local variables
            Expression.Assign(envelopeVar, envelope),
            Expression.Assign(@event, Expression.Property(envelopeVar, eventProperty))
        };
        var returnTarget = Expression.Label(typeof(ValueTask<>).MakeGenericType(aggregateType));
        foreach (var m in createMethods)
        {
            ValidateCreatedWhen(aggregateType, m);
            var envelopeType = m.GetParameters()[0].ParameterType;
            var eventType = _options.GetEventType?.Invoke(envelopeType) ?? envelopeType.GetGenericArguments()[0];
            var check = Expression.TypeIs(@event, eventType);
            var call = BuildCreatedWhen(aggregateType, m, envelopeVar, serviceProvider, cancellationToken);
            var whole = Expression.IfThen(check, Expression.Return(returnTarget, call));
            expressions.Add(whole);
        }

        var @base = Expression.Call(ExceptionHelpers.ThrowCreatedWhenBaseMethod,
            Expression.Constant(aggregateType), @event);
        expressions.Add(@base);
        //Base case: Return ValueTask.FromResult<T>(null)
        //Should never be called however, because we get an exception above
        var @default = ValueTaskHelpers.DefaultMethod.MakeGenericMethod(aggregateType);
        expressions.Add(Expression.Label(returnTarget, Expression.Call(@default)));
        return Expression.Block(new[] {envelopeVar, @event}, expressions);
    }

    public Expression BuildCreatedWhen(Type aggregateType,
        MethodInfo method,
        Expression envelope,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        ValidateCreatedWhen(aggregateType, method);
        envelope = new NewEventEnvelopeExpBuilder(_options)
            .New(method.GetParameters()[0].ParameterType, envelope);
        var parameters = method.GetParameters()
            .Select(p => p.ParameterType == typeof(CancellationToken)
                ? cancellationToken
                : p.GetCustomAttribute(typeof(FromServicesAttribute)) != null
                    ? new GetServiceExpBuilder(_options).GetRequiredService(serviceProvider, p.ParameterType)
                    : envelope)
            .ToArray();
        var invoke = Expression.Call(method, parameters);
        MethodInfo wrapper;
        if (method.ReturnType.IsTask(out var rt))
        {
            if (rt == null)
                throw new InvalidOperationException($"CreateWhen method {method} returns non-generic Task");
            //Looks like Task<T> When(envelope)
            wrapper = ValueTaskHelpers.FromTaskMethod.MakeGenericMethod(rt);
            return Expression.Call(wrapper, invoke);
        }

        if (method.ReturnType.IsValueTask(out rt))
        {
            if (rt == null)
                throw new InvalidOperationException($"CreateWhen method {method} returns non-generic ValueTask");
            //Looks like ValueTask<T> When(envelope)
            //We don't need a wrapper here
            return invoke;
        }

        wrapper = ValueTaskHelpers.FromResultMethod.MakeGenericMethod(method.ReturnType);
        return Expression.Call(wrapper, invoke);
    }

    /// <summary>
    ///     Returns parameters that are not CancellationTokens or have [FromServices] attribute
    /// </summary>
    private static List<ParameterInfo> GetEnvelopeParams(MethodBase method) => method.GetParameters()
        .Where(p => p.ParameterType != typeof(CancellationToken) &&
                    p.GetCustomAttribute(typeof(FromServicesAttribute)) == null)
        .ToList();

    public void ValidateCreatedWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(EventEnvelope<TAny, TAny> @event) method
        FactoryExpBuilder.ValidateWhenMethod(aggregateType, method);
        var parameters = GetEnvelopeParams(method);
        if (parameters.Count != 1)
            throw new InvalidOperationException(
                $"Create When method {method} has too many parameters");
        new FactoryExpBuilder(_options).ValidateStronglyTypedEventEnvelope(parameters[0]);
    }

    public bool IsCreatedWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(EventEnvelope<TAny, TAny> @event) method
        //Return type can be T, ValueTask<T>, or Task<T>

        //Validate method returns the aggregate and takes a single EventEnvelope parameter
        var returnType = method.ReturnType.IsTask(out var rt) ? rt
            : method.ReturnType.IsValueTask(out rt) ? rt
            : method.ReturnType;
        if (returnType != aggregateType) return false;
        var parameters = GetEnvelopeParams(method);
        if (parameters.Count != 1)
            return false;
        return _options.IsStronglyTypedEventEnvelope?.Invoke(parameters[0].ParameterType)
               ?? new FactoryExpBuilder(_options).IsStronglyTypedEventEnvelope(parameters[0].ParameterType);
    }
}