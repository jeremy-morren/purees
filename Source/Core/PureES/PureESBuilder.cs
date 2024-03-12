using Microsoft.Extensions.DependencyInjection;

namespace PureES;

public class PureESBuilder
{
    public IServiceCollection Services { get; }

    public PureESBuilder(IServiceCollection services) => Services = services;
}