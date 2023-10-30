using Microsoft.Extensions.DependencyInjection;

namespace PureES.Core;

public class PureESBuilder
{
    public IServiceCollection Services { get; }

    public PureESBuilder(IServiceCollection services) => Services = services;
}