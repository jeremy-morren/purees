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
        //The method should return T, IEnumerable or CommandResult
        //TODO: Check for IAsyncEnumerable
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

    /// <summary>
    /// Checks that a type is <see cref="CommandResult{TEvent,TResult}"/>
    /// of a deriving type
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <param name="event">Type of response (i.e. <c>TEvent</c>)</param>
    /// <param name="result">Type of result (i.e. <c>TResult</c>)</param>
    /// <returns></returns>
    public static bool IsCommandResult(Type type, out Type @event, out Type result)
    {
        if (type.BaseType != null && type.BaseType != typeof(object))
            // ReSharper disable once TailRecursiveCall
            return IsCommandResult(type.BaseType, out @event, out result);
        @event = null!;
        result = null!;
        var genericArgs = type.GetGenericArguments();
        if (genericArgs.Length != 2) return false;
        if (typeof(CommandResult<,>).MakeGenericType(genericArgs) != type) return false;
        @event = genericArgs[0];
        result = genericArgs[1];
        return true;
    }

    public static bool ReturnsCommandResult(MethodInfo method, out Type result)
    {
        if (!method.ReturnType.IsTask(out var returnType)) 
            return IsCommandResult(method.ReturnType, out _, out result);
        if (returnType == null)
            throw new InvalidOperationException(
                $"Method {method.DeclaringType?.FullName}+{method.Name} returns non-generic Task");
        return IsCommandResult(returnType, out _, out result);
    }
}