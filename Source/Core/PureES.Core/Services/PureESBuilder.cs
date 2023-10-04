using Microsoft.Extensions.DependencyInjection;

namespace PureES.Core.Services;

public class PureESBuilder
{
    public IServiceCollection Services { get; }

    public PureESBuilder(IServiceCollection services) => Services = services;
}