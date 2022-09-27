using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace PureES.Core.ExpBuilders.AggregateCmdHandlers;

public static class HandlerHelpers
{
    public static void ValidateParameters(Expression serviceProvider, Expression cancellationToken)
    {
        if (!typeof(IServiceProvider).IsAssignableFrom(serviceProvider.Type))
            throw new InvalidOperationException("Invalid ServiceProvider expression");
        if (cancellationToken.Type != typeof(CancellationToken))
            throw new InvalidOperationException("Invalid CancellationToken expression");
    }

    public static void ValidateMethod(Type aggregateType, MethodBase method)
    {
        if (method.GetGenericArguments().Length > 0)
            throw new ArgumentException("Method cannot have generic arguments");
        if (!method.IsStatic && !method.IsPublic)
            throw new ArgumentException("Handler methods must be declared as public static");
    }

    public static void ValidateCommandReturnType(Type aggregateType, Type returnType)
    {
        //The method should return T, IReadOnlyList<T>, Task<T>, Task<ImmutableList<T>>
        //Therefore, we are only checking that all enumerable types are ImmutableList<T>
        //TODO: Check for IAsyncEnumerable
        if (typeof(IEnumerable).IsAssignableFrom(returnType) && returnType != typeof(string))
            throw new NotImplementedException("Enumerable return types are not supported");
        if (returnType == aggregateType)
            throw new NotImplementedException("Command handler methods must return event(s), not the aggregate");
        // ReSharper disable once InvertIf
        if (returnType.IsTask(out var valueType))
        {
            if (valueType == null)
                throw new NotImplementedException("Command Handler methods must return an event or Task<TEvent>");
            // ReSharper disable once TailRecursiveCall
            ValidateCommandReturnType(aggregateType, valueType);
        }
    }

    public static bool IsCommandHandler(Type aggregateType, MethodInfo methodInfo)
    {
        if (methodInfo.GetParameters().All(p => p.GetCustomAttribute(typeof(CommandAttribute)) == null))
            return false;
        ValidateCommandReturnType(aggregateType, methodInfo.ReturnType);
        return true;
    }

    public static bool IsCreateHandler(Type aggregateType, MethodInfo method)
    {
        //Check if method has any parameters that match aggregateType
        return IsCommandHandler(aggregateType, method)
            && method.GetParameters().All(p => p.ParameterType != aggregateType);
    }

    public static bool IsUpdateHandler(Type aggregateType, MethodInfo method)
    {
        //Check if method has any parameters that match aggregateType
        if (!IsCommandHandler(aggregateType, method))
            return false;
        var parameters = method.GetParameters()
            .Where(p => p.ParameterType == aggregateType)
            .ToList();
        return parameters.Count switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException("Methods cannot have multiple parameters of the Aggregate Type")
        };
    }

    public static Type GetCommandType(MethodInfo method) =>
        method.GetParameters()
            .FirstOrDefault(p => p.GetCustomAttribute(typeof(CommandAttribute)) != null)
            ?.ParameterType ?? throw new ArgumentException(
            $"Handler methods must have at least 1 parameter decorated with {typeof(CommandAttribute)}");
}