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
        var func = new GetServiceExpBuilder(new CommandHandlerOptions())
            .CompileDelegate<Service>();
        using var provider = Services.Build(s => s.AddSingleton(svc));
        Assert.Same(svc, func(provider));
    }
    
    [Fact]
    public void Get_Transient_Service()
    {
        var func = new GetServiceExpBuilder(new CommandHandlerOptions())
            .CompileDelegate<Service>();
        using var provider = Services.Build(s => s.AddTransient<Service>());
        func(provider); //As long as it succeeds
    }

    private class Service {};
}