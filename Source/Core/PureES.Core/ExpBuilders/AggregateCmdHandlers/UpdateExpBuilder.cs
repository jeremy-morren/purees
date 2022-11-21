using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal class UpdateExpBuilder
{
    private readonly CommandHandlerBuilderOptions _options;

    public UpdateExpBuilder(CommandHandlerBuilderOptions options) => _options = options;

    public Expression InvokeUpdateHandler(Type aggregateType,
        MethodInfo methodInfo,
        Expression aggregate,
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        if (aggregate.Type != aggregateType)
            throw new InvalidOperationException("Invalid aggregate expression");
        HandlerHelpers.ValidateParameters(serviceProvider, cancellationToken);
        HandlerHelpers.ValidateMethod(aggregateType, methodInfo);
        HandlerHelpers.ValidateCommandReturnType(aggregateType, methodInfo.ReturnType);
        if (!HandlerHelpers.IsUpdateHandler(aggregateType, methodInfo))
            throw new ArgumentException("Method is not an update command handler");
        var parameters = new List<Expression>();
        if (methodInfo.GetParameters().Length == 0)
            throw new InvalidOperationException("Command handler methods must have parameters");
        var commandParamFound = false;
        foreach (var p in methodInfo.GetParameters())
            if (p.ParameterType == aggregateType)
            {
                parameters.Add(aggregate);
            }
            else if (p.GetCustomAttribute(typeof(CommandAttribute)) != null)
            {
                if (commandParamFound)
                    throw new InvalidOperationException(Resources.MultipleCommandParameters);
                parameters.Add(command);
                commandParamFound = true;
            }
            else if (p.GetCustomAttribute(typeof(FromServicesAttribute)) != null)
            {
                var exp = new GetServiceExpBuilder(_options)
                    .GetRequiredService(serviceProvider, p.ParameterType);
                parameters.Add(exp);
            }
            else if (p.ParameterType == typeof(CancellationToken))
            {
                parameters.Add(cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(Resources.UndecoratedHandlerParameter);
            }

        return Expression.Call(methodInfo, parameters.ToArray());
    }
}