using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable MemberCanBeMadeStatic.Global

namespace PureES.Core.ExpBuilders;

/// <summary>
/// Builds expressions to resolve
/// services from <see cref="IServiceProvider"/>
/// </summary>
public class GetServiceExpBuilder
{
    private readonly CommandHandlerOptions _options;

    public GetServiceExpBuilder(CommandHandlerOptions options)
    {
        _options = options;
    }

    public Func<IServiceProvider, T> CompileDelegate<T>()
    {
        var param = Expression.Parameter(typeof(IServiceProvider));
        var exp = GetRequiredService(param, typeof(T));
        return Expression.Lambda<Func<IServiceProvider, T>>(exp, param).Compile();
    }
    
    public Expression GetRequiredService(Expression provider, Type serviceType)
    {
        if (serviceType.IsValueType)
            throw new InvalidOperationException($"Service {serviceType} is not a reference type");
        //as ServiceProviderServiceExtensions.GetRequiredService<T>(provider
        //Call the non-generic method and cast
        //As (T)sp.GetRequiredService(serviceType)
        var method = typeof(ServiceProviderServiceExtensions)
                         .GetMethods()
                         .SingleOrDefault(m => 
                             m.Name == nameof(ServiceProviderServiceExtensions.GetRequiredService)
                             && m.GetGenericArguments().Length == 1)
                     ?? throw new InvalidOperationException(
                         "Unable to get IServiceProvider.GetRequiredService<T> method");
        return Expression.Call(method.MakeGenericMethod(serviceType), provider);
    }
}