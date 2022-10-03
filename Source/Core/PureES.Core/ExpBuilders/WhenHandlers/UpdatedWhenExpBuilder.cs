using System.Linq.Expressions;
using System.Reflection;

namespace PureES.Core.ExpBuilders.WhenHandlers;

internal class UpdatedWhenExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public UpdatedWhenExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression BuildUpdateExpression(Type aggregateType,
        Expression current,
        Expression envelope)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        if (current.Type != aggregateType)
            throw new ArgumentException("Invalid current expression");
        var updateMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => IsUpdatedWhen(aggregateType, m))
            .ToList();
        //TODO: validate multiple methods with identical parameters
        //We need an expression along the lines of
        //if (envelope.Event is EventType) return When(NewGenericEnvelope(envelope))
        var eventProperty =
            envelope.Type.GetProperty(nameof(EventEnvelope.Event), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Unable to get EventEnvelope.Event property");
        var @event = Expression.Variable(typeof(object));
        var envelopeVar = Expression.Variable(typeof(EventEnvelope));
        var currentVar = Expression.Variable(aggregateType);
        var expressions = new List<Expression>()
        {
            //Assign to local variables
            Expression.Assign(envelopeVar, envelope),
            Expression.Assign(@event, Expression.Property(envelopeVar, eventProperty)),
            Expression.Assign(currentVar, current)
        };
        var returnTarget = Expression.Label(aggregateType);
        foreach (var m in updateMethods)
        {
            ValidateUpdatedWhen(aggregateType, m);
            var envelopeType = m.GetParameters()[1].ParameterType;
            var eventType =  _options.GetEventType?.Invoke(envelopeType) ?? envelopeType.GetGenericArguments()[0];
            var check = Expression.TypeIs(@event, @eventType);
            var call = BuildUpdatedWhen(aggregateType, m, currentVar, envelopeVar);
            var whole = Expression.IfThen(check, Expression.Return(returnTarget, call));
            expressions.Add(whole);
        }
        var @base = Expression.Call(ExceptionHelpers.ThrowUpdatedWhenBaseMethod,
            Expression.Constant(aggregateType), @event);
        expressions.Add(@base);
        //TODO: Better default value
        expressions.Add(Expression.Label(returnTarget, Expression.Constant(null, aggregateType)));
        return Expression.Block(new[] {envelopeVar, @event, currentVar}, expressions);
    }
    
    public Expression BuildUpdatedWhen(Type aggregateType,
        MethodInfo methodInfo,
        Expression current,
        Expression envelope)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        if (current.Type != aggregateType)
            throw new ArgumentException("Invalid current expression");
        ValidateUpdatedWhen(aggregateType, methodInfo);
        envelope = new NewEventEnvelopeExpBuilder(_options)
            .New(methodInfo.GetParameters()[1].ParameterType, envelope);
        //Looks like T When(current, envelope)
        return Expression.Call(methodInfo, current, envelope);
    }
    
    public void ValidateUpdatedWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(T current, EventEnvelope<TAny, TAny> @event) method
        FactoryExpBuilder.ValidateWhen(aggregateType, method);
        var parameters = method.GetParameters();
        if (parameters.Length != 2)
            throw new InvalidOperationException("UpdateWhen method must have exactly 2 parameters");
        if (parameters[0].ParameterType != aggregateType || parameters[0].IsNullable())
            throw new InvalidOperationException(
                "UpdateWhen method must take non-nullable aggregate as 1st parameter");
        new FactoryExpBuilder(_options).ValidateEnvelope(parameters[1]);
    }
    
    public bool IsUpdatedWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(T current, EventEnvelope<TAny, TAny> @event) method
        //Check if return type is aggregateType, takes 2 parameters
        //Parameter 1 should be aggregateType
        //Parameter 2 should be EventEnvelope
        if (method.ReturnType != aggregateType)
            return false;
        if (method.GetParameters().Length != 2)
            return false;
        if (method.GetParameters()[0].ParameterType != aggregateType)
            return false;
        return _options.IsEventEnvelope?.Invoke(method.GetParameters()[1].ParameterType)
               ?? new FactoryExpBuilder(_options).IsEnvelope(method.GetParameters()[1].ParameterType);
    }
}