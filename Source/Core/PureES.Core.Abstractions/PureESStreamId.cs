using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PureES.Core;

[PublicAPI]
public class PureESStreamId<TSource>
{
    private readonly PureESOptions _options;
    
    public PureESStreamId(PureESOptions options)
    {
        _options = options;
    }

    [ActivatorUtilitiesConstructor] public PureESStreamId(IOptions<PureESOptions> options) : this(options.Value) {}
    
    private Func<TSource, string>? _delegate;

    public string GetId(TSource input)
    {
        _delegate ??= Compile(_options);
        return _delegate(input);
    }

    private static Func<TSource, string> Compile(PureESOptions options)
    {
        options.Validate();
        
        var param = Expression.Parameter(typeof(TSource));
        
        Expression body = param;
        while (body.Type != typeof(string))
        {
            var property = options.GetStreamIdProperty(body.Type) ??
                           throw new InvalidOperationException($"{nameof(options.GetStreamIdProperty)} returned null");

            body = Expression.Property(body, property);
            
            //Repeat until returned property is a string
        }
        return Expression.Lambda<Func<TSource, string>>(Expression.Block(body), param).Compile();
    }
}