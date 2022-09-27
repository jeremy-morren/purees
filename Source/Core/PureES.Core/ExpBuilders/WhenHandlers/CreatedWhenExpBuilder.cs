using System.Linq.Expressions;
using System.Reflection;

namespace PureES.Core.ExpBuilders.WhenHandlers;

public class CreatedWhenExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public CreatedWhenExpBuilder(CommandHandlerOptions options) => _options = options;

    public Expression BuildCreateExpression(Type aggregateType,
        Expression envelope)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        var createMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => IsCreatedWhen(aggregateType, m))
            .ToList();
        //TODO: validate multiple methods with identical parameters
        //We need an expression along the lines of
        //if (envelope.Event is EventType) return When(NewGenericEnvelope(envelope))
        var eventProperty =
            envelope.Type.GetProperty(nameof(EventEnvelope.Event), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Unable to get EventEnvelope.Event property");
        var @event = Expression.Variable(typeof(object));
        var envelopeVar = Expression.Variable(typeof(EventEnvelope));
        var expressions = new List<Expression>()
        {
            //Assign to local variables
            Expression.Assign(envelopeVar, envelope),
            Expression.Assign(@event, Expression.Property(envelopeVar, eventProperty))
        };
        var returnTarget = Expression.Label(aggregateType);
        foreach (var m in createMethods)
        {
            ValidateCreatedWhen(aggregateType, m);
            var envelopeType = m.GetParameters()[0].ParameterType;
            var eventType =  _options.GetEventType?.Invoke(envelopeType) ?? envelopeType.GetGenericArguments()[0];
            var check = Expression.TypeIs(@event, @eventType);
            var call = BuildCreatedWhen(aggregateType, m, envelopeVar);
            var whole = Expression.IfThen(check, Expression.Return(returnTarget, call));
            expressions.Add(whole);
        }
        var @base = Expression.Call(ExceptionHelpers.ThrowCreatedWhenBaseMethod,
            Expression.Constant(aggregateType), @event);
        expressions.Add(@base);
        //TODO: Better default value
        expressions.Add(Expression.Label(returnTarget, Expression.Constant(null, aggregateType)));
        return Expression.Block(new[] {envelopeVar, @event}, expressions);
    }

    public Expression BuildCreatedWhen(Type aggregateType,
        MethodInfo methodInfo,
        Expression envelope)
    {
        if (envelope.Type != typeof(EventEnvelope))
            throw new ArgumentException("Invalid envelope expression");
        ValidateCreatedWhen(aggregateType, methodInfo);
        envelope = new NewEventEnvelopeExpBuilder(_options)
            .New(methodInfo.GetParameters()[0].ParameterType, envelope);
        //Looks like T When(envelope)
        return Expression.Call(methodInfo, envelope);
    }

    public void ValidateCreatedWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(EventEnvelope<TAny, TAny> @event) method
        FactoryExpBuilder.ValidateWhen(aggregateType, method);
        var parameters = method.GetParameters();
        if (parameters.Length != 1)
            throw new InvalidOperationException("Create When method must have exactly 1 parameter");
        new FactoryExpBuilder(_options).ValidateEnvelope(parameters[0]);
    }

    public bool IsCreatedWhen(Type aggregateType, MethodInfo method)
    {
        //We are expecting a T When(EventEnvelope<TAny, TAny> @event) method
        //Validate method returns the aggregate and takes a single EventEnvelope parameter
        if (method.ReturnType != aggregateType) return false;
        var parameters = method.GetParameters();
        if (parameters.Length != 1)
            return false;
        return _options.IsEventEnvelope?.Invoke(parameters[0].ParameterType) 
               ?? new FactoryExpBuilder(_options).IsEnvelope(parameters[0].ParameterType);
    }
}