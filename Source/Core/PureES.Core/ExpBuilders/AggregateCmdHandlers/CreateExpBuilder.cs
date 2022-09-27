using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

internal class CreateExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public CreateExpBuilder(CommandHandlerOptions options) => _options = options;

    public static void Validate(Type aggregateType,
        MethodInfo methodInfo,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        HandlerHelpers.ValidateParameters(serviceProvider, cancellationToken);
        HandlerHelpers.ValidateMethod(aggregateType, methodInfo);
        HandlerHelpers.ValidateCommandReturnType(aggregateType, methodInfo.ReturnType);
        if (!HandlerHelpers.IsCreateHandler(aggregateType, methodInfo))
            throw new ArgumentException("Method is not create command handler");
        if (methodInfo.GetParameters().Length == 0)
            throw new InvalidOperationException("Command handler methods must have parameters");
    }

    public Expression InvokeCreateHandler(Type aggregateType,
        MethodInfo methodInfo,
        Expression command,
        Expression serviceProvider,
        Expression cancellationToken)
    {
        Validate(aggregateType, methodInfo, serviceProvider, cancellationToken);
        var parameters = new List<Expression>();
        var commandParamFound = false;
        foreach (var p in methodInfo.GetParameters())
        {
            if (p.GetCustomAttribute(typeof(CommandAttribute)) != null)
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
                parameters.Add(cancellationToken);
            else
                throw new InvalidOperationException(Resources.UndecoratedCreateHandlerParameters);
        }
        return Expression.Call(methodInfo, parameters.ToArray());
    }
}