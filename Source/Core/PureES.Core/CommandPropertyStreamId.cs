using System.Linq.Expressions;
using System.Reflection;

namespace PureES.Core;

internal class CommandPropertyStreamId<TCommand> : ICommandStreamId<TCommand>
{
    private readonly GetCommandStreamIdProperty _getStreamIdProperty;
    
    public CommandPropertyStreamId(GetCommandStreamIdProperty getStreamIdProperty)
    {
        _getStreamIdProperty = getStreamIdProperty ?? throw new ArgumentNullException(nameof(getStreamIdProperty));
    }

    private Func<TCommand, string>? _delegate;

    public string GetStreamId(TCommand input)
    {
        _delegate ??= Compile(_getStreamIdProperty);
        return _delegate(input);
    }

    private static Func<TCommand, string> Compile(GetCommandStreamIdProperty getStreamIdProperty)
    {
        var param = Expression.Parameter(typeof(TCommand));
        
        Expression body = param;
        while (body.Type != typeof(string))
        {
            var property = getStreamIdProperty(body.Type) ?? 
                           throw new ArgumentException("Return value cannot be null", nameof(getStreamIdProperty));
            
            body = Expression.Property(body, property);
            
            //Repeat until returned property is a string
        }
        return Expression.Lambda<Func<TCommand, string>>(Expression.Block(body), param).Compile();
    }
    
    public static PropertyInfo DefaultGetStreamIdProperty(Type type)
    {
        const string prop = "StreamId";

        //Efficiency: shouldn't need this
        //if (type == typeof(string)) throw new NotImplementedException();

        return type.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Unable to locate property {prop} on type {type}");
    }
}