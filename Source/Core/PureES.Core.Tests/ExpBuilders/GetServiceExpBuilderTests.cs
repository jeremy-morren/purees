using System;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using PureES.Core.ExpBuilders;
using Xunit;

namespace PureES.Core.Tests.ExpBuilders;

public class GetServiceBuilderExpTests
{
    [Fact]
    public void Get_Singleton_Service()
    {
        var svc = new Service();
        using var provider = Services.Build(s => s.AddSingleton(svc));
        CompileDelegate(out var getRequired, out var get);
        Assert.Same(svc, getRequired(provider));
        Assert.Same(svc, get(provider));
    }
    
    [Fact]
    public void Get_NonExisting_Should_Return_Null()
    {
        using var provider = Services.Build();
        CompileDelegate(out var getRequired, out var get);
        Assert.Null(get(provider));
    }

    private class Service
    {
    }
    
    private static void CompileDelegate(out Func<IServiceProvider, Service> getRequiredService,
        out Func<IServiceProvider, Service?> getService)
    {
        var param = Expression.Parameter(typeof(IServiceProvider));
        var getRequired = new GetServiceExpBuilder(new PureESBuilderOptions())
            .GetRequiredService(param, typeof(Service));
        var get = new GetServiceExpBuilder(new PureESBuilderOptions())
            .GetService(param, typeof(Service));
        getRequiredService = Expression.Lambda<Func<IServiceProvider, Service>>(getRequired, param).Compile();
        getService = Expression.Lambda<Func<IServiceProvider, Service>>(get, param).Compile();
    }
}