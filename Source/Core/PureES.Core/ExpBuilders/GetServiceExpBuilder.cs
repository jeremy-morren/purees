

// ReSharper disable MemberCanBeMadeStatic.Global

namespace PureES.Core.ExpBuilders;

/// <summary>
///     Builds expressions to resolve
///     services from <see cref="IServiceProvider" />
/// </summary>
internal class GetServiceExpBuilder
{
    private readonly PureESBuilderOptions _options;

    public GetServiceExpBuilder(PureESBuilderOptions options) => _options = options;


    public Expression GetRequiredService(Expression provider, Type serviceType)
    {
        if (!typeof(IServiceProvider).IsAssignableFrom(provider.Type))
            throw new InvalidOperationException("Invalid service provider expression");
        if (serviceType.IsValueType)
            throw new InvalidOperationException($"Service {serviceType} is not a reference type");
        return Expression.Call(GetRequiredServiceMethod.MakeGenericMethod(serviceType), provider);
    }
    
    public Expression GetService(Expression provider, Type serviceType)
    {
        if (!typeof(IServiceProvider).IsAssignableFrom(provider.Type))
            throw new InvalidOperationException("Invalid service provider expression");
        if (serviceType.IsValueType)
            throw new InvalidOperationException($"Service {serviceType} is not a reference type");
        return Expression.Call(GetServiceMethod.MakeGenericMethod(serviceType), provider);
    }
    
    private static readonly MethodInfo GetRequiredServiceMethod =
        typeof(ServiceProviderServiceExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(m => m.Name == nameof(ServiceProviderServiceExtensions.GetRequiredService) 
                                  && m.GetGenericArguments().Length == 1) 
        ?? throw new InvalidOperationException("Unable to get IServiceProvider.GetRequiredService<T> method");
    
    private static readonly MethodInfo GetServiceMethod =
        typeof(ServiceProviderServiceExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(m => m.Name == nameof(ServiceProviderServiceExtensions.GetService) 
                                  && m.GetGenericArguments().Length == 1) 
        ?? throw new InvalidOperationException("Unable to get IServiceProvider.GetService<T> method");
}